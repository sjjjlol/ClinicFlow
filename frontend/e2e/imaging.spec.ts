import { test, expect } from "@playwright/test";
import { submitLogin } from "./support";

test("DICOM existing study association, viewer pixels, download and unlink", async ({
  page,
  context,
}) => {
  test.setTimeout(90_000);
  await page.goto("/");
  await page.getByLabel("账号", { exact: true }).fill("scheduler");
  await page
    .getByLabel("密码", { exact: true })
    .fill(process.env.DEMO_PASSWORD!);
  await submitLogin(page);
  await page.getByRole("button", { name: "新建预约" }).click();
  const date = new Date(
    Date.UTC(2046, 0, 1) + Math.floor(Math.random() * 100000) * 900000,
  )
    .toISOString()
    .slice(0, 16);
  await page.getByLabel("开始时间（上海 UTC+8）").fill(date);
  await page.getByRole("button", { name: "创建预约", exact: true }).click();
  const panel = page.getByRole("region", { name: "就诊前影像资料" });
  await expect(panel.getByText("CF-IMG-001", { exact: false })).toBeVisible();
  await panel.getByRole("button", { name: "查询患者已有影像" }).click();
  await expect(
    panel.getByRole("button", { name: "关联检查", exact: true }),
  ).toHaveCount(2);
  await panel
    .getByRole("button", { name: "关联检查", exact: true })
    .first()
    .click();
  await expect(
    panel.getByRole("heading", { name: "已关联检查 · 1" }),
  ).toBeVisible();
  await panel.getByRole("button", { name: "查看序列" }).click();
  await expect(
    panel.getByRole("heading", { name: "序列与实例 · 6 个实例" }),
  ).toBeVisible();
  const download = page.waitForEvent("download");
  await panel
    .getByRole("link", { name: "下载实例 1（DICOM）" })
    .first()
    .click();
  expect((await download).suggestedFilename()).toMatch(/\.dcm$/);
  const popup = context.waitForEvent("page");
  await panel.getByRole("link", { name: "打开影像查看器" }).click();
  const viewer = await popup;
  const failures: string[] = [];
  const pixels: string[] = [];
  viewer.on("response", (r) => {
    if (r.url().includes("/api/imaging/") && r.status() >= 400)
      failures.push(`${r.status()} ${r.url()}`);
    if (r.url().includes("/frames/") && r.status() === 200)
      pixels.push(r.url());
  });
  viewer.on("pageerror", (error) => failures.push(error.message));
  viewer.on("console", (msg) => {
    if (msg.type() === "error" || msg.type() === "warning")
      console.log("VIEWER", msg.text());
  });
  viewer.on("response", (r) => {
    if (r.status() >= 400) console.log("VIEWER_HTTP", r.status(), r.url());
  });
  await viewer.waitForLoadState("domcontentloaded");
  await expect
    .poll(() => pixels.length, {
      timeout: 45_000,
      message: "Stone must fetch actual DICOM pixels",
    })
    .toBeGreaterThan(0)
    .catch(async (e) => {
      console.log("Viewer failures", failures);
      await viewer.screenshot({ path: "test-results/imaging-failed.png" });
      throw e;
    });
  await expect(viewer.locator("canvas").first()).toBeVisible();
  await viewer.screenshot({
    path: "test-results/imaging-viewer.png",
    fullPage: true,
  });
  expect(failures).toEqual([]);
  await viewer.close();
  await page.screenshot({
    path: "test-results/imaging-panel.png",
    fullPage: true,
  });
  await panel.getByRole("button", { name: "解除关联", exact: true }).click();
  await expect(
    panel.getByRole("heading", { name: "已关联检查 · 0" }),
  ).toBeVisible();
  await panel.getByText("影像操作记录（最近30条）").click();
  await expect(panel.locator("details li")).toHaveCount(2);
});

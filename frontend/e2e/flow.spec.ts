import { submitLogin } from "./support";
import { test, expect } from "@playwright/test";
test("A01 login and Apple-style workspace", async ({ page }) => {
  await page.goto("/");
  await page.getByLabel("账号", { exact: true }).fill("scheduler");
  await expect(
    page.getByRole("heading", { name: "让预约，有条不紊。" }),
  ).toBeVisible();
  await page
    .getByLabel("密码", { exact: true })
    .fill(process.env.DEMO_PASSWORD!);
  await submitLogin(page);
  await expect(
    page.getByRole("heading", { name: "预约工作台", exact: true }),
  ).toBeVisible();
  await expect(
    page.locator(".resource-card").getByText("预约室 A", { exact: true }),
  ).toBeVisible();
  await page.screenshot({ path: "test-results/workspace.png", fullPage: true });
  await page.getByRole("button", { name: "退出登录" }).click();
  await expect(
    page.getByRole("heading", { name: "让预约，有条不紊。" }),
  ).toBeVisible();
});
test("A02 create, detail and actionable conflict", async ({ page }) => {
  await page.goto("/");
  await page.getByLabel("账号", { exact: true }).fill("scheduler");
  await page
    .getByLabel("密码", { exact: true })
    .fill(process.env.DEMO_PASSWORD!);
  await submitLogin(page);
  await page.getByRole("button", { name: "新建预约" }).click();
  const date = new Date(
    Date.UTC(2034, 0, 1) + Math.floor(Math.random() * 100000) * 900000,
  )
    .toISOString()
    .slice(0, 16);
  await page.getByLabel("开始时间（上海 UTC+8）").fill(date);
  await page.getByRole("button", { name: "创建预约", exact: true }).click();
  await expect(page.getByRole("heading", { name: "预约详情" })).toBeVisible();
  await expect(page.getByText("资料核对", { exact: false })).toBeVisible();
  await page.getByRole("button", { name: "关闭详情" }).click();
  await page.getByRole("button", { name: "新建预约" }).click();
  await page.getByLabel("开始时间（上海 UTC+8）").fill(date);
  await page.getByRole("button", { name: "创建预约", exact: true }).click();
  await expect(page.getByRole("alert")).toContainText("已被占用");
  await page.screenshot({ path: "test-results/conflict.png", fullPage: true });
});
test("A06/A12 reschedule and cancel from detail", async ({ page }) => {
  await page.goto("/");
  await page.getByLabel("账号", { exact: true }).fill("scheduler");
  await page
    .getByLabel("密码", { exact: true })
    .fill(process.env.DEMO_PASSWORD!);
  await submitLogin(page);
  await page.getByRole("button", { name: "新建预约" }).click();
  const date = new Date(
    Date.UTC(2036, 0, 1) + Math.floor(Math.random() * 100000) * 900000,
  )
    .toISOString()
    .slice(0, 16);
  await page.getByLabel("开始时间（上海 UTC+8）").fill(date);
  await page.getByRole("button", { name: "创建预约", exact: true }).click();
  await page.getByRole("button", { name: "改期", exact: true }).click();
  await page
    .getByRole("combobox", { name: "预约资源", exact: true })
    .selectOption("2");
  await page.getByRole("button", { name: "保存改期" }).click();
  await expect(page.getByRole("heading", { name: "预约详情" })).toContainText(
    "v2",
  );
  page.once("dialog", (dialog) => dialog.accept());
  await page.getByRole("button", { name: "取消预约", exact: true }).click();
  await expect(page.getByRole("heading", { name: "预约详情" })).toContainText(
    "已取消",
  );
  await expect(
    page.getByRole("button", { name: "改期", exact: true }),
  ).toHaveCount(0);
});
test("A21 complete collaborative appointment lifecycle", async ({ page }) => {
  async function login(role: string) {
    await page.getByLabel("账号", { exact: true }).fill(role);
    await page
      .getByLabel("密码", { exact: true })
      .fill(process.env.DEMO_PASSWORD!);
    await submitLogin(page);
    await expect(
      page.getByRole("heading", { name: "预约工作台", exact: true }),
    ).toBeVisible();
  }
  await page.goto("/");
  await page.getByLabel("账号", { exact: true }).fill("scheduler");
  await login("scheduler");
  await page.getByRole("button", { name: "新建预约" }).click();
  const date = new Date(
    Date.UTC(2040, 0, 1) + Math.floor(Math.random() * 100000) * 900000,
  )
    .toISOString()
    .slice(0, 16);
  await page.getByLabel("开始时间（上海 UTC+8）").fill(date);
  const response = page.waitForResponse(
    (r) =>
      r.url().endsWith("/api/appointments") && r.request().method() === "POST",
  );
  await page.getByRole("button", { name: "创建预约", exact: true }).click();
  const a = await (await response).json();
  await page.getByRole("button", { name: "确认预约", exact: true }).click();
  await expect(page.getByRole("dialog").getByRole("alert")).toContainText(
    "前置任务",
  );
  await page.getByRole("button", { name: "关闭详情" }).click();
  await page.getByRole("button", { name: "退出登录" }).click();
  await login("taskoperator");
  await page.goto("/?appointment=" + a.id);
  await page
    .getByRole("button", { name: "完成 资料核对", exact: true })
    .click();
  await page
    .getByRole("button", { name: "完成 预约信息复核", exact: true })
    .click();
  await expect(
    page.getByRole("dialog").getByText("已完成", { exact: true }),
  ).toHaveCount(2);
  await expect(
    page.getByRole("button", { name: "确认预约", exact: true }),
  ).toHaveCount(0);
  await page.getByRole("button", { name: "关闭详情" }).click();
  await page.getByRole("button", { name: "退出登录" }).click();
  await login("scheduler");
  await page.getByRole("button", { name: "确认预约", exact: true }).click();
  await expect(page.getByRole("heading", { name: "预约详情" })).toContainText(
    "已确认",
  );
  await page.getByRole("button", { name: "改期", exact: true }).click();
  await page
    .getByRole("combobox", { name: "预约资源", exact: true })
    .selectOption("2");
  await page.getByRole("button", { name: "保存改期" }).click();
  await expect(page.getByRole("heading", { name: "预约详情" })).toContainText(
    "待确认",
  );
  await expect(
    page.getByRole("dialog").getByText("待完成", { exact: true }),
  ).toHaveCount(2);
  page.once("dialog", (d) => d.accept());
  await page.getByRole("button", { name: "取消预约", exact: true }).click();
  await expect(page.getByRole("heading", { name: "预约详情" })).toContainText(
    "已取消",
  );
  await page.screenshot({ path: "test-results/lifecycle.png", fullPage: true });
});
test("Admin observes persistent sync queue and attempt history", async ({
  page,
}) => {
  await page.goto("/");
  await page.getByLabel("账号", { exact: true }).fill("scheduler");
  await page.getByLabel("账号", { exact: true }).fill("admin");
  await page
    .getByLabel("密码", { exact: true })
    .fill(process.env.DEMO_PASSWORD!);
  await submitLogin(page);
  await page.getByRole("button", { name: "同步队列" }).click();
  await expect(
    page.getByRole("heading", { name: "外部同步队列" }),
  ).toBeVisible();
  await expect(page.getByRole("button", { name: "新建预约" })).toHaveCount(0);
  await page
    .getByRole("button", { name: "投递记录", exact: true })
    .first()
    .click();
  await expect(page.getByRole("dialog", { name: "投递记录" })).toBeVisible();
  await page.screenshot({
    path: "test-results/sync-queue.png",
    fullPage: true,
  });
});
test("Mobile workspace and keyboard dialog dismissal", async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto("/");
  await page.getByLabel("账号", { exact: true }).fill("scheduler");
  await page
    .getByLabel("密码", { exact: true })
    .fill(process.env.DEMO_PASSWORD!);
  await submitLogin(page);
  await expect(
    page.getByRole("heading", { name: "预约工作台", exact: true }),
  ).toBeVisible();
  await page.getByRole("button", { name: "新建预约" }).click();
  await expect(page.getByRole("dialog")).toBeVisible();
  await page.keyboard.press("Escape");
  await expect(page.getByRole("dialog")).toHaveCount(0);
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= window.innerWidth,
    ),
  ).toBeTruthy();
  await page.screenshot({ path: "test-results/mobile.png", fullPage: true });
});
test("Sync failure UI preserves retry identity after a network error", async ({
  page,
}) => {
  // Controlled view fixture; real retry/lease persistence is covered by MySQL tests.
  let delivered = false,
    calls = 0;
  const keys: string[] = [];
  await page.route("**/api/sync", (route) =>
    route.fulfill({
      json: [
        {
          id: "ui-fixture",
          appointmentId: "example",
          version: 1,
          status: delivered ? "Delivered" : "Failed",
          attempts: 5,
          lastError: delivered ? null : "HTTP 422",
          nextAttemptUtc: "2030-01-01T00:00:00Z",
        },
      ],
    }),
  );
  await page.route("**/api/sync/ui-fixture/retry", async (route) => {
    keys.push(route.request().headers()["idempotency-key"]);
    if (calls++ === 0) await route.abort("failed");
    else {
      delivered = true;
      await route.fulfill({ json: {} });
    }
  });
  await page.goto("/");
  await page.getByLabel("账号", { exact: true }).fill("scheduler");
  await page.getByLabel("账号", { exact: true }).fill("admin");
  await page
    .getByLabel("密码", { exact: true })
    .fill(process.env.DEMO_PASSWORD!);
  await submitLogin(page);
  await page.getByRole("button", { name: "同步队列" }).click();
  await expect(page.getByText("同步失败", { exact: true })).toBeVisible();
  await expect(page.getByText("HTTP 422", { exact: true })).toBeVisible();
  await page.getByRole("button", { name: "重新触发", exact: true }).click();
  await expect(page.getByRole("alert")).toBeVisible();
  await page.getByRole("button", { name: "重新触发", exact: true }).click();
  await expect(page.getByText("已同步", { exact: true })).toBeVisible();
  expect(keys).toHaveLength(2);
  expect(keys[0]).toBeTruthy();
  expect(keys[0]).toBe(keys[1]);
});

import { submitLogin, submitRegistration } from "./support";
import { test, expect } from "@playwright/test";

test("registered account books only for itself, reschedules, cancels and signs in again", async ({
  page,
}) => {
  const username = "browser_" + crypto.randomUUID().slice(0, 8);
  const password = "12345678";
  await page.goto("/");
  await page.getByRole("button", { name: "没有账号？注册预约账号" }).click();
  await page.getByLabel("账号", { exact: true }).fill(username);
  await page.getByLabel("预约姓名", { exact: true }).fill("浏览器测试预约者");
  await page.getByLabel("密码", { exact: true }).fill(password);
  await page.getByLabel("确认密码", { exact: true }).fill("different");
  await page.getByRole("button", { name: "注册并进入", exact: true }).click();
  await expect(page.getByRole("alert")).toContainText("两次输入的密码不一致");
  await page.getByLabel("确认密码", { exact: true }).fill(password);
  await submitRegistration(page);
  await expect(
    page.getByRole("heading", { name: "我的预约", exact: true }),
  ).toBeVisible();
  await expect(page.getByText("共 0 笔预约")).toBeVisible();
  await expect(page.getByRole("button", { name: "同步队列" })).toHaveCount(0);
  await expect(page.getByRole("button", { name: "故障实验" })).toHaveCount(0);
  await page.getByRole("button", { name: "新建预约" }).click();
  await expect(
    page.getByRole("combobox", { name: "患者", exact: true }),
  ).toBeDisabled();
  await expect(
    page.getByRole("combobox", { name: "患者", exact: true }),
  ).toContainText("浏览器测试预约者");
  const date = new Date(
    Date.UTC(2043, 0, 1) + Math.floor(Math.random() * 100000) * 900000,
  )
    .toISOString()
    .slice(0, 16);
  await page.getByLabel("开始时间（上海 UTC+8）").fill(date);
  await page.getByRole("button", { name: "创建预约", exact: true }).click();
  await expect(page.getByRole("heading", { name: "预约详情" })).toContainText(
    "待确认",
  );
  await expect(
    page.getByRole("button", { name: "确认预约", exact: true }),
  ).toHaveCount(0);
  await expect(
    page.getByRole("button", { name: "登记完成", exact: true }),
  ).toHaveCount(0);
  await page.getByRole("button", { name: "改期", exact: true }).click();
  await page
    .getByRole("combobox", { name: "预约资源", exact: true })
    .selectOption("2");
  await page.getByRole("button", { name: "保存改期" }).click();
  await expect(page.getByRole("heading", { name: "预约详情" })).toContainText(
    "v2",
  );
  page.once("dialog", (d) => d.accept());
  await page.getByRole("button", { name: "取消预约", exact: true }).click();
  await expect(page.getByRole("heading", { name: "预约详情" })).toContainText(
    "已取消",
  );
  await page.getByRole("button", { name: "关闭详情" }).click();
  await page.reload();
  await expect(page.getByText("共 1 笔预约")).toBeVisible();
  await page.screenshot({
    path: "test-results/my-appointments.png",
    fullPage: true,
  });
  await page.getByRole("button", { name: "退出登录" }).click();
  await page.getByLabel("账号", { exact: true }).fill(username);
  await page.getByLabel("密码", { exact: true }).fill(password);
  await submitLogin(page);
  await expect(
    page.getByRole("heading", { name: "我的预约", exact: true }),
  ).toBeVisible();
  await expect(page.getByText("共 1 笔预约")).toBeVisible();
});

test("staff complete a historical confirmed appointment and terminal UI hides mutations", async ({
  page,
}) => {
  async function login(username: string) {
    await page.getByLabel("账号", { exact: true }).fill(username);
    await page
      .getByLabel("密码", { exact: true })
      .fill(process.env.DEMO_PASSWORD!);
    await submitLogin(page);
    await expect(
      page.getByRole("heading", { name: "预约工作台", exact: true }),
    ).toBeVisible();
  }
  await page.goto("/");
  await login("scheduler");
  await page.getByRole("button", { name: "新建预约" }).click();
  const date = new Date(
    Date.UTC(2010, 0, 1) + Math.floor(Math.random() * 100000) * 900000,
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
  await page.getByRole("button", { name: "关闭详情" }).click();
  await page.getByRole("button", { name: "退出登录" }).click();
  await login("scheduler");
  await page.getByRole("button", { name: "确认预约", exact: true }).click();
  await expect(page.getByRole("heading", { name: "预约详情" })).toContainText(
    "已确认",
  );
  page.once("dialog", (d) => d.accept());
  await page.getByRole("button", { name: "登记完成", exact: true }).click();
  await expect(page.getByRole("heading", { name: "预约详情" })).toContainText(
    "已完成",
  );
  await expect(
    page.getByRole("button", { name: "改期", exact: true }),
  ).toHaveCount(0);
  await expect(
    page.getByRole("button", { name: "取消预约", exact: true }),
  ).toHaveCount(0);
  await expect(
    page.getByRole("button", { name: "登记完成", exact: true }),
  ).toHaveCount(0);
  await page.screenshot({ path: "test-results/completed.png", fullPage: true });
  await page.getByRole("button", { name: "关闭详情" }).click();
  await page
    .getByRole("combobox", { name: "状态筛选" })
    .selectOption("Completed");
  await expect(
    page.getByRole("cell", { name: "已完成 · Completed" }).first(),
  ).toBeVisible();
});

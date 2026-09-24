import { test, expect } from "@playwright/test";
test("A01 login and Apple-style workspace", async ({ page }) => {
  await page.goto("/");
  await expect(
    page.getByRole("heading", { name: "让预约，有条不紊。" }),
  ).toBeVisible();
  await page
    .getByLabel("密码", { exact: true })
    .fill(process.env.DEMO_PASSWORD!);
  await page.getByRole("button", { name: "进入工作台" }).click();
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
  await page
    .getByLabel("密码", { exact: true })
    .fill(process.env.DEMO_PASSWORD!);
  await page.getByRole("button", { name: "进入工作台" }).click();
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
  await page
    .getByLabel("密码", { exact: true })
    .fill(process.env.DEMO_PASSWORD!);
  await page.getByRole("button", { name: "进入工作台" }).click();
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
    await page.getByRole("combobox", { name: "演示角色" }).selectOption(role);
    await page
      .getByLabel("密码", { exact: true })
      .fill(process.env.DEMO_PASSWORD!);
    await page.getByRole("button", { name: "进入工作台" }).click();
    await expect(
      page.getByRole("heading", { name: "预约工作台", exact: true }),
    ).toBeVisible();
  }
  await page.goto("/");
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
  await page.getByRole("combobox", { name: "演示角色" }).selectOption("admin");
  await page
    .getByLabel("密码", { exact: true })
    .fill(process.env.DEMO_PASSWORD!);
  await page.getByRole("button", { name: "进入工作台" }).click();
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
test('Mobile workspace and keyboard dialog dismissal', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/');
  await page.getByLabel('密码', { exact: true }).fill(process.env.DEMO_PASSWORD!);
  await page.getByRole('button', { name: '进入工作台' }).click();
  await expect(page.getByRole('heading', { name: '预约工作台', exact: true })).toBeVisible();
  await page.getByRole('button', { name: '新建预约' }).click();
  await expect(page.getByRole('dialog')).toBeVisible();
  await page.keyboard.press('Escape');
  await expect(page.getByRole('dialog')).toHaveCount(0);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBeTruthy();
  await page.screenshot({ path: 'test-results/mobile.png', fullPage: true });
});

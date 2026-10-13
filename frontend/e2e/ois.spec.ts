import { test, expect, type Page } from "@playwright/test";
import { submitLogin } from "./support";

async function login(page: Page, role: string) {
  await page.goto("/");
  await page.getByLabel("账号", { exact: true }).fill(role);
  await page
    .getByLabel("密码", { exact: true })
    .fill(process.env.DEMO_PASSWORD!);
  await submitLogin(page);
  await expect(page.getByRole("heading", { level: 1 })).toContainText("工作台");
}
async function confirm(page: Page) {
  await page.getByRole("button", { name: "预览并核对", exact: true }).click();
  await page.getByRole("button", { name: "确认提交", exact: true }).click();
  await expect(page.getByRole("dialog")).toHaveCount(0);
  await expect(page.getByRole("status")).toContainText("已保存");
}

test("OIS roles cooperate on signed prescription, plan review and experiment scheduling", async ({
  page,
}) => {
  const title = `浏览器验收-${Date.now()}`;
  await login(page, "clinician");
  await page.getByRole("button", { name: "◎ 患者与诊疗", exact: true }).click();
  await page
    .getByRole("button", { name: "＋ 建立诊疗档案", exact: true })
    .click();
  await page.getByLabel("诊疗档案名称").fill(title);
  await confirm(page);
  let care = page
    .locator("article")
    .filter({ has: page.getByRole("heading", { name: title, exact: true }) });
  await care
    .getByRole("button", { name: "签署诊疗记录 / 处方", exact: true })
    .click();
  await page.getByLabel("诊疗记录 / 诊断").fill("模拟诊断，无真实患者数据");
  await page.getByLabel("放疗处方（模拟）").fill("演示放疗处方");
  await confirm(page);
  await expect(care.getByText("演示放疗处方", { exact: true })).toBeVisible();
  await page.getByRole("button", { name: "退出登录", exact: true }).click();
  await expect(page.getByLabel("账号", { exact: true })).toBeVisible();
  await login(page, "physicist");
  await page.getByRole("button", { name: "◎ 患者与诊疗", exact: true }).click();
  care = page
    .locator("article")
    .filter({ has: page.getByRole("heading", { name: title, exact: true }) });
  await care.getByRole("button", { name: "登记计划版本", exact: true }).click();
  await page.getByLabel("外部计划摘要（模拟）").fill(title + " 计划");
  await page.getByLabel("计划使用机器").selectOption("linac-b");
  await page.getByLabel("外部附件引用 / 文件编号").fill("DEMO-PLAN-001");
  await confirm(page);
  await expect(care.getByText("待诊疗审核", { exact: true })).toBeVisible();
  await page.getByRole("button", { name: "退出登录", exact: true }).click();
  await expect(page.getByLabel("账号", { exact: true })).toBeVisible();
  await login(page, "clinician");
  await page.getByRole("button", { name: "◎ 患者与诊疗", exact: true }).click();
  care = page
    .locator("article")
    .filter({ has: page.getByRole("heading", { name: title, exact: true }) });
  await care.getByRole("button", { name: "审核此版本", exact: true }).click();
  await confirm(page);
  await care.getByRole("button", { name: "安排物理实验", exact: true }).click();
  const day = new Date(
    Date.UTC(2037, 0, 1) + Math.floor(Math.random() * 300) * 86400000,
  )
    .toISOString()
    .slice(0, 10);
  await page.getByLabel("开始时间（上海）").fill(day + "T09:00");
  await page.getByLabel("结束时间（上海）").fill(day + "T09:30");
  await confirm(page);
  await page.getByRole("button", { name: "▦ 预约日历", exact: true }).click();
  await page.getByLabel("日历日期").fill(day);
  await expect(
    page.getByRole("button", { name: new RegExp("物理实验 · " + title) }),
  ).toBeVisible();
  await expect(page.getByText("AI 预约助手", { exact: true })).toHaveCount(0);
});

test("OIS single booking preview, cancellation, restore and week calendar", async ({
  page,
}) => {
  await login(page, "scheduler");
  const day = new Date(
    Date.UTC(2038, 0, 1) + Math.floor(Math.random() * 300) * 86400000,
  )
    .toISOString()
    .slice(0, 10);
  await page.getByRole("button", { name: "＋ 门诊预约", exact: true }).click();
  await page.getByLabel("开始时间（上海）").fill(day + "T09:00");
  await page.getByLabel("结束时间（上海）").fill(day + "T09:30");
  await confirm(page);
  await page.getByRole("button", { name: "▦ 预约日历", exact: true }).click();
  await page.getByLabel("日历日期").fill(day);
  const event = page.locator(".ois-event").filter({ hasText: "门诊" });
  await event.click();
  await page.getByRole("button", { name: "取消预约", exact: true }).click();
  await page.getByLabel("操作原因").fill("测试误操作恢复");
  await confirm(page);
  await expect(event).toHaveCount(0);
  const cancelled = page
    .locator(".ois-row")
    .filter({ hasText: day.slice(5).replace("-", "/") })
    .filter({ hasText: "已取消" });
  await cancelled.click();
  await page.getByRole("button", { name: "恢复预约", exact: true }).click();
  await page.getByLabel("操作原因").fill("重新检查后恢复");
  await confirm(page);
  await expect(event).toHaveCount(1);
  await page.getByLabel("日历视图").selectOption("week");
  await expect(page.getByLabel("日历资源")).toBeVisible();
  await expect(event).toHaveCount(1);
});

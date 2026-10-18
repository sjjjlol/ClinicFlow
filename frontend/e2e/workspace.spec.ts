import { test, expect } from "@playwright/test";

for (const [role, title, menus, hidden] of [
  [
    "Scheduler",
    "预约人员",
    ["预约日历"],
    ["患者与诊疗", "治疗计划与验证", "机器与占用"],
  ],
  [
    "Clinician",
    "诊疗人员",
    ["诊疗与实验日历", "患者与诊疗"],
    ["治疗计划与验证", "机器与占用"],
  ],
  [
    "Physicist",
    "物理师",
    ["验证与维护日历", "治疗计划与验证", "机器与占用"],
    ["患者与诊疗", "预约日历"],
  ],
] as const) {
  test(`${role} sees its workspace and actionable guide`, async ({ page }) => {
    await page.route("**/api/auth/me", (route) =>
      route.fulfill({ json: { name: role, role } }),
    );
    await page.route("**/api/ois/workspace", (route) =>
      route.fulfill({
        json: {
          cases: [],
          prescriptions: [],
          plans: [],
          resources: [],
          reservations: [],
          deliveries: [],
          tasks: [],
          patients: [],
          audits: [
            {
              id: 1,
              actor: role,
              role,
              action: "本角色记录",
              targetId: "own",
              detail: "{}",
              atUtc: "2026-10-16T00:00:00Z",
            },
            {
              id: 2,
              actor: "other",
              role: role === "Scheduler" ? "Physicist" : "Scheduler",
              action: "其他角色记录",
              targetId: "other",
              detail: "{}",
              atUtc: "2026-10-16T00:00:00Z",
            },
          ],
        },
      }),
    );
    await page.goto("/");
    await expect(
      page.getByRole("heading", { name: title + "工作台", exact: true }),
    ).toBeVisible();
    for (const menu of menus)
      await expect(
        page.locator("nav").getByRole("button", { name: menu }),
      ).toBeVisible();
    for (const menu of hidden)
      await expect(
        page.locator("nav").getByRole("button", { name: menu }),
      ).toHaveCount(0);
    const guide = page.getByRole("region", { name: title + "操作流程 SOP" });
    await expect(guide.locator(".ois-guide-step")).toHaveCount(5);
    const actions = await guide.getByRole("button").allTextContents();
    for (const action of actions) {
      await guide.getByRole("button", { name: action, exact: true }).click();
      await expect(page.getByRole("heading", { level: 1 })).not.toHaveText(
        title + "工作台",
      );
      await page
        .locator("nav")
        .getByRole("button", { name: "我的工作台" })
        .click();
    }
    await page
      .locator("nav")
      .getByRole("button", { name: "本角色操作历史" })
      .click();
    await expect(page.getByText("本角色记录", { exact: false })).toBeVisible();
    await expect(page.getByText("其他角色记录", { exact: false })).toHaveCount(
      0,
    );
  });
}

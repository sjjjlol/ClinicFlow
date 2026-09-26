import { test, expect } from "@playwright/test";
import { submitRegistration } from "./support";

test("registration honors Retry-After before retrying the unchanged form", async ({
  page,
}) => {
  const username = "retry_" + crypto.randomUUID().slice(0, 8);
  const requests: string[] = [];
  let rejectedAt = 0;
  let retriedAt = 0;
  await page.route("**/api/auth/register", async (route) => {
    requests.push(route.request().postData()!);
    if (requests.length === 1) {
      rejectedAt = Date.now();
      await route.fulfill({
        status: 429,
        contentType: "application/json",
        headers: { "Retry-After": "1" },
        body: JSON.stringify({
          code: "rate_limited",
          message: "操作过于频繁，请在1秒后重试",
        }),
      });
      return;
    }
    retriedAt = Date.now();
    await route.continue();
  });
  await page.goto("/");
  await page.getByRole("button", { name: "没有账号？注册预约账号" }).click();
  await page.getByLabel("账号", { exact: true }).fill(username);
  await page.getByLabel("预约姓名", { exact: true }).fill("限流回归预约者");
  await page.getByLabel("密码", { exact: true }).fill("12345678");
  await page.getByLabel("确认密码", { exact: true }).fill("12345678");

  const rejected = page.waitForResponse(
    (r) => r.url().endsWith("/api/auth/register") && r.status() === 429,
  );
  const submitting = submitRegistration(page);
  await rejected;
  await expect(page.getByRole("alert")).toContainText("操作过于频繁");
  await submitting;
  await expect(
    page.getByRole("heading", { name: "我的预约", exact: true }),
  ).toBeVisible();
  expect(requests).toHaveLength(2);
  expect(requests[1]).toBe(requests[0]);
  expect(retriedAt - rejectedAt).toBeGreaterThanOrEqual(1000);
});

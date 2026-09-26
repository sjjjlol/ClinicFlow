import { expect, test, type Page } from "@playwright/test";

export async function submitRegistration(page: Page) {
  await submitAuth(page, "/api/auth/register", "注册并进入", 201);
}

// HTTP and browser suites can share one server/IP. Honor the real auth limiters
// rather than disabling it in the test environment.
export async function submitLogin(page: Page) {
  await submitAuth(page, "/api/auth/login", "进入工作台", 200);
}

async function submitAuth(
  page: Page,
  path: string,
  button: string,
  expectedStatus: number,
) {
  test.setTimeout(90_000);
  for (let attempt = 0; attempt < 2; attempt++) {
    const response = page.waitForResponse(
      (r) => r.url().endsWith(path) && r.request().method() === "POST",
    );
    await page.getByRole("button", { name: button }).click();
    const result = await response;
    if (result.status() === 429 && attempt === 0) {
      const header = Number(result.headers()["retry-after"] ?? "60");
      const seconds = Number.isFinite(header) && header >= 0 ? header : 60;
      await page.waitForTimeout(Math.min(60, seconds) * 1000 + 100);
      continue;
    }
    expect(result.status()).toBe(expectedStatus);
    return;
  }
}

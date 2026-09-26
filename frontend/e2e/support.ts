import { expect, test, type Page } from "@playwright/test";

// HTTP and browser suites can share one server/IP. Honor the real login limiter
// rather than disabling it in the test environment.
export async function submitLogin(page: Page) {
  test.setTimeout(90_000);
  for (let attempt = 0; attempt < 2; attempt++) {
    const response = page.waitForResponse(
      (r) =>
        r.url().endsWith("/api/auth/login") && r.request().method() === "POST",
    );
    await page.getByRole("button", { name: "进入工作台" }).click();
    const result = await response;
    if (result.status() === 429 && attempt === 0) {
      const seconds = Number(result.headers()["retry-after"] ?? "60");
      await page.waitForTimeout(Math.min(60, seconds) * 1000 + 100);
      continue;
    }
    expect(result.status()).toBe(200);
    return;
  }
}

import { test, expect } from "@playwright/test";
import { createServer } from "node:http";

test("Booker sees real incremental text before final cards, with a locked personal profile", async ({
  page,
}) => {
  let finish: (() => void) | undefined;
  const server = createServer((request, response) => {
    response.setHeader("Access-Control-Allow-Origin", "*");
    response.setHeader(
      "Access-Control-Allow-Headers",
      "content-type,x-csrf-token,accept",
    );
    if (request.method === "OPTIONS") {
      response.writeHead(204);
      response.end();
      return;
    }
    response.writeHead(200, { "Content-Type": "text/event-stream" });
    const write = (data: unknown) =>
      response.write("data: " + JSON.stringify(data) + "\n\n");
    write({ type: "session", sessionId: "stream-fixture" });
    write({ type: "message_start", message: "正在理解预约需求…" });
    write({ type: "delta", text: "已收到你的需求，" });
    finish = () => {
      write({ type: "delta", text: "请核对以下候选。" });
      write({
        type: "result",
        reply: {
          sessionId: "stream-fixture",
          message: "已收到你的需求，请核对以下候选。",
          status: "proposed",
          trace: [],
          candidates: [
            {
              id: "own",
              patientName: "本人测试",
              resourceName: "预约室 A",
              reason: "45分钟",
              booking: {
                patientId: 33,
                resourceId: 1,
                startUtc: "2031-01-06T04:00:00Z",
                endUtc: "2031-01-06T04:45:00Z",
              },
            },
          ],
        },
      });
      response.end();
    };
  });
  await new Promise<void>((resolve) => server.listen(0, "127.0.0.1", resolve));
  try {
    const address = server.address();
    if (!address || typeof address === "string")
      throw new Error("No test server");
    await page.route("**/api/auth/me", (r) =>
      r.fulfill({
        json: { name: "personal_user", role: "Booker", patientId: 33 },
      }),
    );
    await page.route("**/api/patients", (r) =>
      r.fulfill({ json: [{ id: 33, name: "本人测试" }] }),
    );
    await page.route("**/api/resources", (r) =>
      r.fulfill({ json: [{ id: 1, name: "预约室 A" }] }),
    );
    await page.route("**/api/appointments?**", (r) =>
      r.fulfill({ json: { items: [], total: 0 } }),
    );
    await page.route("**/api/agent/config", (r) =>
      r.fulfill({ json: { configured: true, demoEnabled: false } }),
    );
    await page.route("**/api/agent/messages/stream", (r) =>
      r.continue({ url: `http://127.0.0.1:${address.port}/stream` }),
    );
    await page.goto("/");
    await expect(
      page.getByRole("heading", { name: "我的预约", exact: true }),
    ).toBeVisible();
    await expect(
      page.getByRole("combobox", { name: "预约人（本人）" }),
    ).toBeDisabled();
    await expect(
      page.getByRole("combobox", { name: "预约人（本人）" }),
    ).toContainText("本人测试");
    await page.getByLabel("预约需求").fill("下周一下午45分钟");
    await page.getByRole("button", { name: "发送需求", exact: true }).click();
    await expect(page.getByLabel("助手流式回复")).toContainText(
      "已收到你的需求，",
    );
    await expect(
      page.getByRole("button", { name: "确认此候选并创建" }),
    ).toHaveCount(0);
    await expect(
      page.getByRole("button", { name: "停止回复", exact: true }),
    ).toBeVisible();
    await page.screenshot({
      path: "test-results/booker-streaming.png",
      fullPage: true,
    });
    finish!();
    await expect(
      page.getByRole("button", { name: "确认此候选并创建" }),
    ).toBeVisible();
    await expect(page.getByRole("log")).toContainText(
      "已收到你的需求，请核对以下候选。",
    );
    await expect(
      page.getByRole("button", { name: "模拟时段被抢占" }),
    ).toHaveCount(0);
  } finally {
    server.closeAllConnections();
    await new Promise<void>((resolve) => server.close(() => resolve()));
  }
});

test("a truncated stream never exposes actionable candidates", async ({
  page,
}) => {
  await page.route("**/api/auth/me", (r) =>
    r.fulfill({
      json: { name: "personal_user", role: "Booker", patientId: 33 },
    }),
  );
  await page.route("**/api/patients", (r) =>
    r.fulfill({ json: [{ id: 33, name: "本人测试" }] }),
  );
  await page.route("**/api/resources", (r) =>
    r.fulfill({ json: [{ id: 1, name: "预约室 A" }] }),
  );
  await page.route("**/api/appointments?**", (r) =>
    r.fulfill({ json: { items: [], total: 0 } }),
  );
  await page.route("**/api/agent/config", (r) =>
    r.fulfill({ json: { configured: true, demoEnabled: false } }),
  );
  await page.route("**/api/agent/messages/stream", (r) =>
    r.fulfill({
      contentType: "text/event-stream",
      body: 'data: {"type":"delta","text":"未完成的回复"}\n\n',
    }),
  );
  await page.goto("/");
  await page.getByLabel("预约需求").fill("下周一下午45分钟");
  await page.getByRole("button", { name: "发送需求", exact: true }).click();
  await expect(page.getByRole("alert")).toContainText("未收到完整结果");
  await expect(
    page.getByRole("button", { name: "确认此候选并创建" }),
  ).toHaveCount(0);
  await expect(page.getByLabel("预约需求")).toHaveValue("下周一下午45分钟");
});

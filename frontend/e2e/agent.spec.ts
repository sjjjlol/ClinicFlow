import { test, expect } from "@playwright/test";

test("agent cards require explicit confirmation and fresh confirmation after conflict", async ({
  page,
}) => {
  const booking = {
    patientId: 1,
    resourceId: 1,
    startUtc: "2031-01-06T04:00:00Z",
    endUtc: "2031-01-06T04:45:00Z",
  };
  const candidate = {
    id: "first",
    booking,
    patientName: "林晓（模拟）",
    resourceName: "预约室 A",
    reason: "符合下午45分钟约束",
  };
  const replacement = {
    ...candidate,
    id: "second",
    booking: {
      ...booking,
      startUtc: "2031-01-06T05:00:00Z",
      endUtc: "2031-01-06T05:45:00Z",
    },
  };
  let writes = 0;
  let confirming = 0;
  await page.route("**/api/agent/config", (r) =>
    r.fulfill({ json: { configured: true, demoEnabled: false } }),
  );
  await page.route("**/api/agent/messages", (r) =>
    r.fulfill({
      json: {
        sessionId: "ui-test",
        message: "请确认候选",
        candidates: [candidate],
        trace: [],
        status: "proposed",
      },
    }),
  );
  await page.route("**/api/agent/ui-test/confirm", async (r) => {
    confirming++;
    if (confirming === 1) {
      expect(r.request().postDataJSON().candidateId).toBe("first");
      await r.fulfill({
        json: {
          sessionId: "ui-test",
          message: "原时段已被占用，请再次确认替代方案",
          candidates: [replacement],
          trace: [
            {
              tool: "create_appointment",
              arguments: "{}",
              outcome: "slot_conflict",
              durationMs: 2,
            },
          ],
          status: "proposed",
        },
      });
    } else {
      expect(r.request().postDataJSON().candidateId).toBe("second");
      writes++;
      await r.fulfill({
        json: {
          sessionId: "ui-test",
          message: "预约已创建",
          candidates: [],
          trace: [],
          status: "created",
        },
      });
    }
  });
  await page.goto("/");
  await page
    .getByLabel("密码", { exact: true })
    .fill(process.env.DEMO_PASSWORD!);
  await page.getByRole("button", { name: "进入工作台" }).click();
  await page.getByLabel("预约需求").fill("下周一下午45分钟");
  await page.getByRole("button", { name: "发送需求", exact: true }).click();
  await expect(
    page.getByRole("button", { name: "确认此候选并创建" }),
  ).toBeVisible();
  expect(writes).toBe(0);
  await page.getByRole("button", { name: "确认此候选并创建" }).click();
  await expect(
    page.getByText("原时段已被占用，请再次确认替代方案"),
  ).toBeVisible();
  expect(writes).toBe(0);
  await page.getByText("本轮执行记录").click();
  await expect(page.getByText("slot_conflict", { exact: true })).toBeVisible();
  await page.screenshot({
    path: "test-results/agent-recovery.png",
    fullPage: true,
  });
  await page.getByRole("button", { name: "确认此候选并创建" }).click();
  await expect(page.getByRole("status")).toContainText("创建成功");
  expect(writes).toBe(1);
});

test("agent preserves the same candidate when a confirmation response is lost", async ({
  page,
}) => {
  const candidate = {
    id: "stable",
    booking: {
      patientId: 1,
      resourceId: 1,
      startUtc: "2031-01-06T04:00:00Z",
      endUtc: "2031-01-06T04:45:00Z",
    },
    patientName: "林晓（模拟）",
    resourceName: "预约室 A",
    reason: "45分钟",
  };
  let attempts = 0;
  await page.route("**/api/agent/config", (r) =>
    r.fulfill({ json: { configured: true, demoEnabled: false } }),
  );
  await page.route("**/api/agent/messages", (r) =>
    r.fulfill({
      json: {
        sessionId: "retry",
        message: "请确认",
        candidates: [candidate],
        trace: [],
        status: "proposed",
      },
    }),
  );
  await page.route("**/api/agent/retry/confirm", async (r) => {
    expect(r.request().postDataJSON().candidateId).toBe("stable");
    if (++attempts === 1) await r.abort("failed");
    else
      await r.fulfill({
        json: {
          sessionId: "retry",
          message: "创建成功",
          candidates: [],
          trace: [],
          status: "created",
        },
      });
  });
  await page.goto("/");
  await page
    .getByLabel("密码", { exact: true })
    .fill(process.env.DEMO_PASSWORD!);
  await page.getByRole("button", { name: "进入工作台" }).click();
  await page.getByLabel("预约需求").fill("下周一下午45分钟");
  await page.getByRole("button", { name: "发送需求", exact: true }).click();
  await page.getByRole("button", { name: "确认此候选并创建" }).click();
  await expect(page.getByRole("alert")).toBeVisible();
  await expect(
    page.getByRole("button", { name: "新会话", exact: true }),
  ).toBeDisabled();
  await page.getByRole("button", { name: "重试确认此候选" }).click();
  await expect(page.getByRole("status")).toContainText("创建成功");
  expect(attempts).toBe(2);
});

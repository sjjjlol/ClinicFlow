import { test, expect } from "@playwright/test";

test("forms send resource paths, scoped fields, PUT and retry keys", async ({
  page,
}) => {
  const sent: {
    path: string;
    method: string;
    body: unknown;
    key: string;
    csrf: string;
  }[] = [];
  await page.route("**/api/**", async (route) => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    if (path === "/api/auth/csrf")
      return route.fulfill({ json: { token: "test-csrf" } });
    if (path === "/api/auth/me")
      return route.fulfill({ status: 401, json: {} });
    sent.push({
      path,
      method: request.method(),
      body: request.postDataJSON(),
      key: request.headers()["idempotency-key"],
      csrf: request.headers()["x-csrf-token"],
    });
    return route.fulfill({ json: { targetId: "saved" } });
  });
  await page.goto("/");
  await page.evaluate(async () => {
    const modulePath = "/src/ois-api.ts";
    const { submitOisForm } = await import(modulePath);
    await submitOisForm(
      "review",
      { id: "plan-1", version: 3, irrelevant: "drop" },
      "retry-key",
    );
    await submitOisForm(
      "book",
      {
        kind: "Experiment",
        planId: "plan-1",
        resourceId: "machine-1",
        startUtc: "2035-01-01T00:00:00Z",
        endUtc: "2035-01-01T01:00:00Z",
      },
      "experiment-key",
    );
    await submitOisForm(
      "save-draft",
      {
        kind: "prescribe",
        caseId: "case-1",
        version: 2,
        draftVersion: 1,
        draftJson: '{"text":"历史草稿"}',
      },
      "draft-key",
    );
    await submitOisForm(
      "machine",
      { id: "machine-1", version: 4, available: false, reason: "maintenance" },
      "machine-key",
    );
    await submitOisForm(
      "correct-plan",
      { id: "plan-1", version: 3, text: "correction", reason: "reason" },
      "plan-key",
    );
    await submitOisForm(
      "correct-delivery",
      { id: "delivery-1", version: 2, text: "correction", reason: "reason" },
      "delivery-key",
    );
  });
  expect(sent).toHaveLength(6);
  expect(sent[0]).toEqual({
    path: "/api/ois/plans/plan-1/review",
    method: "POST",
    body: { version: 3 },
    key: "retry-key",
    csrf: "test-csrf",
  });
  expect(sent[1].path).toBe("/api/ois/reservations/experiment");
  expect(sent[1].body).not.toHaveProperty("kind");
  expect(sent[2]).toMatchObject({
    path: "/api/ois/cases/case-1/prescription-draft",
    method: "PUT",
    body: { version: 2, draftVersion: 1, draftJson: '{"text":"历史草稿"}' },
  });
  expect(sent[3]).toMatchObject({
    path: "/api/ois/machines/machine-1/availability",
    method: "PUT",
    body: { version: 4, available: false, reason: "maintenance" },
  });
  expect(sent[4].path).toBe("/api/ois/plans/plan-1/corrections");
  expect(sent[5].path).toBe(
    "/api/ois/treatment-deliveries/delivery-1/corrections",
  );
  expect(
    sent.every((request) => request.csrf === "test-csrf" && request.key),
  ).toBeTruthy();
});

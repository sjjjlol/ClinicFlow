let csrf = "";
export class ApiError extends Error {
  constructor(
    public status: number,
    public code: string,
    message: string,
  ) {
    super(message);
  }
}
export async function refreshCsrf() {
  csrf = (await (await fetch("/api/auth/csrf")).json()).token;
}
export async function api<T>(
  url: string,
  body?: unknown,
  key?: string,
): Promise<T> {
  if (body !== undefined && !csrf) await refreshCsrf();
  const res = await fetch(url, {
    method: body === undefined ? "GET" : "POST",
    headers: {
      "Content-Type": "application/json",
      "X-CSRF-TOKEN": csrf,
      ...(key ? { "Idempotency-Key": key } : {}),
    },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new ApiError(
      res.status,
      err.code ?? "request_failed",
      err.message ??
        { 401: "请先登录", 403: "当前角色没有此操作权限" }[res.status] ??
        "请求失败，请稍后重试",
    );
  }
  return res.status === 204 ? (undefined as T) : res.json();
}
export type User = {
  name: string;
  role: "Scheduler" | "TaskOperator" | "Admin";
};
export type Catalog = { id: number; name: string };

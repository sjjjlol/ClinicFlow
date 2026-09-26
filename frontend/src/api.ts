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
  await checkResponse(res, url);
  return res.status === 204 ? (undefined as T) : res.json();
}
async function checkResponse(res: Response, url: string) {
  if (res.status === 401 && !url.startsWith("/api/auth/"))
    window.dispatchEvent(new Event("session-expired"));
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new ApiError(
      res.status,
      err.code ?? "request_failed",
      (err.message
        ? `${err.message}${err.correlationId ? `（请求标识：${err.correlationId}）` : ""}`
        : undefined) ??
        {
          401: "请先登录",
          403: "当前角色没有此操作权限",
          429: "操作过于频繁，请稍后重试",
        }[res.status] ??
        "请求失败，请稍后重试",
    );
  }
}

export type StreamTrace = {
  tool: string;
  arguments: string;
  outcome: string;
  durationMs: number;
};
export type StreamEvent<T> = {
  type: string;
  text?: string;
  message?: string;
  sessionId?: string;
  trace?: StreamTrace;
  reply?: T;
  code?: string;
  status?: number;
};

export async function streamApi<T>(
  url: string,
  body: unknown,
  onEvent: (event: StreamEvent<T>) => void,
  signal: AbortSignal,
): Promise<T> {
  if (!csrf) await refreshCsrf();
  const res = await fetch(url, {
    method: "POST",
    signal,
    headers: {
      "Content-Type": "application/json",
      Accept: "text/event-stream",
      "X-CSRF-TOKEN": csrf,
    },
    body: JSON.stringify(body),
  });
  await checkResponse(res, url);
  if (
    !res.body ||
    !res.headers.get("content-type")?.includes("text/event-stream")
  )
    throw new Error("助手未返回有效流式回复，请重试");
  const reader = res.body.getReader();
  const decoder = new TextDecoder();
  let buffer = "";
  let data: string[] = [];
  try {
    while (true) {
      const { value, done } = await reader.read();
      buffer += done
        ? decoder.decode()
        : decoder.decode(value, { stream: true });
      if (buffer.length > 1_000_000) throw new Error("助手回复过长，请重试");
      let end: number;
      while ((end = buffer.indexOf("\n")) >= 0) {
        const line = buffer.slice(0, end).replace(/\r$/, "");
        buffer = buffer.slice(end + 1);
        if (line.startsWith("data:")) data.push(line.slice(5).trimStart());
        else if (line === "" && data.length) {
          const event = JSON.parse(data.join("\n")) as StreamEvent<T>;
          data = [];
          if (event.type === "error")
            throw new ApiError(
              event.status ?? 503,
              event.code ?? "stream_failed",
              event.message ?? "助手服务中断",
            );
          if (event.type === "result" && event.reply) return event.reply;
          onEvent(event);
        }
      }
      if (done) throw new Error("回复连接已中断，未收到完整结果，请重试");
    }
  } finally {
    await reader.cancel().catch(() => {});
    reader.releaseLock();
  }
}

export type User = {
  patientId?: number | null;
  name: string;
  role: "Scheduler" | "TaskOperator" | "Admin" | "Booker";
};
export type Catalog = { id: number; name: string };

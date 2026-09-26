import { useEffect, useRef, useState, type FormEvent } from "react";
import {
  api,
  ApiError,
  streamApi,
  type StreamEvent,
  type StreamTrace,
  type Catalog,
} from "./api";
import { time, type Appointment } from "./Scheduling";

type Candidate = {
  id: string;
  booking: Omit<Appointment, "id" | "status" | "version">;
  patientName: string;
  resourceName: string;
  reason: string;
};
type Reply = {
  sessionId: string;
  message: string;
  candidates: Candidate[];
  trace: {
    tool: string;
    arguments: string;
    outcome: string;
    durationMs: number;
  }[];
  status: string;
  appointment?: Appointment;
};
export default function AppointmentAssistant({
  patients,
  selectedPatientId,
  onPatientChange,
  onCreated,
  selfService = false,
}: {
  selfService?: boolean;
  patients: Catalog[];
  selectedPatientId: number;
  onPatientChange: (id: number) => void;
  onCreated: (a: Appointment) => Promise<void>;
}) {
  const [config, setConfig] = useState<{
    configured: boolean;
    demoEnabled: boolean;
  } | null>(null);
  const [messages, setMessages] = useState<{ role: string; text: string }[]>(
    [],
  );
  const [reply, setReply] = useState<Reply | null>(null);
  const [sessionId, setSessionId] = useState<string | undefined>();
  const [streamText, setStreamText] = useState("");
  const [progress, setProgress] = useState("");
  const [liveTrace, setLiveTrace] = useState<StreamTrace[]>([]);
  const [sending, setSending] = useState(false);
  const active = useRef<AbortController | null>(null);
  const conversation = useRef<HTMLDivElement>(null);
  useEffect(() => () => active.current?.abort(), []);
  useEffect(() => {
    const node = conversation.current;
    if (node) node.scrollTop = node.scrollHeight;
  }, [messages, streamText, progress]);
  function onStream(event: StreamEvent<Reply>) {
    if (event.type === "session" && event.sessionId)
      setSessionId(event.sessionId);
    if (event.type === "message_start") {
      setStreamText("");
      setProgress(event.message ?? "正在回复…");
    }
    if (event.type === "delta") {
      setStreamText((v) => v + (event.text ?? ""));
      setProgress("正在回复…");
    }
    if (event.type === "progress") setProgress(event.message ?? "正在处理…");
    if (event.type === "trace" && event.trace)
      setLiveTrace((v) => [...v, event.trace!]);
  }
  async function stream(url: string, body: unknown) {
    const controller = new AbortController();
    active.current = controller;
    setStreamText("");
    setLiveTrace([]);
    setProgress("正在连接预约助手…");
    const timer = window.setTimeout(() => controller.abort(), 110_000);
    try {
      return await streamApi<Reply>(url, body, onStream, controller.signal);
    } finally {
      window.clearTimeout(timer);
      active.current = null;
      setStreamText("");
      setProgress("");
    }
  }
  const [input, setInput] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [uncertain, setUncertain] = useState<string | null>(null);
  const [demoIds, setDemoIds] = useState<string[]>([]);
  useEffect(() => {
    api<typeof config>("/api/agent/config")
      .then(setConfig)
      .catch((e) => setError(e.message));
  }, []);
  function receive(next: Reply) {
    setReply(next);
    setSessionId(next.sessionId);
    setMessages((v) => [...v, { role: "助手", text: next.message }]);
  }
  async function send(e: FormEvent) {
    e.preventDefault();
    if (!input.trim()) return;
    setBusy(true);
    setError("");
    setSending(true);
    const text = input;
    setInput("");
    setMessages((v) => [...v, { role: "你", text }]);
    // Any new instruction invalidates the previous proposal, including when transport fails.
    setReply((v) => (v ? { ...v, candidates: [] } : null));
    try {
      receive(
        await stream("/api/agent/messages/stream", {
          sessionId,
          message: text,
          selectedPatientId,
        }),
      );
    } catch (e) {
      setError(
        (e as Error).name === "AbortError"
          ? "回复已停止，未创建预约；可修改需求后重新发送。"
          : (e as Error).message,
      );
      setInput(text);
    } finally {
      setBusy(false);
      setSending(false);
    }
  }
  async function confirm(c: Candidate) {
    if (!reply) return;
    setBusy(true);
    setError("");
    setUncertain(c.id);
    try {
      const next = await stream(
        `/api/agent/${reply.sessionId}/confirm/stream`,
        {
          candidateId: c.id,
        },
      );
      receive(next);
      setUncertain(null);
      if (next.appointment) await onCreated(next.appointment);
    } catch (e) {
      if (
        e instanceof ApiError &&
        ["stale_candidate", "session_expired"].includes(e.code)
      ) {
        setUncertain(null);
        setReply((v) =>
          v ? { ...v, candidates: [], status: "stopped" } : null,
        );
      }
      setError(
        `${(e as Error).message}。若结果不明，请重试同一候选以核实，或先查看预约列表。`,
      );
    } finally {
      setBusy(false);
    }
  }
  async function simulate(c: Candidate) {
    if (
      !reply ||
      !window.confirm(
        "演示操作将真实创建一笔占位预约，之后可在预约列表中取消。继续？",
      )
    )
      return;
    setBusy(true);
    setError("");
    try {
      await api(`/api/agent/${reply.sessionId}/simulate-conflict`, {
        candidateId: c.id,
      });
      setDemoIds((v) => [...v, c.id]);
      setMessages((v) => [
        ...v,
        {
          role: "系统",
          text: "已创建演示占位预约。现在确认原候选，可观察冲突恢复。",
        },
      ]);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  return (
    <section className="panel assistant" aria-label="预约协调助手">
      <div className="panel-heading">
        <h2>
          预约协调助手 <span>Pi Agent · Kimi</span>
        </h2>
        <button
          disabled={busy || !!uncertain}
          onClick={() => {
            setReply(null);
            setSessionId(undefined);
            setLiveTrace([]);
            setMessages([]);
            setError("");
            setInput("");
            setDemoIds([]);
          }}
        >
          新会话
        </button>
      </div>
      <p className="muted">
        描述需求，核对候选，再确认创建。上海时区 · 工作日 09:00–17:00 ·
        {selfService ? "仅为本人预约" : "演示预约档案"}
      </p>
      {config && !config.configured && (
        <p className="notice">
          助手尚未配置 Kimi API，可继续使用预约列表中的“新建预约”。
        </p>
      )}
      <div
        className="assistant-conversation"
        ref={conversation}
        role="log"
        aria-label="助手对话"
        aria-live="polite"
      >
        {messages.length === 0 && (
          <p className="muted">
            {selfService
              ? "例如：帮我找下周一到周五下午连续45分钟的预约，任意预约室。"
              : "例如：帮当前患者找下周一到周五下午连续45分钟的预约，任意预约室。"}
          </p>
        )}
        {messages.map((m, i) => (
          <div
            className={`assistant-message ${m.role === "你" ? "user-message" : ""}`}
            key={i}
          >
            <strong>{m.role}</strong>
            <p>{m.text}</p>
          </div>
        ))}
        {busy && (
          <div
            className="assistant-message streaming-message"
            aria-label="助手流式回复"
          >
            <strong>助手</strong>
            <p>
              {streamText || progress || "正在处理…"}
              <span className="stream-cursor" aria-hidden="true">
                ▍
              </span>
            </p>
          </div>
        )}
      </div>
      {busy && (
        <div className="assistant-progress" aria-live="polite">
          <span>{progress}</span>
          {sending && (
            <button type="button" onClick={() => active.current?.abort()}>
              停止回复
            </button>
          )}
        </div>
      )}
      {busy && liveTrace.length > 0 && (
        <ul className="assistant-live-trace" aria-label="实时执行记录">
          {liveTrace.map((t, i) => (
            <li key={i}>✓ {t.outcome}</li>
          ))}
        </ul>
      )}
      {error && (
        <p role="alert" className="error">
          {error}
        </p>
      )}
      <div className="assistant-candidates">
        {reply?.candidates.map((c) => (
          <article className="assistant-candidate" key={c.id}>
            <strong>
              {c.patientName} · {c.resourceName}
            </strong>
            <p>
              {time(c.booking.startUtc)} — {time(c.booking.endUtc).slice(-5)}
            </p>
            <p>
              {(Date.parse(c.booking.endUtc) - Date.parse(c.booking.startUtc)) /
                60000}{" "}
              分钟 · 上海时间
            </p>
            <p className="muted">{c.reason}</p>
            <button
              className="primary"
              disabled={busy || (!!uncertain && uncertain !== c.id)}
              onClick={() => void confirm(c)}
            >
              {uncertain === c.id ? "重试确认此候选" : "确认此候选并创建"}
            </button>
            {config?.demoEnabled && (
              <button
                disabled={busy || !!uncertain || demoIds.includes(c.id)}
                onClick={() => void simulate(c)}
              >
                {demoIds.includes(c.id) ? "已模拟占位" : "模拟时段被抢占"}
              </button>
            )}
          </article>
        ))}
      </div>
      {!!reply?.trace.length && (
        <details className="assistant-trace">
          <summary>本轮执行记录（{reply.trace.length}）</summary>
          {reply.trace.map((t, i) => (
            <div key={i}>
              <strong>{t.tool}</strong> · {t.durationMs} ms
              <pre>{t.arguments}</pre>
              <p>{t.outcome}</p>
            </div>
          ))}
        </details>
      )}
      {reply?.status === "created" && (
        <p role="status" className="notice">
          创建成功。需要另一笔预约时，请开启新会话。
        </p>
      )}
      <form className="assistant-form" onSubmit={(e) => void send(e)}>
        <label>
          {selfService ? "预约人（本人）" : "助手当前患者"}
          <select
            value={selectedPatientId}
            disabled={selfService || busy || !!uncertain}
            onChange={(e) => {
              onPatientChange(Number(e.target.value));
              setReply((v) => (v ? { ...v, candidates: [] } : null));
            }}
          >
            {patients
              .filter((p) => selfService || p.id === 1 || p.id === 2)
              .map((p) => (
                <option value={p.id} key={p.id}>
                  {p.name}
                </option>
              ))}
          </select>
        </label>
        <label>
          预约需求
          <textarea
            value={input}
            maxLength={2000}
            rows={3}
            disabled={busy || !!uncertain || reply?.status === "created"}
            onChange={(e) => setInput(e.target.value)}
            placeholder="说明日期范围、时长和偏好；信息不足时助手会追问"
          />
        </label>
        <button
          className="primary"
          disabled={
            busy ||
            !!uncertain ||
            !config?.configured ||
            !input.trim() ||
            reply?.status === "created"
          }
        >
          {busy ? "正在处理…" : "发送需求"}
        </button>
      </form>
    </section>
  );
}

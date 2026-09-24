import { useEffect, useState, type FormEvent } from "react";
import { api, ApiError, type Catalog } from "./api";
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
}: {
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
    setMessages((v) => [...v, { role: "助手", text: next.message }]);
  }
  async function send(e: FormEvent) {
    e.preventDefault();
    if (!input.trim()) return;
    setBusy(true);
    setError("");
    const text = input;
    setInput("");
    setMessages((v) => [...v, { role: "你", text }]);
    // Any new instruction invalidates the previous proposal, including when transport fails.
    setReply((v) => (v ? { ...v, candidates: [] } : null));
    try {
      receive(
        await api<Reply>("/api/agent/messages", {
          sessionId: reply?.sessionId,
          message: text,
          selectedPatientId,
        }),
      );
    } catch (e) {
      setError((e as Error).message);
      setInput(text);
    } finally {
      setBusy(false);
    }
  }
  async function confirm(c: Candidate) {
    if (!reply) return;
    setBusy(true);
    setError("");
    setUncertain(c.id);
    try {
      const next = await api<Reply>(`/api/agent/${reply.sessionId}/confirm`, {
        candidateId: c.id,
      });
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
          预约协调助手 <span>Kimi Agent</span>
        </h2>
        <button
          disabled={busy || !!uncertain}
          onClick={() => {
            setReply(null);
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
        仅虚构患者
      </p>
      {config && !config.configured && (
        <p className="notice">
          助手尚未配置 Kimi API，可继续使用预约列表中的“新建预约”。
        </p>
      )}
      <div
        className="assistant-conversation"
        role="log"
        aria-label="助手对话"
        aria-live="polite"
      >
        {messages.length === 0 && (
          <p className="muted">
            例如：帮当前患者找下周一到周五下午连续 45 分钟的预约，任意预约室。
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
      </div>
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
          助手当前患者
          <select
            value={selectedPatientId}
            disabled={busy || !!uncertain}
            onChange={(e) => {
              onPatientChange(Number(e.target.value));
              setReply((v) => (v ? { ...v, candidates: [] } : null));
            }}
          >
            {patients
              .filter((p) => p.id === 1 || p.id === 2)
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

import { useDialogFocus } from "./useDialogFocus";
import { useEffect, useState, type FormEvent } from "react";
import { api, type Catalog, type User } from "./api";
export type Appointment = {
  id: string;
  patientId: number;
  resourceId: number;
  startUtc: string;
  endUtc: string;
  status: string;
  version: number;
};
type Detail = {
  appointment: Appointment;
  tasks: { id: number; name: string; completed: boolean }[];
  audit: {
    id: number;
    action: string;
    actor: string;
    atUtc: string;
    version: number;
  }[];
  sync: {
    id: string;
    status: string;
    version: number;
    attempts: number;
    lastError: string | null;
  }[];
};
const labels: Record<string, string> = {
  Pending: "待确认",
  Confirmed: "已确认",
  Cancelled: "已取消",
  Delivered: "已同步",
  Failed: "同步失败",
  Processing: "同步中",
  SyncRetry: "重新触发同步",
  Created: "创建预约",
  Rescheduled: "改期",
  TaskCompleted: "完成任务",
};
export function time(s: string) {
  return new Intl.DateTimeFormat("zh-CN", {
    timeZone: "Asia/Shanghai",
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
    hour12: false,
  }).format(new Date(s.endsWith("Z") ? s : s + "Z"));
}
function localInput(s: string) {
  return new Date(
    new Date(s.endsWith("Z") ? s : s + "Z").getTime() + 8 * 3600000,
  )
    .toISOString()
    .slice(0, 16);
}
export default function Scheduling({
  user,
  resources,
}: {
  user: User;
  resources: Catalog[];
}) {
  const [patients, setPatients] = useState<Catalog[]>([]),
    [items, setItems] = useState<Appointment[]>([]),
    [total, setTotal] = useState(0),
    [page, setPage] = useState(1),
    [status, setStatus] = useState("");
  const [detail, setDetail] = useState<Detail | null>(null),
    [form, setForm] = useState<"create" | Appointment | null>(null),
    [error, setError] = useState(""),
    [busy, setBusy] = useState(false),
    [loading, setLoading] = useState(true),
    [notice, setNotice] = useState("");
  const [draft, setDraft] = useState({
    patientId: 1,
    resourceId: 1,
    start: localInput(
      new Date(
        Math.ceil(Date.now() / 900000) * 900000 + 86400000,
      ).toISOString(),
    ),
    duration: 60,
  });
  const [claims, setClaims] = useState<{ slotStartUtc: string }[]>([]);
  const [request, setRequest] = useState<{
    fingerprint: string;
    key: string;
  } | null>(null);
  useDialogFocus(form ? "form" : detail ? "detail" : null, () => {
    if (busy) return;
    if (form) setForm(null);
    else setDetail(null);
  });
  async function refresh() {
    setLoading(true);
    try {
      const r = await api<{ items: Appointment[]; total: number }>(
        `/api/appointments?page=${page}&size=10${status ? "&status=" + status : ""}`,
      );
      setItems(r.items);
      setTotal(r.total);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setLoading(false);
    }
  }
  async function open(id: string) {
    try {
      setDetail(await api<Detail>("/api/appointments/" + id));
    } catch (e) {
      setError((e as Error).message);
    }
  }
  useEffect(() => {
    const id = new URLSearchParams(location.search).get("appointment");
    if (id) void open(id);
    api<Catalog[]>("/api/patients")
      .then(setPatients)
      .catch((e) => setError(e.message));
  }, []);
  useEffect(() => {
    void refresh();
  }, [page, status]);
  useEffect(() => {
    if (!form || !draft.start) return;
    let active = true;
    const start = new Date(draft.start.slice(0, 10) + "T00:00:00+08:00");
    api<{ slotStartUtc: string }[]>(
      `/api/resources/${draft.resourceId}/slots?start=${start.toISOString()}&end=${new Date(+start + 86400000).toISOString()}`,
    )
      .then((v) => {
        if (active) setClaims(v);
      })
      .catch((e) => {
        if (active) setError(e.message);
      });
    return () => {
      active = false;
    };
  }, [form, draft.start, draft.resourceId]);
  async function submit(e: FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError("");
    try {
      const start = new Date(draft.start + ":00+08:00");
      const body = {
        patientId: draft.patientId,
        resourceId: draft.resourceId,
        startUtc: start.toISOString(),
        endUtc: new Date(+start + draft.duration * 60000).toISOString(),
      };
      const endpoint =
        form === "create"
          ? "/api/appointments"
          : `/api/appointments/${form!.id}/reschedule`;
      const payload =
        form === "create" ? body : { ...body, version: form!.version };
      const fingerprint = endpoint + JSON.stringify(payload);
      const key =
        request?.fingerprint === fingerprint
          ? request.key
          : crypto.randomUUID();
      setRequest({ fingerprint, key });
      const a = await api<Appointment>(endpoint, payload, key);
      setForm(null);
      setRequest(null);
      setNotice(
        form === "create"
          ? "预约已创建，时段已为你保留。"
          : "改期成功，前置任务已重置。",
      );
      await refresh();
      await open(a.id);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  async function mutate(action: string, taskId?: number) {
    if (!detail) return;
    if (
      action === "cancel" &&
      !window.confirm("确认取消此预约？占用时段将被释放。")
    )
      return;
    setBusy(true);
    setError("");
    const body = {
      version: detail.appointment.version,
      ...(taskId ? { taskId } : {}),
    };
    const endpoint = `/api/appointments/${detail.appointment.id}/${action}`;
    const fingerprint = endpoint + JSON.stringify(body);
    const key =
      request?.fingerprint === fingerprint ? request.key : crypto.randomUUID();
    setRequest({ fingerprint, key });
    try {
      const a = await api<Appointment>(endpoint, body, key);
      setRequest(null);
      await open(a.id);
      await refresh();
      setNotice(
        action === "cancel"
          ? "预约已取消，资源时段已释放。"
          : action === "confirm"
            ? "预约已确认。"
            : "前置任务已完成。",
      );
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  return (
    <>
      <section className="panel">
        <div className="panel-heading">
          <h2>
            预约安排 <span>Appointments</span>
          </h2>
          <div className="toolbar">
            <select
              aria-label="状态筛选"
              value={status}
              onChange={(e) => {
                setStatus(e.target.value);
                setPage(1);
              }}
            >
              <option value="">全部状态</option>
              {["Pending", "Confirmed", "Cancelled"].map((s) => (
                <option key={s} value={s}>
                  {labels[s]}
                </option>
              ))}
            </select>
            <button
              onClick={() => {
                void refresh();
                setError("");
              }}
            >
              刷新
            </button>
            {user.role === "Scheduler" && (
              <button
                className="primary"
                onClick={() => {
                  setForm("create");
                  setError("");
                  setRequest(null);
                }}
              >
                ＋ 新建预约
              </button>
            )}
          </div>
        </div>
        {notice && (
          <div className="notice" role="status">
            ✓ {notice}
          </div>
        )}
        {error && !form && !detail && (
          <div className="error" role="alert">
            {error}
          </div>
        )}
        {loading ? (
          <div className="empty">正在加载预约…</div>
        ) : items.length === 0 ? (
          <div className="empty">
            <div className="empty-icon">▦</div>
            <h3>为新的一天，安排第一笔预约</h3>
            <p>选择患者与资源，让每个时间段各就其位。</p>
          </div>
        ) : (
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>患者</th>
                  <th>资源</th>
                  <th>预约时间 · UTC+8</th>
                  <th>状态</th>
                  <th>版本</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {items.map((a) => (
                  <tr key={a.id}>
                    <td>
                      <span className="patient-avatar">
                        {patients.find((p) => p.id === a.patientId)?.name[0] ??
                          "患"}
                      </span>
                      {patients.find((p) => p.id === a.patientId)?.name}
                    </td>
                    <td>
                      {resources.find((r) => r.id === a.resourceId)?.name}
                    </td>
                    <td>
                      {time(a.startUtc)} — {time(a.endUtc).slice(-5)}
                    </td>
                    <td>
                      <span
                        className={
                          "badge " +
                          (a.status === "Confirmed"
                            ? "green"
                            : a.status === "Cancelled"
                              ? "gray"
                              : "")
                        }
                      >
                        {labels[a.status]} · {a.status}
                      </span>
                    </td>
                    <td className="muted">v{a.version}</td>
                    <td>
                      <button
                        className="link-button"
                        onClick={() => open(a.id)}
                      >
                        查看 →
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
        <footer className="pagination">
          <span>共 {total} 笔预约</span>
          <div>
            <button disabled={page === 1} onClick={() => setPage(page - 1)}>
              ‹
            </button>
            <span>{page}</span>
            <button
              disabled={page * 10 >= total}
              onClick={() => setPage(page + 1)}
            >
              ›
            </button>
          </div>
        </footer>
      </section>
      {form && (
        <div className="overlay">
          <section
            className="dialog"
            role="dialog"
            aria-modal="true"
            aria-labelledby="booking-title"
          >
            <div className="dialog-heading">
              <div>
                <p className="eyebrow">NEW APPOINTMENT</p>
                <h2 id="booking-title">
                  {form === "create" ? "新建预约" : "调整预约时间"}
                </h2>
              </div>
              <button
                aria-label="关闭表单"
                disabled={busy}
                onClick={() => setForm(null)}
              >
                ×
              </button>
            </div>
            <form onSubmit={submit}>
              <div className="form-grid">
                <label>
                  患者
                  <select
                    disabled={form !== "create"}
                    value={draft.patientId}
                    onChange={(e) =>
                      setDraft({ ...draft, patientId: +e.target.value })
                    }
                  >
                    {patients.map((p) => (
                      <option key={p.id} value={p.id}>
                        {p.name}
                      </option>
                    ))}
                  </select>
                </label>
                <label>
                  预约资源
                  <select
                    value={draft.resourceId}
                    onChange={(e) =>
                      setDraft({ ...draft, resourceId: +e.target.value })
                    }
                  >
                    {resources.map((r) => (
                      <option key={r.id} value={r.id}>
                        {r.name}
                      </option>
                    ))}
                  </select>
                </label>
                <label>
                  开始时间（上海 UTC+8）
                  <input
                    type="datetime-local"
                    step="900"
                    required
                    value={draft.start}
                    onChange={(e) =>
                      setDraft({ ...draft, start: e.target.value })
                    }
                  />
                </label>
                <label>
                  时长
                  <select
                    value={draft.duration}
                    onChange={(e) =>
                      setDraft({ ...draft, duration: +e.target.value })
                    }
                  >
                    {[15, 30, 45, 60, 90, 120, 180, 240].map((n) => (
                      <option key={n} value={n}>
                        {n} 分钟
                      </option>
                    ))}
                  </select>
                </label>
              </div>
              <div className="availability">
                <small>当日已占用时段</small>
                <p>
                  {claims.length
                    ? claims
                        .map((c) => time(c.slotStartUtc).slice(-5))
                        .join(" · ")
                    : "暂无占用，可选择合适的时间。"}
                </p>
                <small>每个标记代表15分钟；提交时会再次检查可用性。</small>
              </div>
              {error && (
                <p role="alert" className="error">
                  {error}
                </p>
              )}
              <div className="dialog-actions">
                <button
                  type="button"
                  disabled={busy}
                  onClick={() => setForm(null)}
                >
                  取消
                </button>
                <button className="primary" disabled={busy}>
                  {busy
                    ? "正在保存…"
                    : form === "create"
                      ? "创建预约"
                      : "保存改期"}
                </button>
              </div>
            </form>
          </section>
        </div>
      )}
      {detail && !form && (
        <div className="overlay" onClick={() => setDetail(null)}>
          <section
            className="dialog detail"
            role="dialog"
            aria-modal="true"
            aria-labelledby="detail-title"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="dialog-heading">
              <div>
                <p className="eyebrow">APPOINTMENT DETAILS</p>
                <h2 id="detail-title">
                  预约详情{" "}
                  <span className="badge">
                    {labels[detail.appointment.status]} · v
                    {detail.appointment.version}
                  </span>
                </h2>
              </div>
              <button aria-label="关闭详情" onClick={() => setDetail(null)}>
                ×
              </button>
            </div>
            {error && (
              <p className="error" role="alert">
                {error}
              </p>
            )}
            <div className="detail-summary">
              <strong>
                {
                  patients.find((p) => p.id === detail.appointment.patientId)
                    ?.name
                }
              </strong>
              <p>
                {
                  resources.find((r) => r.id === detail.appointment.resourceId)
                    ?.name
                }{" "}
                · {time(detail.appointment.startUtc)} —{" "}
                {time(detail.appointment.endUtc).slice(-5)}
              </p>
              <small className="muted">{detail.appointment.id}</small>
            </div>
            <h3>
              前置任务 <small>Prerequisites</small>
            </h3>
            {detail.tasks.map((t) => (
              <div className="task-row" key={t.id}>
                <span>
                  {t.completed ? "✓" : "○"} {t.name}
                </span>
                <span className={"badge " + (t.completed ? "green" : "gray")}>
                  {t.completed ? "已完成" : "待完成"}
                </span>
                {user.role === "TaskOperator" &&
                  !t.completed &&
                  detail.appointment.status === "Pending" && (
                    <button
                      disabled={busy}
                      className="link-button"
                      onClick={() => mutate("complete-task", t.id)}
                    >
                      完成 {t.name}
                    </button>
                  )}
              </div>
            ))}
            <h3>
              外部同步 <small>External sync</small>
            </h3>
            <p className="muted">本地预约已保存。外部同步独立处理。</p>
            {detail.sync.slice(0, 3).map((s) => (
              <div className="task-row" key={s.id}>
                <span>v{s.version}</span>
                <span
                  className={
                    "badge " +
                    (s.status === "Failed"
                      ? "red"
                      : s.status === "Delivered"
                        ? "green"
                        : "")
                  }
                >
                  {s.status === "Pending"
                    ? "等待同步"
                    : (labels[s.status] ?? s.status)}
                </span>
                {s.lastError && (
                  <small className="sync-error">{s.lastError}</small>
                )}
              </div>
            ))}
            <h3>
              操作记录 <small>Audit trail</small>
            </h3>
            <div className="timeline">
              {detail.audit.map((a) => (
                <div key={a.id}>
                  <i />
                  <strong>{labels[a.action] ?? a.action}</strong>
                  <span>
                    {a.actor} · {time(a.atUtc)} · v{a.version}
                  </span>
                </div>
              ))}
            </div>
            <div className="dialog-actions">
              <button
                disabled={busy}
                onClick={() => {
                  setError("");
                  void open(detail.appointment.id);
                }}
              >
                刷新详情
              </button>
              {user.role === "Scheduler" &&
                detail.appointment.status !== "Cancelled" && (
                  <>
                    {detail.appointment.status === "Pending" && (
                      <button
                        disabled={busy}
                        className="primary"
                        onClick={() => mutate("confirm")}
                      >
                        确认预约
                      </button>
                    )}
                    <button
                      disabled={busy}
                      className="danger"
                      onClick={() => mutate("cancel")}
                    >
                      取消预约
                    </button>
                    <button
                      disabled={busy}
                      className="primary"
                      onClick={() => {
                        const a = detail.appointment;
                        setDraft({
                          patientId: a.patientId,
                          resourceId: a.resourceId,
                          start: localInput(a.startUtc),
                          duration:
                            (Date.parse(a.endUtc) - Date.parse(a.startUtc)) /
                            60000,
                        });
                        setForm(a);
                        setError("");
                        setRequest(null);
                      }}
                    >
                      改期
                    </button>
                  </>
                )}
            </div>
          </section>
        </div>
      )}
    </>
  );
}

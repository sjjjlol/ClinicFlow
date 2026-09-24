import { useDialogFocus } from "./useDialogFocus";
import { useEffect, useState } from "react";
import { api } from "./api";
import { time } from "./Scheduling";
type Message = {
  id: string;
  appointmentId: string;
  version: number;
  status: string;
  attempts: number;
  lastError: string | null;
  nextAttemptUtc: string;
};
export default function SyncQueue() {
  const [items, setItems] = useState<Message[]>([]),
    [error, setError] = useState(""),
    [busy, setBusy] = useState(""),
    [loading, setLoading] = useState(true),
    [history, setHistory] = useState<
      { id: number; atUtc: string; outcome: string }[] | null
    >(null);
  useDialogFocus(history ? "history" : null, () => setHistory(null));
  async function load() {
    try {
      setItems(await api("/api/sync"));
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setLoading(false);
    }
  }
  useEffect(() => {
    void load();
    const timer = setInterval(load, 5000);
    return () => clearInterval(timer);
  }, []);
  async function retry(id: string) {
    setBusy(id);
    setError("");
    try {
      const storeKey = "sync-retry:" + id;
      let key = sessionStorage.getItem(storeKey);
      if (!key) {
        key = crypto.randomUUID();
        sessionStorage.setItem(storeKey, key);
      }
      await api("/api/sync/" + id + "/retry", {}, key);
      sessionStorage.removeItem(storeKey);
      await load();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy("");
    }
  }
  return (
    <section className="panel">
      <div className="panel-heading">
        <div>
          <h2>
            外部同步队列 <span>Outbox</span>
          </h2>
          <p className="muted">
            本地保存与外部投递独立 · 每 5 秒刷新 · 最近 100 条
          </p>
        </div>
        <button onClick={() => load()}>刷新队列</button>
      </div>
      {error && (
        <p className="error" role="alert">
          {error}
        </p>
      )}
      {loading ? (
        <div className="empty">正在加载同步队列…</div>
      ) : items.length === 0 ? (
        <div className="empty">暂无同步消息</div>
      ) : (
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>消息 / 预约</th>
                <th>版本</th>
                <th>投递状态</th>
                <th>尝试次数</th>
                <th>最近错误</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {items.map((m) => (
                <tr key={m.id}>
                  <td>
                    <strong>{m.id.slice(0, 8)}</strong>
                    <br />
                    <a
                      className="link-button"
                      href={"/?appointment=" + m.appointmentId}
                    >
                      {m.appointmentId.slice(0, 8)}
                    </a>
                  </td>
                  <td>v{m.version}</td>
                  <td>
                    <span
                      className={
                        "badge " +
                        (m.status === "Delivered"
                          ? "green"
                          : m.status === "Failed"
                            ? "red"
                            : "")
                      }
                    >
                      {
                        (
                          {
                            Pending: "等待重试",
                            Processing: "投递中",
                            Failed: "同步失败",
                            Delivered: "已同步",
                          } as Record<string, string>
                        )[m.status]
                      }
                    </span>
                  </td>
                  <td>{m.attempts} / 5</td>
                  <td className="muted">{m.lastError ?? "—"}</td>
                  <td>
                    <button
                      className="link-button"
                      onClick={async () => {
                        try {
                          setHistory(
                            await api("/api/sync/" + m.id + "/attempts"),
                          );
                        } catch (e) {
                          setError((e as Error).message);
                        }
                      }}
                    >
                      投递记录
                    </button>
                    {m.status === "Failed" && (
                      <button disabled={!!busy} onClick={() => retry(m.id)}>
                        重新触发
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {history && (
        <div className="overlay">
          <section
            role="dialog"
            aria-modal="true"
            aria-label="投递记录"
            className="dialog"
          >
            <div className="dialog-heading">
              <h2>投递记录</h2>
              <button aria-label="关闭记录" onClick={() => setHistory(null)}>
                ×
              </button>
            </div>
            {history.length ? (
              history.map((h) => (
                <div className="task-row" key={h.id}>
                  <span>{time(h.atUtc)}</span>
                  <strong>{h.outcome}</strong>
                </div>
              ))
            ) : (
              <p className="muted">尚未开始投递</p>
            )}
            <p className="footnote">
              Started 表示领取已持久化，进程中断时可能没有最终响应。
            </p>
          </section>
        </div>
      )}
    </section>
  );
}

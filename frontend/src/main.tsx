import SyncQueue from "./SyncQueue";
import Scheduling from "./Scheduling";
import { createRoot } from "react-dom/client";
import { useEffect, useState, type FormEvent } from "react";
import { api, refreshCsrf, type User, type Catalog } from "./api";
import "./style.css";
function App() {
  const [view, setView] = useState<"appointments" | "sync" | "labs">(
    "appointments",
  );
  const [user, setUser] = useState<User | null>(null),
    [ready, setReady] = useState(false),
    [error, setError] = useState("");
  const [busy, setBusy] = useState(false),
    [resources, setResources] = useState<Catalog[]>([]);
  useEffect(() => {
    api<User>("/api/auth/me")
      .then(setUser)
      .catch(() => {})
      .finally(() => setReady(true));
  }, []);
  useEffect(() => {
    if (user)
      api<Catalog[]>("/api/resources")
        .then(setResources)
        .catch((e) => setError(e.message));
  }, [user]);
  async function login(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setBusy(true);
    setError("");
    const f = new FormData(e.currentTarget);
    try {
      setUser(
        await api<User>("/api/auth/login", {
          username: f.get("username"),
          password: f.get("password"),
        }),
      );
      await refreshCsrf();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  if (!ready) return <div className="loading">正在连接工作台…</div>;
  if (!user)
    return (
      <div className="login-scene">
        <div className="orb one" />
        <div className="orb two" />
        <form className="login-card" onSubmit={login}>
          <div className="brand-icon">✳</div>
          <p className="eyebrow">CLINICFLOW</p>
          <h1>让预约，有条不紊。</h1>
          <p className="muted">清晰的安排，安心的协作。</p>
          <label>
            演示角色
            <select name="username">
              <option value="scheduler">预约人员 · Scheduler</option>
              <option value="taskoperator">任务人员 · TaskOperator</option>
              <option value="admin">管理员 · Admin</option>
            </select>
          </label>
          <label>
            密码
            <input
              name="password"
              type="password"
              autoComplete="current-password"
              required
              placeholder="输入本机 .env 中的演示密码"
            />
          </label>
          {error && (
            <p role="alert" className="error">
              {error}
            </p>
          )}
          <button className="primary wide" disabled={busy}>
            {busy ? "正在登录…" : "进入工作台 →"}
          </button>
          <p className="footnote">学习演示环境 · 仅使用虚构数据</p>
        </form>
      </div>
    );
  return (
    <div className="shell">
      <aside>
        <a className="brand" href="/">
          ✳ <span>ClinicFlow</span>
        </a>
        <div className="workspace-label">工作空间</div>
        <nav>
          <button
            className={view === "appointments" ? "nav-active" : ""}
            onClick={() => setView("appointments")}
          >
            ▦ <span>预约工作台</span>
          </button>
          {user.role === "Admin" && (
            <button
              className={view === "sync" ? "nav-active" : ""}
              onClick={() => setView("sync")}
            >
              ↻ <span>同步队列</span>
            </button>
          )}
          <button
            className={view === "labs" ? "nav-active" : ""}
            onClick={() => setView("labs")}
          >
            ⌘ <span>故障实验</span>
          </button>
        </nav>
        <div className="sidebar-bottom">
          <span className="avatar">{user.name[0].toUpperCase()}</span>
          <div>
            <strong>{user.role}</strong>
            <small>演示账号</small>
          </div>
          <button
            aria-label="退出登录"
            onClick={async () => {
              await api("/api/auth/logout", {});
              setUser(null);
              setView("appointments");
              await refreshCsrf();
            }}
          >
            ↪
          </button>
        </div>
      </aside>
      <main className="workspace">
        <header>
          <span>
            工作空间 <span className="crumb">/ 预约工作台</span>
          </span>
          <span className="environment">
            <i /> 演示环境
          </span>
        </header>
        <section className="page-heading">
          <p className="eyebrow">YOUR DAY, IN FLOW</p>
          <h1>预约工作台</h1>
          <p className="muted">
            每一个安排，都清晰可见。
            <span className="timezone">Asia/Shanghai · UTC+8</span>
          </p>
        </section>
        {error && (
          <div role="alert" className="error">
            {error}
          </div>
        )}
        <div className="stats">
          <article>
            <small>可用资源</small>
            <strong>{resources.length}</strong>
            <span>准备就绪</span>
          </article>
          <article>
            <small>当前角色</small>
            <strong className="text-stat">{user.role}</strong>
            <span>服务端权限保护</span>
          </article>
          <article>
            <small>工作台状态</small>
            <strong className="text-stat">已连接</strong>
            <span>所有数据来自真实 API</span>
          </article>
        </div>
        {view === "labs" ? (
          <section className="panel">
            <div className="panel-heading">
              <h2>
                故障实验 <span>Learning lab</span>
              </h2>
            </div>
            <div className="lab-info">
              <p>通过三个独立实验，理解系统在失败时的行为。</p>
              <div className="resource-grid">
                {["L1 · 并发覆盖", "L2 · 重复外部处理", "L3 · 慢查询"].map(
                  (t) => (
                    <div className="resource-card" key={t}>
                      <strong>{t}</strong>
                    </div>
                  ),
                )}
              </div>
              <p className="muted">
                实验使用独立程序与数据存储，需在本机终端显式启动。
              </p>
              <a
                className="link-button"
                href="https://github.com/sjjjlol/ClinicFlow/blob/main/labs/README.md"
                target="_blank"
                rel="noreferrer"
              >
                阅读实验说明与复现步骤 ↗
              </a>
            </div>
          </section>
        ) : view === "sync" && user.role === "Admin" ? (
          <SyncQueue />
        ) : (
          <Scheduling user={user} resources={resources} />
        )}
        <section className="panel">
          <div className="panel-heading">
            <h2>
              预约资源 <span>Resources</span>
            </h2>
          </div>
          <div className="resource-grid">
            {resources.map((r) => (
              <div className="resource-card" key={r.id}>
                <div className="resource-icon">▣</div>
                <strong>{r.name}</strong>
                <span className="muted">模拟诊疗预约资源</span>
                <span className="badge green">已启用</span>
              </div>
            ))}
          </div>
        </section>
      </main>
    </div>
  );
}
createRoot(document.getElementById("root")!).render(<App />);

import { useState, type FormEvent } from "react";
import { api, refreshCsrf, type User } from "./api";

export default function Auth({
  onLogin,
  initialError,
}: {
  onLogin: (user: User) => void;
  initialError: string;
}) {
  const [username, setUsername] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(initialError);
  async function submit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const data = new FormData(e.currentTarget);
    setError("");
    setBusy(true);
    try {
      await refreshCsrf();
      const user = await api<User>("/api/auth/login", {
        username,
        password: data.get("password"),
      });
      await refreshCsrf();
      onLogin(user);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  return (
    <div className="login-scene">
      <div className="orb one" />
      <div className="orb two" />
      <form className="login-card" onSubmit={submit}>
        <div className="brand-icon">✳</div>
        <p className="eyebrow">CLINICFLOW OIS</p>
        <h1>放疗协作，有序衔接。</h1>
        <p className="muted">ClinicFlow OIS · 预约、诊疗与物理验证</p>
        <label>
          账号
          <input
            name="username"
            autoComplete="username"
            required
            maxLength={40}
            value={username}
            onChange={(e) => setUsername(e.target.value)}
            placeholder="输入工作人员账号"
          />
        </label>
        <label>
          密码
          <input
            name="password"
            type="password"
            autoComplete="current-password"
            required
            maxLength={128}
            placeholder="输入你的密码"
          />
        </label>
        {error && (
          <p role="alert" className="error">
            {error}
          </p>
        )}
        <button className="primary wide" disabled={busy}>
          {busy ? "正在处理…" : "进入工作台 →"}
        </button>
        <details className="demo-login">
          <summary>工作人员演示账号</summary>
          <label>
            演示角色
            <select
              value={
                ["scheduler", "clinician", "physicist"].includes(username)
                  ? username
                  : ""
              }
              onChange={(e) => setUsername(e.target.value)}
            >
              <option value="" disabled>
                选择演示账号
              </option>
              <option value="scheduler">预约人员 · Scheduler</option>
              <option value="clinician">诊疗人员 · Clinician</option>
              <option value="physicist">物理师 · Physicist</option>
            </select>
          </label>
          <p className="footnote">演示密码由本机配置提供。</p>
        </details>
        <p className="footnote">学习演示环境 · 请使用虚构姓名</p>
      </form>
    </div>
  );
}

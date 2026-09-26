import { useState, type FormEvent } from "react";
import { api, refreshCsrf, type User } from "./api";

export default function Auth({
  onLogin,
  initialError,
}: {
  onLogin: (user: User) => void;
  initialError: string;
}) {
  const [register, setRegister] = useState(false);
  const [username, setUsername] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(initialError);
  async function submit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const data = new FormData(e.currentTarget);
    setError("");
    if (register && data.get("password") !== data.get("confirmPassword")) {
      setError("两次输入的密码不一致");
      return;
    }
    setBusy(true);
    try {
      await refreshCsrf();
      const user = await api<User>(
        register ? "/api/auth/register" : "/api/auth/login",
        {
          username,
          password: data.get("password"),
          ...(register ? { displayName: data.get("displayName") } : {}),
        },
      );
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
      <form
        key={register ? "register" : "login"}
        className="login-card"
        onSubmit={submit}
      >
        <div className="brand-icon">✳</div>
        <p className="eyebrow">CLINICFLOW</p>
        <h1>{register ? "创建你的预约账号" : "让预约，有条不紊。"}</h1>
        <p className="muted">
          {register
            ? "注册后可为自己预约，并随时查看进度。"
            : "登录账号，管理每一次预约。"}
        </p>
        <label>
          账号
          <input
            name="username"
            autoComplete="username"
            required
            minLength={3}
            maxLength={40}
            value={username}
            onChange={(e) => setUsername(e.target.value)}
            pattern="[A-Za-z0-9][A-Za-z0-9_\-]{2,39}"
            placeholder="3–40位字母、数字、下划线或短横线"
          />
        </label>
        {register && (
          <label>
            预约姓名
            <input
              name="displayName"
              autoComplete="name"
              required
              maxLength={80}
              placeholder="请输入演示姓名"
            />
          </label>
        )}
        <label>
          密码
          <input
            name="password"
            type="password"
            autoComplete={register ? "new-password" : "current-password"}
            required
            minLength={register ? 8 : undefined}
            maxLength={128}
            placeholder={register ? "至少8个字符" : "输入你的密码"}
          />
        </label>
        {register && (
          <label>
            确认密码
            <input
              name="confirmPassword"
              type="password"
              autoComplete="new-password"
              required
              maxLength={128}
            />
          </label>
        )}
        {error && (
          <p role="alert" className="error">
            {error}
          </p>
        )}
        <button className="primary wide" disabled={busy}>
          {busy ? "正在处理…" : register ? "注册并进入" : "进入工作台 →"}
        </button>
        <button
          className="link-button wide"
          type="button"
          disabled={busy}
          onClick={() => {
            setRegister(!register);
            setError("");
          }}
        >
          {register ? "已有账号？返回登录" : "没有账号？注册预约账号"}
        </button>
        {!register && (
          <details className="demo-login">
            <summary>工作人员演示账号</summary>
            <label>
              演示角色
              <select
                value={
                  ["scheduler", "taskoperator", "admin"].includes(username)
                    ? username
                    : ""
                }
                onChange={(e) => setUsername(e.target.value)}
              >
                <option value="" disabled>
                  选择演示账号
                </option>
                <option value="scheduler">预约人员 · Scheduler</option>
                <option value="taskoperator">任务人员 · TaskOperator</option>
                <option value="admin">管理员 · Admin</option>
              </select>
            </label>
            <p className="footnote">演示密码由本机配置提供。</p>
          </details>
        )}
        <p className="footnote">学习演示环境 · 请使用虚构姓名</p>
      </form>
    </div>
  );
}

import { useEffect, useRef, useState, type FormEvent } from "react";
import Auth from "./Auth";
import { api, ApiError, refreshCsrf, type User } from "./api";
import { useDialogFocus } from "./useDialogFocus";
import OisCalendar from "./OisCalendar";
import {
  displayTime,
  label,
  localTime,
  nextSlot,
  type CareCase,
  type Command,
  type Editor,
  type Field,
  type Plan,
  type Reservation,
  type Workspace,
} from "./ois-types";
import "./ois.css";

const text = (name: string, label: string, optional = false): Field => ({
  name,
  label,
  type: "textarea",
  optional,
});
const reason = () => text("reason", "操作原因");
const dateFields = (start = nextSlot(), end?: string): Field[] => [
  {
    name: "startUtc",
    label: "开始时间（上海）",
    type: "datetime-local",
    value: start,
  },
  {
    name: "endUtc",
    label: "结束时间（上海）",
    type: "datetime-local",
    value:
      end ||
      localTime(new Date(new Date(start + "+08:00").getTime() + 30 * 60000)),
  },
];
const nav = [
  { id: "home", name: "我的工作台", icon: "◈" },
  { id: "calendar", name: "预约日历", icon: "▦" },
  { id: "patients", name: "患者与诊疗", icon: "◎" },
  { id: "machines", name: "机器与占用", icon: "▤" },
  { id: "tasks", name: "协作待办", icon: "☷" },
  { id: "history", name: "操作历史", icon: "↺" },
];

export default function OisApp() {
  const [user, setUser] = useState<User | null>(null),
    [ready, setReady] = useState(false);
  const [data, setData] = useState<Workspace | null>(null),
    [view, setView] = useState("home");
  const [error, setError] = useState(""),
    [notice, setNotice] = useState(""),
    [busy, setBusy] = useState(false);
  const [editor, setEditor] = useState<Editor | null>(null),
    [selected, setSelected] = useState<string | null>(null),
    [patientId, setPatientId] = useState(1);
  const [search, setSearch] = useState("");
  const retry = useRef<{ signature: string; key: string } | null>(null);
  async function refresh() {
    const next = await api<Workspace>("/api/ois/workspace");
    setData(next);
    return next;
  }
  useEffect(() => {
    api<User>("/api/auth/me")
      .then(setUser)
      .catch((e) => {
        if (!(e instanceof ApiError && e.status === 401)) setError(e.message);
      })
      .finally(() => setReady(true));
  }, []);
  useEffect(() => {
    if (user && ["Scheduler", "Clinician", "Physicist"].includes(user.role)) {
      refresh().catch((e) => setError(e.message));
      const timer = setInterval(() => refresh().catch(() => {}), 30000);
      return () => clearInterval(timer);
    }
  }, [user]);
  useEffect(() => {
    const expired = () => {
      setUser(null);
      setData(null);
      setEditor(null);
      setSelected(null);
    };
    window.addEventListener("session-expired", expired);
    return () => window.removeEventListener("session-expired", expired);
  }, []);
  useDialogFocus(editor?.title || selected, () => {
    if (!busy) {
      setEditor(null);
      setSelected(null);
    }
  });
  async function execute(operation: string, command: Command) {
    setBusy(true);
    setError("");
    setNotice("");
    const signature = JSON.stringify({ operation, command });
    if (retry.current?.signature !== signature)
      retry.current = { signature, key: crypto.randomUUID() };
    let committed = false;
    try {
      await api("/api/ois/commands/" + operation, command, retry.current.key);
      committed = true;
      retry.current = null;
      setEditor(null);
      setSelected(null);
      setNotice("已保存，操作记录可追溯。");
      await refresh();
    } catch (e) {
      setError(
        committed
          ? "操作已保存，但刷新失败，请点击刷新工作台。"
          : (e as Error).message,
      );
      if (e instanceof ApiError && e.status < 500) {
        retry.current = null;
        await refresh().catch(() => {});
      }
    } finally {
      setBusy(false);
    }
  }
  async function logout() {
    try {
      await api("/api/auth/logout", {});
      setUser(null);
      setData(null);
      setEditor(null);
      setSelected(null);
      setError("");
      await refreshCsrf();
    } catch (e) {
      setError((e as Error).message);
    }
  }
  if (!ready) return <div className="loading">正在连接 OIS…</div>;
  if (!user)
    return (
      <Auth
        initialError={error}
        onLogin={(u) => {
          setUser(u);
          setError("");
        }}
      />
    );
  if (!["Scheduler", "Clinician", "Physicist"].includes(user.role))
    return (
      <div className="ois-access">
        <h1>此账号没有 OIS 工作台权限</h1>
        <p>请使用 scheduler、clinician 或 physicist 账号。</p>
        <button onClick={logout}>退出登录</button>
      </div>
    );
  if (!data)
    return (
      <div className="loading">
        <p>{error || "正在读取工作台…"}</p>
        <button onClick={() => refresh().catch((e) => setError(e.message))}>
          重试
        </button>
        <button onClick={logout}>退出登录</button>
      </div>
    );
  const role = user.role,
    clinician = role === "Clinician",
    physicist = role === "Physicist",
    scheduler = role === "Scheduler";
  const patientName = (id: number | null) =>
    data.patients.find((p) => p.id === id)?.name || "无需患者到场";
  const resourceName = (id: string) =>
    data.resources.find((r) => r.id === id)?.name || id;
  const patientOptions = data.patients.map((p) => ({
    value: String(p.id),
    label: p.name + " · " + p.identifier,
  }));
  const machineOptions = data.resources
    .filter((r) => r.kind === "Machine")
    .map((r) => ({
      value: r.id,
      label: r.name + (r.available ? "" : "（停用）"),
    }));
  const myTasks = data.tasks.filter((t) => t.role === role && !t.resolvedBy);
  const today = localTime(new Date()).slice(0, 10);
  const todayBookings = data.reservations
    .filter(
      ({ reservation: r }) =>
        localTime(r.startUtc).slice(0, 10) === today &&
        (physicist
          ? r.kind === "Experiment"
          : clinician
            ? r.kind === "Consultation" || r.kind === "Treatment"
            : r.patientId !== null),
    )
    .sort((a, b) =>
      a.reservation.startUtc.localeCompare(b.reservation.startUtc),
    );
  const open = (e: Editor) => {
    setError("");
    setSelected(null);
    setEditor(e);
  };
  function book(
    kind: string,
    resourceId?: string,
    start?: string,
    plan?: Plan,
  ) {
    const options =
      kind === "Consultation"
        ? data!.resources
            .filter((r) => r.kind === "Clinician")
            .map((r) => ({ value: r.id, label: r.name }))
        : machineOptions;
    const fields: Field[] = [];
    if (kind === "Consultation" || kind === "Treatment")
      fields.push({
        name: "patientId",
        label: "患者",
        options: patientOptions,
        value: String(patientId),
      });
    if (kind === "Treatment")
      fields.push({
        name: "caseId",
        label: "诊疗档案（须属于所选患者）",
        options: data!.cases.map((c) => ({
          value: c.id,
          label: patientName(c.patientId) + " · " + c.title,
        })),
      });
    fields.push(
      {
        name: "resourceId",
        label: "预约资源",
        options,
        value: plan?.machineId || resourceId || options[0]?.value,
      },
      ...dateFields(start),
    );
    if (kind === "Maintenance") fields.push(reason());
    open({
      title: "新增" + label(kind) + (kind === "Experiment" ? "占用" : "预约"),
      operation: "book",
      command: { kind, ...(plan ? { planId: plan.id } : {}) },
      fields,
      context: plan
        ? `计划 v${plan.revision} · ${plan.summary}；实验不要求患者到场。`
        : undefined,
    });
  }
  function move(r: Reservation, resource?: string, start?: string) {
    const options = data!.resources
      .filter(
        (x) => x.kind === (r.kind === "Consultation" ? "Clinician" : "Machine"),
      )
      .map((x) => ({ value: x.id, label: x.name }));
    const end = start
      ? localTime(
          new Date(
            new Date(start + "+08:00").getTime() +
              new Date(r.endUtc).getTime() -
              new Date(r.startUtc).getTime(),
          ),
        )
      : localTime(r.endUtc);
    open({
      title: "预约改期",
      operation: "reschedule",
      command: { id: r.id, version: r.version },
      fields: [
        {
          name: "resourceId",
          label: "调整后的资源",
          options,
          value: resource || r.resourceId,
        },
        ...dateFields(start || localTime(r.startUtc), end),
        reason(),
      ],
      context: `原安排：${patientName(r.patientId)} · ${resourceName(r.resourceId)} · ${displayTime(r.startUtc)} — ${displayTime(r.endUtc)}`,
    });
  }
  function prescribe(c: CareCase) {
    open({
      title: c.currentPrescriptionId ? "签署新处方版本" : "签署诊疗记录与处方",
      operation: "prescribe",
      command: { id: c.id, version: c.version },
      fields: [
        text("diagnosis", "诊疗记录 / 诊断"),
        text("text", "放疗处方（模拟）"),
        ...(c.currentPrescriptionId ? [reason()] : []),
      ],
      context: "签署后保留历史，修改须产生新版本。处方变更后需要重新登记计划。",
    });
  }
  function registerPlan(c: CareCase) {
    open({
      title: "登记治疗计划版本",
      operation: "plan",
      command: { caseId: c.id, version: c.version },
      fields: [
        text("text", "外部计划摘要（模拟）"),
        { name: "resourceId", label: "计划使用机器", options: machineOptions },
        {
          name: "attachment",
          label: "外部附件引用 / 文件编号",
          optional: true,
        },
        ...(c.currentPlanId ? [reason()] : []),
      ],
      context: "计划不在本系统计算。新版本必须重新审核和验证。",
    });
  }
  function review(p: Plan) {
    open({
      title: "签署计划审核",
      operation: "review",
      command: { id: p.id, version: p.version },
      fields: [],
      context: `${patientName(data!.cases.find((c) => c.id === p.caseId)?.patientId || null)} · v${p.revision} · ${p.summary}`,
    });
  }
  function validate(r: Reservation) {
    open({
      title: "签署物理验证结果",
      operation: "validate",
      command: { id: r.id, version: r.version },
      fields: [
        {
          name: "outcome",
          label: "验证结论",
          options: [
            { value: "Passed", label: "通过" },
            { value: "Failed", label: "不通过" },
          ],
        },
        {
          name: "failureType",
          label: "失败处理方向（不通过时生效）",
          options: [
            { value: "Plan", label: "计划问题 → 物理师修订" },
            { value: "Clinical", label: "诊疗问题 → 诊疗人员评估" },
            { value: "Equipment", label: "设备问题 → 停用机器" },
          ],
        },
        text("text", "实验结果与依据（模拟）"),
      ],
      context: `${resourceName(r.resourceId)} · ${data!.plans.find((p) => p.id === r.planId)?.summary}`,
    });
  }
  function reservationCard(r: Reservation, blockers: string[]) {
    return (
      <button className="ois-row" key={r.id} onClick={() => setSelected(r.id)}>
        <span className={"ois-dot " + r.kind.toLowerCase()} />
        <span>
          <strong>
            {label(r.kind)} · {patientName(r.patientId)}
          </strong>
          <small>
            {displayTime(r.startUtc)} · {resourceName(r.resourceId)}
          </small>
        </span>
        <span className={`ois-badge ${blockers.length ? "danger" : ""}`}>
          {blockers.length ? "待放行 / 受阻" : label(r.status)}
        </span>
      </button>
    );
  }
  const selectedEntry = data.reservations.find(
    (x) => x.reservation.id === selected,
  );
  const selectedReservation = selectedEntry?.reservation;
  function canManage(r: Reservation) {
    return r.kind === "Experiment"
      ? clinician
      : r.kind === "Maintenance"
        ? physicist
        : scheduler;
  }
  function corrections(id: string, version: number) {
    open({
      title: "追加签署记录更正",
      operation: "correction",
      command: { id, version },
      fields: [reason(), text("text", "更正内容")],
      context:
        "原签署事实保留。验证更正会阻止当前计划继续用于治疗，需登记新版计划。",
    });
  }
  function casePanel(c: CareCase) {
    const plans = data!.plans
      .filter((p) => p.caseId === c.id)
      .sort((a, b) => b.revision - a.revision);
    return (
      <article key={c.id} className="ois-panel">
        <div className="ois-toolbar">
          <div>
            <p className="ois-kicker">诊疗档案</p>
            <h2>{c.title}</h2>
          </div>
          {clinician && (
            <button onClick={() => prescribe(c)}>签署诊疗记录 / 处方</button>
          )}
          {physicist && (
            <button
              disabled={!c.currentPrescriptionId}
              onClick={() => registerPlan(c)}
            >
              登记计划版本
            </button>
          )}
        </div>
        {data!.prescriptions
          .filter((p) => p.caseId === c.id)
          .map((rx) => (
            <details
              className="ois-record"
              key={rx.id}
              open={rx.id === c.currentPrescriptionId}
            >
              <summary>
                {rx.id === c.currentPrescriptionId ? "当前处方" : "历史处方"} ·{" "}
                {rx.diagnosis} · {displayTime(rx.signedUtc)}
              </summary>
              <p className="ois-pre">{rx.content}</p>
              <small>
                {rx.signedBy} 签署 {rx.reason && " · 更正原因：" + rx.reason}
              </small>
            </details>
          ))}
        {!c.currentPrescriptionId && (
          <p className="ois-empty">待诊疗人员签署诊疗记录与处方。</p>
        )}
        {plans.map((p) => (
          <div className="ois-plan" key={p.id}>
            <div className="ois-toolbar">
              <h3>
                计划 v{p.revision}{" "}
                {c.currentPlanId === p.id ? (
                  <span className="ois-badge">当前</span>
                ) : (
                  <span className="ois-badge">历史</span>
                )}
              </h3>
              <span
                className={`ois-badge ${p.validation === "Failed" ? "danger" : ""}`}
              >
                {label(p.validation)}
              </span>
              <span className="ois-badge">
                {p.reviewedBy ? "诊疗审核通过" : "待诊疗审核"}
              </span>
            </div>
            <p className="ois-pre">{p.summary}</p>
            <p className="ois-muted">
              {resourceName(p.machineId)} · 登记：{p.registeredBy}
              {p.attachment && " · 附件引用：" + p.attachment}
            </p>
            {p.validationNote && (
              <p>
                验证记录：{p.validationNote} · {p.validatedBy}
              </p>
            )}
            <div className="ois-actions">
              {clinician &&
                c.currentPlanId === p.id &&
                !p.reviewedBy &&
                p.validation !== "Failed" && (
                  <button onClick={() => review(p)}>审核此版本</button>
                )}
              {clinician &&
                c.currentPlanId === p.id &&
                p.validation === "Pending" && (
                  <button
                    onClick={() =>
                      book("Experiment", p.machineId, undefined, p)
                    }
                  >
                    安排物理实验
                  </button>
                )}
              {physicist && p.validatedBy && (
                <button onClick={() => corrections(p.id, p.version)}>
                  追加验证更正
                </button>
              )}
            </div>
          </div>
        ))}
        <h3>预约与治疗历史</h3>
        {data!.reservations
          .filter((x) => x.reservation.caseId === c.id)
          .map((x) => reservationCard(x.reservation, x.blockers))}
        {data!.deliveries
          .filter((d) =>
            data!.reservations.some(
              (x) =>
                x.reservation.id === d.reservationId &&
                x.reservation.caseId === c.id,
            ),
          )
          .map((d) => (
            <div className="ois-record" key={d.id}>
              <strong>治疗执行 · {label(d.outcome)}</strong>
              <p>
                {displayTime(d.startedUtc)} · 计划 v
                {data!.plans.find((p) => p.id === d.planId)?.revision} ·{" "}
                {resourceName(d.machineId)} · {d.startedBy}
              </p>
              <p>{d.note}</p>
              {clinician && d.outcome !== "InProgress" && (
                <button onClick={() => corrections(d.id, d.version)}>
                  追加执行记录更正
                </button>
              )}
              {data!.audits
                .filter((a) => a.action === "correction" && a.targetId === d.id)
                .map((a) => (
                  <p key={a.id}>
                    更正 · {displayTime(a.atUtc)} · {a.actor}：
                    {JSON.parse(a.detail).command.text}
                  </p>
                ))}
            </div>
          ))}
      </article>
    );
  }

  return (
    <div className="ois-shell">
      <aside className="ois-sidebar">
        <a href="/" className="ois-brand">
          <span>✳</span> ClinicFlow<small>ONCOLOGY INFORMATION</small>
        </a>
        <div className="ois-role">{label(role)}工作台</div>
        <nav>
          {nav.map((n) => (
            <button
              key={n.id}
              className={view === n.id ? "active" : ""}
              onClick={() => setView(n.id)}
            >
              <span>{n.icon}</span>
              {n.name}
              {n.id === "tasks" && myTasks.length > 0 && (
                <b>{myTasks.length}</b>
              )}
            </button>
          ))}
        </nav>
        <div className="ois-account">
          <strong>{user.name}</strong>
          <small>单院区 · 演示环境</small>
          <button onClick={logout}>退出登录</button>
        </div>
      </aside>
      <main className="ois-main">
        <header className="ois-topbar">
          <span>工作空间 / {nav.find((n) => n.id === view)?.name}</span>
          <span>Asia/Shanghai · 模拟数据</span>
        </header>
        <div className="ois-content">
          <div className="ois-page-title">
            <div>
              <p className="ois-kicker">CARE, CONNECTED</p>
              <h1>
                {view === "home"
                  ? label(role) + "工作台"
                  : nav.find((n) => n.id === view)?.name}
              </h1>
              <p className="ois-muted">
                让预约、审核与执行的每一步，都有明确的下一步。
              </p>
            </div>
            <button
              onClick={() =>
                refresh()
                  .then(() => setNotice("工作台已刷新"))
                  .catch((e) => setError(e.message))
              }
            >
              刷新工作台
            </button>
          </div>
          {error && !editor && (
            <div className="ois-alert" role="alert">
              {error}
            </div>
          )}
          {notice && (
            <div className="ois-notice" role="status">
              {notice}
            </div>
          )}
          {view === "home" && (
            <>
              <div className="ois-stats">
                <article>
                  <small>今日安排</small>
                  <strong>{todayBookings.length}</strong>
                  <span>门诊 / 治疗 / 验证</span>
                </article>
                <article>
                  <small>我的待办</small>
                  <strong>{myTasks.length}</strong>
                  <span>{myTasks.filter((t) => !t.readBy).length} 条未读</span>
                </article>
                <article>
                  <small>待放行治疗</small>
                  <strong>
                    {
                      data.reservations.filter(
                        (x) =>
                          x.reservation.kind === "Treatment" &&
                          x.reservation.status === "Booked" &&
                          x.blockers.length > 0,
                      ).length
                    }
                  </strong>
                  <span>预约保留，执行受保护</span>
                </article>
                <article>
                  <small>可用机器</small>
                  <strong>
                    {
                      data.resources.filter(
                        (r) => r.kind === "Machine" && r.available,
                      ).length
                    }
                  </strong>
                  <span>验证与治疗共用时段</span>
                </article>
              </div>
              <div className="ois-actions">
                {scheduler && (
                  <>
                    <button
                      className="primary"
                      onClick={() => book("Consultation")}
                    >
                      ＋ 门诊预约
                    </button>
                    <button onClick={() => book("Treatment")}>
                      ＋ 单次治疗预约
                    </button>
                  </>
                )}
                {clinician && (
                  <button
                    className="primary"
                    onClick={() => setView("patients")}
                  >
                    打开患者诊疗档案
                  </button>
                )}
                {physicist && (
                  <button
                    className="primary"
                    onClick={() => setView("patients")}
                  >
                    登记 / 查看治疗计划
                  </button>
                )}
                <button onClick={() => setView("tasks")}>处理协作待办</button>
              </div>
              <section className="ois-panel">
                <div className="ois-toolbar">
                  <h2>今日工作队列</h2>
                  <button onClick={() => setView("calendar")}>
                    查看日历 →
                  </button>
                </div>
                {todayBookings.length ? (
                  todayBookings.map((x) =>
                    reservationCard(x.reservation, x.blockers),
                  )
                ) : (
                  <p className="ois-empty">
                    今天暂无安排。可从日历查看其他日期。
                  </p>
                )}
              </section>
            </>
          )}
          {view === "calendar" && (
            <>
              <div className="ois-actions">
                {scheduler && (
                  <>
                    <button
                      className="primary"
                      onClick={() => book("Consultation")}
                    >
                      ＋ 门诊预约
                    </button>
                    <button onClick={() => book("Treatment")}>
                      ＋ 单次治疗预约
                    </button>
                  </>
                )}
                {clinician && (
                  <button onClick={() => setView("patients")}>
                    从计划安排物理实验
                  </button>
                )}
                {physicist && (
                  <button onClick={() => book("Maintenance")}>
                    ＋ 维护占用
                  </button>
                )}
              </div>
              <OisCalendar
                data={data}
                onSelect={(r) => setSelected(r.id)}
                onCreate={(resource, time) => {
                  const r = data.resources.find((x) => x.id === resource)!;
                  if (scheduler)
                    book(
                      r.kind === "Clinician" ? "Consultation" : "Treatment",
                      resource,
                      time,
                    );
                  else if (physicist && r.kind === "Machine")
                    book("Maintenance", resource, time);
                  else {
                    setView("patients");
                    setNotice("请选择当前计划，再安排物理实验。");
                  }
                }}
                onMove={(r, res, time) => {
                  if (canManage(r)) move(r, res, time);
                  else setError("当前角色不能调整这类预约。");
                }}
              />
              <section className="ois-panel">
                <h2>全部预约与占用（含已取消）</h2>
                {data.reservations
                  .slice()
                  .sort((a, b) =>
                    b.reservation.startUtc.localeCompare(
                      a.reservation.startUtc,
                    ),
                  )
                  .map((x) => reservationCard(x.reservation, x.blockers))}
              </section>
            </>
          )}
          {view === "patients" && (
            <>
              <section className="ois-panel">
                <div className="ois-toolbar">
                  <h2>患者档案</h2>
                  <input
                    aria-label="搜索患者"
                    placeholder="搜索姓名或编号"
                    value={search}
                    onChange={(e) => setSearch(e.target.value)}
                  />
                </div>
                <div className="ois-patients">
                  {data.patients
                    .filter((p) =>
                      (p.name + p.identifier)
                        .toLowerCase()
                        .includes(search.toLowerCase()),
                    )
                    .map((p) => (
                      <button
                        key={p.id}
                        className={p.id === patientId ? "selected" : ""}
                        onClick={() => setPatientId(p.id)}
                      >
                        <strong>{p.name}</strong>
                        <small>{p.identifier}</small>
                      </button>
                    ))}
                </div>
                <div className="ois-toolbar">
                  <p>
                    当前患者：<strong>{patientName(patientId)}</strong>
                  </p>
                  {clinician && (
                    <button
                      className="primary"
                      onClick={() =>
                        open({
                          title: "建立诊疗档案",
                          operation: "case",
                          command: { patientId },
                          fields: [text("text", "诊疗档案名称")],
                          context: patientName(patientId),
                        })
                      }
                    >
                      ＋ 建立诊疗档案
                    </button>
                  )}
                </div>
              </section>
              {data.cases
                .filter((c) => c.patientId === patientId)
                .map(casePanel)}
              {!data.cases.some((c) => c.patientId === patientId) && (
                <p className="ois-empty">暂无诊疗档案，等待诊疗人员建立。</p>
              )}
              <section className="ois-panel">
                <h2>患者预约</h2>
                {data.reservations
                  .filter(
                    (x) =>
                      x.reservation.patientId === patientId &&
                      !x.reservation.caseId,
                  )
                  .map((x) => reservationCard(x.reservation, x.blockers))}
              </section>
            </>
          )}
          {view === "machines" && (
            <div className="ois-machine-grid">
              {data.resources
                .filter((r) => r.kind === "Machine")
                .map((r) => (
                  <article className="ois-panel" key={r.id}>
                    <span
                      className={`ois-badge ${r.available ? "" : "danger"}`}
                    >
                      {r.available ? "可用" : "停用"}
                    </span>
                    <h2>{r.name}</h2>
                    <p>{r.reason || "暂无设备异常"}</p>
                    <p className="ois-muted">
                      {
                        data.reservations.filter(
                          (x) =>
                            x.reservation.resourceId === r.id &&
                            x.reservation.status === "Booked",
                        ).length
                      }{" "}
                      个待执行占用
                    </p>
                    {physicist && (
                      <div className="ois-actions">
                        <button
                          onClick={() =>
                            open({
                              title: r.available ? "停用机器" : "恢复机器",
                              operation: "machine",
                              command: {
                                id: r.id,
                                version: r.version,
                                available: !r.available,
                              },
                              fields: [reason()],
                              context: r.available
                                ? "未来预约保留并标记受阻，生成协调待办。"
                                : "恢复机器不会使失败的计划自动通过。",
                            })
                          }
                        >
                          {r.available ? "登记设备停用" : "登记恢复可用"}
                        </button>
                        <button onClick={() => book("Maintenance", r.id)}>
                          维护占用
                        </button>
                      </div>
                    )}
                  </article>
                ))}
            </div>
          )}
          {view === "tasks" && (
            <section className="ois-panel">
              <h2>{label(role)}协作待办</h2>
              <p className="ois-muted">
                完成待办表示已记录处理结果，不会自动放行治疗。
              </p>
              {data.tasks
                .filter((t) => t.role === role)
                .sort(
                  (a, b) =>
                    Number(!!a.resolvedBy) - Number(!!b.resolvedBy) ||
                    b.createdUtc.localeCompare(a.createdUtc),
                )
                .map((t) => (
                  <article className="ois-task" key={t.id}>
                    <div>
                      <span className="ois-badge">
                        {t.resolvedBy ? "已处理" : t.readBy ? "待处理" : "未读"}
                      </span>
                      <strong>{t.reason}</strong>
                      <small>
                        {displayTime(t.createdUtc)} · 关联记录{" "}
                        {t.targetId.slice(0, 8)}
                      </small>
                      {t.resolution && (
                        <p>
                          {t.resolvedBy}：{t.resolution}
                        </p>
                      )}
                    </div>
                    <div className="ois-actions">
                      <button
                        onClick={() => {
                          const r = data.reservations.find(
                            (x) => x.reservation.id === t.targetId,
                          )?.reservation;
                          const p = data.plans.find((x) => x.id === t.targetId);
                          const d = data.deliveries.find(
                            (x) => x.id === t.targetId,
                          );
                          const c = data.cases.find(
                            (x) =>
                              x.id ===
                              (p?.caseId ||
                                data.reservations.find(
                                  (x) => x.reservation.id === d?.reservationId,
                                )?.reservation.caseId ||
                                t.targetId),
                          );
                          if (r) setSelected(r.id);
                          else if (c) {
                            setPatientId(c.patientId);
                            setView("patients");
                          }
                        }}
                      >
                        查看关联
                      </button>
                      {!t.resolvedBy && (
                        <>
                          <button
                            disabled={busy || !!t.readBy}
                            onClick={() =>
                              execute("read-task", {
                                id: t.id,
                                version: t.version,
                              })
                            }
                          >
                            标为已读
                          </button>
                          <button
                            onClick={() =>
                              open({
                                title: "记录待办处理结果",
                                operation: "resolve-task",
                                command: { id: t.id, version: t.version },
                                fields: [text("text", "处理结果")],
                                context: t.reason,
                              })
                            }
                          >
                            处理待办
                          </button>
                        </>
                      )}
                    </div>
                  </article>
                ))}
              {!data.tasks.some((t) => t.role === role) && (
                <p className="ois-empty">暂无待办。</p>
              )}
            </section>
          )}
          {view === "history" && (
            <section className="ois-panel">
              <h2>追加式操作历史</h2>
              {data.audits
                .slice()
                .reverse()
                .map((a) => (
                  <details className="ois-record" key={a.id}>
                    <summary>
                      {displayTime(a.atUtc)} · {a.actor} · {a.action} ·{" "}
                      {a.targetId.slice(0, 8)}
                    </summary>
                    <pre>{JSON.stringify(JSON.parse(a.detail), null, 2)}</pre>
                  </details>
                ))}
            </section>
          )}
        </div>
      </main>
      {selectedReservation && !editor && (
        <div className="ois-overlay">
          <section
            role="dialog"
            aria-modal="true"
            aria-labelledby="reservation-title"
            className="ois-dialog"
          >
            <div className="ois-toolbar">
              <h2 id="reservation-title">
                {label(selectedReservation.kind)}详情
              </h2>
              <button onClick={() => setSelected(null)}>关闭</button>
            </div>
            <h3>{patientName(selectedReservation.patientId)}</h3>
            <p>
              {resourceName(selectedReservation.resourceId)} ·{" "}
              {displayTime(selectedReservation.startUtc)} —{" "}
              {displayTime(selectedReservation.endUtc)}
            </p>
            <span className="ois-badge">
              {label(selectedReservation.status)}
            </span>
            {selectedReservation.reason && (
              <p>原因：{selectedReservation.reason}</p>
            )}
            {selectedEntry!.blockers.length > 0 && (
              <div className="ois-alert">
                {selectedEntry!.blockers.join("；")}
              </div>
            )}
            {error && (
              <div role="alert" className="ois-alert">
                {error}
              </div>
            )}
            <div className="ois-actions">
              {canManage(selectedReservation) &&
                selectedReservation.status === "Booked" && (
                  <>
                    <button onClick={() => move(selectedReservation)}>
                      改期
                    </button>
                    <button
                      onClick={() =>
                        open({
                          title: "取消预约 / 占用",
                          operation: "cancel",
                          command: {
                            id: selectedReservation.id,
                            version: selectedReservation.version,
                          },
                          fields: [reason()],
                        })
                      }
                    >
                      取消预约
                    </button>
                    {selectedReservation.patientId && (
                      <button
                        onClick={() =>
                          open({
                            title: "登记未到场",
                            operation: "no-show",
                            command: {
                              id: selectedReservation.id,
                              version: selectedReservation.version,
                            },
                            fields: [reason()],
                          })
                        }
                      >
                        未到场
                      </button>
                    )}
                  </>
                )}
              {canManage(selectedReservation) &&
                selectedReservation.status === "Cancelled" && (
                  <button
                    onClick={() =>
                      open({
                        title: "恢复预约",
                        operation: "restore",
                        command: {
                          id: selectedReservation.id,
                          version: selectedReservation.version,
                        },
                        fields: [reason()],
                        context:
                          "恢复原时段前重新检查冲突，若已过期请新建预约。",
                      })
                    }
                  >
                    恢复预约
                  </button>
                )}
              {physicist &&
                selectedReservation.kind === "Experiment" &&
                selectedReservation.status === "Booked" && (
                  <button onClick={() => validate(selectedReservation)}>
                    提交验证结果
                  </button>
                )}
              {clinician &&
                selectedReservation.kind === "Consultation" &&
                selectedReservation.status === "Booked" && (
                  <button
                    onClick={() =>
                      open({
                        title: "登记门诊完成",
                        operation: "consult",
                        command: {
                          id: selectedReservation.id,
                          version: selectedReservation.version,
                        },
                        fields: [
                          {
                            name: "caseId",
                            label: "本次诊疗档案",
                            options: data.cases
                              .filter(
                                (c) =>
                                  c.patientId === selectedReservation.patientId,
                              )
                              .map((c) => ({ value: c.id, label: c.title })),
                          },
                        ],
                        context: "请先在患者档案签署诊疗记录和处方。",
                      })
                    }
                  >
                    登记接诊完成
                  </button>
                )}
              {clinician &&
                selectedReservation.kind === "Treatment" &&
                selectedReservation.status === "Booked" && (
                  <>
                    <button
                      className="primary"
                      disabled={selectedEntry!.blockers.length > 0}
                      onClick={() =>
                        open({
                          title: "开始单次治疗",
                          operation: "start",
                          command: {
                            id: selectedReservation.id,
                            version: selectedReservation.version,
                            planId:
                              data.cases.find(
                                (c) => c.id === selectedReservation.caseId,
                              )?.currentPlanId || "",
                          },
                          fields: [
                            {
                              name: "identityChecked",
                              label: "已核对患者姓名与患者编号",
                              type: "checkbox",
                            },
                          ],
                          context:
                            patientName(selectedReservation.patientId) +
                            " · " +
                            data.patients.find(
                              (p) => p.id === selectedReservation.patientId,
                            )?.identifier,
                        })
                      }
                    >
                      核对并开始治疗
                    </button>
                    <button
                      onClick={() =>
                        open({
                          title: "登记治疗未执行",
                          operation: "not-executed",
                          command: {
                            id: selectedReservation.id,
                            version: selectedReservation.version,
                          },
                          fields: [reason()],
                        })
                      }
                    >
                      未执行
                    </button>
                  </>
                )}
              {clinician && selectedReservation.status === "InProgress" && (
                <button
                  onClick={() => {
                    const d = data.deliveries.find(
                      (x) => x.reservationId === selectedReservation.id,
                    )!;
                    open({
                      title: "记录治疗执行结果",
                      operation: "finish",
                      command: { id: d.id, version: d.version },
                      fields: [
                        {
                          name: "outcome",
                          label: "执行结果",
                          options: [
                            { value: "Completed", label: "完成" },
                            { value: "Interrupted", label: "中断，转人工处理" },
                          ],
                        },
                        text("text", "执行记录 / 中断原因"),
                      ],
                    });
                  }}
                >
                  记录完成 / 中断
                </button>
              )}
              {selectedReservation.caseId && (
                <button
                  onClick={() => {
                    setPatientId(
                      data.cases.find(
                        (c) => c.id === selectedReservation.caseId,
                      )!.patientId,
                    );
                    setView("patients");
                    setSelected(null);
                  }}
                >
                  查看诊疗档案
                </button>
              )}
            </div>
          </section>
        </div>
      )}
      {editor && (
        <CommandEditor
          key={editor.title + JSON.stringify(editor.command)}
          editor={editor}
          busy={busy}
          error={error}
          onClose={() => !busy && setEditor(null)}
          onSubmit={(c) => execute(editor.operation, c)}
        />
      )}
    </div>
  );
}

function CommandEditor({
  editor,
  busy,
  error,
  onClose,
  onSubmit,
}: {
  editor: Editor;
  busy: boolean;
  error: string;
  onClose: () => void;
  onSubmit: (c: Command) => void;
}) {
  const [values, setValues] = useState<Record<string, string | boolean>>(() =>
    Object.fromEntries(
      editor.fields.map((f) => [
        f.name,
        f.value ??
          (f.type === "checkbox" ? false : f.options?.[0]?.value || ""),
      ]),
    ),
  );
  const [preview, setPreview] = useState(false);
  function submit(e: FormEvent) {
    e.preventDefault();
    if (!preview) {
      setPreview(true);
      return;
    }
    const command = { ...editor.command };
    for (const f of editor.fields) {
      const value = values[f.name];
      command[f.name] =
        f.type === "datetime-local"
          ? new Date(String(value) + "+08:00").toISOString()
          : f.name === "patientId"
            ? Number(value)
            : value;
    }
    onSubmit(command);
  }
  return (
    <div className="ois-overlay">
      <form
        className="ois-dialog"
        role="dialog"
        aria-modal="true"
        aria-labelledby="editor-title"
        onSubmit={submit}
      >
        <div className="ois-toolbar">
          <h2 id="editor-title">{editor.title}</h2>
          <button type="button" disabled={busy} onClick={onClose}>
            关闭
          </button>
        </div>
        {editor.context && <p className="ois-context">{editor.context}</p>}
        {preview ? (
          <>
            <h3>请核对本次操作</h3>
            <dl>
              {editor.fields.map((f) => (
                <div key={f.name}>
                  <dt>{f.label}</dt>
                  <dd>
                    {typeof values[f.name] === "boolean"
                      ? values[f.name]
                        ? "已确认"
                        : "未确认"
                      : f.options?.find((x) => x.value === values[f.name])
                          ?.label ||
                        String(values[f.name]) ||
                        "未填写"}
                  </dd>
                </div>
              ))}
            </dl>
            <p className="ois-muted">
              提交后保存签署或操作历史。系统将再次检查权限、版本和资源冲突。
            </p>
          </>
        ) : (
          editor.fields.map((f) => (
            <label className="ois-field" key={f.name}>
              {f.type !== "checkbox" && f.label}
              {f.options ? (
                <select
                  required={!f.optional}
                  value={String(values[f.name])}
                  onChange={(e) =>
                    setValues({ ...values, [f.name]: e.target.value })
                  }
                >
                  {f.options.length === 0 && (
                    <option value="">暂无可选记录</option>
                  )}
                  {f.options.map((o) => (
                    <option key={o.value} value={o.value}>
                      {o.label}
                    </option>
                  ))}
                </select>
              ) : f.type === "textarea" ? (
                <textarea
                  rows={3}
                  required={!f.optional}
                  maxLength={4000}
                  value={String(values[f.name])}
                  onChange={(e) =>
                    setValues({ ...values, [f.name]: e.target.value })
                  }
                />
              ) : f.type === "checkbox" ? (
                <span>
                  <input
                    required
                    checked={!!values[f.name]}
                    type="checkbox"
                    onChange={(e) =>
                      setValues({ ...values, [f.name]: e.target.checked })
                    }
                  />
                  {f.label}
                </span>
              ) : (
                <input
                  type={f.type || "text"}
                  step={f.type === "datetime-local" ? 900 : undefined}
                  required={!f.optional}
                  maxLength={1000}
                  value={String(values[f.name])}
                  onChange={(e) =>
                    setValues({ ...values, [f.name]: e.target.value })
                  }
                />
              )}
            </label>
          ))
        )}
        {error && (
          <div className="ois-alert" role="alert">
            {error}
            <p>若记录版本已更新，请关闭表单，刷新后重新打开。</p>
          </div>
        )}
        <div className="ois-actions">
          {preview && (
            <button
              type="button"
              disabled={busy}
              onClick={() => setPreview(false)}
            >
              返回修改
            </button>
          )}
          <button className="primary" disabled={busy}>
            {busy ? "正在保存…" : preview ? "确认提交" : "预览并核对"}
          </button>
        </div>
      </form>
    </div>
  );
}

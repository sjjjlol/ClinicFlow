import { useEffect, useRef, useState } from "react";
import {
  displayTime,
  label,
  localTime,
  type Reservation,
  type Workspace,
} from "./ois-types";

export default function OisCalendar({
  data,
  onSelect,
  onCreate,
  onMove,
}: {
  data: Workspace;
  onSelect: (r: Reservation) => void;
  onCreate: (resource: string, time: string) => void;
  onMove: (r: Reservation, resource: string, time: string) => void;
}) {
  const [date, setDate] = useState(localTime(new Date()).slice(0, 10));
  const [mode, setMode] = useState("day");
  const [resource, setResource] = useState(data.resources[0]?.id || "");
  const scroll = useRef<HTMLDivElement>(null);
  useEffect(() => {
    if (scroll.current) scroll.current.scrollTop = 8 * 64;
  }, []);
  const addDays = (day: string, amount: number) =>
    new Date(new Date(day + "T12:00:00Z").getTime() + amount * 86400000)
      .toISOString()
      .slice(0, 10);
  const columns =
    mode === "day"
      ? data.resources.map((r) => ({ date, resource: r.id, title: r.name }))
      : Array.from({ length: 7 }, (_, i) => ({
          date: addDays(date, i),
          resource,
          title: addDays(date, i).slice(5),
        }));
  return (
    <section className="ois-panel">
      <div className="ois-toolbar">
        <h2>资源日历</h2>
        <button
          onClick={() => setDate(addDays(date, mode === "day" ? -1 : -7))}
        >
          上一{mode === "day" ? "天" : "周"}
        </button>
        <input
          aria-label="日历日期"
          type="date"
          value={date}
          onChange={(e) => e.target.value && setDate(e.target.value)}
        />
        <button onClick={() => setDate(addDays(date, mode === "day" ? 1 : 7))}>
          下一{mode === "day" ? "天" : "周"}
        </button>
        <button onClick={() => setDate(localTime(new Date()).slice(0, 10))}>
          今天
        </button>
        <select
          aria-label="日历视图"
          value={mode}
          onChange={(e) => setMode(e.target.value)}
        >
          <option value="day">日视图 · 全部资源</option>
          <option value="week">周视图 · 单个资源</option>
        </select>
        {mode === "week" && (
          <select
            aria-label="日历资源"
            value={resource}
            onChange={(e) => setResource(e.target.value)}
          >
            {data.resources.map((r) => (
              <option key={r.id} value={r.id}>
                {r.name}
              </option>
            ))}
          </select>
        )}
      </div>
      <p className="ois-muted">
        点击空白时段新增；拖动已有预约预览改期。所有时间均为上海时间，保存时检查患者与资源冲突。
      </p>
      <div className="ois-calendar-scroll" ref={scroll}>
        <div
          className="ois-calendar"
          style={{
            gridTemplateColumns: `54px repeat(${columns.length}, minmax(145px, 1fr))`,
          }}
        >
          <div className="ois-cal-head">时间</div>
          {columns.map((c) => (
            <div key={c.date + c.resource} className="ois-cal-head">
              {c.title}
              {!data.resources.find((r) => r.id === c.resource)?.available && (
                <span className="ois-badge danger">停用</span>
              )}
            </div>
          ))}
          <div className="ois-hours">
            {Array.from({ length: 24 }, (_, h) => (
              <div key={h}>{String(h).padStart(2, "0")}:00</div>
            ))}
          </div>
          {columns.map((c) => (
            <div className="ois-day" key={c.date + c.resource}>
              {Array.from({ length: 96 }, (_, i) => {
                const time = `${c.date}T${String(Math.floor(i / 4)).padStart(2, "0")}:${String((i % 4) * 15).padStart(2, "0")}`;
                return (
                  <button
                    key={i}
                    className="ois-slot"
                    aria-label={`${c.title} ${time} 新增`}
                    onClick={() => onCreate(c.resource, time)}
                    onDragOver={(e) => e.preventDefault()}
                    onDrop={(e) => {
                      e.preventDefault();
                      const id = e.dataTransfer.getData("text/plain");
                      const r = data.reservations.find(
                        (x) => x.reservation.id === id,
                      )?.reservation;
                      if (r) onMove(r, c.resource, time);
                    }}
                  />
                );
              })}
              {data.reservations
                .filter(
                  ({ reservation: r }) =>
                    r.resourceId === c.resource &&
                    r.status !== "Cancelled" &&
                    localTime(r.startUtc).slice(0, 10) <= c.date &&
                    localTime(r.endUtc).slice(0, 10) >= c.date,
                )
                .map(({ reservation: r, blockers }) => {
                  const dayStart = new Date(
                    c.date + "T00:00:00+08:00",
                  ).getTime();
                  const top = Math.max(
                    0,
                    ((new Date(r.startUtc).getTime() - dayStart) / 3600000) *
                      64,
                  );
                  const end = Math.min(
                    1536,
                    ((new Date(r.endUtc).getTime() - dayStart) / 3600000) * 64,
                  );
                  if (end <= top) return null;
                  return (
                    <button
                      key={r.id}
                      draggable={r.status === "Booked"}
                      onDragStart={(e) =>
                        e.dataTransfer.setData("text/plain", r.id)
                      }
                      className={`ois-event ${r.kind.toLowerCase()} ${blockers.length ? "blocked" : ""}`}
                      style={{ top, height: Math.max(16, end - top - 2) }}
                      onClick={() => onSelect(r)}
                      title={`${label(r.kind)} · ${displayTime(r.startUtc)} · ${label(r.status)} ${blockers.join("；")}`}
                    >
                      <strong>
                        {label(r.kind)} ·{" "}
                        {data.patients.find((p) => p.id === r.patientId)
                          ?.name ||
                          data.plans.find((p) => p.id === r.planId)?.summary ||
                          r.reason}
                      </strong>
                      <small>
                        {localTime(r.startUtc).slice(11)} ·{" "}
                        {blockers.length ? "受阻" : label(r.status)}
                      </small>
                    </button>
                  );
                })}
            </div>
          ))}
        </div>
      </div>
      <div className="ois-legend">
        ● 门诊　<span>● 治疗</span>　<span>● 物理实验</span>　受阻预约保留时段
      </div>
    </section>
  );
}

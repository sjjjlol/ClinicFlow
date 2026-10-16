import type { User } from "./api";
import type { Reservation } from "./ois-types";

type View = "home" | "calendar" | "patients" | "machines" | "tasks" | "history";
const common = [
  { id: "tasks", name: "协作待办", icon: "☷" },
  { id: "history", name: "本角色操作历史", icon: "↺" },
] as const;
export const workspaces = {
  Scheduler: {
    description: "安排门诊与治疗，协调改期、取消和受阻预约。",
    schedule: "门诊 / 治疗",
    nav: [{ id: "calendar", name: "预约日历", icon: "▦" }],
    guide: [
      {
        title: "1. 安排患者预约",
        text: "在预约日历选择患者、门诊或治疗时段，核对资源后提交。",
        view: "calendar",
        action: "安排预约",
      },
      {
        title: "2. 协调受阻预约",
        text: "查看受阻原因，与诊疗人员或物理师协作；需要时改期或取消。预约成功不代表治疗已放行。",
        view: "tasks",
        action: "查看协调待办",
      },
      {
        title: "3. 跟进预约状态",
        text: "在日历查看预约详情与处理结果，避免重复安排。",
        view: "calendar",
        action: "查看预约",
      },
    ],
  },
  Clinician: {
    description: "完成诊疗与处方、审核计划、安排实验并执行治疗。",
    schedule: "门诊 / 治疗 / 实验排期",
    nav: [
      { id: "calendar", name: "诊疗与实验日历", icon: "▦" },
      { id: "patients", name: "患者与诊疗", icon: "◎" },
    ],
    guide: [
      {
        title: "1. 建档并签署处方",
        text: "选择患者，建立诊疗档案，签署诊疗记录与放疗处方。处方修订会产生新版本。",
        view: "patients",
        action: "打开诊疗档案",
      },
      {
        title: "2. 审核计划并安排实验",
        text: "物理师登记计划后，审核当前版本并安排物理实验，等待验证结果。",
        view: "patients",
        action: "审核与实验排期",
      },
      {
        title: "3. 核对并执行治疗",
        text: "从治疗预约详情核对计划与放行条件，开始治疗并登记完成或中断结果。",
        view: "calendar",
        action: "查看治疗安排",
      },
    ],
  },
  Physicist: {
    description: "登记治疗计划、完成物理验证并维护设备可用状态。",
    schedule: "物理验证 / 设备维护",
    nav: [
      { id: "calendar", name: "验证与维护日历", icon: "▦" },
      { id: "patients", name: "治疗计划与验证", icon: "◎" },
      { id: "machines", name: "机器与占用", icon: "▤" },
    ],
    guide: [
      {
        title: "1. 登记治疗计划",
        text: "依据已签署的当前处方，登记外部计划、使用机器和附件编号；修订时登记新版本。",
        view: "patients",
        action: "登记与查看计划",
      },
      {
        title: "2. 执行物理验证",
        text: "诊疗人员排期后，从实验详情签署验证结果。不通过时记录原因及处理方向。",
        view: "calendar",
        action: "查看验证安排",
      },
      {
        title: "3. 维护设备状态",
        text: "登记设备停用、恢复与维护占用。设备恢复后仍需确认计划验证和治疗放行条件。",
        view: "machines",
        action: "管理设备",
      },
    ],
  },
} satisfies Record<
  User["role"],
  {
    description: string;
    schedule: string;
    nav: { id: View; name: string; icon: string }[];
    guide: { title: string; text: string; view: View; action: string }[];
  }
>;

export function workspaceNav(role: User["role"]) {
  return [
    { id: "home", name: "我的工作台", icon: "◈" },
    ...workspaces[role].nav,
    ...common,
  ];
}

export function isRoleReservation(role: User["role"], r: Reservation) {
  if (role === "Scheduler")
    return r.kind === "Consultation" || r.kind === "Treatment";
  if (role === "Physicist")
    return r.kind === "Experiment" || r.kind === "Maintenance";
  return (
    r.kind === "Consultation" ||
    r.kind === "Treatment" ||
    r.kind === "Experiment"
  );
}

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
    workflow:
      "门诊预约 → 交接诊疗人员 → 单次治疗预约 → 跟进受阻与变更 → 记录协调结果",
    guide: [
      {
        title: "1. 查看当天安排与待办",
        text: "登录后先看“今日工作队列”和“协作待办”，确认需要安排、改期或协调的患者。",
        result: "确认待处理事项；已有预约先查看详情，避免重复安排。",
        view: "tasks",
        action: "查看协作待办",
      },
      {
        title: "2. 安排门诊预约",
        text: "进入预约日历，点击“门诊预约”，选择患者、诊疗资源及起止时间，预览核对后确认提交。",
        result: "预约建立后，由诊疗人员接诊、建档并签署处方。",
        view: "calendar",
        action: "打开预约日历",
      },
      {
        title: "3. 安排单次治疗预约",
        text: "诊疗档案建立后，根据诊疗安排点击“单次治疗预约”，选择患者档案、机器与时段并提交。",
        result:
          "预约仅保留时段；诊疗审核、物理验证及设备条件满足后，诊疗人员才能开始治疗。",
        view: "calendar",
        action: "安排治疗预约",
      },
      {
        title: "4. 跟进受阻与预约变更",
        text: "查看预约详情中的受阻原因，联系诊疗人员或物理师处理。需要调整时执行改期、取消或登记未到场；误取消可尝试恢复。",
        result: "恢复时会重新检查冲突；已过期或原时段不可用时重新安排。",
        view: "calendar",
        action: "查看预约详情",
      },
      {
        title: "5. 记录协调结果",
        text: "回到协作待办，将事项标为已读，完成协调后填写处理结果；在本角色操作历史中核查记录。",
        result: "完成待办不会自动解除治疗受阻条件，需由对应责任角色完成处理。",
        view: "tasks",
        action: "处理协调待办",
      },
    ],
    notes: [
      "职责范围：门诊与治疗预约、改期、取消、恢复、未到场登记及预约协调。",
      "交接顺序：预约人员安排门诊 → 诊疗人员签署处方 → 物理师登记计划 → 诊疗人员审核并排实验 → 物理师验证 → 诊疗人员执行治疗。治疗预约可先保留时段。",
    ],
  },
  Clinician: {
    description: "完成诊疗与处方、审核计划、安排实验并执行治疗。",
    schedule: "门诊 / 治疗 / 实验排期",
    nav: [
      { id: "calendar", name: "诊疗与实验日历", icon: "▦" },
      { id: "patients", name: "患者与诊疗", icon: "◎" },
    ],
    workflow:
      "查看门诊 → 建档与签署处方 → 等待计划并审核 → 安排实验并等待验证 → 执行治疗并记录结果",
    guide: [
      {
        title: "1. 查看门诊与协作待办",
        text: "登录后查看今日工作队列，在诊疗与实验日历中确认门诊安排，在协作待办中查看需评估的问题。",
        result: "确认本次患者及需要处理的诊疗事项。",
        view: "calendar",
        action: "查看诊疗安排",
      },
      {
        title: "2. 建档、签署处方并完成接诊",
        text: "进入患者与诊疗，选择患者并建立诊疗档案，签署诊疗记录与处方；再回到门诊预约详情，关联本次档案并登记接诊完成。",
        result: "处方交由物理师用于登记治疗计划；修改已签署处方会生成新版本。",
        view: "patients",
        action: "建档与签署处方",
      },
      {
        title: "3. 审核当前治疗计划",
        text: "等待物理师登记计划后，打开患者档案，核对当前处方对应的计划版本、摘要及使用机器，审核此版本。",
        result:
          "计划审核通过后仍需物理验证；处方或计划更新后须重新核对当前版本。",
        view: "patients",
        action: "查看与审核计划",
      },
      {
        title: "4. 安排物理实验并跟进结果",
        text: "在当前计划下点击“安排物理实验”，选择机器时段并提交。需要调整实验时，在日历详情中改期或取消。",
        result:
          "由物理师执行实验并签署结果；若退回诊疗问题，先评估并处理，再继续后续流程。",
        view: "patients",
        action: "安排物理实验",
      },
      {
        title: "5. 执行单次治疗并登记结果",
        text: "预约人员安排治疗后，在治疗预约详情中确认无受阻项，核对患者身份及当前计划，开始治疗，再登记完成或中断结果。",
        result:
          "未执行的治疗应登记原因；中断后评估后续处理，不会自动视为完成或补约。",
        view: "calendar",
        action: "查看治疗与执行",
      },
    ],
    notes: [
      "职责范围：接诊、诊疗档案、处方签署与修订、计划审核、实验排期、治疗执行及诊疗问题评估。",
      "异常处理：根据协作待办处理诊疗问题；更正已签署处方或执行记录时保留原记录与原因。完成待办不等于计划放行。",
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
    workflow:
      "查看处方与待办 → 登记计划 → 等待审核与实验排期 → 签署验证结果 → 跟进修订；设备维护按需处理",
    guide: [
      {
        title: "1. 查看当前处方与待办",
        text: "登录后查看协作待办，在治疗计划与验证中选择患者，确认诊疗人员已签署当前处方。",
        result: "尚无已签署处方时，等待诊疗人员完成，不能直接登记计划。",
        view: "patients",
        action: "查看处方与档案",
      },
      {
        title: "2. 登记治疗计划版本",
        text: "依据当前处方登记外部治疗计划摘要、使用机器及附件引用；计划修改时填写原因并登记新版本。",
        result:
          "计划交由诊疗人员审核并安排物理实验；本系统记录外部计划，不计算治疗计划。",
        view: "patients",
        action: "登记治疗计划",
      },
      {
        title: "3. 查看物理实验安排",
        text: "在验证与维护日历中查看诊疗人员安排的物理实验，核对计划版本、机器与时间。需要调整实验排期时交由诊疗人员处理。",
        result: "按安排进行计划物理验证；物理实验不要求患者到场。",
        view: "calendar",
        action: "查看实验安排",
      },
      {
        title: "4. 签署物理验证结果",
        text: "打开实验详情，提交通过或不通过结论，并填写结果依据。不通过时选择计划、诊疗或设备问题处理方向。",
        result:
          "计划问题由物理师修订，诊疗问题交诊疗人员评估，设备问题需处理设备状态；验证通过后仍须满足其他放行条件。",
        view: "calendar",
        action: "提交验证结果",
      },
      {
        title: "5. 跟进修订与记录更正",
        text: "根据验证结果和协作待办处理退回事项；计划变更后登记新版本并重新走审核与验证。需要更正验证记录时追加更正说明。",
        result:
          "验证记录更正会阻止当前计划继续用于治疗，需登记新版计划；完成事项后记录待办处理结果。",
        view: "patients",
        action: "查看计划与更正",
      },
    ],
    notes: [
      "职责范围：计划登记与修订、物理验证及更正、设备停用与恢复、维护占用和物理相关待办。",
      "按需设备维护：进入“机器与占用”登记停用、恢复或维护占用；维护占用可在日历中改期或取消。设备恢复不会使失败计划自动通过。",
    ],
  },
} satisfies Record<
  User["role"],
  {
    description: string;
    schedule: string;
    nav: { id: View; name: string; icon: string }[];
    workflow: string;
    guide: {
      title: string;
      text: string;
      result: string;
      view: View;
      action: string;
    }[];
    notes: string[];
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

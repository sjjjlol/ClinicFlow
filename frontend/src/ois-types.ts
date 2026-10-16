export type Entity = { id: string; version: number };
export type CareCase = Entity & {
  patientId: number;
  title: string;
  terminated: boolean;
  terminationReason: string;
  currentPrescriptionId: string | null;
  currentPlanId: string | null;
};
export type Prescription = Entity & {
  caseId: string;
  treatmentSite: string;
  totalDoseGy: number;
  fractionDoseGy: number;
  fractions: number;
  treatmentMaterials: string;
  diagnosis: string;
  content: string;
  reason: string;
  signedBy: string;
  signedUtc: string;
  supersedesId: string | null;
};
export type Plan = Entity & {
  caseId: string;
  prescriptionId: string;
  revision: number;
  verificationMethod: string;
  equipment: string;
  measurementConditions: string;
  expectedValues: string;
  standardName: string;
  standardVersion: string;
  acceptanceCriteria: string;
  allowedDeviation: string;
  summary: string;
  attachment: string;
  machineId: string;
  registeredBy: string;
  reviewedBy: string | null;
  validation: string;
  validatedBy: string | null;
  validationNote: string;
  failureType: string;
};
export type Resource = Entity & {
  name: string;
  kind: string;
  available: boolean;
  reason: string;
};
export type Reservation = Entity & {
  measuredValues: string;
  resultAttachment: string;
  validationOutcome: string;
  validationNote: string;
  failureType: string;
  signedBy: string;
  signedUtc: string | null;
  kind: string;
  patientId: number | null;
  caseId: string | null;
  planId: string | null;
  resourceId: string;
  startUtc: string;
  endUtc: string;
  status: string;
  reason: string;
};
export type Delivery = Entity & {
  reservationId: string;
  planId: string;
  machineId: string;
  startedBy: string;
  startedUtc: string;
  finishedBy: string | null;
  finishedUtc: string | null;
  outcome: string;
  note: string;
};
export type WorkTask = Entity & {
  targetId: string;
  role: string;
  reason: string;
  readBy: string | null;
  resolvedBy: string | null;
  resolution: string;
  createdUtc: string;
};
export type Workspace = {
  drafts: (Entity & {
    caseId: string;
    operation: string;
    payload: string;
    caseVersion: number;
  })[];
  cases: CareCase[];
  prescriptions: Prescription[];
  plans: Plan[];
  resources: Resource[];
  reservations: { reservation: Reservation; blockers: string[] }[];
  deliveries: Delivery[];
  tasks: WorkTask[];
  patients: { id: number; name: string; identifier: string }[];
  audits: {
    id: number;
    actor: string;
    role: string;
    action: string;
    targetId: string;
    detail: string;
    atUtc: string;
  }[];
};
export type Command = Record<string, string | number | boolean>;
export type Field = {
  name: string;
  label: string;
  type?: "text" | "textarea" | "datetime-local" | "checkbox" | "number";
  options?: { value: string; label: string }[];
  value?: string | boolean;
  optional?: boolean;
};
export type Editor = {
  title: string;
  operation: string;
  command: Command;
  fields: Field[];
  context?: string;
  draftVersion?: number;
  initialValues?: Record<string, string | boolean>;
};
export const labels: Record<string, string> = {
  Scheduler: "预约人员",
  Clinician: "诊疗人员",
  Physicist: "物理师",
  Consultation: "门诊",
  Treatment: "治疗",
  Experiment: "物理实验",
  Maintenance: "维护",
  Booked: "已预约",
  InProgress: "执行中",
  Completed: "已完成",
  Interrupted: "中断",
  NotExecuted: "未执行",
  NoShow: "未到场",
  Cancelled: "已取消",
  Passed: "验证通过",
  Failed: "验证失败",
  Pending: "待验证",
  Plan: "计划问题",
  Clinical: "诊疗问题",
  Equipment: "设备问题",
};
export const label = (value: string) => labels[value] || value;
export function localTime(date: string | Date) {
  return new Date(new Date(date).getTime() + 8 * 3600000)
    .toISOString()
    .slice(0, 16);
}
export function displayTime(date: string) {
  return new Intl.DateTimeFormat("zh-CN", {
    timeZone: "Asia/Shanghai",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
    hour12: false,
  }).format(new Date(date));
}
export function nextSlot() {
  return localTime(new Date(Math.ceil((Date.now() + 60000) / 900000) * 900000));
}

import { api } from "./api";
import type { Command } from "./ois-types";

// Request fields describe one use case. Target identifiers are carried in the URL.
export interface OisRequests {
  createCase: { patientId?: number; text?: string };
  terminateCase: { id: string; version?: number; reason?: string };
  addCaseNote: { id: string; version?: number; text?: string };
  signPrescription: {
    id: string;
    version?: number;
    diagnosis?: string;
    text?: string;
    reason?: string;
    treatmentSite?: string;
    totalDoseGy?: number;
    fractionDoseGy?: number;
    fractions?: number;
    treatmentMaterials?: string;
  };
  registerPlan: {
    caseId: string;
    version?: number;
    resourceId?: string;
    text?: string;
    reason?: string;
    attachment?: string;
    verificationMethod?: string;
    equipment?: string;
    measurementConditions?: string;
    expectedValues?: string;
    standardName?: string;
    standardVersion?: string;
    acceptanceCriteria?: string;
    allowedDeviation?: string;
  };
  reviewPlan: { id: string; version?: number };
  rescheduleReservation: {
    id: string;
    version?: number;
    resourceId?: string;
    startUtc?: string | null;
    endUtc?: string | null;
    reason?: string;
  };
  restoreReservation: { id: string; version?: number; reason?: string };
  cancelReservation: { id: string; version?: number; reason?: string };
  recordNoShow: { id: string; version?: number; reason?: string };
  submitExperimentResult: {
    id: string;
    version?: number;
    outcome?: string;
    text?: string;
    failureType?: string;
    measuredValues?: string;
    resultAttachment?: string;
  };
  completeConsultation: { id: string; version?: number; caseId?: string };
  startTreatment: {
    id: string;
    version?: number;
    planId?: string;
    identityChecked?: boolean;
  };
  finishTreatment: {
    id: string;
    version?: number;
    outcome?: string;
    text?: string;
  };
  recordNotExecuted: { id: string; version?: number; reason?: string };
  setMachineAvailability: {
    id: string;
    version?: number;
    available?: boolean;
    reason?: string;
  };
  readTask: { id: string; version?: number };
  resolveTask: { id: string; version?: number; text?: string };
  bookConsultation: {
    resourceId?: string;
    startUtc?: string | null;
    endUtc?: string | null;
    reason?: string;
    patientId?: number;
    caseId?: string;
  };
  bookTreatment: {
    resourceId?: string;
    startUtc?: string | null;
    endUtc?: string | null;
    reason?: string;
    patientId?: number;
    caseId?: string;
  };
  bookExperiment: {
    resourceId?: string;
    startUtc?: string | null;
    endUtc?: string | null;
    reason?: string;
    planId?: string;
  };
  bookMaintenance: {
    resourceId?: string;
    startUtc?: string | null;
    endUtc?: string | null;
    reason?: string;
  };
  savePrescriptionDraft: {
    caseId: string;
    version?: number;
    draftJson?: string;
    draftVersion?: number;
  };
  savePlanDraft: {
    caseId: string;
    version?: number;
    draftJson?: string;
    draftVersion?: number;
  };
  correctPlan: { id: string; version?: number; text?: string; reason?: string };
  correctTreatmentDelivery: {
    id: string;
    version?: number;
    text?: string;
    reason?: string;
  };
}
const endpoints = {
  createCase: {
    path: "/api/ois/cases",
    method: "POST",
    target: null,
    fields: ["patientId", "text"],
  },
  terminateCase: {
    path: "/api/ois/cases/{id}/terminate",
    method: "POST",
    target: "id",
    fields: ["version", "reason"],
  },
  addCaseNote: {
    path: "/api/ois/cases/{id}/notes",
    method: "POST",
    target: "id",
    fields: ["version", "text"],
  },
  signPrescription: {
    path: "/api/ois/cases/{id}/prescriptions",
    method: "POST",
    target: "id",
    fields: [
      "version",
      "diagnosis",
      "text",
      "reason",
      "treatmentSite",
      "totalDoseGy",
      "fractionDoseGy",
      "fractions",
      "treatmentMaterials",
    ],
  },
  registerPlan: {
    path: "/api/ois/cases/{id}/plans",
    method: "POST",
    target: "caseId",
    fields: [
      "version",
      "resourceId",
      "text",
      "reason",
      "attachment",
      "verificationMethod",
      "equipment",
      "measurementConditions",
      "expectedValues",
      "standardName",
      "standardVersion",
      "acceptanceCriteria",
      "allowedDeviation",
    ],
  },
  reviewPlan: {
    path: "/api/ois/plans/{id}/review",
    method: "POST",
    target: "id",
    fields: ["version"],
  },
  rescheduleReservation: {
    path: "/api/ois/reservations/{id}/reschedule",
    method: "POST",
    target: "id",
    fields: ["version", "resourceId", "startUtc", "endUtc", "reason"],
  },
  restoreReservation: {
    path: "/api/ois/reservations/{id}/restore",
    method: "POST",
    target: "id",
    fields: ["version", "reason"],
  },
  cancelReservation: {
    path: "/api/ois/reservations/{id}/cancel",
    method: "POST",
    target: "id",
    fields: ["version", "reason"],
  },
  recordNoShow: {
    path: "/api/ois/reservations/{id}/no-show",
    method: "POST",
    target: "id",
    fields: ["version", "reason"],
  },
  submitExperimentResult: {
    path: "/api/ois/reservations/{id}/experiment-results",
    method: "POST",
    target: "id",
    fields: [
      "version",
      "outcome",
      "text",
      "failureType",
      "measuredValues",
      "resultAttachment",
    ],
  },
  completeConsultation: {
    path: "/api/ois/reservations/{id}/complete-consultation",
    method: "POST",
    target: "id",
    fields: ["version", "caseId"],
  },
  startTreatment: {
    path: "/api/ois/reservations/{id}/start-treatment",
    method: "POST",
    target: "id",
    fields: ["version", "planId", "identityChecked"],
  },
  finishTreatment: {
    path: "/api/ois/treatment-deliveries/{id}/finish",
    method: "POST",
    target: "id",
    fields: ["version", "outcome", "text"],
  },
  recordNotExecuted: {
    path: "/api/ois/reservations/{id}/not-executed",
    method: "POST",
    target: "id",
    fields: ["version", "reason"],
  },
  setMachineAvailability: {
    path: "/api/ois/machines/{id}/availability",
    method: "PUT",
    target: "id",
    fields: ["version", "available", "reason"],
  },
  readTask: {
    path: "/api/ois/tasks/{id}/read",
    method: "POST",
    target: "id",
    fields: ["version"],
  },
  resolveTask: {
    path: "/api/ois/tasks/{id}/resolve",
    method: "POST",
    target: "id",
    fields: ["version", "text"],
  },
  bookConsultation: {
    path: "/api/ois/reservations/consultation",
    method: "POST",
    target: null,
    fields: [
      "resourceId",
      "startUtc",
      "endUtc",
      "reason",
      "patientId",
      "caseId",
    ],
  },
  bookTreatment: {
    path: "/api/ois/reservations/treatment",
    method: "POST",
    target: null,
    fields: [
      "resourceId",
      "startUtc",
      "endUtc",
      "reason",
      "patientId",
      "caseId",
    ],
  },
  bookExperiment: {
    path: "/api/ois/reservations/experiment",
    method: "POST",
    target: null,
    fields: ["resourceId", "startUtc", "endUtc", "reason", "planId"],
  },
  bookMaintenance: {
    path: "/api/ois/reservations/maintenance",
    method: "POST",
    target: null,
    fields: ["resourceId", "startUtc", "endUtc", "reason"],
  },
  savePrescriptionDraft: {
    path: "/api/ois/cases/{id}/prescription-draft",
    method: "PUT",
    target: "caseId",
    fields: ["version", "draftJson", "draftVersion"],
  },
  savePlanDraft: {
    path: "/api/ois/cases/{id}/plan-draft",
    method: "PUT",
    target: "caseId",
    fields: ["version", "draftJson", "draftVersion"],
  },
  correctPlan: {
    path: "/api/ois/plans/{id}/corrections",
    method: "POST",
    target: "id",
    fields: ["version", "text", "reason"],
  },
  correctTreatmentDelivery: {
    path: "/api/ois/treatment-deliveries/{id}/corrections",
    method: "POST",
    target: "id",
    fields: ["version", "text", "reason"],
  },
} as const;

export function sendOisRequest<K extends keyof OisRequests>(
  operation: K,
  request: OisRequests[K],
  key: string,
) {
  const endpoint = endpoints[operation];
  const values = request as Record<string, unknown>;
  const path = endpoint.target
    ? endpoint.path.replace(
        "{id}",
        encodeURIComponent(String(values[endpoint.target])),
      )
    : endpoint.path;
  const body = Object.fromEntries(
    endpoint.fields
      .filter((field) => values[field] !== undefined)
      .map((field) => [field, values[field]]),
  );
  return api<{ targetId: string }>(path, body, key, endpoint.method);
}

// The generic form editor is a UI concern; its field bag never becomes an HTTP contract.
const formOperations: Record<string, keyof OisRequests> = {
  case: "createCase",
  terminate: "terminateCase",
  note: "addCaseNote",
  prescribe: "signPrescription",
  plan: "registerPlan",
  review: "reviewPlan",
  reschedule: "rescheduleReservation",
  restore: "restoreReservation",
  cancel: "cancelReservation",
  "no-show": "recordNoShow",
  validate: "submitExperimentResult",
  consult: "completeConsultation",
  start: "startTreatment",
  finish: "finishTreatment",
  "not-executed": "recordNotExecuted",
  machine: "setMachineAvailability",
  "read-task": "readTask",
  "resolve-task": "resolveTask",
  "book:Consultation": "bookConsultation",
  "book:Treatment": "bookTreatment",
  "book:Experiment": "bookExperiment",
  "book:Maintenance": "bookMaintenance",
  "save-draft:prescribe": "savePrescriptionDraft",
  "save-draft:plan": "savePlanDraft",
  "correct-plan": "correctPlan",
  "correct-delivery": "correctTreatmentDelivery",
};
export function submitOisForm(operation: string, values: Command, key: string) {
  const name =
    operation === "book" || operation === "save-draft"
      ? `${operation}:${values.kind}`
      : operation;
  const endpoint = formOperations[name];
  if (!endpoint) throw new Error("未知业务操作");
  return sendOisRequest(
    endpoint,
    values as unknown as OisRequests[typeof endpoint],
    key,
  );
}

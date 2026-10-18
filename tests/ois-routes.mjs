const routes = {
  "case": {
    "path": "/api/ois/cases",
    "method": "POST",
    "target": null,
    "fields": [
      "patientId",
      "text"
    ]
  },
  "terminate": {
    "path": "/api/ois/cases/{id}/terminate",
    "method": "POST",
    "target": "id",
    "fields": [
      "version",
      "reason"
    ]
  },
  "note": {
    "path": "/api/ois/cases/{id}/notes",
    "method": "POST",
    "target": "id",
    "fields": [
      "version",
      "text"
    ]
  },
  "prescribe": {
    "path": "/api/ois/cases/{id}/prescriptions",
    "method": "POST",
    "target": "id",
    "fields": [
      "version",
      "diagnosis",
      "text",
      "reason",
      "treatmentSite",
      "totalDoseGy",
      "fractionDoseGy",
      "fractions",
      "treatmentMaterials"
    ]
  },
  "plan": {
    "path": "/api/ois/cases/{id}/plans",
    "method": "POST",
    "target": "caseId",
    "fields": [
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
      "allowedDeviation"
    ]
  },
  "review": {
    "path": "/api/ois/plans/{id}/review",
    "method": "POST",
    "target": "id",
    "fields": [
      "version"
    ]
  },
  "reschedule": {
    "path": "/api/ois/reservations/{id}/reschedule",
    "method": "POST",
    "target": "id",
    "fields": [
      "version",
      "resourceId",
      "startUtc",
      "endUtc",
      "reason"
    ]
  },
  "restore": {
    "path": "/api/ois/reservations/{id}/restore",
    "method": "POST",
    "target": "id",
    "fields": [
      "version",
      "reason"
    ]
  },
  "cancel": {
    "path": "/api/ois/reservations/{id}/cancel",
    "method": "POST",
    "target": "id",
    "fields": [
      "version",
      "reason"
    ]
  },
  "no-show": {
    "path": "/api/ois/reservations/{id}/no-show",
    "method": "POST",
    "target": "id",
    "fields": [
      "version",
      "reason"
    ]
  },
  "validate": {
    "path": "/api/ois/reservations/{id}/experiment-results",
    "method": "POST",
    "target": "id",
    "fields": [
      "version",
      "outcome",
      "text",
      "failureType",
      "measuredValues",
      "resultAttachment"
    ]
  },
  "consult": {
    "path": "/api/ois/reservations/{id}/complete-consultation",
    "method": "POST",
    "target": "id",
    "fields": [
      "version",
      "caseId"
    ]
  },
  "start": {
    "path": "/api/ois/reservations/{id}/start-treatment",
    "method": "POST",
    "target": "id",
    "fields": [
      "version",
      "planId",
      "identityChecked"
    ]
  },
  "finish": {
    "path": "/api/ois/treatment-deliveries/{id}/finish",
    "method": "POST",
    "target": "id",
    "fields": [
      "version",
      "outcome",
      "text"
    ]
  },
  "not-executed": {
    "path": "/api/ois/reservations/{id}/not-executed",
    "method": "POST",
    "target": "id",
    "fields": [
      "version",
      "reason"
    ]
  },
  "machine": {
    "path": "/api/ois/machines/{id}/availability",
    "method": "PUT",
    "target": "id",
    "fields": [
      "version",
      "available",
      "reason"
    ]
  },
  "read-task": {
    "path": "/api/ois/tasks/{id}/read",
    "method": "POST",
    "target": "id",
    "fields": [
      "version"
    ]
  },
  "resolve-task": {
    "path": "/api/ois/tasks/{id}/resolve",
    "method": "POST",
    "target": "id",
    "fields": [
      "version",
      "text"
    ]
  },
  "book:Consultation": {
    "path": "/api/ois/reservations/consultation",
    "method": "POST",
    "target": null,
    "fields": [
      "resourceId",
      "startUtc",
      "endUtc",
      "reason",
      "patientId",
      "caseId"
    ]
  },
  "book:Treatment": {
    "path": "/api/ois/reservations/treatment",
    "method": "POST",
    "target": null,
    "fields": [
      "resourceId",
      "startUtc",
      "endUtc",
      "reason",
      "patientId",
      "caseId"
    ]
  },
  "book:Experiment": {
    "path": "/api/ois/reservations/experiment",
    "method": "POST",
    "target": null,
    "fields": [
      "resourceId",
      "startUtc",
      "endUtc",
      "reason",
      "planId"
    ]
  },
  "book:Maintenance": {
    "path": "/api/ois/reservations/maintenance",
    "method": "POST",
    "target": null,
    "fields": [
      "resourceId",
      "startUtc",
      "endUtc",
      "reason"
    ]
  },
  "save-draft:prescribe": {
    "path": "/api/ois/cases/{id}/prescription-draft",
    "method": "PUT",
    "target": "caseId",
    "fields": [
      "version",
      "draftJson",
      "draftVersion"
    ]
  },
  "save-draft:plan": {
    "path": "/api/ois/cases/{id}/plan-draft",
    "method": "PUT",
    "target": "caseId",
    "fields": [
      "version",
      "draftJson",
      "draftVersion"
    ]
  },
  "correct-plan": {
    "path": "/api/ois/plans/{id}/corrections",
    "method": "POST",
    "target": "id",
    "fields": [
      "version",
      "text",
      "reason"
    ]
  },
  "correct-delivery": {
    "path": "/api/ois/treatment-deliveries/{id}/corrections",
    "method": "POST",
    "target": "id",
    "fields": [
      "version",
      "text",
      "reason"
    ]
  }
};
export function oisRequest(client, operation, command, key) {
  const name = operation === 'book' || operation === 'save-draft' ? `${operation}:${command.kind}` : operation;
  const endpoint = routes[name];
  if (!endpoint) throw new Error(`Unknown test operation: ${name}`);
  const path = endpoint.target ? endpoint.path.replace('{id}', encodeURIComponent(command[endpoint.target])) : endpoint.path;
  const body = Object.fromEntries(endpoint.fields.filter(field => command[field] !== undefined).map(field => [field, command[field]]));
  return client.request(path, body, key, endpoint.method);
}
export { routes };

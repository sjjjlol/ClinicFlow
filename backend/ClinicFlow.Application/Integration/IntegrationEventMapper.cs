using System;
using System.Collections.Generic;
using System.Text.Json;
using ClinicFlow.Ois.Events;

namespace ClinicFlow.Application.Integration
{
    internal static class IntegrationEventMapper
    {
        private static readonly JsonSerializerOptions Json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        internal static IntegrationEvent Map(DomainEvent domainEvent)
        {
            switch (domainEvent)
            {
                case PrescriptionSigned e:
                    return new IntegrationEvent(e.EventId, "clinicflow.prescription-signed", 1, e.OccurredUtc,
                        JsonSerializer.Serialize(new PrescriptionSignedV1 { CaseId = e.CaseId, PrescriptionId = e.PrescriptionId }, Json));
                case CaseTerminated e:
                    return new IntegrationEvent(e.EventId, "clinicflow.case-terminated", 1, e.OccurredUtc,
                        JsonSerializer.Serialize(new CaseTerminatedV1 { CaseId = e.CaseId }, Json));
                case PlanRegistered e:
                    return new IntegrationEvent(e.EventId, "clinicflow.plan-registered", 1, e.OccurredUtc,
                        JsonSerializer.Serialize(new PlanRegisteredV1 { PlanId = e.PlanId, CaseId = e.CaseId, PrescriptionId = e.PrescriptionId }, Json));
                case PlanValidated e:
                    return new IntegrationEvent(e.EventId, "clinicflow.plan-validated", 1, e.OccurredUtc,
                        JsonSerializer.Serialize(new PlanValidatedV1 { PlanId = e.PlanId, CaseId = e.CaseId, ReservationId = e.ReservationId, MachineId = e.MachineId, Outcome = e.Outcome, FailureType = e.FailureType }, Json));
                case TreatmentFinished e:
                    return new IntegrationEvent(e.EventId, "clinicflow.treatment-finished", 1, e.OccurredUtc,
                        JsonSerializer.Serialize(new TreatmentFinishedV1 { DeliveryId = e.DeliveryId, ReservationId = e.ReservationId, PlanId = e.PlanId, Outcome = e.Outcome }, Json));
                default: throw new InvalidOperationException("Unregistered integration mapping");
            }
        }
    }
}

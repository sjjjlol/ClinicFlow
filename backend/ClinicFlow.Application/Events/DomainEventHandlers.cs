using System;
using System.Collections.Generic;
using System.Linq;
using ClinicFlow.Ois.Events;
using ClinicFlow.Application.Integration;

namespace ClinicFlow.Ois
{
    public sealed partial class OisSession
    {
        internal List<IntegrationEvent> IntegrationEvents { get; } = new List<IntegrationEvent>();
        internal void HandleDomainEvents()
        {
            var aggregates = state.Cases.Cast<IHasDomainEvents>().Concat(state.Plans).Concat(state.Deliveries).ToList();
            foreach (var aggregate in aggregates)
            foreach (var domainEvent in aggregate.DequeueDomainEvents())
            {
                // Synchronous handlers run before persistence commits. Exceptions roll back all changes.
                switch (domainEvent)
                {
                    case PrescriptionSigned e:
                        Support.AddTask(e.CaseId, "Physicist", "处方已签署，请登记匹配的治疗计划", e.OccurredUtc);
                        Scheduling.BlockedTasks(e.CaseId, "处方更新，待计划审核与验证", e.OccurredUtc);
                        break;
                    case CaseTerminated e:
                        Scheduling.BlockedTasks(e.CaseId, "诊疗方案已终止", e.OccurredUtc);
                        Support.AddTask(e.CaseId, "Physicist", "诊疗方案已终止：" + e.Reason, e.OccurredUtc);
                        break;
                    case PlanRegistered e:
                        Clinical.SelectPlan(e.CaseId, e.PlanId);
                        Support.AddTask(e.PlanId, "Clinician", "新计划待审核及实验排期", e.OccurredUtc);
                        Scheduling.BlockedTasks(e.CaseId, "计划更新，待重新审核与验证", e.OccurredUtc);
                        break;
                    case PlanValidated e:
                        Scheduling.CompleteExperiment(e.ReservationId);
                        if (e.Outcome == "Failed")
                        {
                            Support.AddTask(e.PlanId, e.FailureType == "Clinical" ? "Clinician" : "Physicist", e.Note, e.OccurredUtc);
                            if (e.FailureType != "Clinical")
                                Support.AddTask(e.PlanId, "Clinician", "物理验证失败，物理师处理中：" + e.Note, e.OccurredUtc);
                            Scheduling.BlockedTasks(e.CaseId, "物理验证失败：" + e.Note, e.OccurredUtc);
                            if (e.FailureType == "Equipment") Scheduling.StopMachine(e.MachineId, e.Note, e.OccurredUtc);
                        }
                        break;
                    case TreatmentFinished e:
                        if (e.Outcome == "Interrupted")
                            Support.AddTask(e.DeliveryId, "Clinician", "治疗中断，需评估后续安排：" + e.Note, e.OccurredUtc);
                        break;
                    default:
                        throw new InvalidOperationException("Unregistered domain event handler: " + domainEvent.GetType().Name);
                }
                IntegrationEvents.Add(IntegrationEventMapper.Map(domainEvent));
            }
        }
    }

}

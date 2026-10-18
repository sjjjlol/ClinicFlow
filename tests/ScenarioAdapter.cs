using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClinicFlow;
using ClinicFlow.Ois;
using ClinicFlow.Ois.Operations;
using ClinicFlow.Ois.Requests;
namespace ClinicFlow.Tests
{
    // Compatibility vocabulary for existing scenario fixtures only; production has no dispatcher.
    internal static class ScenarioAdapter
    {
        private static T Convert<T>(ScenarioCommand input) where T : new()
        {
            var result = new T();
            foreach (var p in typeof(T).GetProperties().Where(p => p.CanWrite))
                p.SetValue(result, typeof(ScenarioCommand).GetProperty(p.Name)!.GetValue(input));
            return result;
        }
        public static string Apply(this OisState w, string op, ScenarioCommand c, string actor, string role, DateTime now)
        {
            if (op == "case") return new CasesOperations(w).CreateCase(Convert<CreateCaseRequest>(c), actor, role, now);
            if (op == "terminate") return new CasesOperations(w).TerminateCase(Convert<TerminateCaseRequest>(c), actor, role, now);
            if (op == "note") return new CasesOperations(w).AddCaseNote(Convert<AddCaseNoteRequest>(c), actor, role, now);
            if (op == "prescribe") return new PrescriptionsOperations(w).SignPrescription(Convert<SignPrescriptionRequest>(c), actor, role, now);
            if (op == "plan") return new PlansOperations(w).RegisterPlan(Convert<RegisterPlanRequest>(c), actor, role, now);
            if (op == "review") return new PlansOperations(w).ReviewPlan(Convert<ReviewPlanRequest>(c), actor, role, now);
            if (op == "reschedule") return new ReservationsOperations(w).RescheduleReservation(Convert<RescheduleReservationRequest>(c), actor, role, now);
            if (op == "restore") return new ReservationsOperations(w).RestoreReservation(Convert<RestoreReservationRequest>(c), actor, role, now);
            if (op == "cancel") return new ReservationsOperations(w).CancelReservation(Convert<CancelReservationRequest>(c), actor, role, now);
            if (op == "no-show") return new ReservationsOperations(w).RecordNoShow(Convert<RecordNoShowRequest>(c), actor, role, now);
            if (op == "validate") return new ExperimentsOperations(w).SubmitExperimentResult(Convert<SubmitExperimentResultRequest>(c), actor, role, now);
            if (op == "consult") return new ConsultationsOperations(w).CompleteConsultation(Convert<CompleteConsultationRequest>(c), actor, role, now);
            if (op == "start") return new TreatmentDeliveriesOperations(w).StartTreatment(Convert<StartTreatmentRequest>(c), actor, role, now);
            if (op == "finish") return new TreatmentDeliveriesOperations(w).FinishTreatment(Convert<FinishTreatmentRequest>(c), actor, role, now);
            if (op == "not-executed") return new TreatmentDeliveriesOperations(w).RecordNotExecuted(Convert<RecordNotExecutedRequest>(c), actor, role, now);
            if (op == "machine") return new MachinesOperations(w).SetMachineAvailability(Convert<SetMachineAvailabilityRequest>(c), actor, role, now);
            if (op == "read-task") return new TasksOperations(w).ReadTask(Convert<ReadTaskRequest>(c), actor, role, now);
            if (op == "resolve-task") return new TasksOperations(w).ResolveTask(Convert<ResolveTaskRequest>(c), actor, role, now);
            if (op == "book" && c.Kind == "Consultation") return new ReservationsOperations(w).BookConsultation(Convert<BookConsultationRequest>(c), actor, role, now);
            if (op == "book" && c.Kind == "Treatment") return new ReservationsOperations(w).BookTreatment(Convert<BookTreatmentRequest>(c), actor, role, now);
            if (op == "book" && c.Kind == "Experiment") return new ReservationsOperations(w).BookExperiment(Convert<BookExperimentRequest>(c), actor, role, now);
            if (op == "book" && c.Kind == "Maintenance") return new ReservationsOperations(w).BookMaintenance(Convert<BookMaintenanceRequest>(c), actor, role, now);
            if (op == "save-draft" && c.Kind == "prescribe") return new PrescriptionsOperations(w).SavePrescriptionDraft(Convert<SavePrescriptionDraftRequest>(c), actor, role, now);
            if (op == "save-draft" && c.Kind == "plan") return new PlansOperations(w).SavePlanDraft(Convert<SavePlanDraftRequest>(c), actor, role, now);
            if (op == "correction" && w.Plans.Any(x => x.Id == c.Id)) return new PlansOperations(w).CorrectPlan(Convert<CorrectPlanRequest>(c), actor, role, now);
            if (op == "correction" && w.Deliveries.Any(x => x.Id == c.Id)) return new TreatmentDeliveriesOperations(w).CorrectTreatmentDelivery(Convert<CorrectTreatmentDeliveryRequest>(c), actor, role, now);
            if (op == "correction" && w.Prescriptions.Any(x => x.Id == c.Id)) throw new BusinessException("revision_required", "处方更正请签署新处方版本", 400);
            throw new BusinessException("invalid_operation", "未知测试操作", 400);
        }
        public static Task<object> Execute(this CommandExecutor executor, string op, ScenarioCommand c, string actor, string role, string key, string correlation, CancellationToken ct)
        {
            if (op == "case") return executor.Execute(Convert<CreateCaseRequest>(c), actor, role, key, correlation, ct, (w, input, a, r, now) => new CasesOperations(w).CreateCase(input, a, r, now));
            if (op == "terminate") return executor.Execute(Convert<TerminateCaseRequest>(c), actor, role, key, correlation, ct, (w, input, a, r, now) => new CasesOperations(w).TerminateCase(input, a, r, now));
            if (op == "note") return executor.Execute(Convert<AddCaseNoteRequest>(c), actor, role, key, correlation, ct, (w, input, a, r, now) => new CasesOperations(w).AddCaseNote(input, a, r, now));
            if (op == "prescribe") return executor.Execute(Convert<SignPrescriptionRequest>(c), actor, role, key, correlation, ct, (w, input, a, r, now) => new PrescriptionsOperations(w).SignPrescription(input, a, r, now));
            if (op == "plan") return executor.Execute(Convert<RegisterPlanRequest>(c), actor, role, key, correlation, ct, (w, input, a, r, now) => new PlansOperations(w).RegisterPlan(input, a, r, now));
            if (op == "review") return executor.Execute(Convert<ReviewPlanRequest>(c), actor, role, key, correlation, ct, (w, input, a, r, now) => new PlansOperations(w).ReviewPlan(input, a, r, now));
            if (op == "reschedule") return executor.Execute(Convert<RescheduleReservationRequest>(c), actor, role, key, correlation, ct, (w, input, a, r, now) => new ReservationsOperations(w).RescheduleReservation(input, a, r, now));
            if (op == "restore") return executor.Execute(Convert<RestoreReservationRequest>(c), actor, role, key, correlation, ct, (w, input, a, r, now) => new ReservationsOperations(w).RestoreReservation(input, a, r, now));
            if (op == "cancel") return executor.Execute(Convert<CancelReservationRequest>(c), actor, role, key, correlation, ct, (w, input, a, r, now) => new ReservationsOperations(w).CancelReservation(input, a, r, now));
            if (op == "no-show") return executor.Execute(Convert<RecordNoShowRequest>(c), actor, role, key, correlation, ct, (w, input, a, r, now) => new ReservationsOperations(w).RecordNoShow(input, a, r, now));
            if (op == "validate") return executor.Execute(Convert<SubmitExperimentResultRequest>(c), actor, role, key, correlation, ct, (w, input, a, r, now) => new ExperimentsOperations(w).SubmitExperimentResult(input, a, r, now));
            if (op == "consult") return executor.Execute(Convert<CompleteConsultationRequest>(c), actor, role, key, correlation, ct, (w, input, a, r, now) => new ConsultationsOperations(w).CompleteConsultation(input, a, r, now));
            if (op == "start") return executor.Execute(Convert<StartTreatmentRequest>(c), actor, role, key, correlation, ct, (w, input, a, r, now) => new TreatmentDeliveriesOperations(w).StartTreatment(input, a, r, now));
            if (op == "finish") return executor.Execute(Convert<FinishTreatmentRequest>(c), actor, role, key, correlation, ct, (w, input, a, r, now) => new TreatmentDeliveriesOperations(w).FinishTreatment(input, a, r, now));
            if (op == "not-executed") return executor.Execute(Convert<RecordNotExecutedRequest>(c), actor, role, key, correlation, ct, (w, input, a, r, now) => new TreatmentDeliveriesOperations(w).RecordNotExecuted(input, a, r, now));
            if (op == "machine") return executor.Execute(Convert<SetMachineAvailabilityRequest>(c), actor, role, key, correlation, ct, (w, input, a, r, now) => new MachinesOperations(w).SetMachineAvailability(input, a, r, now));
            if (op == "read-task") return executor.Execute(Convert<ReadTaskRequest>(c), actor, role, key, correlation, ct, (w, input, a, r, now) => new TasksOperations(w).ReadTask(input, a, r, now));
            if (op == "resolve-task") return executor.Execute(Convert<ResolveTaskRequest>(c), actor, role, key, correlation, ct, (w, input, a, r, now) => new TasksOperations(w).ResolveTask(input, a, r, now));
            if (op == "book" && c.Kind == "Consultation") return executor.Execute(Convert<BookConsultationRequest>(c), actor, role, key, correlation, ct, (w, input, a, r, now) => new ReservationsOperations(w).BookConsultation(input, a, r, now));
            if (op == "book" && c.Kind == "Treatment") return executor.Execute(Convert<BookTreatmentRequest>(c), actor, role, key, correlation, ct, (w, input, a, r, now) => new ReservationsOperations(w).BookTreatment(input, a, r, now));
            if (op == "book" && c.Kind == "Experiment") return executor.Execute(Convert<BookExperimentRequest>(c), actor, role, key, correlation, ct, (w, input, a, r, now) => new ReservationsOperations(w).BookExperiment(input, a, r, now));
            if (op == "book" && c.Kind == "Maintenance") return executor.Execute(Convert<BookMaintenanceRequest>(c), actor, role, key, correlation, ct, (w, input, a, r, now) => new ReservationsOperations(w).BookMaintenance(input, a, r, now));
            if (op == "save-draft" && c.Kind == "prescribe") return executor.Execute(Convert<SavePrescriptionDraftRequest>(c), actor, role, key, correlation, ct, (w, input, a, r, now) => new PrescriptionsOperations(w).SavePrescriptionDraft(input, a, r, now));
            if (op == "save-draft" && c.Kind == "plan") return executor.Execute(Convert<SavePlanDraftRequest>(c), actor, role, key, correlation, ct, (w, input, a, r, now) => new PlansOperations(w).SavePlanDraft(input, a, r, now));
            throw new BusinessException("invalid_operation", "未知测试操作", 400);
        }
    }
}

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClinicFlow;
using ClinicFlow.Ois;
using ClinicFlow.Ois.Requests;
namespace ClinicFlow.Tests
{
    // Compatibility vocabulary for existing scenario fixtures only; production has no dispatcher.
    internal static class ScenarioAdapter
    {
        public static System.Collections.Generic.List<string> Blockers(this OisState data, Reservation r) => new OisSession(data).Scheduling.Blockers(r.Id);
        private static T Convert<T>(ScenarioCommand input) where T : new()
        {
            var result = new T();
            foreach (var p in typeof(T).GetProperties().Where(p => p.CanWrite))
                p.SetValue(result, typeof(ScenarioCommand).GetProperty(p.Name)!.GetValue(input));
            return result;
        }
        public static string Apply(this OisState data, string op, ScenarioCommand c, string actor, string role, DateTime now)
        {
            var w = new OisSession(data);
            if (op == "case") return w.Clinical.CreateCase(Convert<CreateCaseRequest>(c), actor, role, now);
            if (op == "terminate") return w.Clinical.TerminateCase(Convert<TerminateCaseRequest>(c), actor, role, now);
            if (op == "note") return w.Clinical.AddCaseNote(Convert<AddCaseNoteRequest>(c), actor, role, now);
            if (op == "prescribe") return w.Clinical.SignPrescription(Convert<SignPrescriptionRequest>(c), actor, role, now);
            if (op == "plan") return w.Planning.RegisterPlan(Convert<RegisterPlanRequest>(c), actor, role, now);
            if (op == "review") return w.Planning.ReviewPlan(Convert<ReviewPlanRequest>(c), actor, role, now);
            if (op == "reschedule") return w.Scheduling.RescheduleReservation(Convert<RescheduleReservationRequest>(c), actor, role, now);
            if (op == "restore") return w.Scheduling.RestoreReservation(Convert<RestoreReservationRequest>(c), actor, role, now);
            if (op == "cancel") return w.Scheduling.CancelReservation(Convert<CancelReservationRequest>(c), actor, role, now);
            if (op == "no-show") return w.Scheduling.RecordNoShow(Convert<RecordNoShowRequest>(c), actor, role, now);
            if (op == "validate") return w.Planning.SubmitExperimentResult(Convert<SubmitExperimentResultRequest>(c), actor, role, now);
            if (op == "consult") return w.Scheduling.CompleteConsultation(Convert<CompleteConsultationRequest>(c), actor, role, now);
            if (op == "start") return w.Scheduling.StartTreatment(Convert<StartTreatmentRequest>(c), actor, role, now);
            if (op == "finish") return w.Scheduling.FinishTreatment(Convert<FinishTreatmentRequest>(c), actor, role, now);
            if (op == "not-executed") return w.Scheduling.RecordNotExecuted(Convert<RecordNotExecutedRequest>(c), actor, role, now);
            if (op == "machine") return w.Scheduling.SetMachineAvailability(Convert<SetMachineAvailabilityRequest>(c), actor, role, now);
            if (op == "read-task") return w.Support.ReadTask(Convert<ReadTaskRequest>(c), actor, role, now);
            if (op == "resolve-task") return w.Support.ResolveTask(Convert<ResolveTaskRequest>(c), actor, role, now);
            if (op == "book" && c.Kind == "Consultation") return w.Scheduling.BookConsultation(Convert<BookConsultationRequest>(c), actor, role, now);
            if (op == "book" && c.Kind == "Treatment") return w.Scheduling.BookTreatment(Convert<BookTreatmentRequest>(c), actor, role, now);
            if (op == "book" && c.Kind == "Experiment") return w.Scheduling.BookExperiment(Convert<BookExperimentRequest>(c), actor, role, now);
            if (op == "book" && c.Kind == "Maintenance") return w.Scheduling.BookMaintenance(Convert<BookMaintenanceRequest>(c), actor, role, now);
            if (op == "save-draft" && c.Kind == "prescribe") return w.Clinical.SavePrescriptionDraft(Convert<SavePrescriptionDraftRequest>(c), actor, role, now);
            if (op == "save-draft" && c.Kind == "plan") return w.Planning.SavePlanDraft(Convert<SavePlanDraftRequest>(c), actor, role, now);
            if (op == "correction" && data.Plans.Any(x => x.Id == c.Id)) return w.Planning.CorrectPlan(Convert<CorrectPlanRequest>(c), actor, role, now);
            if (op == "correction" && data.Deliveries.Any(x => x.Id == c.Id)) return w.Scheduling.CorrectTreatmentDelivery(Convert<CorrectTreatmentDeliveryRequest>(c), actor, role, now);
            if (op == "correction" && data.Prescriptions.Any(x => x.Id == c.Id)) throw new BusinessException("revision_required", "处方更正请签署新处方版本", 400);
            throw new BusinessException("invalid_operation", "未知测试操作", 400);
        }
        public static Task<object> Execute(this CommandExecutor executor, string op, ScenarioCommand c, string actor, string role, string key, string correlation, CancellationToken ct)
        {
            if (op == "case") return executor.Execute(Convert<CreateCaseRequest>(c), actor, role, key, correlation, ct, (w, input, a, r, now) => w.Clinical.CreateCase(input, a, r, now));
            if (op == "terminate") return executor.Execute(Convert<TerminateCaseRequest>(c), actor, role, key, correlation, ct, (w, input, a, r, now) => w.Clinical.TerminateCase(input, a, r, now));
            if (op == "note") return executor.Execute(Convert<AddCaseNoteRequest>(c), actor, role, key, correlation, ct, (w, input, a, r, now) => w.Clinical.AddCaseNote(input, a, r, now));
            if (op == "prescribe") return executor.Execute(Convert<SignPrescriptionRequest>(c), actor, role, key, correlation, ct, (w, input, a, r, now) => w.Clinical.SignPrescription(input, a, r, now));
            if (op == "plan") return executor.Execute(Convert<RegisterPlanRequest>(c), actor, role, key, correlation, ct, (w, input, a, r, now) => w.Planning.RegisterPlan(input, a, r, now));
            if (op == "review") return executor.Execute(Convert<ReviewPlanRequest>(c), actor, role, key, correlation, ct, (w, input, a, r, now) => w.Planning.ReviewPlan(input, a, r, now));
            if (op == "reschedule") return executor.Execute(Convert<RescheduleReservationRequest>(c), actor, role, key, correlation, ct, (w, input, a, r, now) => w.Scheduling.RescheduleReservation(input, a, r, now));
            if (op == "restore") return executor.Execute(Convert<RestoreReservationRequest>(c), actor, role, key, correlation, ct, (w, input, a, r, now) => w.Scheduling.RestoreReservation(input, a, r, now));
            if (op == "cancel") return executor.Execute(Convert<CancelReservationRequest>(c), actor, role, key, correlation, ct, (w, input, a, r, now) => w.Scheduling.CancelReservation(input, a, r, now));
            if (op == "no-show") return executor.Execute(Convert<RecordNoShowRequest>(c), actor, role, key, correlation, ct, (w, input, a, r, now) => w.Scheduling.RecordNoShow(input, a, r, now));
            if (op == "validate") return executor.Execute(Convert<SubmitExperimentResultRequest>(c), actor, role, key, correlation, ct, (w, input, a, r, now) => w.Planning.SubmitExperimentResult(input, a, r, now));
            if (op == "consult") return executor.Execute(Convert<CompleteConsultationRequest>(c), actor, role, key, correlation, ct, (w, input, a, r, now) => w.Scheduling.CompleteConsultation(input, a, r, now));
            if (op == "start") return executor.Execute(Convert<StartTreatmentRequest>(c), actor, role, key, correlation, ct, (w, input, a, r, now) => w.Scheduling.StartTreatment(input, a, r, now));
            if (op == "finish") return executor.Execute(Convert<FinishTreatmentRequest>(c), actor, role, key, correlation, ct, (w, input, a, r, now) => w.Scheduling.FinishTreatment(input, a, r, now));
            if (op == "not-executed") return executor.Execute(Convert<RecordNotExecutedRequest>(c), actor, role, key, correlation, ct, (w, input, a, r, now) => w.Scheduling.RecordNotExecuted(input, a, r, now));
            if (op == "machine") return executor.Execute(Convert<SetMachineAvailabilityRequest>(c), actor, role, key, correlation, ct, (w, input, a, r, now) => w.Scheduling.SetMachineAvailability(input, a, r, now));
            if (op == "read-task") return executor.Execute(Convert<ReadTaskRequest>(c), actor, role, key, correlation, ct, (w, input, a, r, now) => w.Support.ReadTask(input, a, r, now));
            if (op == "resolve-task") return executor.Execute(Convert<ResolveTaskRequest>(c), actor, role, key, correlation, ct, (w, input, a, r, now) => w.Support.ResolveTask(input, a, r, now));
            if (op == "book" && c.Kind == "Consultation") return executor.Execute(Convert<BookConsultationRequest>(c), actor, role, key, correlation, ct, (w, input, a, r, now) => w.Scheduling.BookConsultation(input, a, r, now));
            if (op == "book" && c.Kind == "Treatment") return executor.Execute(Convert<BookTreatmentRequest>(c), actor, role, key, correlation, ct, (w, input, a, r, now) => w.Scheduling.BookTreatment(input, a, r, now));
            if (op == "book" && c.Kind == "Experiment") return executor.Execute(Convert<BookExperimentRequest>(c), actor, role, key, correlation, ct, (w, input, a, r, now) => w.Scheduling.BookExperiment(input, a, r, now));
            if (op == "book" && c.Kind == "Maintenance") return executor.Execute(Convert<BookMaintenanceRequest>(c), actor, role, key, correlation, ct, (w, input, a, r, now) => w.Scheduling.BookMaintenance(input, a, r, now));
            if (op == "save-draft" && c.Kind == "prescribe") return executor.Execute(Convert<SavePrescriptionDraftRequest>(c), actor, role, key, correlation, ct, (w, input, a, r, now) => w.Clinical.SavePrescriptionDraft(input, a, r, now));
            if (op == "save-draft" && c.Kind == "plan") return executor.Execute(Convert<SavePlanDraftRequest>(c), actor, role, key, correlation, ct, (w, input, a, r, now) => w.Planning.SavePlanDraft(input, a, r, now));
            throw new BusinessException("invalid_operation", "未知测试操作", 400);
        }
    }
}

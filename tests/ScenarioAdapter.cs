using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClinicFlow;
using ClinicFlow.Ois;
using ClinicFlow.Application.Commands;
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
            var result = ApplyCore(w, data, op, c, actor, role, now);
            w.HandleDomainEvents();
            return result;
        }
        private static string ApplyCore(OisSession w, OisState data, string op, ScenarioCommand c, string actor, string role, DateTime now)
        {
            if (op == "case") return w.Clinical.CreateCase(Convert<CreateCaseCommand>(c), actor, role, now);
            if (op == "terminate") return w.Clinical.TerminateCase(Convert<TerminateCaseCommand>(c), actor, role, now);
            if (op == "note") return w.Clinical.AddCaseNote(Convert<AddCaseNoteCommand>(c), actor, role, now);
            if (op == "prescribe") return w.Clinical.SignPrescription(Convert<SignPrescriptionCommand>(c), actor, role, now);
            if (op == "plan") return w.Planning.RegisterPlan(Convert<RegisterPlanCommand>(c), actor, role, now);
            if (op == "review") return w.Planning.ReviewPlan(Convert<ReviewPlanCommand>(c), actor, role, now);
            if (op == "reschedule") return w.Scheduling.RescheduleReservation(Convert<RescheduleReservationCommand>(c), actor, role, now);
            if (op == "restore") return w.Scheduling.RestoreReservation(Convert<RestoreReservationCommand>(c), actor, role, now);
            if (op == "cancel") return w.Scheduling.CancelReservation(Convert<CancelReservationCommand>(c), actor, role, now);
            if (op == "no-show") return w.Scheduling.RecordNoShow(Convert<RecordNoShowCommand>(c), actor, role, now);
            if (op == "validate") return w.Planning.SubmitExperimentResult(Convert<SubmitExperimentResultCommand>(c), actor, role, now);
            if (op == "consult") return w.Scheduling.CompleteConsultation(Convert<CompleteConsultationCommand>(c), actor, role, now);
            if (op == "start") return w.Scheduling.StartTreatment(Convert<StartTreatmentCommand>(c), actor, role, now);
            if (op == "finish") return w.Scheduling.FinishTreatment(Convert<FinishTreatmentCommand>(c), actor, role, now);
            if (op == "not-executed") return w.Scheduling.RecordNotExecuted(Convert<RecordNotExecutedCommand>(c), actor, role, now);
            if (op == "machine") return w.Scheduling.SetMachineAvailability(Convert<SetMachineAvailabilityCommand>(c), actor, role, now);
            if (op == "read-task") return w.Support.ReadTask(Convert<ReadTaskCommand>(c), actor, role, now);
            if (op == "resolve-task") return w.Support.ResolveTask(Convert<ResolveTaskCommand>(c), actor, role, now);
            if (op == "book" && c.Kind == "Consultation") return w.Scheduling.BookConsultation(Convert<BookConsultationCommand>(c), actor, role, now);
            if (op == "book" && c.Kind == "Treatment") return w.Scheduling.BookTreatment(Convert<BookTreatmentCommand>(c), actor, role, now);
            if (op == "book" && c.Kind == "Experiment") return w.Scheduling.BookExperiment(Convert<BookExperimentCommand>(c), actor, role, now);
            if (op == "book" && c.Kind == "Maintenance") return w.Scheduling.BookMaintenance(Convert<BookMaintenanceCommand>(c), actor, role, now);
            if (op == "save-draft" && c.Kind == "prescribe") return w.Clinical.SavePrescriptionDraft(Convert<SavePrescriptionDraftCommand>(c), actor, role, now);
            if (op == "save-draft" && c.Kind == "plan") return w.Planning.SavePlanDraft(Convert<SavePlanDraftCommand>(c), actor, role, now);
            if (op == "correction" && data.Plans.Any(x => x.Id == c.Id)) return w.Planning.CorrectPlan(Convert<CorrectPlanCommand>(c), actor, role, now);
            if (op == "correction" && data.Deliveries.Any(x => x.Id == c.Id)) return w.Scheduling.CorrectTreatmentDelivery(Convert<CorrectTreatmentDeliveryCommand>(c), actor, role, now);
            if (op == "correction" && data.Prescriptions.Any(x => x.Id == c.Id)) throw new BusinessException("revision_required", "处方更正请签署新处方版本", BusinessErrorKind.InvalidInput);
            throw new BusinessException("invalid_operation", "未知测试操作", BusinessErrorKind.InvalidInput);
        }
        public static Task<object> Execute(this CommandExecutor executor, string op, ScenarioCommand c, string actor, string role, string key, string correlation, CancellationToken ct)
        {
            if (op == "case") return executor.Execute(Convert<CreateCaseCommand>(c), actor, role, key, correlation, ct, (w, input, a, r, now) => w.Clinical.CreateCase(input, a, r, now));
            if (op == "terminate") return executor.Execute(Convert<TerminateCaseCommand>(c), actor, role, key, correlation, ct, (w, input, a, r, now) => w.Clinical.TerminateCase(input, a, r, now));
            if (op == "note") return executor.Execute(Convert<AddCaseNoteCommand>(c), actor, role, key, correlation, ct, (w, input, a, r, now) => w.Clinical.AddCaseNote(input, a, r, now));
            if (op == "prescribe") return executor.Execute(Convert<SignPrescriptionCommand>(c), actor, role, key, correlation, ct, (w, input, a, r, now) => w.Clinical.SignPrescription(input, a, r, now));
            if (op == "plan") return executor.Execute(Convert<RegisterPlanCommand>(c), actor, role, key, correlation, ct, (w, input, a, r, now) => w.Planning.RegisterPlan(input, a, r, now));
            if (op == "review") return executor.Execute(Convert<ReviewPlanCommand>(c), actor, role, key, correlation, ct, (w, input, a, r, now) => w.Planning.ReviewPlan(input, a, r, now));
            if (op == "reschedule") return executor.Execute(Convert<RescheduleReservationCommand>(c), actor, role, key, correlation, ct, (w, input, a, r, now) => w.Scheduling.RescheduleReservation(input, a, r, now));
            if (op == "restore") return executor.Execute(Convert<RestoreReservationCommand>(c), actor, role, key, correlation, ct, (w, input, a, r, now) => w.Scheduling.RestoreReservation(input, a, r, now));
            if (op == "cancel") return executor.Execute(Convert<CancelReservationCommand>(c), actor, role, key, correlation, ct, (w, input, a, r, now) => w.Scheduling.CancelReservation(input, a, r, now));
            if (op == "no-show") return executor.Execute(Convert<RecordNoShowCommand>(c), actor, role, key, correlation, ct, (w, input, a, r, now) => w.Scheduling.RecordNoShow(input, a, r, now));
            if (op == "validate") return executor.Execute(Convert<SubmitExperimentResultCommand>(c), actor, role, key, correlation, ct, (w, input, a, r, now) => w.Planning.SubmitExperimentResult(input, a, r, now));
            if (op == "consult") return executor.Execute(Convert<CompleteConsultationCommand>(c), actor, role, key, correlation, ct, (w, input, a, r, now) => w.Scheduling.CompleteConsultation(input, a, r, now));
            if (op == "start") return executor.Execute(Convert<StartTreatmentCommand>(c), actor, role, key, correlation, ct, (w, input, a, r, now) => w.Scheduling.StartTreatment(input, a, r, now));
            if (op == "finish") return executor.Execute(Convert<FinishTreatmentCommand>(c), actor, role, key, correlation, ct, (w, input, a, r, now) => w.Scheduling.FinishTreatment(input, a, r, now));
            if (op == "not-executed") return executor.Execute(Convert<RecordNotExecutedCommand>(c), actor, role, key, correlation, ct, (w, input, a, r, now) => w.Scheduling.RecordNotExecuted(input, a, r, now));
            if (op == "machine") return executor.Execute(Convert<SetMachineAvailabilityCommand>(c), actor, role, key, correlation, ct, (w, input, a, r, now) => w.Scheduling.SetMachineAvailability(input, a, r, now));
            if (op == "read-task") return executor.Execute(Convert<ReadTaskCommand>(c), actor, role, key, correlation, ct, (w, input, a, r, now) => w.Support.ReadTask(input, a, r, now));
            if (op == "resolve-task") return executor.Execute(Convert<ResolveTaskCommand>(c), actor, role, key, correlation, ct, (w, input, a, r, now) => w.Support.ResolveTask(input, a, r, now));
            if (op == "book" && c.Kind == "Consultation") return executor.Execute(Convert<BookConsultationCommand>(c), actor, role, key, correlation, ct, (w, input, a, r, now) => w.Scheduling.BookConsultation(input, a, r, now));
            if (op == "book" && c.Kind == "Treatment") return executor.Execute(Convert<BookTreatmentCommand>(c), actor, role, key, correlation, ct, (w, input, a, r, now) => w.Scheduling.BookTreatment(input, a, r, now));
            if (op == "book" && c.Kind == "Experiment") return executor.Execute(Convert<BookExperimentCommand>(c), actor, role, key, correlation, ct, (w, input, a, r, now) => w.Scheduling.BookExperiment(input, a, r, now));
            if (op == "book" && c.Kind == "Maintenance") return executor.Execute(Convert<BookMaintenanceCommand>(c), actor, role, key, correlation, ct, (w, input, a, r, now) => w.Scheduling.BookMaintenance(input, a, r, now));
            if (op == "save-draft" && c.Kind == "prescribe") return executor.Execute(Convert<SavePrescriptionDraftCommand>(c), actor, role, key, correlation, ct, (w, input, a, r, now) => w.Clinical.SavePrescriptionDraft(input, a, r, now));
            if (op == "save-draft" && c.Kind == "plan") return executor.Execute(Convert<SavePlanDraftCommand>(c), actor, role, key, correlation, ct, (w, input, a, r, now) => w.Planning.SavePlanDraft(input, a, r, now));
            throw new BusinessException("invalid_operation", "未知测试操作", BusinessErrorKind.InvalidInput);
        }
    }
}

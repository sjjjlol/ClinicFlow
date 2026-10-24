using System;
using System.Collections.Generic;
using System.Linq;

namespace ClinicFlow.Ois.Contracts
{
    public interface IClinicalContext
    {
        CaseSummary Case(string id, int? version = null);
        bool PatientExists(int id);
        bool HasClinical(string prescriptionId);
        void SelectPlan(string caseId, string? planId);
    }
    public interface IPlanningContext
    {
        PlanSummary Plan(string id);
        void EnsureCurrentPlan(string id);
        List<string> ReleaseBlockers(string caseId, string machineId);
        bool FailureResolved(string targetId);
    }
    public interface ISchedulingContext
    {
        void NoActiveTreatment(string caseId);
        void RequireMachine(string id);
        bool MachineAvailable(string id);
        ExperimentSlot Experiment(string id, int version, DateTime now);
        void CompleteExperiment(string id);
        void StopMachine(string id, string reason, DateTime now);
        void BlockedTasks(string caseId, string reason, DateTime now);
    }
    public sealed class CaseSummary
    {
        public string Id { get; }
        public int PatientId { get; }
        public bool Terminated { get; }
        public string? CurrentPrescriptionId { get; }
        public string? CurrentPlanId { get; }
        public CaseSummary(string id, int patientId, bool terminated, string? currentPrescriptionId, string? currentPlanId)
        {
            Id = id;
            PatientId = patientId;
            Terminated = terminated;
            CurrentPrescriptionId = currentPrescriptionId;
            CurrentPlanId = currentPlanId;
        }
    }
    public sealed class PlanSummary
    {
        public string Id { get; }
        public string CaseId { get; }
        public string MachineId { get; }
        public bool CanExperiment { get; }
        public string? ReviewedBy { get; }
        public PlanSummary(string id, string caseId, string machineId, bool canExperiment, string? reviewedBy)
        {
            Id = id;
            CaseId = caseId;
            MachineId = machineId;
            CanExperiment = canExperiment;
            ReviewedBy = reviewedBy;
        }
    }
    public sealed class ExperimentSlot
    {
        public string Id { get; }
        public string PlanId { get; }
        public string ResourceId { get; }
        public ExperimentSlot(string id, string planId, string resourceId)
        {
            Id = id;
            PlanId = planId;
            ResourceId = resourceId;
        }
    }

}

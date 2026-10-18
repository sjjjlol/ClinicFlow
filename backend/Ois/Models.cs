using System;
using Microsoft.EntityFrameworkCore;

namespace ClinicFlow.Ois
{
    public interface IOisEntity
    {
        string Id { get; set; }
        int Version { get; set; }
    }

    public class CareCase : IOisEntity
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public int Version { get; set; } = 1;
        public int PatientId { get; set; }
        public string Title { get; set; } = "";
        public bool Terminated { get; set; }
        public string TerminationReason { get; set; } = "";
        public string? CurrentPrescriptionId { get; set; }
        public string? CurrentPlanId { get; set; }
    }

    // Signed clinical facts are append-only; corrections reference the preceding record.
    public class Prescription : IOisEntity
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public int Version { get; set; } = 1;
        public string CaseId { get; set; } = "";
        public string? SupersedesId { get; set; }
        public string TreatmentSite { get; set; } = "";
        public decimal TotalDoseGy { get; set; }
        public decimal FractionDoseGy { get; set; }
        public int Fractions { get; set; }
        public string TreatmentMaterials { get; set; } = "";
        public string Diagnosis { get; set; } = "";
        public string Content { get; set; } = "";
        public string Reason { get; set; } = "";
        public string SignedBy { get; set; } = "";
        public DateTime SignedUtc { get; set; }
    }

    public class TreatmentPlan : IOisEntity
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public int Version { get; set; } = 1;
        public string CaseId { get; set; } = "";
        public string PrescriptionId { get; set; } = "";
        public int Revision { get; set; }
        public string VerificationMethod { get; set; } = "";
        public string Equipment { get; set; } = "";
        public string MeasurementConditions { get; set; } = "";
        public string ExpectedValues { get; set; } = "";
        public string StandardName { get; set; } = "";
        public string StandardVersion { get; set; } = "";
        public string AcceptanceCriteria { get; set; } = "";
        public string AllowedDeviation { get; set; } = "";
        public string Summary { get; set; } = "";
        public string Attachment { get; set; } = "";
        public string MachineId { get; set; } = "";
        public string RegisteredBy { get; set; } = "";
        public DateTime RegisteredUtc { get; set; }
        public string? ReviewedBy { get; set; }
        public DateTime? ReviewedUtc { get; set; }
        public string Validation { get; set; } = "Pending";
        public string? ValidatedBy { get; set; }
        public DateTime? ValidatedUtc { get; set; }
        public string ValidationNote { get; set; } = "";
        public string FailureType { get; set; } = "";
    }

    public class OisResource : IOisEntity
    {
        public string Id { get; set; } = "";
        public int Version { get; set; } = 1;
        public string Name { get; set; } = "";
        public string Kind { get; set; } = "Machine";
        public bool Available { get; set; } = true;
        public string Reason { get; set; } = "";
    }

    public class Reservation : IOisEntity
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public int Version { get; set; } = 1;
        public string MeasuredValues { get; set; } = "";
        public string ResultAttachment { get; set; } = "";
        public string ValidationOutcome { get; set; } = "";
        public string ValidationNote { get; set; } = "";
        public string FailureType { get; set; } = "";
        public string SignedBy { get; set; } = "";
        public DateTime? SignedUtc { get; set; }
        public string Kind { get; set; } = "Consultation";
        public int? PatientId { get; set; }
        public string? CaseId { get; set; }
        public string? PlanId { get; set; }
        public string ResourceId { get; set; } = "";
        public DateTime StartUtc { get; set; }
        public DateTime EndUtc { get; set; }
        public string Status { get; set; } = "Booked";
        public string Reason { get; set; } = "";
    }

    public class TreatmentDelivery : IOisEntity
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public int Version { get; set; } = 1;
        public string ReservationId { get; set; } = "";
        public string PlanId { get; set; } = "";
        public string MachineId { get; set; } = "";
        public string StartedBy { get; set; } = "";
        public DateTime StartedUtc { get; set; }
        public string Outcome { get; set; } = "InProgress";
        public string? FinishedBy { get; set; }
        public DateTime? FinishedUtc { get; set; }
        public string Note { get; set; } = "";
    }

    public class OisTask : IOisEntity
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public int Version { get; set; } = 1;
        public string TargetId { get; set; } = "";
        public string Role { get; set; } = "";
        public string Reason { get; set; } = "";
        public string? ReadBy { get; set; }
        public string? ResolvedBy { get; set; }
        public string Resolution { get; set; } = "";
        public DateTime CreatedUtc { get; set; }
        public DateTime? ResolvedUtc { get; set; }
    }

    public class OisDraft : IOisEntity
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public int Version { get; set; } = 1;
        public string CaseId { get; set; } = "";
        public string Actor { get; set; } = "";
        public string Operation { get; set; } = "";
        public string Payload { get; set; } = "";
        public int CaseVersion { get; set; }
    }

    public class OisAudit
    {
        public long Id { get; set; }
        public string Actor { get; set; } = "";
        public string Role { get; set; } = "";
        public string Action { get; set; } = "";
        public string TargetId { get; set; } = "";
        public string Detail { get; set; } = "";
        public string CorrelationId { get; set; } = "";
        public DateTime AtUtc { get; set; }
    }

    // Single-site demo writer gate. Database locking works across API processes.
    public class OisGate
    {
        public int Id { get; set; } = 1;
    }

    public static class OisModel
    {
        public static void Configure(ModelBuilder b)
        {
            Configure<CareCase>(b);
            Configure<OisDraft>(b);
            b.Entity<OisDraft>().HasIndex(x => new { x.CaseId, x.Actor, x.Operation }).IsUnique();
            Configure<Prescription>(b);
            Configure<TreatmentPlan>(b);
            Configure<OisResource>(b);
            Configure<Reservation>(b);
            Configure<TreatmentDelivery>(b);
            Configure<OisTask>(b);
            b.Entity<OisGate>().HasData(new OisGate());
            b.Entity<OisAudit>().HasIndex(x => new { x.TargetId, x.Id });
            b.Entity<CareCase>()
                .HasOne<Patient>()
                .WithMany()
                .HasForeignKey(x => x.PatientId)
                .OnDelete(DeleteBehavior.Restrict);
            b.Entity<Prescription>()
                .HasOne<CareCase>()
                .WithMany()
                .HasForeignKey(x => x.CaseId)
                .OnDelete(DeleteBehavior.Restrict);
            b.Entity<TreatmentPlan>()
                .HasOne<CareCase>()
                .WithMany()
                .HasForeignKey(x => x.CaseId)
                .OnDelete(DeleteBehavior.Restrict);
            b.Entity<TreatmentPlan>()
                .HasOne<Prescription>()
                .WithMany()
                .HasForeignKey(x => x.PrescriptionId)
                .OnDelete(DeleteBehavior.Restrict);
            b.Entity<TreatmentPlan>()
                .HasOne<OisResource>()
                .WithMany()
                .HasForeignKey(x => x.MachineId)
                .OnDelete(DeleteBehavior.Restrict);
            b.Entity<TreatmentPlan>().HasIndex(x => new { x.CaseId, x.Revision }).IsUnique();
            b.Entity<Reservation>()
                .HasOne<OisResource>()
                .WithMany()
                .HasForeignKey(x => x.ResourceId)
                .OnDelete(DeleteBehavior.Restrict);
            b.Entity<Reservation>()
                .HasOne<Patient>()
                .WithMany()
                .HasForeignKey(x => x.PatientId)
                .OnDelete(DeleteBehavior.Restrict);
            b.Entity<Reservation>()
                .HasOne<CareCase>()
                .WithMany()
                .HasForeignKey(x => x.CaseId)
                .OnDelete(DeleteBehavior.Restrict);
            b.Entity<Reservation>()
                .HasOne<TreatmentPlan>()
                .WithMany()
                .HasForeignKey(x => x.PlanId)
                .OnDelete(DeleteBehavior.Restrict);
            b.Entity<Reservation>().HasIndex(x => new { x.ResourceId, x.StartUtc });
            b.Entity<Reservation>().HasIndex(x => new { x.PatientId, x.StartUtc });
            b.Entity<TreatmentDelivery>()
                .HasOne<Reservation>()
                .WithMany()
                .HasForeignKey(x => x.ReservationId)
                .OnDelete(DeleteBehavior.Restrict);
            b.Entity<TreatmentDelivery>()
                .HasOne<TreatmentPlan>()
                .WithMany()
                .HasForeignKey(x => x.PlanId)
                .OnDelete(DeleteBehavior.Restrict);
            b.Entity<TreatmentDelivery>().HasIndex(x => x.ReservationId).IsUnique();
            b.Entity<OisResource>()
                .HasData(
                    new OisResource
                    {
                        Id = "clinician",
                        Name = "诊疗人员 · 门诊",
                        Kind = "Clinician",
                    },
                    new OisResource { Id = "linac-a", Name = "直线加速器 A（模拟）" },
                    new OisResource { Id = "linac-b", Name = "直线加速器 B（模拟）" }
                );
        }

        static void Configure<T>(ModelBuilder b)
            where T : class, IOisEntity
        {
            b.Entity<T>().HasKey(x => x.Id);
            b.Entity<T>().Property(x => x.Id).HasMaxLength(36);
            b.Entity<T>().Property(x => x.Version).IsConcurrencyToken();
        }
    }

    // Preserve property order and defaults: persisted idempotency fingerprints depend on this contract.
    public class OisCommand
    {
        public string Id { get; set; } = "";
        public int Version { get; set; }
        public int PatientId { get; set; }
        public string CaseId { get; set; } = "";
        public string PlanId { get; set; } = "";
        public string ResourceId { get; set; } = "";
        public string Kind { get; set; } = "";
        public DateTimeOffset? StartUtc { get; set; }
        public DateTimeOffset? EndUtc { get; set; }
        public string Text { get; set; } = "";
        public string Diagnosis { get; set; } = "";
        public string Reason { get; set; } = "";
        public string Attachment { get; set; } = "";
        public string Outcome { get; set; } = "";
        public string FailureType { get; set; } = "";
        public bool IdentityChecked { get; set; }
        public string TreatmentSite { get; set; } = "";
        public decimal TotalDoseGy { get; set; }
        public decimal FractionDoseGy { get; set; }
        public int Fractions { get; set; }
        public string TreatmentMaterials { get; set; } = "";
        public string VerificationMethod { get; set; } = "";
        public string Equipment { get; set; } = "";
        public string MeasurementConditions { get; set; } = "";
        public string ExpectedValues { get; set; } = "";
        public string StandardName { get; set; } = "";
        public string StandardVersion { get; set; } = "";
        public string AcceptanceCriteria { get; set; } = "";
        public string AllowedDeviation { get; set; } = "";
        public string MeasuredValues { get; set; } = "";
        public string ResultAttachment { get; set; } = "";
        public string DraftJson { get; set; } = "";
        public int DraftVersion { get; set; }
        public bool Available { get; set; } = true;
    }
}

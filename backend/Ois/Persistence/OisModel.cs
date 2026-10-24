using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace ClinicFlow.Ois
{
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
            b.Entity<PhysicalValidation>().HasKey(x => x.ReservationId);
            b.Entity<PhysicalValidation>().Property(x => x.ReservationId).HasMaxLength(36);
            b.Entity<PhysicalValidation>().HasOne<Reservation>().WithOne()
                .HasForeignKey<PhysicalValidation>(x => x.ReservationId).OnDelete(DeleteBehavior.Restrict);
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
}

using ClinicFlow.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace ClinicFlow.Imaging;

public class ImagingIdentity
{
    public int PatientId { get; set; }
    public string Source { get; set; } = "orthanc-demo";
    public string ExternalPatientId { get; set; } = "";
    public string Issuer { get; set; } = "";
}

public class ImagingLink
{
    public string AppointmentId { get; set; } = "";
    public string StudyInstanceUid { get; set; } = "";
    public string Source { get; set; } = "";
    public string Description { get; set; } = "";
    public string LinkedBy { get; set; } = "";
    public DateTime LinkedUtc { get; set; }
}

public class ImagingAudit
{
    public long Id { get; set; }
    public string AppointmentId { get; set; } = "";
    public string StudyInstanceUid { get; set; } = "";
    public string Action { get; set; } = "";
    public string Actor { get; set; } = "";
    public string CorrelationId { get; set; } = "";
    public DateTime AtUtc { get; set; }
}

public record LinkStudy(string StudyInstanceUid);

public record StudySummary(
    string StudyInstanceUid,
    string Description,
    string StudyDate,
    string Modalities
);

public static class ImagingModel
{
    public static void Configure(ModelBuilder b)
    {
        var identity = b.Entity<ImagingIdentity>();
        identity.HasKey(x => x.PatientId);
        identity.Property(x => x.Source).HasMaxLength(40);
        identity.Property(x => x.ExternalPatientId).HasMaxLength(64).UseCollation("utf8mb4_bin");
        identity.Property(x => x.Issuer).HasMaxLength(64).UseCollation("utf8mb4_bin");
        identity
            .HasIndex(x => new
            {
                x.Source,
                x.ExternalPatientId,
                x.Issuer,
            })
            .IsUnique();
        identity
            .HasOne<Patient>()
            .WithMany()
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Restrict);
        identity.HasData(
            new ImagingIdentity
            {
                PatientId = 1,
                ExternalPatientId = "CF-IMG-001",
                Issuer = "ClinicFlowDemo",
            },
            new ImagingIdentity
            {
                PatientId = 2,
                ExternalPatientId = "CF-IMG-002",
                Issuer = "ClinicFlowDemo",
            }
        );
        var link = b.Entity<ImagingLink>();
        link.HasKey(x => new { x.AppointmentId, x.StudyInstanceUid });
        link.Property(x => x.AppointmentId).HasMaxLength(36);
        link.Property(x => x.StudyInstanceUid).HasMaxLength(64);
        link.Property(x => x.Source).HasMaxLength(40);
        link.Property(x => x.Description).HasMaxLength(256);
        link.Property(x => x.LinkedBy).HasMaxLength(100);
        link.HasOne<Appointment>()
            .WithMany()
            .HasForeignKey(x => x.AppointmentId)
            .OnDelete(DeleteBehavior.Restrict);
        var audit = b.Entity<ImagingAudit>();
        audit.Property(x => x.AppointmentId).HasMaxLength(36);
        audit.Property(x => x.StudyInstanceUid).HasMaxLength(64);
        audit.HasIndex(x => new { x.AppointmentId, x.Id });
        audit
            .HasOne<Appointment>()
            .WithMany()
            .HasForeignKey(x => x.AppointmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

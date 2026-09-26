using System.Text.Json;
using ClinicFlow.Scheduling;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace ClinicFlow.Imaging;

public class ImagingService(ClinicDb db, DicomWebClient dicom)
{
    public async Task<ImagingIdentity> Identity(string appointmentId, CancellationToken ct)
    {
        var appointment =
            await db
                .Appointments.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == appointmentId, ct)
            ?? throw new BusinessException("not_found", "预约不存在", 404);
        var identity = await db.Set<ImagingIdentity>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.PatientId == appointment.PatientId, ct);
        if (
            identity is null
            || identity.Source != DicomWebClient.Source
            || string.IsNullOrWhiteSpace(identity.ExternalPatientId)
            || string.IsNullOrWhiteSpace(identity.Issuer)
        )
            throw new BusinessException(
                "imaging_identity_missing",
                "该患者尚未配置外部影像身份映射",
                409
            );
        return identity;
    }

    public async Task<JsonElement[]> LinkedMetadata(
        string appointmentId,
        string uid,
        CancellationToken ct
    )
    {
        DicomWebClient.ValidateUid(uid);
        var identity = await Identity(appointmentId, ct);
        if (
            !await db.Set<ImagingLink>()
                .AnyAsync(
                    x =>
                        x.AppointmentId == appointmentId
                        && x.StudyInstanceUid == uid
                        && x.Source == identity.Source,
                    ct
                )
        )
            throw new BusinessException("imaging_not_linked", "该影像尚未关联到此预约", 404);
        return await dicom.Verify(uid, identity, ct);
    }

    public async Task Link(
        string appointmentId,
        string uid,
        string actor,
        string correlation,
        CancellationToken ct
    )
    {
        DicomWebClient.ValidateUid(uid);
        var identity = await Identity(appointmentId, ct);
        var metadata = await dicom.Verify(uid, identity, ct);
        db.Add(
            new ImagingLink
            {
                AppointmentId = appointmentId,
                StudyInstanceUid = uid,
                Source = identity.Source,
                Description = DicomWebClient.Value(metadata[0], "00081030")[
                    ..Math.Min(256, DicomWebClient.Value(metadata[0], "00081030").Length)
                ],
                LinkedBy = actor,
                LinkedUtc = DateTime.UtcNow,
            }
        );
        Audit(appointmentId, uid, "Linked", actor, correlation);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is MySqlException { Number: 1062 })
        {
            throw new BusinessException(
                "imaging_duplicate",
                "该影像检查已经关联，无需重复添加",
                409
            );
        }
    }

    public async Task Remove(
        string appointmentId,
        string uid,
        string actor,
        string correlation,
        CancellationToken ct
    )
    {
        DicomWebClient.ValidateUid(uid);
        // Local unlink remains available when the PACS or mapping is unavailable.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var removed = await db.Set<ImagingLink>()
            .Where(x => x.AppointmentId == appointmentId && x.StudyInstanceUid == uid)
            .ExecuteDeleteAsync(ct);
        if (removed == 0)
            throw new BusinessException("imaging_not_linked", "影像关联不存在或已解除", 404);
        Audit(appointmentId, uid, "Unlinked", actor, correlation);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private void Audit(string id, string uid, string action, string actor, string correlation) =>
        db.Add(
            new ImagingAudit
            {
                AppointmentId = id,
                StudyInstanceUid = uid,
                Action = action,
                Actor = actor,
                CorrelationId = correlation,
                AtUtc = DateTime.UtcNow,
            }
        );
}

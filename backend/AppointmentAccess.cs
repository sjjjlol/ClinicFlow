using System.Security.Claims;
using ClinicFlow.Scheduling;

namespace ClinicFlow;

// Registered accounts have an immutable patient binding, issued only by the server.
public static class AppointmentAccess
{
    public static int? PatientScope(this ClaimsPrincipal user)
    {
        if (!user.IsInRole("Booker"))
            return null;
        if (!int.TryParse(user.FindFirstValue("patient_id"), out var id))
            throw new BusinessException("invalid_account", "账号档案不完整，请重新登录", 403);
        return id;
    }

    public static IQueryable<Appointment> VisibleTo(
        this IQueryable<Appointment> appointments,
        ClaimsPrincipal user
    )
    {
        var patient = user.PatientScope();
        return patient is null ? appointments : appointments.Where(x => x.PatientId == patient);
    }

    public static IQueryable<Patient> VisibleTo(
        this IQueryable<Patient> patients,
        ClaimsPrincipal user
    )
    {
        var patient = user.PatientScope();
        return patient is null ? patients : patients.Where(x => x.Id == patient);
    }
}

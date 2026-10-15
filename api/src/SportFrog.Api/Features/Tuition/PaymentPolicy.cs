using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Tuition;

/// <summary>
/// Whether a charge may have a new payment recorded against it.
/// </summary>
/// <remarks>
/// A pure check on a row already loaded — same shape as
/// <see cref="Courses.Attendance.AttendancePolicy.InspectSession"/>, which
/// is exactly what this plays the same role for: <see cref="RecordPayment"/>
/// asks it first, before anything about the payment itself is looked at.
/// </remarks>
internal sealed class PaymentPolicy
{
    /// <summary>
    /// Why this charge cannot take a new payment right now, or null when
    /// it can.
    /// </summary>
    public string? InspectCharge(TuitionCharge charge)
    {
        if (charge.VoidedAt is not null)
        {
            return "Este cargo está anulado, así que no corresponde registrarle pagos.";
        }

        if (charge.WaivedAt is not null)
        {
            return "Este cargo está condonado, así que no corresponde registrarle pagos.";
        }

        return null;
    }
}

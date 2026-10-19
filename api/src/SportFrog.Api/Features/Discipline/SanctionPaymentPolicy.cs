using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Domain.Discipline;

namespace SportFrog.Api.Features.Discipline;

/// <summary>
/// Whether a sanction may have a new payment recorded against it.
/// </summary>
/// <remarks>
/// A pure check on a row already loaded — same shape as
/// <c>Features.Tuition.PaymentPolicy</c>, which plays the same role for a
/// pension charge. Two more cases than that one has, because a sanction can
/// end in two ways a charge cannot: lifted on appeal, or carrying no fine
/// term at all.
/// </remarks>
internal sealed class SanctionPaymentPolicy
{
    /// <summary>
    /// Why this sanction cannot take a new payment right now, or null when
    /// it can.
    /// </summary>
    public string? InspectSanction(Sanction sanction)
    {
        if (sanction.VoidedAt is not null)
        {
            return "Esta sanción está anulada, así que no corresponde registrarle pagos.";
        }

        if (sanction.LiftedAt is not null)
        {
            return "Esta sanción fue levantada, así que no corresponde registrarle pagos.";
        }

        if (sanction.CompletionRule == SanctionCompletionRule.Matches)
        {
            return "Esta sanción no lleva multa, así que no corresponde registrarle pagos.";
        }

        if (sanction.FineWaivedAt is not null)
        {
            return "La multa de esta sanción ya fue condonada, así que no corresponde registrarle pagos.";
        }

        return null;
    }
}

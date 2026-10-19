namespace SportFrog.Api.Infrastructure.Persistence.Entities;

/// <summary>
/// A disciplinary sanction against one person, for one competition.
/// </summary>
/// <remarks>
/// The person is an <see cref="Athlete"/> row — the organization's table of
/// people, not only of competitors — because a sanction reaches a deportista
/// and a cuerpo técnico alike: a coach sent off is disciplined the same way a
/// player is, and <see cref="TeamStaff"/>'s own remarks are why both already
/// share that one table.
///
/// <see cref="TeamId"/> is carried on the row rather than read through a
/// roster entry or a staff registration, and that is deliberate: a sanction
/// outlives either. Somebody can be withdrawn from the team, or their roster
/// entry struck, long after a suspension imposed while they were on it is
/// still being served — <c>Features.Discipline.SanctionProgress</c> counts
/// matches this team plays from <see cref="ImposedAt"/> onward regardless of
/// whether the sanctioned person is still registered to any of them.
///
/// Status is never a column here, the same reasoning as
/// <see cref="TuitionCharge"/>: <c>SportFrog.Domain.Discipline.SanctionLedger</c>
/// resolves it fresh every time from this row's own terms against what has
/// actually been served, so an appeal that reopens a sanction already marked
/// "cumplida" cannot leave a stale answer lying around for nobody to update.
///
/// Lifting (an appeal upheld) and voiding (raised by mistake) are
/// independent toggles, the same shape <see cref="TuitionCharge.WaivedAt"/>
/// and <see cref="TuitionCharge.VoidedAt"/> already are relative to each
/// other: a sanction can be lifted and later discovered to have been raised
/// against the wrong person entirely, and <c>SanctionLedger</c> already
/// resolves that combination — voided outranks lifted — without this entity
/// needing to forbid it.
/// </remarks>
public sealed class Sanction
{
    public Guid Id { get; set; }

    public Guid OrgId { get; set; }

    public Guid CompetitionId { get; set; }

    public Guid AthleteId { get; set; }

    public Guid TeamId { get; set; }

    /// <summary>The match it arose from, if any. Free-standing sanctions — a disciplinary hearing with no single match behind it — carry none.</summary>
    public Guid? MatchId { get; set; }

    /// <summary>Why it was imposed. Always required: a sanction with no stated reason is not one anybody can defend or appeal.</summary>
    public required string Reason { get; set; }

    public SanctionCompletionRule CompletionRule { get; set; }

    /// <summary>How many matches clear it. Set exactly when <see cref="CompletionRule"/> is <see cref="SanctionCompletionRule.Matches"/> or <see cref="SanctionCompletionRule.Both"/>.</summary>
    public int? MatchesRequired { get; set; }

    /// <summary>What the fine asks for, in bolivianos. Set exactly when <see cref="CompletionRule"/> is <see cref="SanctionCompletionRule.Fine"/> or <see cref="SanctionCompletionRule.Both"/>.</summary>
    public decimal? FineAmount { get; set; }

    /// <summary>
    /// Whether the fine was forgiven. Independent of how much of it, if any,
    /// had already been paid — the same reasoning
    /// <see cref="TuitionCharge.WaivedAt"/> follows for a pension charge.
    /// </summary>
    public DateTimeOffset? FineWaivedAt { get; set; }
    public string? FineWaiverReason { get; set; }

    /// <summary>
    /// Whether the physical card is currently held back from the person it
    /// names.
    /// </summary>
    /// <remarks>
    /// Not a revocation: <see cref="IssuedDocument.Status"/> stays
    /// <c>issued</c>, because the card itself is not void — it is sitting in
    /// a drawer. See <c>Features.Discipline.WithholdCredential</c> for why
    /// retiring the sanction does not have to mean reprinting anything.
    /// </remarks>
    public DateTimeOffset? CredentialWithheldAt { get; set; }

    public Guid ImposedBy { get; set; }
    public DateTimeOffset ImposedAt { get; set; }

    /// <summary>Set when an appeal succeeds. The sanction was never owed, not merely satisfied.</summary>
    public DateTimeOffset? LiftedAt { get; set; }
    public string? LiftReason { get; set; }

    /// <summary>Set when the sanction itself should never have been raised.</summary>
    public DateTimeOffset? VoidedAt { get; set; }
    public string? VoidReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Athlete? Athlete { get; set; }
    public Team? Team { get; set; }
    public Competition? Competition { get; set; }
    public Match? Match { get; set; }
}

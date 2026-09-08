namespace SportFrog.Api.Infrastructure.Persistence.Entities;

/// <summary>
/// One team's slot in a category's classification stage: entered, and
/// whatever it scored.
/// </summary>
/// <remarks>
/// The row exists from the moment the classification stage opens, long
/// before anybody performs — the same reason almost everything about a
/// <see cref="Match"/>'s result is nullable. A performance with no score is
/// not incomplete data, it is a team still waiting its turn.
///
/// Deleting is logical, for the same reason as a match: a physical delete
/// would take a judged result out from under a ranking that has to keep
/// resolving.
/// </remarks>
public sealed class Performance
{
    public Guid Id { get; set; }

    public Guid OrgId { get; set; }

    /// <summary>
    /// Repeated from the category rather than read through it, same as
    /// <see cref="Match.CompetitionId"/>.
    /// </summary>
    public Guid CompetitionId { get; set; }

    public Guid CategoryId { get; set; }

    /// <summary>
    /// The competing unit — one athlete, a pair or a trio, exactly as
    /// <see cref="Sport.IsIndividual"/> already represents them. How many
    /// roster entries stand behind it is not this row's concern.
    /// </summary>
    public Guid TeamId { get; set; }

    public PerformanceStatus Status { get; set; } = PerformanceStatus.Pending;

    /// <summary>
    /// The judges' score, ×100 — see <c>PeriodScore</c>'s remarks on
    /// <c>Home</c>/<c>Away</c> for why. Null until the team has performed.
    /// </summary>
    public int? Score { get; set; }

    /// <summary>Who first recorded the score, and when. See <see cref="Match.RecordedBy"/>.</summary>
    public Guid? RecordedBy { get; set; }

    public DateTimeOffset? RecordedAt { get; set; }

    public Guid? ModifiedBy { get; set; }

    public DateTimeOffset? ModifiedAt { get; set; }

    public string? Notes { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public Competition? Competition { get; set; }
    public Category? Category { get; set; }
    public Team? Team { get; set; }
}

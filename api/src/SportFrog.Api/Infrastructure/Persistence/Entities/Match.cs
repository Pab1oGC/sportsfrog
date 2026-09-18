namespace SportFrog.Api.Infrastructure.Persistence.Entities;

/// <summary>
/// One fixture: two teams of a category, at a time and a place, and whatever
/// became of it.
///
/// The row exists from the moment the calendar is drawn, long before anyone
/// plays — which is why almost everything about the result is nullable. A
/// fixture with no score is not incomplete data, it is next Sunday.
/// </summary>
/// <remarks>
/// Deleting is logical. Player events reference the match with ON DELETE
/// CASCADE, so a physical delete would silently take the statistics with it,
/// and the fixture is named by a standings table that has to keep resolving.
/// </remarks>
public sealed class Match
{
    public Guid Id { get; set; }

    public Guid OrgId { get; set; }

    /// <summary>
    /// Repeated from the category rather than read through it, because the
    /// schema puts a foreign key on it and every listing of a competition's
    /// fixtures asks for it directly.
    /// </summary>
    public Guid CompetitionId { get; set; }

    public Guid CategoryId { get; set; }

    /// <summary>
    /// The side listed first. In sports where it means anything it is the
    /// home team; where it does not, it is still the side written on the left
    /// of the scoreline, and the score columns follow it.
    /// </summary>
    /// <remarks>
    /// Null exactly while <see cref="HomeSourceMatchId"/> is set: a knockout
    /// drawn in full books every round's date and venue before a ball is
    /// kicked, and a later round's side is not a team yet, it is "whoever
    /// wins that other match". <c>ck_home_slot_defined</c> is what refuses a
    /// row naming neither.
    /// </remarks>
    public Guid? HomeTeamId { get; set; }

    /// <summary>See <see cref="HomeTeamId"/>; the same story, the other side.</summary>
    public Guid? AwayTeamId { get; set; }

    /// <summary>
    /// The match whose winner fills <see cref="HomeTeamId"/>. Set once, at
    /// the draw, and never cleared — once that match is played, the winner
    /// is copied into <see cref="HomeTeamId"/> alongside it rather than in
    /// its place, since this is also how a bracket draws the lines
    /// connecting one round to the next.
    /// </summary>
    public Guid? HomeSourceMatchId { get; set; }

    /// <summary>See <see cref="HomeSourceMatchId"/>; the same story, the other side.</summary>
    public Guid? AwaySourceMatchId { get; set; }

    /// <summary>
    /// Where it is played. Null while the calendar has a time but not yet a
    /// pitch, which is the ordinary state of a fixture the day it is drawn.
    /// </summary>
    public Guid? VenueSpaceId { get; set; }

    /// <summary>Which round of the competition. Null in a format without rounds.</summary>
    public short? RoundNumber { get; set; }

    /// <summary>
    /// Which stage: "grupos", "cuartos", "final". Free text because the
    /// stages of a knockout are named differently by every organizer and
    /// nothing computes from it.
    /// </summary>
    public string? Phase { get; set; }

    /// <summary>
    /// When it is played. Null for a fixture that exists but has no date yet
    /// — a draw is often made before the calendar is.
    /// </summary>
    public DateTimeOffset? ScheduledAt { get; set; }

    public MatchState Status { get; set; } = MatchState.Scheduled;

    /// <summary>
    /// The side awarded a walkover. Set when, and only when, the status says
    /// so — the schema enforces that pairing, so neither can be changed
    /// without the other.
    /// </summary>
    public Guid? WalkoverTeamId { get; set; }

    /// <summary>The score period by period, once there is one.</summary>
    public IReadOnlyList<PeriodScore>? PeriodScores { get; set; }

    /// <summary>
    /// The match score, consolidated according to the sport.
    /// </summary>
    /// <remarks>
    /// Not always the sum of the periods. Under a cumulative sport it is;
    /// under a sport played in sets these hold sets won, so a volleyball match
    /// finishing 3-1 stores 3 and 1 here while the periods hold the points of
    /// each set.
    /// </remarks>
    public int? HomeTotal { get; set; }

    public int? AwayTotal { get; set; }

    /// <summary>
    /// The shootout that broke a level knockout match, once it needed one.
    /// </summary>
    /// <remarks>
    /// Beside the match score rather than inside it: the 90 minutes stayed a
    /// draw, and a table that reads goals scored must not learn to read a
    /// shootout as more of them. Null for every match that never needed one —
    /// which is most of them, since a league match is allowed to end level
    /// and only a knockout tie has to produce somebody.
    /// </remarks>
    public short? PenaltyHomeScore { get; set; }

    public short? PenaltyAwayScore { get; set; }

    /// <summary>
    /// Who first recorded the result, and when.
    /// </summary>
    /// <remarks>
    /// Business data rather than a technical trace, which is why it is on the
    /// row and not left to the audit log: "who loaded this result" is a
    /// question a competition asks out loud when a score is disputed, and it
    /// has to be answerable without reading an audit trail.
    /// </remarks>
    public Guid? RecordedBy { get; set; }

    public DateTimeOffset? RecordedAt { get; set; }

    /// <summary>Who corrected it afterwards, if anyone. Kept apart from the original author.</summary>
    public Guid? ModifiedBy { get; set; }

    public DateTimeOffset? ModifiedAt { get; set; }

    /// <summary>Anything the referee or the organizer needs written down.</summary>
    public string? Notes { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public Competition? Competition { get; set; }
    public Category? Category { get; set; }
    public Team? HomeTeam { get; set; }
    public Team? AwayTeam { get; set; }
    public Match? HomeSourceMatch { get; set; }
    public Match? AwaySourceMatch { get; set; }
    public VenueSpace? VenueSpace { get; set; }

    /// <summary>
    /// Whether this fixture already names two real teams, rather than
    /// waiting on one or both of <see cref="HomeSourceMatchId"/>/
    /// <see cref="AwaySourceMatchId"/> to be played.
    /// </summary>
    /// <remarks>
    /// What every operation that needs to know who is actually playing —
    /// starting the match, recording its result, awarding it — checks before
    /// touching a fixture the bracket has not resolved yet.
    /// </remarks>
    public bool HasBothTeams => HomeTeamId is not null && AwayTeamId is not null;
}

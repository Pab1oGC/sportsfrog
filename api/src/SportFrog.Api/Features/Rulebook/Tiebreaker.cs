namespace SportFrog.Api.Features.Rulebook;

/// <summary>
/// The criteria a standings table may be broken by.
/// </summary>
/// <remarks>
/// A closed set, because each name here has to correspond to something the
/// standings code knows how to compute. Accepting an arbitrary string would
/// let an organization save a ruleset that reads sensibly and cannot be
/// applied, and the failure would surface as a wrong table weeks later rather
/// than as a refusal at the point of writing it.
///
/// Adding one is a change to this list and to whatever computes the table,
/// together.
/// </remarks>
internal static class Tiebreaker
{
    /// <summary>The result between the teams that are level.</summary>
    public const string HeadToHead = "head_to_head";

    /// <summary>Scored minus conceded. Sets, in a sport played in sets.</summary>
    public const string ScoreDifference = "score_difference";

    public const string ScoreFor = "score_for";

    public const string ScoreAgainst = "score_against";

    /// <summary>Matches won, for tables where a win outranks a better margin.</summary>
    public const string Wins = "wins";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        HeadToHead,
        ScoreDifference,
        ScoreFor,
        ScoreAgainst,
        Wins,
    };
}

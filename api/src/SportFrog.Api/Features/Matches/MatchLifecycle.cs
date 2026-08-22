using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Matches;

/// <summary>
/// Which states a fixture may move to from where it is.
/// </summary>
/// <remarks>
/// Written as a table for the same reason the competition's is: the
/// interesting question is which moves do not exist, and a chain of
/// conditions hides exactly that.
///
/// Two of the six are not reached through here at all. A match becomes
/// <see cref="MatchState.Finished"/> by having a result recorded, and
/// <see cref="MatchState.Walkover"/> by having one awarded — both need
/// something more than a state name, and the schema refuses either without
/// it. Letting them be set as a bare transition would produce a finished
/// match with no score, which <c>ck_result_completeness</c> exists to stop.
/// </remarks>
internal static class MatchLifecycle
{
    private static readonly Dictionary<MatchState, MatchState[]> Allowed = new()
    {
        [MatchState.Scheduled] =
        [
            MatchState.InProgress,
            MatchState.Postponed,
            MatchState.Cancelled,
        ],

        // Back to scheduled is for the whistle that was blown by mistake, or
        // the tap on the wrong fixture. A match abandoned after starting is
        // postponed or cancelled, which are the two ways of saying it will or
        // will not be replayed.
        [MatchState.InProgress] =
        [
            MatchState.Scheduled,
            MatchState.Postponed,
            MatchState.Cancelled,
        ],

        // A postponement owes a result, so it goes back on the calendar. It
        // can also be given up on.
        [MatchState.Postponed] =
        [
            MatchState.Scheduled,
            MatchState.InProgress,
            MatchState.Cancelled,
        ],

        // Reopening a played match is done by correcting its result, not by
        // moving it: the score is what makes it finished, so the score is what
        // has to change. Cancelling one is refused here for the same reason —
        // it would leave totals behind on a match that supposedly never
        // counted.
        [MatchState.Finished] = [],

        [MatchState.Walkover] = [],

        // A cancelled fixture can be put back on the calendar: calling a match
        // off and then finding a date for it after all is ordinary, and the
        // alternative is drawing it again from scratch.
        [MatchState.Cancelled] =
        [
            MatchState.Scheduled,
        ],
    };

    public static bool CanMove(MatchState from, MatchState to) =>
        from != to && Allowed[from].Contains(to);

    public static IReadOnlyCollection<MatchState> Destinations(MatchState from) => Allowed[from];
}

using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Competitions;

/// <summary>
/// Which states a competition may move to from where it is.
/// </summary>
/// <remarks>
/// Written as a table rather than as a chain of conditions in the endpoint,
/// because the interesting question about a state machine is which edges do
/// not exist, and that is invisible in a chain of ifs. Here the absence of an
/// edge is something you can look at.
///
/// The graph only says which moves are shaped correctly. Whether a particular
/// move is allowed right now also depends on what has been played, and that
/// is asked separately — see <see cref="CompetitionActivity"/>.
/// </remarks>
internal static class CompetitionLifecycle
{
    private static readonly Dictionary<CompetitionState, CompetitionState[]> Allowed = new()
    {
        // Nothing has been announced yet, so the only ways out are forward
        // into a published calendar or out of the product entirely.
        [CompetitionState.Draft] =
        [
            CompetitionState.Scheduled,
            CompetitionState.Cancelled,
        ],

        // Back to draft is deliberate: fixtures get redrawn before a ball is
        // kicked more often than anyone would like, and the alternative is
        // organizers cancelling and rebuilding the whole competition.
        [CompetitionState.Scheduled] =
        [
            CompetitionState.Draft,
            CompetitionState.InProgress,
            CompetitionState.Cancelled,
        ],

        [CompetitionState.InProgress] =
        [
            CompetitionState.Finished,
            CompetitionState.Cancelled,
        ],

        // Reopening is allowed, and it is the one edge worth arguing about.
        // Closing a competition by mistake is easy and common; leaving that
        // unrecoverable would mean a wrong result stands forever because
        // somebody clicked one button early. Nothing is destroyed by closing,
        // so nothing has to be rebuilt by reopening.
        [CompetitionState.Finished] =
        [
            CompetitionState.InProgress,
        ],

        // Terminal. Cancelling is a decision to abandon the event, and an
        // event that resumes was not abandoned — it is a new one, set up
        // again. Keeping this closed is what makes the record of a
        // cancellation mean something.
        [CompetitionState.Cancelled] = [],
    };

    public static bool CanMove(CompetitionState from, CompetitionState to) =>
        from != to && Allowed[from].Contains(to);

    /// <summary>
    /// Where it could go instead, for the refusal message.
    /// </summary>
    public static IReadOnlyCollection<CompetitionState> Destinations(CompetitionState from) =>
        Allowed[from];
}

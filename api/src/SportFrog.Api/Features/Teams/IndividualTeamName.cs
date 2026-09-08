namespace SportFrog.Api.Features.Teams;

/// <summary>
/// What an individual-sport team is called, derived from who is on it.
/// </summary>
/// <remarks>
/// A squad's name comes from its club, chosen once and left alone as its
/// roster changes underneath it — <see cref="CreateTeam"/> never revisits
/// it. A team built for an individual sport has no such name to fall back
/// on: it is one athlete, a pair or a trio, and it exists only because
/// these particular people do. Poomsae runs all three under
/// <c>sports.is_individual</c> — one athlete performs alone, two as a pair,
/// three as a team — so the join has to read naturally for any of them.
/// </remarks>
internal static class IndividualTeamName
{
    public static string From(IEnumerable<(string FirstName, string LastName)> members) =>
        string.Join(" / ", members.Select(member => $"{member.FirstName} {member.LastName}"));
}

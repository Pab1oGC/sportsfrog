using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Infrastructure.Auth;

/// <summary>
/// How much each role may do, relative to the others.
///
/// The four roles RF-02 names form a line: someone who records results can
/// read them, someone who runs a competition can record its results, and an
/// administrator can do both. <c>owner</c> sits above administrator as the
/// account that created the organization.
/// </summary>
public static class MembershipRoleAuthority
{
    /// <summary>
    /// Stated here rather than taken from the order the enum happens to
    /// declare. Reordering an enum is a harmless-looking edit; if permissions
    /// were derived from it, that edit would silently redistribute them.
    /// </summary>
    private static readonly Dictionary<MembershipRole, int> Ranks = new()
    {
        [MembershipRole.Viewer] = 0,
        [MembershipRole.Recorder] = 1,
        [MembershipRole.Operator] = 2,
        [MembershipRole.Admin] = 3,
        [MembershipRole.Owner] = 4,
    };

    /// <summary>
    /// Whether a role held reaches what an operation asks for.
    /// </summary>
    public static bool Reaches(this MembershipRole held, MembershipRole required) =>
        Rank(held) >= Rank(required);

    private static int Rank(MembershipRole role) =>
        Ranks.TryGetValue(role, out var rank)
            ? rank
            // A role added to the enum without a place in this line has no
            // defined authority. Refusing to guess is the only safe answer:
            // guessing high grants what nobody decided to grant.
            : throw new InvalidOperationException(
                $"Role '{role}' has no defined authority. Add it to {nameof(MembershipRoleAuthority)}.");
}

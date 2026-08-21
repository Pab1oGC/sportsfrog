using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Competitions;

/// <summary>
/// Decides whether a competition has a public page at all.
/// </summary>
/// <remarks>
/// Kept apart from the state, because the two answer different questions. The
/// state says how far along the event is; this says whether anyone outside
/// the organization can see it. A league can run its whole season privately
/// and publish the standings at the end, or announce the fixtures the day
/// they are drawn — both are ordinary, and neither is implied by the other.
///
/// What a visitor then sees on that page is a third question, answered by the
/// competition's settings.
///
/// An administrator's decision rather than an operator's: this is the switch
/// that takes an organization's data outside it, and RNF-16 is specific about
/// that being deliberate where minors play.
/// </remarks>
public static class PublishCompetition
{
    public sealed record Request(bool IsPublic);

    public static IEndpointRouteBuilder MapPublishCompetition(this IEndpointRouteBuilder routes)
    {
        routes.MapPut("/competitions/{id:guid}/publication", HandleAsync)
            .RequireRole(MembershipRole.Admin)
            .WithName(nameof(PublishCompetition))
            .WithSummary("Publishes a competition, or withdraws it from public view.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid id,
        Request request,
        SportFrogDbContext database,
        CancellationToken cancellationToken)
    {
        var competition = await database.Competitions.SingleOrDefaultAsync(
            candidate => candidate.Id == id, cancellationToken);

        if (competition is null)
        {
            return Results.NotFound();
        }

        if (request.IsPublic && competition.Status == CompetitionState.Draft)
        {
            // A draft has no fixtures and possibly no categories: the page
            // would resolve and show nothing, which is worse than not
            // resolving. Announce it first.
            return Results.Problem(
                detail: "A competition still being set up cannot be published: there would be " +
                        "nothing on the page. Move it to scheduled first.",
                statusCode: StatusCodes.Status409Conflict);
        }

        // Unpublishing is never refused. Whatever the reason — a wrong result
        // on display, a parent asking for a child's name to come down — the
        // answer has to be available immediately and unconditionally.
        competition.IsPublic = request.IsPublic;

        await database.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }
}

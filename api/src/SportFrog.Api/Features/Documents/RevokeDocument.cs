using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Documents;

/// <summary>
/// Withdraws a document that has already been printed and handed over.
/// </summary>
/// <remarks>
/// The card is out in the world and cannot be recalled, so this does not
/// delete anything — deleting the row would make the serial answer "no such
/// document", which is the one answer it must never give about a card
/// somebody is still carrying. It stays, and it answers "revoked". That is the
/// whole point of the record.
///
/// It is also how a credential is reissued. Somebody who lost their card is
/// revoked and then included in the next batch: the batch skips whoever
/// already holds a valid document, and a revoked one is not valid. There is no
/// separate reissue operation because there does not need to be one.
/// </remarks>
public static class RevokeDocument
{
    public sealed record Request(string Reason);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator() =>
            RuleFor(request => request.Reason)
                .NotEmpty().WithMessage("Hay que decir por qué se revoca.")
                .MaximumLength(300);
    }

    public static IEndpointRouteBuilder MapRevokeDocument(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/documents/issued/{id:guid}/revoke", HandleAsync)
            .RequireRole(MembershipRole.Operator)
            .WithName(nameof(RevokeDocument))
            .WithSummary("Withdraws an issued document, keeping the record that it existed.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid id,
        Request request,
        SportFrogDbContext database,
        OrganizationContext organization,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var document = await database.IssuedDocuments
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (document is null)
        {
            return Results.NotFound();
        }

        if (document.Status == DocumentState.Revoked)
        {
            // Revoking twice would move the date and overwrite the reason,
            // which is rewriting why something happened rather than recording
            // that it did.
            return Results.Problem(
                detail: "Ese documento ya está revocado.",
                statusCode: StatusCodes.Status409Conflict);
        }

        document.Status = DocumentState.Revoked;
        document.RevokedAt = clock.GetUtcNow();
        document.RevokedBy = organization.UserId;

        // Kept, and kept internal. Public verification says a card is no longer
        // valid and never says why: the reason is about a person, and the
        // answer to "is this card good" does not need it.
        document.RevocationReason = request.Reason.Trim();

        await database.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }
}

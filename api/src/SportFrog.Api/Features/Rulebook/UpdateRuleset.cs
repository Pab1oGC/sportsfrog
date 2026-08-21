using Microsoft.EntityFrameworkCore;
using Npgsql;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Rulebook;

/// <summary>
/// Corrects a ruleset of the active organization.
/// </summary>
public static class UpdateRuleset
{
    public static IEndpointRouteBuilder MapUpdateRuleset(this IEndpointRouteBuilder routes)
    {
        routes.MapPut("/rulesets/{id:guid}", HandleAsync)
            .RequireRole(MembershipRole.Admin)
            .WithName(nameof(UpdateRuleset))
            .WithSummary("Corrects a ruleset.");

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        Guid id,
        RulesetContract contract,
        SportFrogDbContext database,
        RulesetUsage usage,
        CancellationToken cancellationToken)
    {
        var ruleset = await database.Rulesets.SingleOrDefaultAsync(
            candidate => candidate.Id == id, cancellationToken);

        if (ruleset is null)
        {
            return Results.NotFound();
        }

        if (contract.SportCode != ruleset.SportCode)
        {
            // Not a correction but a different ruleset. Everything in the
            // configuration — the outcomes priced, the metrics named, whether
            // the periods have to be odd — was written against one sport, and
            // moving it to another would leave a document that validated
            // against rules nobody is playing by.
            return Results.Problem(
                detail: $"This ruleset is written for {ruleset.SportCode} and stays with it. " +
                        "Register a separate ruleset for another sport.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var name = contract.Name.Trim();

        if (await database.Rulesets.AnyAsync(
                other => other.Id != id && other.Name == name, cancellationToken))
        {
            return Results.Problem(
                detail: "A ruleset with that name already exists.",
                statusCode: StatusCodes.Status409Conflict);
        }

        ruleset.Name = name;
        ruleset.Config = contract.Config;

        // Whether the rules themselves changed, as opposed to only the name.
        // Asked of the change tracker rather than by comparing the documents
        // here, because the tracker already knows: the value comparer on the
        // jsonb column defines what makes two configurations the same, and a
        // second opinion written at this call site would eventually disagree
        // with it.
        var rulesChanged = database.Entry(ruleset).Property(x => x.Config).IsModified;

        if (rulesChanged && await usage.IsInUseAsync(id, cancellationToken))
        {
            // Renaming stays open — a ruleset called "Liga mayor" that should
            // read "Liga mayor 2026" is a label, and labels are safe to fix
            // mid-season. The rules are not: results already recorded were
            // scored, ranked and published under what this document said, and
            // rewriting it now would restate them without anyone touching a
            // match.
            //
            // Nothing is saved, and nothing has to be undone by hand: the
            // entry channel rolls the transaction back on a refusal, so the
            // assignments above never reach the row.
            return Results.Problem(
                detail: "This ruleset is already being played under, so its rules cannot be " +
                        "rewritten: results recorded under them would change without anyone " +
                        "editing a match. Register a new ruleset for the next competition. " +
                        "Renaming this one is still allowed.",
                statusCode: StatusCodes.Status409Conflict);
        }

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
                  { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return Results.Problem(
                detail: "A ruleset with that name already exists.",
                statusCode: StatusCodes.Status409Conflict);
        }

        return Results.NoContent();
    }
}

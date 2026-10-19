using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Discipline;

/// <summary>
/// Imposes a disciplinary sanction on a person, for a competition.
/// </summary>
/// <remarks>
/// The person is an <see cref="Athlete"/> row — deportista or cuerpo técnico
/// alike, see <see cref="Sanction"/>'s own remarks — and the team names which
/// side's matches clear a suspension counted in matches. Neither has to be
/// who the person currently plays for or works for: a sanction imposed while
/// they were on a team keeps naming that team even after they leave it, the
/// same way an already-printed credential keeps naming it.
/// </remarks>
public static class ImposeSanction
{
    public sealed record Request(
        Guid AthleteId,
        Guid TeamId,
        Guid? MatchId,
        string Reason,
        SanctionCompletionRule CompletionRule,
        int? MatchesRequired,
        decimal? FineAmount);

    public sealed record Response(Guid Id);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.AthleteId)
                .NotEmpty().WithMessage("La persona sancionada es obligatoria.");

            RuleFor(request => request.TeamId)
                .NotEmpty().WithMessage("El equipo es obligatorio.");

            RuleFor(request => request.Reason)
                .NotEmpty().WithMessage("Hay que decir el motivo de la sanción.")
                .MaximumLength(500);

            RuleFor(request => request.CompletionRule)
                .IsInEnum().WithMessage("La forma de cumplir la sanción no es válida.");

            RuleFor(request => request.MatchesRequired)
                .GreaterThan(0).WithMessage("La cantidad de partidos tiene que ser mayor a cero.")
                .When(request => request.MatchesRequired.HasValue);

            // Mirrors ck_sanctions_matches_required_paired.
            RuleFor(request => request)
                .Must(request => (request.CompletionRule is SanctionCompletionRule.Matches
                        or SanctionCompletionRule.Both)
                    == request.MatchesRequired.HasValue)
                .WithMessage("La cantidad de partidos es obligatoria cuando la sanción se cumple " +
                             "con partidos, y no corresponde cuando no.");

            RuleFor(request => request.FineAmount)
                .GreaterThan(0).WithMessage("La multa tiene que ser mayor a cero.")
                .When(request => request.FineAmount.HasValue);

            // Mirrors ck_sanctions_fine_amount_paired.
            RuleFor(request => request)
                .Must(request => (request.CompletionRule is SanctionCompletionRule.Fine
                        or SanctionCompletionRule.Both)
                    == request.FineAmount.HasValue)
                .WithMessage("El monto de la multa es obligatorio cuando la sanción se cumple " +
                             "con una multa, y no corresponde cuando no.");
        }
    }

    public static IEndpointRouteBuilder MapImposeSanction(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/competitions/{competitionId:guid}/sanctions", HandleAsync)
            .RequireRole(MembershipRole.Operator)
            .RequireScope(new ScopeRequirement(ScopeType.Competition, ScopeTargetKind.Competition, "competitionId"))
            .WithName(nameof(ImposeSanction))
            .WithSummary("Imposes a disciplinary sanction on a person, for a competition.");

        return routes;
    }

    internal static async Task<IResult> HandleAsync(
        Guid competitionId,
        Request request,
        SportFrogDbContext database,
        OrganizationContext organization,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        if (!await database.Competitions.AnyAsync(
                candidate => candidate.Id == competitionId, cancellationToken))
        {
            return Results.NotFound();
        }

        if (!await database.Athletes.AnyAsync(
                candidate => candidate.Id == request.AthleteId, cancellationToken))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["AthleteId"] = ["Ninguna persona de esta organización tiene ese identificador."],
            });
        }

        // The team has to belong to this competition — the same join
        // ScopeResolver uses to answer the same question for a scoped
        // membership, asked here instead for the request body's own sake.
        var teamCompetitionId = await database.Teams
            .Where(team => team.Id == request.TeamId)
            .Select(team => (Guid?)team.Category!.CompetitionId)
            .SingleOrDefaultAsync(cancellationToken);

        if (teamCompetitionId != competitionId)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["TeamId"] = ["Ese equipo no pertenece a esta competencia."],
            });
        }

        if (request.MatchId is { } matchId
            && !await database.Matches.AnyAsync(
                candidate => candidate.Id == matchId && candidate.CompetitionId == competitionId,
                cancellationToken))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["MatchId"] = ["Ese partido no pertenece a esta competencia."],
            });
        }

        var sanction = new Sanction
        {
            Id = Guid.NewGuid(),
            OrgId = organization.RequireOrganizationId(),
            CompetitionId = competitionId,
            AthleteId = request.AthleteId,
            TeamId = request.TeamId,
            MatchId = request.MatchId,
            Reason = request.Reason.Trim(),
            CompletionRule = request.CompletionRule,
            MatchesRequired = request.MatchesRequired,
            FineAmount = request.FineAmount,
            ImposedBy = organization.UserId ?? Guid.Empty,
            ImposedAt = clock.GetUtcNow(),
        };

        database.Sanctions.Add(sanction);

        await database.SaveChangesAsync(cancellationToken);

        return Results.Created($"/competitions/{competitionId}/sanctions/{sanction.Id}", new Response(sanction.Id));
    }
}

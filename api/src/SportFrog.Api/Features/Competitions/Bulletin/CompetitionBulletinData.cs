using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Storage;
using SportFrog.Domain.Rules;

namespace SportFrog.Api.Features.Competitions.Bulletin;

/// <summary>One division of the competition, as the bulletin explains it.</summary>
internal sealed record BulletinCategory(
    string Name,
    string? Gender,
    string? AgeRange,
    string? WeightRange,
    short? MaxRosterSize,
    string PeriodsSummary,
    IReadOnlyList<string> Outcomes,
    IReadOnlyList<string> Tiebreakers,
    bool IsJudged);

/// <summary>Everything the bulletin says, gathered once before either renderer draws it.</summary>
/// <param name="LogoBytes">
/// The competition's own mark, already read from storage — null when it
/// never set one, in which case both renderers simply leave it out rather
/// than drawing a placeholder for a picture nobody uploaded.
/// </param>
/// <param name="AccentColor">
/// The competition's resolved theme colour — see
/// <see cref="CompetitionBranding"/>. Both renderers read this, the same way
/// and with the same fallback, so a bulletin's Word copy carries the same
/// colour as its PDF instead of the plain blue every competition used to get.
/// </param>
internal sealed record BulletinData(
    string OrganizationName,
    string CompetitionName,
    string SportName,
    string Season,
    string FormatLabel,
    DateOnly? StartsOn,
    DateOnly? EndsOn,
    IReadOnlyList<BulletinCategory> Categories,
    string? Introduction,
    string? Sanctions,
    string? GeneralProvisions,
    string? ContactInfo,
    byte[]? LogoBytes,
    string? AccentColor);

/// <summary>
/// Composes a competition's bulletin from what the schema already knows —
/// its categories and the rules each one plays under — plus the organizer's
/// own prose from <see cref="Domain.Competitions.BulletinSettings"/>.
/// </summary>
/// <remarks>
/// Shared by both renderers: the PDF and the Word document say the same
/// thing because they are drawn from the same gathered data rather than each
/// querying for itself, the same reason <c>PerformancesQuery</c> is shared
/// by every reader of a classification stage.
/// </remarks>
internal static class CompetitionBulletinData
{
    public static async Task<BulletinData?> ReadAsync(
        SportFrogDbContext database, ObjectStore store, Guid competitionId, CancellationToken cancellationToken)
    {
        var competition = await database.Competitions
            .AsNoTracking()
            .Include(candidate => candidate.Sport)
            .Include(candidate => candidate.Ruleset)
            .SingleOrDefaultAsync(candidate => candidate.Id == competitionId, cancellationToken);

        if (competition?.Sport is not { } sport || competition.Ruleset is not { } defaultRuleset)
        {
            return null;
        }

        var organizationName = await database.Organizations
            .AsNoTracking()
            .Where(candidate => candidate.Id == competition.OrgId)
            .Select(candidate => candidate.Name)
            .SingleAsync(cancellationToken);

        var categories = await database.Categories
            .AsNoTracking()
            .Where(candidate => candidate.CompetitionId == competitionId)
            .OrderBy(candidate => candidate.DisplayOrder)
            .Include(candidate => candidate.Ruleset)
            .ToListAsync(cancellationToken);

        var bulletin = competition.Settings.Bulletin;

        var branding = await CompetitionBranding.FromAsync(store, competition, cancellationToken);

        return new BulletinData(
            organizationName,
            competition.Name,
            sport.Name,
            competition.Season,
            FormatLabel(competition.Format),
            competition.StartsOn,
            competition.EndsOn,
            [.. categories.Select(category => Compose(category, category.Ruleset ?? defaultRuleset, sport))],
            bulletin?.Introduction,
            bulletin?.Sanctions,
            bulletin?.GeneralProvisions,
            bulletin?.ContactInfo,
            branding.LogoBytes,
            branding.AccentColor);
    }

    private static BulletinCategory Compose(Category category, Ruleset ruleset, Sport sport)
    {
        var config = ruleset.Config;

        return new BulletinCategory(
            category.Name,
            category.Gender,
            AgeRange(category),
            WeightRange(category),
            category.MaxRosterSize,
            PeriodsSummary(config.Periods),
            [.. MatchOutcomes.RequiredFor(sport.ScoreMode, config.Periods.Count)
                .Select(outcome => Points(config, outcome))],
            [.. config.Tiebreakers.Select(code => TiebreakerLabels.For(sport).GetValueOrDefault(code, Prettify(code)))],
            sport.ScoreMode == ScoreMode.Judged);
    }

    /// <summary>How the score decides an outcome, worded for a reader rather than a key.</summary>
    private static string Points(RulesetConfiguration config, string outcome)
    {
        var label = LabelOutcome(outcome);
        return config.Points.TryGetValue(outcome, out var points)
            ? $"{label}: {points} {(Math.Abs(points) == 1 ? "punto" : "puntos")}"
            : label;
    }

    /// <summary>
    /// "win_3_0" reads as "Victoria 3-0"; a plain "win", "draw" or "loss"
    /// reads as just the word. See <c>SetsMatchOutcomeRules.Format</c> for
    /// where the suffixed shape comes from.
    /// </summary>
    private static string LabelOutcome(string key)
    {
        var parts = key.Split('_');
        var label = parts[0] switch
        {
            MatchOutcomes.Win => "Victoria",
            MatchOutcomes.Draw => "Empate",
            MatchOutcomes.Loss => "Derrota",
            _ => Prettify(parts[0]),
        };

        return parts.Length >= 3 ? $"{label} {parts[1]}-{parts[2]}" : label;
    }

    private static string Prettify(string key) =>
        CultureInfo.InvariantCulture.TextInfo.ToTitleCase(key.Replace('_', ' '));

    private static string PeriodsSummary(PeriodRules periods) =>
        periods.Minutes is { } minutes
            ? $"{periods.Count} {Plural(periods.Label, periods.Count)} de {minutes} minutos"
            : $"{periods.Count} {Plural(periods.Label, periods.Count)}, definido por tanteo y no por reloj";

    private static string Plural(string label, short count) =>
        count == 1 ? label : label.EndsWith('s') ? label : $"{label}s";

    private static string? AgeRange(Category category) => category switch
    {
        { BirthDateFrom: { } from, BirthDateTo: { } to } =>
            $"Nacidos entre el {from:dd/MM/yyyy} y el {to:dd/MM/yyyy}",
        { BirthDateFrom: { } from } => $"Nacidos desde el {from:dd/MM/yyyy}",
        { BirthDateTo: { } to } => $"Nacidos hasta el {to:dd/MM/yyyy}",
        _ => null,
    };

    private static string? WeightRange(Category category) => category switch
    {
        { MinWeightKg: { } min, MaxWeightKg: { } max } => $"{min:0.##} a {max:0.##} kg",
        { MinWeightKg: { } min } => $"Desde {min:0.##} kg",
        { MaxWeightKg: { } max } => $"Hasta {max:0.##} kg",
        _ => null,
    };

    private static string FormatLabel(string format) => format switch
    {
        SportFrog.Domain.Competitions.CompetitionFormat.League => "Todos contra todos",
        SportFrog.Domain.Competitions.CompetitionFormat.Knockout => "Eliminación directa",
        SportFrog.Domain.Competitions.CompetitionFormat.Groups => "Fase de grupos y eliminación directa",
        _ => format,
    };
}

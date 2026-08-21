using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Features.Rosters;

/// <summary>Something a registration says that the category does not admit.</summary>
internal sealed record RosterViolation(string Property, string Message);

/// <summary>
/// Whether a person may be registered for a team, and in the shirt they were
/// given.
///
/// This is where the category's eligibility rules — who it admits, how many,
/// born when — stop being description and start being enforced. Everything
/// the category module refuses to change once a competition leaves draft is
/// refused there precisely because these checks already ran against it.
/// </summary>
/// <remarks>
/// Shared between registering and correcting, which ask the same question of
/// slightly different inputs: a correction has to leave the entry being
/// corrected out of its own comparisons, or a player would collide with
/// themselves.
/// </remarks>
internal sealed class RosterPolicy(SportFrogDbContext database)
{
    /// <summary>
    /// Everything wrong with the registration, or nothing.
    /// </summary>
    /// <param name="excluding">
    /// The entry being corrected, left out of the counts and the collision
    /// checks. Absent when registering someone new.
    /// </param>
    public async Task<IReadOnlyList<RosterViolation>> InspectAsync(
        Team team,
        Category category,
        Athlete athlete,
        short? jerseyNumber,
        Guid? excluding,
        CancellationToken cancellationToken)
    {
        var violations = new List<RosterViolation>();

        InspectAthlete(category, athlete, violations);

        await InspectPlaceAsync(team, category, athlete, excluding, violations, cancellationToken);
        await InspectJerseyAsync(team, jerseyNumber, excluding, violations, cancellationToken);

        return violations;
    }

    /// <summary>
    /// Whether the person themselves fits what the category admits.
    /// </summary>
    private static void InspectAthlete(
        Category category,
        Athlete athlete,
        List<RosterViolation> violations)
    {
        if (!athlete.IsActive)
        {
            violations.Add(new RosterViolation(
                "AthleteId",
                $"{athlete.FirstName} {athlete.LastName} is not active in this organization, " +
                "so they cannot be registered."));
        }

        // A category with no restriction admits anyone, and an athlete whose
        // sex was never recorded cannot be shown to fail one — but it also
        // cannot be shown to pass, and a category drawn along that line is
        // drawn along it for a reason. Refused, naming what is missing, so it
        // is fixed on the athlete rather than waved through here.
        if (category.Gender is { } admitted)
        {
            if (athlete.Gender is null)
            {
                violations.Add(new RosterViolation(
                    "AthleteId",
                    $"This category admits {admitted} only, and no sex is recorded for " +
                    $"{athlete.FirstName} {athlete.LastName}. Record it before registering them."));
            }
            else if (athlete.Gender != admitted)
            {
                violations.Add(new RosterViolation(
                    "AthleteId",
                    $"This category admits {admitted} only."));
            }
        }

        // Read as the window of birth dates admitted: the earliest is the
        // oldest player it takes, the latest the youngest. Either end may be
        // open.
        if (category.BirthDateFrom is { } earliest && athlete.BirthDate < earliest)
        {
            violations.Add(new RosterViolation(
                "AthleteId",
                $"{athlete.FirstName} {athlete.LastName} was born on {athlete.BirthDate:yyyy-MM-dd}, " +
                $"before {earliest:yyyy-MM-dd}, so they are too old for this category."));
        }

        if (category.BirthDateTo is { } latest && athlete.BirthDate > latest)
        {
            violations.Add(new RosterViolation(
                "AthleteId",
                $"{athlete.FirstName} {athlete.LastName} was born on {athlete.BirthDate:yyyy-MM-dd}, " +
                $"after {latest:yyyy-MM-dd}, so they are too young for this category."));
        }
    }

    /// <summary>
    /// Whether there is a place for them: one per person per category, and no
    /// more than the squad the category allows.
    /// </summary>
    private async Task InspectPlaceAsync(
        Team team,
        Category category,
        Athlete athlete,
        Guid? excluding,
        List<RosterViolation> violations,
        CancellationToken cancellationToken)
    {
        // Nothing in the schema stops this: the unique index only covers one
        // team, so the same person can be registered by two clubs in the same
        // division and turn up for both. It is the check that has to say no,
        // and it looks across the whole category rather than the one team.
        //
        // Withdrawn registrations count. Someone who left a team mid-season
        // has not become available to its rivals — that is a transfer, and a
        // transfer is a decision somebody makes, not a gap the rules leave
        // open.
        var elsewhere = await database.RosterEntries
            .Where(entry => entry.Id != excluding)
            .Where(entry => entry.AthleteId == athlete.Id)
            .Where(entry => entry.Team!.CategoryId == category.Id)
            .Select(entry => new { entry.Team!.Name, Withdrawn = entry.WithdrawnAt != null })
            .FirstOrDefaultAsync(cancellationToken);

        if (elsewhere is not null)
        {
            violations.Add(new RosterViolation(
                "AthleteId",
                elsewhere.Withdrawn
                    ? $"{athlete.FirstName} {athlete.LastName} was registered for " +
                      $"{elsewhere.Name} in this category and withdrew. Striking that " +
                      "registration is what frees them, and only if nothing was recorded for it."
                    : $"{athlete.FirstName} {athlete.LastName} already plays for " +
                      $"{elsewhere.Name} in this category. Nobody plays twice in one division."));
        }

        if (category.MaxRosterSize is not { } cap)
        {
            return;
        }

        // Only those actually on the team count against the cap: a player who
        // withdrew left a place behind them.
        var registered = await database.RosterEntries
            .Where(entry => entry.Id != excluding)
            .Where(entry => entry.TeamId == team.Id && entry.WithdrawnAt == null)
            .CountAsync(cancellationToken);

        if (registered >= cap)
        {
            violations.Add(new RosterViolation(
                "AthleteId",
                $"{team.Name} already has the {cap} players this category allows. Withdraw " +
                "someone before registering another."));
        }
    }

    /// <summary>
    /// Whether the shirt is free.
    /// </summary>
    /// <remarks>
    /// Checked here for the message; the partial unique index is what makes it
    /// true, and it is the one that settles two registrations racing for the
    /// same number.
    /// </remarks>
    private async Task InspectJerseyAsync(
        Team team,
        short? jerseyNumber,
        Guid? excluding,
        List<RosterViolation> violations,
        CancellationToken cancellationToken)
    {
        if (jerseyNumber is not { } number)
        {
            return;
        }

        var wearer = await database.RosterEntries
            .Where(entry => entry.Id != excluding)
            .Where(entry => entry.TeamId == team.Id
                && entry.WithdrawnAt == null
                && entry.JerseyNumber == number)
            .Select(entry => entry.Athlete!.FirstName + " " + entry.Athlete.LastName)
            .FirstOrDefaultAsync(cancellationToken);

        if (wearer is not null)
        {
            violations.Add(new RosterViolation(
                "JerseyNumber",
                $"{wearer} already wears {number} for {team.Name}."));
        }
    }
}

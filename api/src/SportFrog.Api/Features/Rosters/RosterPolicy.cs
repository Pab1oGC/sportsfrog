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
    /// <param name="pending">
    /// Registrations already accepted in the same operation but not yet
    /// written. Zero for a single registration, which is the only kind that
    /// existed when this was written; an import accepts a squad at once, and
    /// without this the twenty-first row of a file would be told the team has
    /// room because the first twenty are not in the database yet.
    /// </param>
    public async Task<IReadOnlyList<RosterViolation>> InspectAsync(
        Team team,
        Category category,
        Athlete athlete,
        short? jerseyNumber,
        Guid? excluding,
        CancellationToken cancellationToken,
        int pending = 0)
    {
        var violations = new List<RosterViolation>();

        InspectAthlete(category, athlete, violations);

        await InspectPlaceAsync(
            team, category, athlete, excluding, pending, violations, cancellationToken);

        if (await InspectJerseyAsync(team, jerseyNumber, excluding, cancellationToken) is { } taken)
        {
            violations.Add(taken);
        }

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
                $"La persona {athlete.FirstName} {athlete.LastName} no está activa en la " +
                "organización, así que no se la puede inscribir."));
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
                    $"Esta categoría admite solo {admitted}, y no hay sexo registrado para " +
                    $"{athlete.FirstName} {athlete.LastName}. Hay que registrarlo antes de " +
                    "inscribir a esta persona."));
            }
            else if (athlete.Gender != admitted)
            {
                violations.Add(new RosterViolation(
                    "AthleteId",
                    $"Esta categoría admite solo {admitted}."));
            }
        }

        // Read as the window of birth dates admitted: the earliest is the
        // oldest player it takes, the latest the youngest. Either end may be
        // open.
        if (category.BirthDateFrom is { } earliest && athlete.BirthDate < earliest)
        {
            violations.Add(new RosterViolation(
                "AthleteId",
                $"{athlete.FirstName} {athlete.LastName} nació el {athlete.BirthDate:yyyy-MM-dd}, " +
                $"antes del {earliest:yyyy-MM-dd}, así que excede la edad de esta categoría."));
        }

        if (category.BirthDateTo is { } latest && athlete.BirthDate > latest)
        {
            violations.Add(new RosterViolation(
                "AthleteId",
                $"{athlete.FirstName} {athlete.LastName} nació el {athlete.BirthDate:yyyy-MM-dd}, " +
                $"después del {latest:yyyy-MM-dd}, así que todavía no llega a la edad de esta " +
                "categoría."));
        }

        // Read as the window of weight admitted, same shape as the birth-date
        // one — but unlike sex, there is no boundary value a missing weigh-in
        // could be assumed to sit on the right side of, so a category with
        // any restriction at all needs a real number to compare against.
        if (category.MinWeightKg is not null || category.MaxWeightKg is not null)
        {
            if (athlete.WeightKg is null)
            {
                violations.Add(new RosterViolation(
                    "AthleteId",
                    $"Esta categoría admite un rango de peso, y no hay un pesaje registrado " +
                    $"para {athlete.FirstName} {athlete.LastName}. Hay que registrarlo antes de " +
                    "inscribir a esta persona."));
            }
            else
            {
                if (category.MinWeightKg is { } minimum && athlete.WeightKg < minimum)
                {
                    violations.Add(new RosterViolation(
                        "AthleteId",
                        $"{athlete.FirstName} {athlete.LastName} pesa {athlete.WeightKg} kg, " +
                        $"menos que el mínimo de {minimum} kg de esta categoría."));
                }

                if (category.MaxWeightKg is { } maximum && athlete.WeightKg > maximum)
                {
                    violations.Add(new RosterViolation(
                        "AthleteId",
                        $"{athlete.FirstName} {athlete.LastName} pesa {athlete.WeightKg} kg, " +
                        $"más que el máximo de {maximum} kg de esta categoría."));
                }
            }
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
        int pending,
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
                    ? $"La inscripción de {athlete.FirstName} {athlete.LastName} en " +
                      $"{elsewhere.Name} de esta categoría figura como retirada. Lo que la " +
                      "libera es anular esa inscripción, y solo si no se registró nada en ella."
                    : $"{athlete.FirstName} {athlete.LastName} ya juega en {elsewhere.Name} en " +
                      "esta categoría. Nadie juega en dos equipos de una misma división."));
        }

        // The ceiling the sport itself imposes, which a category may narrow
        // and never widen: Kyorugi is fought one against one, so no roster
        // size typed into a category can make a pair of them exist.
        //
        // Asked of the database rather than read off category.Competition.Sport
        // because not every caller loads that navigation, and one arriving
        // null would turn the rule into a silence — the failure mode this file
        // avoids everywhere else. It is one indexed lookup on top of the few
        // this policy already makes per call, which the import's own remark
        // already accepts as the price of asking the real rules.
        var sportCeiling = await database.Categories
            .AsNoTracking()
            .Where(candidate => candidate.Id == category.Id)
            .Select(candidate => candidate.Competition!.Sport!.MaxEntrySize)
            .SingleOrDefaultAsync(cancellationToken);

        if (LowerOf(category.MaxRosterSize, sportCeiling) is not { } cap)
        {
            return;
        }

        // Only those actually on the team count against the cap: a player who
        // withdrew left a place behind them.
        var registered = await database.RosterEntries
            .Where(entry => entry.Id != excluding)
            .Where(entry => entry.TeamId == team.Id && entry.WithdrawnAt == null)
            .CountAsync(cancellationToken);

        if (registered + pending >= cap)
        {
            // "Jugadores" solo en un deporte de equipo. Donde la unidad que
            // compite es una persona o una pareja, el cupo de la categoría es
            // su modalidad — individual, pareja, trío — y hablar de jugadores
            // ahí nombra un plantel que no existe.
            violations.Add(new RosterViolation(
                "AthleteId",
                team.IsIndividual
                    ? $"{team.Name} ya está completa: esta categoría se compite de a {cap} " +
                      $"{(cap == 1 ? "deportista" : "deportistas")}. Hay que retirar a alguien " +
                      "antes de inscribir a otra persona."
                    : $"{team.Name} ya tiene los {cap} jugadores que admite esta categoría. Hay " +
                      "que retirar a alguien antes de inscribir a otra persona."));
        }
    }

    /// <summary>
    /// The stricter of two caps, where either may be absent.
    /// </summary>
    /// <remarks>
    /// Absent on both sides means uncapped, which is what a team sport with
    /// no squad limit set is: nothing to compare against, so nothing refused.
    /// </remarks>
    private static short? LowerOf(short? first, short? second) =>
        first is null ? second
        : second is null ? first
        : Math.Min(first.Value, second.Value);

    /// <summary>
    /// Whether the shirt is free, or who is wearing it.
    /// </summary>
    /// <remarks>
    /// Public and separate from the rest because correcting a registration
    /// asks only this. The other rules were settled when the player was
    /// registered, and re-running them to change a number would refuse the
    /// change for reasons that have nothing to do with numbers.
    ///
    /// Checked here for the message; the partial unique index is what makes it
    /// true, and it is the one that settles two registrations racing for the
    /// same number.
    /// </remarks>
    public async Task<RosterViolation?> InspectJerseyAsync(
        Team team,
        short? jerseyNumber,
        Guid? excluding,
        CancellationToken cancellationToken)
    {
        if (jerseyNumber is not { } number)
        {
            return null;
        }

        var wearer = await database.RosterEntries
            .Where(entry => entry.Id != excluding)
            .Where(entry => entry.TeamId == team.Id
                && entry.WithdrawnAt == null
                && entry.JerseyNumber == number)
            .Select(entry => entry.Athlete!.FirstName + " " + entry.Athlete.LastName)
            .FirstOrDefaultAsync(cancellationToken);

        return wearer is null
            ? null
            : new RosterViolation(
                "JerseyNumber", $"{wearer} ya lleva el {number} en {team.Name}.");
    }
}

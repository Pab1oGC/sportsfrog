using FluentValidation;

namespace SportFrog.Api.Features.Courses;

/// <summary>
/// A course's own data and its whole weekly schedule, together.
/// </summary>
/// <remarks>
/// <see cref="Schedules"/> carries the entire set on every write, never one
/// slot at a time: there is no endpoint that adds or removes a single row,
/// so a save always replaces every <c>course_schedules</c> row of the
/// course with exactly what this list says. <see cref="WeeklySlot"/> is the
/// domain project's own type, reused here rather than mirrored —
/// <see cref="Rulebook.RulesetContract"/> already reuses
/// <c>RulesetConfiguration</c> the same way, for the same reason: the shape
/// a rule is checked against should not drift from the shape it is written
/// in.
/// </remarks>
public sealed record CourseContract(
    string SportCode,
    string Name,
    Guid? VenueSpaceId,
    Guid? CoachMembershipId,
    short? Capacity,
    decimal MonthlyFeeBob,
    DateOnly StartsOn,
    DateOnly? EndsOn,
    bool IsActive,
    IReadOnlyList<WeeklySlot> Schedules);

internal sealed class CourseContractValidator : AbstractValidator<CourseContract>
{
    /// <summary>
    /// Fourteen is already generous — two groups every day of the week.
    /// The ceiling exists to catch a form submitted twice by mistake, not
    /// to restrict a real school.
    /// </summary>
    private const int MaximumWeeklySlots = 14;

    /// <summary>
    /// A school of a thousand students in one course is a mistyped number,
    /// same reasoning as <see cref="Categories.CategoryContractValidator"/>'s
    /// own roster ceiling.
    /// </summary>
    private const short MaximumCapacity = 300;

    public CourseContractValidator()
    {
        RuleFor(contract => contract.SportCode)
            .NotEmpty().WithMessage("El deporte del curso es obligatorio.");

        RuleFor(contract => contract.Name)
            .NotEmpty().WithMessage("El nombre del curso es obligatorio.")
            .MaximumLength(120);

        RuleFor(contract => contract.Capacity)
            .InclusiveBetween((short)1, MaximumCapacity)
            .When(contract => contract.Capacity.HasValue)
            .WithMessage($"El cupo debe estar entre 1 y {MaximumCapacity}, o dejarse sin definir " +
                         "para no ponerle límite.");

        // Mirrors ck_courses_fee_non_negative.
        RuleFor(contract => contract.MonthlyFeeBob)
            .GreaterThanOrEqualTo(0)
            .WithMessage("La pensión mensual no puede ser negativa.");

        // Mirrors ck_courses_dates.
        RuleFor(contract => contract.EndsOn)
            .GreaterThanOrEqualTo(contract => contract.StartsOn)
            .When(contract => contract.EndsOn.HasValue)
            .WithMessage("La fecha de fin no puede ser anterior a la de inicio.");

        RuleFor(contract => contract.Schedules)
            .NotEmpty()
            .WithMessage("El curso necesita al menos una franja semanal para poder dictarse.");

        RuleFor(contract => contract.Schedules)
            .Must(slots => slots.Count <= MaximumWeeklySlots)
            .WithMessage($"Un curso no puede tener más de {MaximumWeeklySlots} franjas semanales.");

        // Two slots on the same day are a real case (a morning and an
        // evening group) and are accepted — see WeeklySlot's own remarks.
        // An exact duplicate is not a second group, it is the same row
        // sent twice.
        RuleFor(contract => contract.Schedules)
            .Must(slots => slots.Distinct().Count() == slots.Count)
            .When(contract => contract.Schedules.Count > 0)
            .WithMessage("Hay una franja repetida exactamente igual en el horario.");

        // Mirrors ck_course_schedules_time_range.
        RuleForEach(contract => contract.Schedules)
            .ChildRules(slot => slot.RuleFor(s => s.EndsAt)
                .GreaterThan(s => s.StartsAt)
                .WithMessage("La hora de fin de una franja debe ser posterior a la de inicio."));
    }
}

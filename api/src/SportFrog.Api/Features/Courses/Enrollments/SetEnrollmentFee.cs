using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Tenancy;

namespace SportFrog.Api.Features.Courses.Enrollments;

/// <summary>
/// Sets, changes or clears one enrollment's own exception to its course's
/// monthly fee.
/// </summary>
/// <remarks>
/// A scholarship or a discount is a fact about one registration, not about
/// the course — see <see cref="CourseContract"/>'s own fee and
/// <see cref="Infrastructure.Persistence.Entities.CourseEnrollment.FeeOverrideBob"/>'s
/// remarks. Clearing it needs no reason: a reason explains why an exception
/// exists, not why one does not.
/// </remarks>
public static class SetEnrollmentFee
{
    public sealed record Request(decimal? FeeOverrideBob, string? FeeOverrideReason);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            // Mirrors ck_course_enrollments_fee_override_non_negative.
            RuleFor(request => request.FeeOverrideBob)
                .GreaterThanOrEqualTo(0)
                .When(request => request.FeeOverrideBob.HasValue)
                .WithMessage("El monto no puede ser negativo.");

            RuleFor(request => request.FeeOverrideReason)
                .MaximumLength(300);

            // Mirrors ck_course_enrollments_fee_override_reason: the amount
            // decides the reason must exist, not the other way around — an
            // override of exactly zero still needs one.
            RuleFor(request => request)
                .Must(request => request.FeeOverrideBob.HasValue
                    == !string.IsNullOrWhiteSpace(request.FeeOverrideReason))
                .WithMessage("Un monto distinto necesita un motivo, y un motivo sin monto no " +
                             "corrige nada.");
        }
    }

    public static IEndpointRouteBuilder MapSetEnrollmentFee(this IEndpointRouteBuilder routes)
    {
        routes.MapPut("/enrollments/{id:guid}/fee", HandleAsync)
            .RequireRole(MembershipRole.Operator)
            .RequireScope(new ScopeRequirement(ScopeType.Course, ScopeTargetKind.CourseEnrollment, "id"))
            .WithName(nameof(SetEnrollmentFee))
            .WithSummary("Sets, changes or clears an enrollment's own fee.");

        return routes;
    }

    internal static async Task<IResult> HandleAsync(
        Guid id,
        Request request,
        SportFrogDbContext database,
        CancellationToken cancellationToken)
    {
        var entry = await database.CourseEnrollments.SingleOrDefaultAsync(
            candidate => candidate.Id == id, cancellationToken);

        if (entry is null)
        {
            return Results.NotFound();
        }

        entry.FeeOverrideBob = request.FeeOverrideBob;
        entry.FeeOverrideReason = string.IsNullOrWhiteSpace(request.FeeOverrideReason)
            ? null
            : request.FeeOverrideReason.Trim();

        await database.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }
}

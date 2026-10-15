using Microsoft.EntityFrameworkCore;
using SportFrog.Api.Infrastructure.Jobs;
using SportFrog.Api.Infrastructure.Persistence;

namespace SportFrog.Api.Features.Tuition;

/// <summary>
/// Raises this month's pension for every organization, once a month, with
/// nobody asking.
/// </summary>
/// <remarks>
/// The scheduled twin of <see cref="AccrueTuition"/> — see
/// <see cref="TuitionAccrual"/>'s own remarks for how the two share every
/// rule about what is owed and differ only in who calls and how often.
///
/// The organizations table carries no row-level security of its own (its
/// policy protects everything hanging off an organization, not the
/// organizations themselves), so it is the one place a job with no
/// organization yet established can safely be read from. Everything after
/// that — one organization at a time — runs through
/// <see cref="OrganizationJobScope"/> exactly as any other job does, except
/// that nobody asked for it, so there is no person to carry as its author.
///
/// One organization's accrual failing — a dangling enrollment, a course
/// with no price, whatever it turns out to be — must not cost every other
/// organization its month. Each is tried and recorded on its own, and a
/// failure here is retried next month rather than at the top of the queue:
/// unlike a photograph batch, an accrual is not sitting on a person waiting
/// for an answer.
/// </remarks>
public sealed class TuitionAccrualJob(
    SportFrogDbContext database,
    OrganizationJobScope scopes,
    TimeProvider clock,
    ILogger<TuitionAccrualJob> logger)
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var period = new DateOnly(now.Year, now.Month, 1);

        var organizationIds = await database.Organizations
            .Where(organization => organization.IsActive)
            .Select(organization => organization.Id)
            .ToListAsync(cancellationToken);

        foreach (var organizationId in organizationIds)
        {
            try
            {
                var result = await scopes.RunAsync(organizationId, userId: null, async (services, _) =>
                    await services.GetRequiredService<TuitionAccrual>()
                        .RunAsync(organizationId, period, cancellationToken),
                    cancellationToken);

                logger.LogInformation(
                    "Tuition accrual for organization {Organization}, period {Period}: " +
                        "{Charged} charged, {AlreadyCharged} already charged.",
                    organizationId, result.Period, result.Charged, result.AlreadyCharged);
            }
            catch (Exception failure) when (failure is not OperationCanceledException)
            {
                logger.LogError(
                    failure, "Tuition accrual failed for organization {Organization}.", organizationId);
            }
        }
    }
}

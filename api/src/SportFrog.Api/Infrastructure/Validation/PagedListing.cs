using System.Globalization;
using Microsoft.EntityFrameworkCore;

namespace SportFrog.Api.Infrastructure.Validation;

/// <summary>
/// Optional, explicit paging for a listing that otherwise returns everything
/// its filter matches.
/// </summary>
/// <remarks>
/// Unchanged for any caller that never asks for it and never opts into
/// <c>maxWhenUnpaged</c> below: skip and take absent is exactly today's
/// query, run the same way it always was. That matters more than it looks —
/// the panel's shared list hook (<c>web/src/hooks/use-crud.js</c>) reads a
/// listing's response body directly as the rows a grid renders
/// (<c>rows: data || []</c>), and every page built on it — athletes, clubs,
/// competitions among them — would break the day this wrapped that body in
/// an envelope, without a single line of client code having changed to ask
/// for one. The total count a caller needs to keep paging is carried in a
/// response header instead (<see cref="TotalCountHeader"/>), set only when
/// paging was actually requested or <c>maxWhenUnpaged</c> was, so the body
/// stays a plain array either way.
///
/// <c>maxWhenUnpaged</c> is an opt-in exception to "unpaged returns
/// everything", for a listing whose own caller knows it only grows: an
/// organization's competitions, read unpaged by half a dozen dropdowns and
/// cascading filters across the panel that all need the full list and none
/// of which will ever ask for a page (see <c>ReadCompetitions</c>). Left at
/// its default of <c>null</c>, nothing changes for a caller that has not
/// opted in — athletes and clubs, today. Passed, it caps how much an unpaged
/// request can return the same way <see cref="MaximumPageSize"/> already
/// caps an explicit one, so a selector reading years of history slows down
/// gracefully long before it tries to hold all of it in one response.
/// </remarks>
public static class PagedListing
{
    public const string TotalCountHeader = "X-Total-Count";

    /// <summary>Used when a caller asks to page but does not say how large a page.</summary>
    private const int DefaultPageSize = 50;

    /// <summary>
    /// Past this, a request for an explicit page is asking for the same
    /// "everything, in one answer" this exists to bound. A caller reads this
    /// directly to apply the same ceiling to an unpaged listing via
    /// <c>maxWhenUnpaged</c>, rather than repeating the number.
    /// </summary>
    public const int MaximumPageSize = 500;

    public static async Task<IReadOnlyList<T>> ApplyAsync<T>(
        IQueryable<T> query,
        HttpContext httpContext,
        int? skip,
        int? take,
        CancellationToken cancellationToken,
        int? maxWhenUnpaged = null)
    {
        if (skip is null && take is null)
        {
            if (maxWhenUnpaged is not { } cap)
            {
                return await query.ToListAsync(cancellationToken);
            }

            // Still answers the real total, even though nothing here was
            // asked to page: a caller that opted in is explicitly the kind
            // that may one day return fewer rows than exist, and the count
            // is what would let it notice.
            await SetTotalCountHeaderAsync(query, httpContext, cancellationToken);

            return await query.Take(cap).ToListAsync(cancellationToken);
        }

        await SetTotalCountHeaderAsync(query, httpContext, cancellationToken);

        return await query
            .Skip(Math.Max(skip ?? 0, 0))
            .Take(Math.Clamp(take ?? DefaultPageSize, 1, MaximumPageSize))
            .ToListAsync(cancellationToken);
    }

    private static async Task SetTotalCountHeaderAsync<T>(
        IQueryable<T> query, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var total = await query.CountAsync(cancellationToken);
        httpContext.Response.Headers[TotalCountHeader] = total.ToString(CultureInfo.InvariantCulture);
    }
}

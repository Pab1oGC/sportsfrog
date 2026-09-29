using System.Globalization;
using Microsoft.EntityFrameworkCore;

namespace SportFrog.Api.Infrastructure.Validation;

/// <summary>
/// Optional, explicit paging for a listing that otherwise returns everything
/// its filter matches.
/// </summary>
/// <remarks>
/// Unchanged for any caller that never asks for it: skip and take absent is
/// exactly today's query, run the same way it always was. That matters more
/// than it looks — the panel's shared list hook
/// (<c>web/src/hooks/use-crud.js</c>) reads a listing's response body
/// directly as the rows a grid renders (<c>rows: data || []</c>), and every
/// page built on it — athletes, clubs, competitions among them — would break
/// the day this wrapped that body in an envelope, without a single line of
/// client code having changed to ask for one. The total count a caller needs
/// to keep paging is carried in a response header instead
/// (<see cref="TotalCountHeader"/>), set only when paging was actually
/// requested, so the body stays a plain array either way.
/// </remarks>
public static class PagedListing
{
    public const string TotalCountHeader = "X-Total-Count";

    /// <summary>Used when a caller asks to page but does not say how large a page.</summary>
    private const int DefaultPageSize = 50;

    /// <summary>
    /// Past this, a request is asking for the same "everything, in one
    /// answer" this exists to bound — the cap applies to an explicit
    /// request for a lot at once, same as to no request at all.
    /// </summary>
    private const int MaximumPageSize = 500;

    public static async Task<IReadOnlyList<T>> ApplyAsync<T>(
        IQueryable<T> query,
        HttpContext httpContext,
        int? skip,
        int? take,
        CancellationToken cancellationToken)
    {
        if (skip is null && take is null)
        {
            return await query.ToListAsync(cancellationToken);
        }

        var total = await query.CountAsync(cancellationToken);
        httpContext.Response.Headers[TotalCountHeader] = total.ToString(CultureInfo.InvariantCulture);

        return await query
            .Skip(Math.Max(skip ?? 0, 0))
            .Take(Math.Clamp(take ?? DefaultPageSize, 1, MaximumPageSize))
            .ToListAsync(cancellationToken);
    }
}

using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace SportFrog.Api.Infrastructure.RateLimiting;

/// <summary>
/// Caps on how often a caller may knock (section 10).
///
/// Two shapes, because the two things being protected fail differently.
/// Signing in is guessed at: a caller trying thousands of passwords is doing
/// something no legitimate client does, so the cap is tight. The public view
/// is read, often and by many: the cap there is only meant to stop one
/// visitor from taking the service away from the rest.
/// </summary>
public static class RateLimitPolicies
{
    /// <summary>Credential endpoints: signing in, renewing, registering, changing a password.</summary>
    public const string Authentication = "authentication";

    /// <summary>The anonymous read path.</summary>
    public const string Public = "public";

    public static IServiceCollection AddSportFrogRateLimiting(this IServiceCollection services) =>
        services.AddRateLimiter(options =>
        {
            options.AddPolicy(Authentication, PartitionByCaller(attempts: 10, perMinutes: 1));
            options.AddPolicy(Public, PartitionByCaller(attempts: 120, perMinutes: 1));

            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    // Told plainly, so a well-behaved client can wait rather
                    // than retry into the wall.
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)retryAfter.TotalSeconds).ToString();
                }

                await context.HttpContext.Response.WriteAsJsonAsync(
                    new { detail = "Demasiadas solicitudes. Probá de nuevo en un momento." },
                    cancellationToken);
            };
        });

    /// <summary>
    /// One window per caller address.
    /// </summary>
    /// <remarks>
    /// Keyed by remote address, which is only meaningful if the address is
    /// the caller's. Behind a proxy the API sees the proxy, and every visitor
    /// would share one budget — so forwarded headers have to be honoured
    /// wherever this is deployed, or the cap protects nothing and locks
    /// everyone out together.
    /// </remarks>
    private static Func<HttpContext, RateLimitPartition<string>> PartitionByCaller(
        int attempts,
        int perMinutes) =>
        context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = attempts,
                Window = TimeSpan.FromMinutes(perMinutes),

                // No queue: a request over the limit is refused now rather
                // than held open, which is what keeps a flood from consuming
                // the capacity it was meant to be denied.
                QueueLimit = 0,
            });
}

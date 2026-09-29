using System.Net;
using System.Net.Sockets;

namespace SportFrog.Api.Features.Venues;

/// <summary>
/// Follows a chain of HTTP redirects by hand, refusing to step onto an
/// address this server has no business fetching on an admin's behalf.
/// </summary>
/// <remarks>
/// <see cref="ManageVenues"/>'s host allowlist catches the obvious case — a
/// URL that never named a Google short-link host to begin with — but a short
/// link is, structurally, a redirect an admin does not control the
/// destination of, and letting <c>HttpClientHandler.AllowAutoRedirect</c>
/// chase it blindly only ever validated the address connected to
/// <em>first</em>. Nothing past that first hop is a Google-owned host
/// anymore, so nothing past it gets the benefit of the doubt a fixed domain
/// allowlist gave the first one: a legitimate <c>goo.gl</c> link, or one made
/// to look like it for exactly as long as it takes to pass that check, can
/// still end its own redirect chain at an address only this server's own
/// network can reach — a cloud metadata endpoint, MinIO, Postgres, anything
/// else behind it. This resolves and checks every hop before connecting to
/// it, the first one included.
///
/// What this does not defend against: a name that resolves safely at the
/// moment it is checked here and unsafely a moment later, at the instant the
/// real connection is opened (classic DNS rebinding). Closing that needs
/// pinning the connection to the address already validated — its own
/// meaningful piece of work, particularly once TLS's own hostname
/// verification has to keep working against a request now aimed at a raw
/// IP — and is deliberately left for a separate change rather than folded
/// into this one silently.
/// </remarks>
internal static class SafeRedirectResolver
{
    /// <summary>
    /// Matches the handler's own former <c>MaxAutomaticRedirections</c>, from
    /// when following was left to it.
    /// </summary>
    private const int MaximumHops = 5;

    public sealed record Result(Uri? FinalUri, string? Problem);

    public static async Task<Result> FollowAsync(
        HttpClient client, Uri start, CancellationToken cancellationToken)
    {
        var current = start;

        for (var hop = 0; hop <= MaximumHops; hop++)
        {
            if (await IsUnsafeAsync(current.Host, cancellationToken))
            {
                // Worded the same as the ordinary "could not reach it" case
                // below: which of the two happened is not something an admin
                // pasting a link needs told apart, and this codebase's own
                // taste is not to describe the shape of a defence to whoever
                // is on the other side of it.
                return new Result(null, "No se pudo seguir ese enlace.");
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, current);
            using var response = await client.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!IsRedirect(response.StatusCode) || response.Headers.Location is not { } location)
            {
                return new Result(current, null);
            }

            // A Location header may be relative to where it was sent from —
            // resolved against the current address, an absolute one passes
            // through unchanged.
            current = location.IsAbsoluteUri ? location : new Uri(current, location);
        }

        return new Result(null, "Ese enlace redirige demasiadas veces.");
    }

    private static bool IsRedirect(HttpStatusCode status) =>
        status is HttpStatusCode.MovedPermanently
            or HttpStatusCode.Found
            or HttpStatusCode.SeeOther
            or HttpStatusCode.TemporaryRedirect
            or HttpStatusCode.PermanentRedirect;

    private static async Task<bool> IsUnsafeAsync(string host, CancellationToken cancellationToken)
    {
        IPAddress[] addresses;

        try
        {
            addresses = await Dns.GetHostAddressesAsync(host, cancellationToken);
        }
        catch (SocketException)
        {
            // Does not resolve at all, either way — let the request itself
            // fail and be reported as the ordinary "could not reach it" that
            // it is, rather than this guessing at why.
            return false;
        }

        // Every address a name resolves to has to be safe, not merely one of
        // them: a name that answers with both a public and a private address
        // would otherwise pass on the strength of the one nobody meant to
        // connect to.
        return addresses.Length == 0 || addresses.Any(IsDisallowed);
    }

    /// <summary>
    /// Loopback, link-local — where a cloud provider's metadata service
    /// lives, at 169.254.169.254 — or one of the private ranges no public
    /// Maps redirect has any real reason to resolve to.
    /// </summary>
    private static bool IsDisallowed(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
        {
            return true;
        }

        var bytes = address.GetAddressBytes();

        return address.AddressFamily switch
        {
            AddressFamily.InterNetwork => bytes[0] == 10                                  // 10.0.0.0/8
                || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)                        // 172.16.0.0/12
                || (bytes[0] == 192 && bytes[1] == 168)                                    // 192.168.0.0/16
                || (bytes[0] == 169 && bytes[1] == 254),                                   // 169.254.0.0/16
            AddressFamily.InterNetworkV6 => (bytes[0] & 0xfe) == 0xfc                      // fc00::/7 (unique local)
                || (bytes[0] == 0xfe && (bytes[1] & 0xc0) == 0x80),                        // fe80::/10 (link-local)

            // Neither IPv4 nor IPv6 is not an address a Maps redirect
            // resolves to either way.
            _ => true,
        };
    }
}

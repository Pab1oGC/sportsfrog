using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;

namespace SportFrog.Api.Infrastructure.Observability;

/// <summary>
/// The last resort for whatever an endpoint's own handling did not catch.
/// </summary>
/// <remarks>
/// Every deliberate failure this API answers already goes through
/// <c>Results.Problem</c> at the point it happens — a validation refusal, a
/// unique violation caught as a 409, a concurrency conflict caught the same
/// way. This exists for what none of those anticipated: a downstream outage,
/// a null reference nobody wrote a test for, a library throwing something
/// new. Before this, that class of failure fell through to whatever ASP.NET
/// Core's own default behaviour happens to be for the hosting environment —
/// unverified here, and different in Development than in Production —
/// rather than the one deliberate answer every other failure in this API
/// already gives: a <c>problem+json</c> body shaped the same way, with
/// nothing about the exception itself repeated back to whoever sent the
/// request that caused it, in every environment alike. Production is not
/// the only place this matters: Development is exactly where nobody has
/// verified this path either, so it gets no special treatment.
///
/// <c>UseSerilogRequestLogging</c> (see <see cref="Logging"/>) already
/// records the full exception, server-side, once it bubbles past this
/// handler and back up the pipeline — logging it again here as well is
/// redundant for the exception itself, so this only attaches the trace id
/// the two share, rather than writing the same stack trace twice.
/// </remarks>
internal sealed class GlobalExceptionHandler(IProblemDetailsService problemDetails)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails =
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Ocurrió un error inesperado.",

                // Nothing from the exception itself, ever: this API has no
                // caller a detailed message would be safe to show, and the
                // trace id is what actually gets someone from a report to
                // the real cause, in the server's own log.
                Detail = "Algo salió mal de un modo que no se esperaba. Volvé a intentarlo en un " +
                    $"momento; si sigue pasando, el identificador {traceId} ayuda a encontrar qué fue.",
            },
        });
    }
}

using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace SportFrog.Api.Infrastructure.Observability;

/// <summary>
/// Structured logging (section 8).
///
/// Structured rather than printed, because the questions worth asking of a
/// multi-organization system are filters, not searches: which organization
/// saw the errors, how long that operation took, who was refused. A line of
/// prose answers none of those without a regular expression and a guess.
/// </summary>
public static class Logging
{
    public static IHostBuilder UseSportFrogLogging(this IHostBuilder host) =>
        host.UseSerilog((context, services, configuration) => configuration
            .MinimumLevel.Information()

            // The framework narrates every request twice at Information.
            // Left on, it buries the events that were worth recording.
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)

            // Carries the properties attached by ILogger scopes — the
            // organization and role the entry channel establishes — into
            // every event written underneath them.
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "SportFrog.Api")
            .ReadFrom.Services(services)
            .WriteTo.Console(FormatterFor(context.HostingEnvironment)));

    /// <summary>
    /// JSON where something collects it, readable text where a person is
    /// watching it scroll.
    /// </summary>
    private static Serilog.Formatting.ITextFormatter FormatterFor(IHostEnvironment environment) =>
        environment.IsDevelopment()
            ? new Serilog.Formatting.Display.MessageTemplateTextFormatter(
                "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            : new CompactJsonFormatter();

    /// <summary>
    /// One line per request, with how long it took.
    /// </summary>
    /// <remarks>
    /// Response time is a requirement with a number attached (RNF-02), and a
    /// number nobody measures is a hope. This is where that measurement comes
    /// from in production.
    /// </remarks>
    public static IApplicationBuilder UseSportFrogRequestLogging(this IApplicationBuilder app) =>
        app.UseSerilogRequestLogging(options =>
        {
            options.MessageTemplate =
                "{RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0} ms";

            // A refused request is not an error of the server's, but it is
            // worth seeing without turning the volume up on everything.
            options.GetLevel = (httpContext, _, exception) =>
                exception is not null || httpContext.Response.StatusCode >= 500
                    ? LogEventLevel.Error
                    : httpContext.Response.StatusCode >= 400
                        ? LogEventLevel.Warning
                        : LogEventLevel.Information;
        });
}

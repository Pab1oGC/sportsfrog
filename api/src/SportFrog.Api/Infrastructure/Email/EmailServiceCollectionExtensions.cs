namespace SportFrog.Api.Infrastructure.Email;

/// <summary>Registers outgoing mail — real, once <see cref="SmtpOptions"/> names a relay, logged otherwise.</summary>
public static class EmailServiceCollectionExtensions
{
    public static IServiceCollection AddSportFrogEmail(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bound, but never ValidateOnStart: an empty Host is the ordinary,
        // supported state of an installation that has not set up mail yet —
        // see SmtpOptions's remarks — and failing startup over it would make
        // every feature in the process depend on one nobody has asked for.
        services
            .AddOptions<SmtpOptions>()
            .Bind(configuration.GetSection(SmtpOptions.SectionName));

        services.AddScoped(EmailSenderFactory.Create);

        return services;
    }
}

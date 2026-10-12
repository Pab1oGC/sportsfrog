using Microsoft.Extensions.Options;

namespace SportFrog.Api.Infrastructure.Validation;

/// <summary>Registers the photo validator client.</summary>
public static class PhotoValidationServiceCollectionExtensions
{
    public static IServiceCollection AddSportFrogPhotoValidation(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<PhotoValidationOptions>()
            .Bind(configuration.GetSection(PhotoValidationOptions.SectionName))
            .ValidateDataAnnotations();

        services.AddHttpClient<IPhotoValidator, PhotoValidatorClient>((provider, client) =>
        {
            client.Timeout = provider.GetRequiredService<IOptions<PhotoValidationOptions>>().Value.Timeout;
        });

        return services;
    }
}

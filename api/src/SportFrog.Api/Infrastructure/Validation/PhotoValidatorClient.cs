using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace SportFrog.Api.Infrastructure.Validation;

/// <summary>
/// Sends a photograph to the validator service and reads back its verdict.
/// </summary>
/// <remarks>
/// This class only translates between HTTP and <see cref="PhotoVerdict"/>. The
/// rules, the thresholds and the Spanish messages live in the validator, so
/// nothing here decides or reinterprets them. Anything that is not a usable
/// verdict (a transport failure, a timeout, an error status, an unknown state)
/// becomes <see cref="PhotoValidatorUnavailableException"/>, and the caller
/// decides what to store.
///
/// Photographs are logged by nothing here. They are children's faces.
/// </remarks>
public sealed class PhotoValidatorClient(
    HttpClient http,
    IOptions<PhotoValidationOptions> options,
    ILogger<PhotoValidatorClient> logger) : IPhotoValidator
{
    private const string ValidatePath = "/validar";
    private const string FormField = "archivo";
    private const string FormFileName = "foto.jpg";

    public async Task<PhotoVerdict> ValidateAsync(byte[] photo, CancellationToken cancellationToken)
    {
        var endpoint = Endpoint();

        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(photo);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(file, FormField, FormFileName);

        using var response = await Post(endpoint, form, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw Unavailable($"The photo validator answered {(int)response.StatusCode}.");
        }

        var body = await ReadBody(response, cancellationToken);
        return ToVerdict(body);
    }

    private Uri Endpoint()
    {
        var baseUrl = options.Value.Url;

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw Unavailable("The photo validator is not configured (Validador:Url).");
        }

        return new Uri(new Uri(baseUrl), ValidatePath);
    }

    private async Task<HttpResponseMessage> Post(
        Uri endpoint, HttpContent content, CancellationToken cancellationToken)
    {
        try
        {
            return await http.PostAsync(endpoint, content, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw Unavailable($"The photo validator could not be reached at {endpoint}.", exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient reports its own timeout as a cancellation. The caller
            // cancelling is not a failure of the validator and is not caught here.
            throw Unavailable("The photo validator did not answer in time.", exception);
        }
    }

    private async Task<ValidatorResponse> ReadBody(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var body = await response.Content.ReadFromJsonAsync<ValidatorResponse>(
                JsonSerializerOptions.Web, cancellationToken);

            return body ?? throw Unavailable("The photo validator answered with an empty body.");
        }
        catch (JsonException exception)
        {
            throw Unavailable("The photo validator answered with something that is not JSON.", exception);
        }
        catch (NotSupportedException exception)
        {
            throw Unavailable("The photo validator answered with an unsupported content type.", exception);
        }
    }

    private PhotoVerdict ToVerdict(ValidatorResponse body)
    {
        if (body.VersionReglas is null)
        {
            throw Unavailable("The photo validator answered without a rules version.");
        }

        var state = body.Estado switch
        {
            "aprobada" => PhotoVerdictState.Approved,
            "rechazada" => PhotoVerdictState.Rejected,
            _ => throw Unavailable($"The photo validator answered an unknown state: '{body.Estado}'."),
        };

        return new PhotoVerdict(
            state,
            body.Motivos ?? [],
            body.Advertencias ?? [],
            body.NoVerificado ?? [],
            body.VersionReglas,
            body.Calibrado);
    }

    private PhotoValidatorUnavailableException Unavailable(string reason, Exception? inner = null)
    {
        logger.LogWarning("Photo validation unavailable: {Reason}", reason);
        return new PhotoValidatorUnavailableException(reason, inner);
    }

    /// <summary>The validator's JSON, as documented in its own API.</summary>
    private sealed record ValidatorResponse(
        [property: JsonPropertyName("estado")] string? Estado,
        [property: JsonPropertyName("motivos")] IReadOnlyList<string>? Motivos,
        [property: JsonPropertyName("advertencias")] IReadOnlyList<string>? Advertencias,
        [property: JsonPropertyName("no_verificado")] IReadOnlyList<string>? NoVerificado,
        [property: JsonPropertyName("version_reglas")] string? VersionReglas,
        [property: JsonPropertyName("calibrado")] bool Calibrado);
}

using Microsoft.AspNetCore.Http.HttpResults;
using SportFrog.Api.Features.Athletes;
using SportFrog.Api.Infrastructure.Auth;
using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Api.Infrastructure.Storage;
using SportFrog.Api.Infrastructure.Validation;

namespace SportFrog.Api.Features.Athletes.Photos;

/// <summary>
/// Judges a photograph before it is saved, so the person uploading it can see
/// why it fails and try again with another one.
/// </summary>
/// <remarks>
/// Nothing is stored: no object in the bucket and no row. A verdict is an
/// answer, not a record, and a photograph that is only being tried must not
/// leave anything behind.
///
/// The validator is called directly rather than through
/// <see cref="PhotoAssessor"/>. That one turns a validator that does not answer
/// into "not evaluated", which is the right record for a stored photograph. Here
/// the person is waiting on the answer, so an unreachable validator is reported
/// as unavailable, and they are asked to try again instead of being given a
/// verdict nobody produced.
///
/// The photograph is normalized first, the same way a registration does, so the
/// verdict is about the picture that would actually be printed. Nothing about
/// it is logged: it is a child's face.
/// </remarks>
public static class PreviewAthletePhoto
{
    /// <summary>The same ceiling the validator service enforces on its own upload.</summary>
    internal const long MaximumUpload = 10L * 1024 * 1024;

    internal const string FormField = "archivo";

    /// <param name="Reasons">Lo que bloquea: la foto no se puede guardar.</param>
    /// <param name="Warnings">Defectos que no bloquean: se puede guardar igual.</param>
    /// <param name="Unverified">Reglas que no se revisaron. No son defectos.</param>
    public sealed record Response(
        string State,
        IReadOnlyList<string> Reasons,
        IReadOnlyList<string> Warnings,
        IReadOnlyList<string> Unverified,
        string RulesVersion,
        bool Calibrated);

    public static IEndpointRouteBuilder MapPreviewAthletePhoto(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/athletes/photos/preview", HandleAsync)
            .RequireRole(MembershipRole.Operator)
            .DisableAntiforgery()
            .WithName(nameof(PreviewAthletePhoto))
            .WithSummary("Judges a photograph without storing it, so it can be retaken before saving.");

        return routes;
    }

    internal static async Task<IResult> HandleAsync(
        IFormFile archivo,
        IPhotoValidator validator,
        CancellationToken cancellationToken)
    {
        if (archivo.Length == 0)
        {
            return Refuse("El archivo está vacío.");
        }

        if (archivo.Length > MaximumUpload)
        {
            return Refuse("La foto pesa más de 10 MB.");
        }

        var contents = await ReadAsync(archivo, cancellationToken);

        if (ImageNormalizer.Normalize(contents) is not { } photo)
        {
            return Refuse("La foto no se pudo leer como una imagen.");
        }

        try
        {
            var verdict = await validator.ValidateAsync(photo.Content, cancellationToken);

            return TypedResults.Ok(ToResponse(verdict));
        }
        catch (PhotoValidatorUnavailableException)
        {
            return TypedResults.Problem(
                title: "No se pudo validar la foto",
                detail: "El validador no respondió. Intentá de nuevo en unos minutos.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static async Task<byte[]> ReadAsync(IFormFile file, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);

        return buffer.ToArray();
    }

    private static Response ToResponse(PhotoVerdict verdict) => new(
        PhotoValidationView.ToWire(verdict.State == PhotoVerdictState.Approved
            ? PhotoValidationState.Approved
            : PhotoValidationState.Rejected),
        verdict.Reasons,
        verdict.Warnings,
        verdict.Unverified,
        verdict.RulesVersion,
        verdict.Calibrated);

    private static IResult Refuse(string message) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]>
        {
            [FormField] = [message],
        });
}

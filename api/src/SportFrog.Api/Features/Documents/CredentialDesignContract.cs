using FluentValidation;

namespace SportFrog.Api.Features.Documents;

/// <summary>
/// What a caller sends to create or correct a credential design.
/// </summary>
/// <remarks>
/// The pictures are the one place this differs from a stored value: a
/// background or logo that is a data URL is a new upload and gets stored; one
/// that is already a storage key passes through unchanged. That is what lets
/// an editor send back the design it was given without re-uploading anything.
/// </remarks>
public sealed record CredentialDesignContract(
    string Name,
    string? LegalText,
    string? BackgroundKey,
    string? AccentColorHex,
    string? LogoKey,
    bool IsDefault);

internal sealed class CredentialDesignContractValidator : AbstractValidator<CredentialDesignContract>
{
    public CredentialDesignContractValidator()
    {
        RuleFor(contract => contract.Name)
            .NotEmpty().WithMessage("El nombre del diseño es obligatorio.")
            .MaximumLength(80);

        // Bounded tightly: it prints across two narrow columns of a fixed card,
        // and nothing in the renderer shrinks or wraps it past that space.
        RuleFor(contract => contract.LegalText)
            .MaximumLength(800)
            .When(contract => contract.LegalText is not null);

        RuleFor(contract => contract.AccentColorHex)
            .Matches("^#[0-9A-Fa-f]{6}$")
            .WithMessage("El color debe tener la forma #rrggbb.")
            .When(contract => contract.AccentColorHex is not null);
    }
}

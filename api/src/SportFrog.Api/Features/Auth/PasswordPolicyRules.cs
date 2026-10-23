using FluentValidation;
using SportFrog.Domain.ValueObjects;

namespace SportFrog.Api.Features.Auth;

/// <summary>
/// The contract rule that holds a new password to
/// <see cref="Password"/>'s policy, and the one sentence that explains the
/// policy to whoever tripped over it.
/// </summary>
/// <remarks>
/// Three endpoints set a password — <see cref="ChangePassword"/>,
/// <see cref="ResetPassword"/> and the ones that create an account — and all
/// of them owe the caller the same explanation. Written out at each, the
/// sentences drift: one of them gets the special-character requirement added
/// and the others go on describing a policy the system no longer has.
///
/// The policy itself is not here. It lives in <see cref="Password"/>, where
/// it belongs; this only states it in the language the API answers in.
/// </remarks>
internal static class PasswordPolicyRules
{
    /// <summary>What a rejected password failed to be.</summary>
    internal static readonly string Unsatisfied =
        $"La contraseña debe tener al menos {Password.MinimumLength} caracteres e " +
        "incluir una mayúscula, una minúscula, un número y un carácter especial.";

    /// <summary>
    /// Requires the value to satisfy the password policy, reported in the
    /// terms above.
    /// </summary>
    internal static IRuleBuilderOptions<T, string> MustSatisfyPasswordPolicy<T>(
        this IRuleBuilder<T, string> rule) =>
        rule
            .Must(password => Password.TryParse(password, out _))
            .WithMessage(Unsatisfied);
}

using System.Globalization;
using SportFrog.Api.Infrastructure.Email;

namespace SportFrog.Api.Features.Auth;

/// <summary>
/// The one mail password recovery sends: where it points, and what it says
/// in both the plain-text and the HTML a client might render.
/// </summary>
/// <remarks>
/// Separate from <see cref="SendPasswordResetJob"/>, which mints the token
/// and hands it to <see cref="IEmailSender"/>: composing a message and
/// delivering one fail differently and are worth reading apart. Everything
/// here is a pure function of its arguments, which is also what makes the
/// wording and the link testable without a relay or a database.
/// </remarks>
internal static class PasswordResetMail
{
    public const string Subject = "Restablecé tu contraseña de SportFrog";

    /// <summary>
    /// The address the mail points at: the frontend's reset page, with the
    /// token appended.
    /// </summary>
    /// <remarks>
    /// Escaped rather than interpolated raw. The token is base64url and has
    /// nothing in it that needs escaping today — see
    /// <c>OpaqueSecret.Base64UrlEncode</c> — which is precisely the kind of
    /// fact that stops being true quietly, and a link is not where that
    /// should first be noticed.
    ///
    /// Only the token travels, never the address: a mailed URL ends up in
    /// browser history, in access logs and in the Referer header of anything
    /// the page loads, and the account it belongs to is already known from
    /// the token itself.
    /// </remarks>
    public static string Link(string baseUrl, string token) =>
        $"{baseUrl.TrimEnd('/')}?token={Uri.EscapeDataString(token)}";

    public static string Body(string link, TimeSpan lifetime) =>
        "Alguien pidió restablecer la contraseña de esta cuenta de SportFrog.\n\n" +
        $"Para elegir una nueva, abrí este enlace:\n{link}\n\n" +
        $"El enlace vence en {Vigencia(lifetime)} y sirve una sola vez.\n\n" +
        "Si no lo pediste, no hace falta que hagas nada: tu contraseña actual " +
        "sigue siendo la única que funciona.\n\n" +
        "Este es un aviso automático de SportFrog.";

    /// <summary>
    /// The same message as <see cref="Body"/>, with the line the site uses —
    /// see <see cref="EmailTemplate"/> for why it is tables and inline styles
    /// rather than the real CSS the frontend has.
    /// </summary>
    public static string HtmlBody(string link, TimeSpan lifetime) =>
        EmailTemplate.Render(
            "Restablecer contraseña",
            "Alguien pidió restablecer la contraseña de esta cuenta. Si no fuiste vos, " +
            "podés ignorar este mensaje: tu contraseña actual sigue siendo la única que funciona.",
            [
                ("Vigencia del enlace", Vigencia(lifetime)),
                ("Usos", "Uno solo"),
            ],
            ("Elegir una nueva contraseña", link));

    /// <summary>
    /// How long the link lasts, said the way a person would say it rather
    /// than as a <see cref="TimeSpan"/>.
    /// </summary>
    private static string Vigencia(TimeSpan lifetime)
    {
        var hours = (int)lifetime.TotalHours;

        if (hours >= 1)
        {
            return hours == 1 ? "1 hora" : $"{hours} horas";
        }

        var minutes = Math.Max(1, (int)lifetime.TotalMinutes);

        return minutes == 1
            ? "1 minuto"
            : string.Format(CultureInfo.InvariantCulture, "{0} minutos", minutes);
    }
}

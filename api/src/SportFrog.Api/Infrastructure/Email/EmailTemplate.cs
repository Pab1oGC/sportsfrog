using System.Net;
using System.Text;

namespace SportFrog.Api.Infrastructure.Email;

/// <summary>
/// The one HTML shell every outgoing notice email is rendered through, so a
/// visitor's inbox carries the same brand as the site instead of the bare
/// "\n"-joined string it used to be.
/// </summary>
/// <remarks>
/// Table-based layout and inline styles only — no &lt;style&gt; block, no web
/// font, no external image. That is not a stylistic choice to match the
/// site's own build (which does use a real font and CSS) — it is what
/// actually survives Outlook's Word rendering engine and a stripped-down
/// webmail client. The same Ocean Breeze blues the site uses for its
/// "ink"/accent color are used here too (see playful-hero.jsx on the
/// frontend), but through inline hex on table cells, the one styling
/// mechanism guaranteed to reach every inbox.
///
/// Every string that does not come from this file is untrusted: a team or
/// club name is free text an organizer typed, and this is HTML now, not a
/// plain-text line where that never mattered. <see cref="Render"/>
/// HTML-encodes every one of them itself, once, so no caller has to
/// remember to — the same reasoning <c>EmailTemplate</c> exists at all
/// instead of each job building its own markup.
/// </remarks>
internal static class EmailTemplate
{
    private const string AzulOscuro = "#1D709F";
    private const string AzulClaro = "#4CB8E6";
    private const string CeladesteFondo = "#E7F5FD";
    private const string Tinta = "#111315";

    public static string Render(string etiquetaSuperior, string mensaje, IReadOnlyList<(string Etiqueta, string Valor)> filas)
    {
        var sb = new StringBuilder();

        sb.Append("<!DOCTYPE html><html lang=\"es\"><body style=\"margin:0;padding:0;background-color:")
          .Append(CeladesteFondo)
          .Append(";font-family:Verdana,Geneva,sans-serif;\">");

        sb.Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"background-color:")
          .Append(CeladesteFondo)
          .Append(";padding:32px 16px;\"><tr><td align=\"center\">");

        sb.Append("<table role=\"presentation\" width=\"480\" cellpadding=\"0\" cellspacing=\"0\" style=\"max-width:480px;width:100%;background-color:#ffffff;border:3px solid ")
          .Append(AzulOscuro)
          .Append(";border-radius:16px;overflow:hidden;\">");

        // Franja superior: el mismo "chip" de logo que ya usa el navbar del
        // sitio (caja de color + "SF"), en texto -- una imagen externa
        // necesitaria un dominio publico estable para alojarla, y muchos
        // clientes de correo no cargan imagenes remotas por default de
        // entrada.
        sb.Append("<tr><td style=\"background-color:")
          .Append(AzulOscuro)
          .Append(";padding:20px 24px;\"><table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\"><tr>")
          .Append("<td style=\"background-color:")
          .Append(AzulClaro)
          .Append(";border-radius:8px;width:36px;height:36px;text-align:center;vertical-align:middle;\">")
          .Append("<span style=\"color:#ffffff;font-weight:bold;font-size:15px;\">SF</span></td>")
          .Append("<td style=\"padding-left:10px;color:#ffffff;font-size:18px;font-weight:bold;\">SportFrog</td>")
          .Append("</tr></table></td></tr>");

        sb.Append("<tr><td style=\"padding:28px 24px;\">");
        sb.Append("<p style=\"margin:0 0 8px;color:#00A4D1;font-size:12px;font-weight:bold;letter-spacing:1px;text-transform:uppercase;\">")
          .Append(WebUtility.HtmlEncode(etiquetaSuperior))
          .Append("</p>");
        sb.Append("<p style=\"margin:0 0 20px;color:")
          .Append(Tinta)
          .Append(";font-size:16px;line-height:1.5;\">")
          .Append(WebUtility.HtmlEncode(mensaje))
          .Append("</p>");

        sb.Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"border-collapse:separate;border-spacing:0 8px;\">");
        foreach (var (etiqueta, valor) in filas)
        {
            sb.Append("<tr><td style=\"background-color:")
              .Append(CeladesteFondo)
              .Append(";border-radius:8px;padding:10px 14px;\">")
              .Append("<span style=\"color:")
              .Append(AzulOscuro)
              .Append(";font-size:11px;font-weight:bold;text-transform:uppercase;display:block;\">")
              .Append(WebUtility.HtmlEncode(etiqueta))
              .Append("</span><span style=\"color:")
              .Append(Tinta)
              .Append(";font-size:14px;\">")
              .Append(WebUtility.HtmlEncode(valor))
              .Append("</span></td></tr>");
        }
        sb.Append("</table></td></tr>");

        sb.Append("<tr><td style=\"padding:16px 24px;border-top:2px solid ")
          .Append(CeladesteFondo)
          .Append(";\"><p style=\"margin:0;color:#8a9aa5;font-size:11px;\">Este es un aviso autom&#225;tico de SportFrog.</p></td></tr>");

        sb.Append("</table></td></tr></table></body></html>");

        return sb.ToString();
    }
}

using System.Globalization;

namespace SportFrog.Domain.Documents;

/// <summary>
/// Who or what a document is about, gathered once.
/// </summary>
/// <remarks>
/// Read in one query for the whole batch rather than one per card. Four
/// hundred credentials asking the database four hundred times for a club name
/// that is the same club name is the difference between a batch that takes a
/// minute and one that takes twenty.
/// </remarks>
public sealed record DocumentSubject(
    Guid? AthleteId,
    Guid? TeamId,
    string Label,
    string FirstName,
    string LastName,
    string? PhotoKey,
    string? Document,
    DateOnly? BirthDate,
    short? JerseyNumber,
    string? Position,
    string? TeamName,
    string? ClubName,
    string? CategoryName);

/// <summary>What is true of every document in the batch.</summary>
public sealed record DocumentContext(
    string OrganizationName,
    string OrganizationSlug,
    string CompetitionName,
    string Season);

/// <summary>One document, ready to be drawn.</summary>
public sealed record DocumentPrint(
    DocumentSubject Subject,
    DocumentContext Context,
    string Serial,
    string VerifyUrl,
    string? CertificateType,
    DateOnly? ValidFrom,
    DateOnly? ValidTo,
    DateTimeOffset IssuedAt);

/// <summary>
/// Turns a field's source into the words that go on the card.
/// </summary>
/// <remarks>
/// The other half of <see cref="TemplateDesign"/>: that one publishes what may
/// be asked for, this one answers it. They are two lists that have to agree,
/// and the way they are kept agreeing is that a source the catalogue does not
/// publish is refused when the layout is saved, so nothing unknown ever
/// reaches here.
///
/// An answer of null means the card leaves that space blank. That is
/// deliberate for most fields: a player with no shirt number yet should get a
/// credential without one rather than no credential. The one exception is the
/// photograph, which the batch checks for before it starts — a credential with
/// a hole where the face goes is not a credential.
/// </remarks>
public static class DocumentValues
{
    /// <summary>
    /// Dates are printed the way they are read here, not the way they are
    /// stored. The wire format is for machines.
    /// </summary>
    private const string DateFormat = "dd/MM/yyyy";

    public static string? Text(string source, DocumentPrint print)
    {
        var subject = print.Subject;
        var context = print.Context;

        return source switch
        {
            "athlete.full_name" => subject.Label,
            "athlete.first_name" => Blank(subject.FirstName),
            "athlete.last_name" => Blank(subject.LastName),
            "athlete.document" => Blank(subject.Document),
            "athlete.birth_date" => subject.BirthDate?.ToString(DateFormat, CultureInfo.InvariantCulture),

            "roster.jersey_number" => subject.JerseyNumber?.ToString(CultureInfo.InvariantCulture),
            "roster.position" => Blank(subject.Position),

            "team.name" => Blank(subject.TeamName),
            "club.name" => Blank(subject.ClubName),
            "category.name" => Blank(subject.CategoryName),

            "competition.name" => Blank(context.CompetitionName),
            "competition.season" => Blank(context.Season),
            "organization.name" => Blank(context.OrganizationName),

            "certificate.type" => Blank(print.CertificateType),

            "document.serial" => print.Serial,
            "document.issued_at" => print.IssuedAt.ToString(DateFormat, CultureInfo.InvariantCulture),
            "document.valid_from" => print.ValidFrom?.ToString(DateFormat, CultureInfo.InvariantCulture),
            "document.valid_to" => print.ValidTo?.ToString(DateFormat, CultureInfo.InvariantCulture),

            // 'text' is the designer's own words and never comes through here;
            // it is read straight off the field. Anything else was refused when
            // the layout was saved.
            _ => null,
        };
    }

    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}

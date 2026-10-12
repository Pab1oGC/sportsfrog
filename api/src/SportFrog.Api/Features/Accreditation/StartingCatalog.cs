using SportFrog.Api.Infrastructure.Persistence.Entities;
using SportFrog.Domain.Accreditation;

namespace SportFrog.Api.Features.Accreditation;

/// <summary>
/// The starting accreditation catalogue a football competition is created
/// with, so an organizer does not open an empty screen.
/// </summary>
/// <remarks>
/// The codes come from the Pan American Sports Organization's Accreditation
/// Users' Guide — see the header of the seed migration this mirrors. Scoped to
/// football by design, the same as that migration.
///
/// The seed migration <c>20261003012208_SeedFootballAccreditationCatalog</c>
/// is history and is never edited. This class is the source for every
/// competition created from now on; if the starting content changes, it
/// changes here and gets a new migration for the competitions that already
/// exist, not an edit to the one that ran.
/// </remarks>
public static class StartingCatalog
{
    public const string FootballSportCode = "football";

    /// <summary>Whether a competition of this sport starts with a catalogue.</summary>
    public static bool HasFor(string sportCode) => sportCode == FootballSportCode;

    /// <summary>
    /// The rows that make up the catalogue for one competition, ready to be
    /// added to the same unit of work that creates the competition.
    /// </summary>
    public static Rows Build(Competition competition, DateTimeOffset now)
    {
        var items = new List<AccreditationItem>();

        AccreditationItem Item(AccreditationItemKind kind, string code, string name, short order, string? colorHex = null) =>
            new()
            {
                Id = Guid.NewGuid(),
                OrgId = competition.OrgId,
                CompetitionId = competition.Id,
                Kind = kind,
                Code = code,
                Name = name,
                ColorHex = colorHex,
                DisplayOrder = order,
                CreatedAt = now,
                UpdatedAt = now,
            };

        var discipline = Item(AccreditationItemKind.Discipline, "FUT", "Fútbol", 0);
        var village = Item(AccreditationItemKind.Venue, "VIL", "Villa / Concentración", 0);
        var press = Item(AccreditationItemKind.Venue, "MPC", "Centro de Prensa", 1);
        var stadiums = Item(AccreditationItemKind.Venue, "EST", "Estadios de competencia", 2);
        var transport = Item(AccreditationItemKind.Service, "TA", "Transporte de atletas", 0);
        var dining = Item(AccreditationItemKind.Service, "COM", "Comedor", 1);
        var fieldZone = Item(AccreditationItemKind.Zone, "AZUL", "Campo de juego y áreas operativas", 0, "#1F3864");
        var preparation = Item(AccreditationItemKind.Zone, "2", "Área de preparación de atletas", 1);
        var residential = Item(AccreditationItemKind.Zone, "R", "Zona residencial de la villa", 2);

        items.AddRange([
            discipline, village, press, stadiums, transport, dining, fieldZone, preparation, residential,
        ]);

        var categories = new List<AccreditationCategory>
        {
            CategoryRow("Aa", "Deportista", "#1F3864", 0),
            CategoryRow("Ao", "Cuerpo técnico", "#6B2D90", 1),
        };

        AccreditationCategory CategoryRow(string code, string name, string colorHex, short order) =>
            new()
            {
                Id = Guid.NewGuid(),
                OrgId = competition.OrgId,
                CompetitionId = competition.Id,
                Code = code,
                Name = name,
                ColorHex = colorHex,
                DisplayOrder = order,
                CreatedAt = now,
                UpdatedAt = now,
            };

        // Both categories carry the same package: a team's technical staff
        // needs the same doors onto the field of play that an athlete does.
        var links = categories
            .SelectMany(category => items.Select(item => new AccreditationCategoryItem
            {
                OrgId = competition.OrgId,
                CompetitionId = competition.Id,
                CategoryId = category.Id,
                ItemId = item.Id,
            }))
            .ToList();

        return new Rows(items, categories, links);
    }

    public sealed record Rows(
        IReadOnlyList<AccreditationItem> Items,
        IReadOnlyList<AccreditationCategory> Categories,
        IReadOnlyList<AccreditationCategoryItem> Links);
}

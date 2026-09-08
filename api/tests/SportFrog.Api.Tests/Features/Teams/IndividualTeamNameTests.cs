using AwesomeAssertions;
using SportFrog.Api.Features.Teams;

namespace SportFrog.Api.Tests.Features.Teams;

/// <summary>
/// What an individual-sport team is called, derived from who is on it —
/// one athlete, a pair, or a small team, exactly the shapes poomsae runs.
/// </summary>
public sealed class IndividualTeamNameTests
{
    [Fact]
    public void From_OneAthlete_IsJustTheirName()
    {
        IndividualTeamName.From([("Ana", "Rojas")]).Should().Be("Ana Rojas");
    }

    [Fact]
    public void From_TwoAthletes_JoinsBothNamesInOrder()
    {
        IndividualTeamName.From([("Ana", "Rojas"), ("Beto", "Gomez")])
            .Should().Be("Ana Rojas / Beto Gomez");
    }

    [Fact]
    public void From_ThreeAthletes_JoinsAllThree()
    {
        IndividualTeamName.From([("Ana", "Rojas"), ("Beto", "Gomez"), ("Cami", "Diaz")])
            .Should().Be("Ana Rojas / Beto Gomez / Cami Diaz");
    }
}

using AwesomeAssertions;
using SportFrog.Domain.Competitions;
using SportFrog.Domain.Rules;

namespace SportFrog.Domain.Tests.Competitions;

/// <summary>
/// <see cref="CompetitionStages.For"/>: which stages a competition's own
/// draw actually gives it.
/// </summary>
public sealed class CompetitionStagesTests
{
    [Fact]
    public void For_League_IsOneStage()
    {
        CompetitionStages.For(CompetitionFormat.League, ScoreMode.Cumulative)
            .Should().Equal(StageKind.League);
    }

    [Fact]
    public void For_Knockout_IsOneBracket()
    {
        CompetitionStages.For(CompetitionFormat.Knockout, ScoreMode.Sets)
            .Should().Equal(StageKind.Bracket);
    }

    [Fact]
    public void For_Groups_RunsAGroupStageThenABracket()
    {
        CompetitionStages.For(CompetitionFormat.Groups, ScoreMode.Cumulative)
            .Should().Equal(StageKind.Groups, StageKind.Bracket);
    }

    [Fact]
    public void For_GroupsUnderAJudgedSport_RunsClassificationInsteadOfAGroupStage()
    {
        // No hay nada que tabular en una tabla de posiciones bajo un puntaje
        // de jueces, así que la primera fase es la de clasificación -- no
        // depende del formato sino del deporte.
        CompetitionStages.For(CompetitionFormat.Groups, ScoreMode.Judged)
            .Should().Equal(StageKind.Classification, StageKind.Bracket);
    }

    [Theory]
    [InlineData(ScoreMode.Cumulative)]
    [InlineData(ScoreMode.Sets)]
    [InlineData(ScoreMode.Judged)]
    public void For_LeagueAndKnockout_DoNotDependOnScoreMode(ScoreMode scoreMode)
    {
        CompetitionStages.For(CompetitionFormat.League, scoreMode).Should().Equal(StageKind.League);
        CompetitionStages.For(CompetitionFormat.Knockout, scoreMode).Should().Equal(StageKind.Bracket);
    }

    [Fact]
    public void For_UnknownFormat_Throws()
    {
        var act = () => CompetitionStages.For("torneo-relampago", ScoreMode.Cumulative);

        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("cuartos")]
    public void KindOfMatch_League_IsAlwaysLeague(string? phase)
    {
        CompetitionStages.KindOfMatch(CompetitionFormat.League, ScoreMode.Cumulative, phase)
            .Should().Be(StageKind.League);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("final")]
    public void KindOfMatch_Knockout_IsAlwaysBracket(string? phase)
    {
        CompetitionStages.KindOfMatch(CompetitionFormat.Knockout, ScoreMode.Sets, phase)
            .Should().Be(StageKind.Bracket);
    }

    [Fact]
    public void KindOfMatch_Groups_WithNoPhase_IsTheGroupStage()
    {
        CompetitionStages.KindOfMatch(CompetitionFormat.Groups, ScoreMode.Cumulative, phase: null)
            .Should().Be(StageKind.Groups);
    }

    [Fact]
    public void KindOfMatch_Groups_WithAPhase_IsTheBracket()
    {
        CompetitionStages.KindOfMatch(CompetitionFormat.Groups, ScoreMode.Cumulative, phase: "semifinal")
            .Should().Be(StageKind.Bracket);
    }

    [Fact]
    public void KindOfMatch_GroupsUnderAJudgedSport_IsAlwaysTheBracket()
    {
        // La clasificación nunca llega a ser un Match (se juega como
        // Performance, ver OpenClassificationStage), así que todo partido
        // real de una categoría juzgada por grupos es de la llave, tenga o
        // no fase asignada.
        CompetitionStages.KindOfMatch(CompetitionFormat.Groups, ScoreMode.Judged, phase: null)
            .Should().Be(StageKind.Bracket);
        CompetitionStages.KindOfMatch(CompetitionFormat.Groups, ScoreMode.Judged, phase: "final")
            .Should().Be(StageKind.Bracket);
    }
}

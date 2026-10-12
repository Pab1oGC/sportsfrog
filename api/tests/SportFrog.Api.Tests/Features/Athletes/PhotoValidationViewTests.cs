using AwesomeAssertions;
using SportFrog.Api.Features.Athletes;
using SportFrog.Api.Infrastructure.Persistence.Entities;

namespace SportFrog.Api.Tests.Features.Athletes;

public sealed class PhotoValidationViewTests
{
    private static readonly DateTimeOffset ValidatedAt = new(2026, 10, 7, 12, 30, 0, TimeSpan.Zero);

    [Fact]
    public void A_rejected_athlete_is_shown_with_every_reason_warning_and_version()
    {
        var athlete = NewAthlete();
        athlete.PhotoValidationState = PhotoValidationState.Rejected;
        athlete.PhotoValidationReasons = ["Se ven lentes de sol."];
        athlete.PhotoValidationWarnings = ["No verificado: boca cerrada."];
        athlete.PhotoRulesVersion = "0.1.0";
        athlete.PhotoValidatedAt = ValidatedAt;

        var view = PhotoValidationView.From(athlete);

        view.State.Should().Be("rejected");
        view.Reasons.Should().Equal("Se ven lentes de sol.");
        view.Warnings.Should().Equal("No verificado: boca cerrada.");
        view.RulesVersion.Should().Be("0.1.0");
        view.ValidatedAt.Should().Be(ValidatedAt);
    }

    [Fact]
    public void An_athlete_nobody_has_checked_is_shown_as_not_evaluated_with_nothing_else()
    {
        var view = PhotoValidationView.From(NewAthlete());

        view.State.Should().Be("not_evaluated");
        view.Reasons.Should().BeEmpty();
        view.Warnings.Should().BeEmpty();
        view.RulesVersion.Should().BeNull();
        view.ValidatedAt.Should().BeNull();
    }

    [Theory]
    [InlineData(PhotoValidationState.NotEvaluated, "not_evaluated")]
    [InlineData(PhotoValidationState.Approved, "approved")]
    [InlineData(PhotoValidationState.Rejected, "rejected")]
    public void Each_state_has_the_wire_name_clients_compare_against(PhotoValidationState state, string expected) =>
        PhotoValidationView.ToWire(state).Should().Be(expected);

    private static Athlete NewAthlete() => new()
    {
        FirstName = "Ana",
        LastName = "Pérez",
        DocumentId = "1234567",
    };
}

using AwesomeAssertions;
using SportFrog.Api.Features.Performances;

namespace SportFrog.Api.Tests.Features.Performances;

/// <summary>
/// Que se compare la arma final completa -- no un movimiento a la vez -- es
/// lo que hace posible intercambiar el turno de dos equipos sin que ninguno
/// de los dos choque con el lugar del otro a mitad de camino.
/// </summary>
public sealed class PerformanceOrderConflictsTests
{
    private static readonly DateOnly Day1 = new(2026, 5, 1);
    private static readonly DateOnly Day2 = new(2026, 5, 2);
    private static readonly Guid MatA = Guid.NewGuid();
    private static readonly Guid MatB = Guid.NewGuid();

    private static OrderSlot Slot(Guid? venue, DateOnly? day, short? order, Guid? performanceId = null) =>
        new(performanceId ?? Guid.NewGuid(), "Equipo", venue, day, order);

    [Fact]
    public void NingunMovimiento_SinNada_Que_Choque_NoDaViolaciones()
    {
        var moved = new[] { (0, Slot(MatA, Day1, 1)) };

        var violations = PerformanceOrderConflicts.Find(moved, others: []);

        violations.Should().BeEmpty();
    }

    [Fact]
    public void DosMovimientos_MismoTapete_MismoDia_MismoTurno_ChocanEntreSi()
    {
        var moved = new[]
        {
            (0, Slot(MatA, Day1, 1)),
            (1, Slot(MatA, Day1, 1)),
        };

        var violations = PerformanceOrderConflicts.Find(moved, others: []);

        violations.Should().ContainKey(0);
        violations.Should().ContainKey(1);
    }

    [Fact]
    public void Swap_DeDosTurnos_NoChoca()
    {
        // El caso que motivo este archivo: el equipo A toma el turno 1 (que
        // tenia el B) y el B toma el turno 2 (que tenia el A). Pedido uno a
        // la vez contra SchedulePerformance, el primero de los dos siempre
        // choca con el que todavia no se movio.
        var perfA = Guid.NewGuid();
        var perfB = Guid.NewGuid();
        var moved = new[]
        {
            (0, Slot(MatA, Day1, 1, perfA)),
            (1, Slot(MatA, Day1, 2, perfB)),
        };

        var violations = PerformanceOrderConflicts.Find(moved, others: []);

        violations.Should().BeEmpty();
    }

    [Fact]
    public void DistintoTapete_MismoTurno_NoChoca()
    {
        var moved = new[]
        {
            (0, Slot(MatA, Day1, 1)),
            (1, Slot(MatB, Day1, 1)),
        };

        var violations = PerformanceOrderConflicts.Find(moved, others: []);

        violations.Should().BeEmpty();
    }

    [Fact]
    public void DistintoDia_MismoTurno_NoChoca()
    {
        var moved = new[]
        {
            (0, Slot(MatA, Day1, 1)),
            (1, Slot(MatA, Day2, 1)),
        };

        var violations = PerformanceOrderConflicts.Find(moved, others: []);

        violations.Should().BeEmpty();
    }

    [Fact]
    public void Movimiento_ChocaConUnaActuacionQueNadieMueve_SeReportaSoloLaMovida()
    {
        var quieta = Slot(MatA, Day1, 1);
        var moved = new[] { (0, Slot(MatA, Day1, 1)) };

        var violations = PerformanceOrderConflicts.Find(moved, others: [quieta]);

        violations.Should().ContainKey(0);
        violations.Should().HaveCount(1);
    }

    [Fact]
    public void MovimientoSinTurnoCompleto_NuncaChoca()
    {
        // Sacar a un equipo del orden (sin tapete, dia o turno) para hacerle
        // lugar a otro no es "un movimiento mas" que pueda chocar.
        var moved = new[]
        {
            (0, Slot(venue: null, day: null, order: null)),
            (1, Slot(MatA, Day1, 1)),
        };

        var violations = PerformanceOrderConflicts.Find(moved, others: []);

        violations.Should().BeEmpty();
    }
}

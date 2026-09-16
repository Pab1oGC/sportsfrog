using AwesomeAssertions;
using SportFrog.Api.Features.Matches;

namespace SportFrog.Api.Tests.Features.Matches;

/// <summary>
/// Que se compare la arma final completa -- no un movimiento a la vez -- es
/// lo que hace posible un swap entre dos partidos sin que ninguno de los dos
/// choque con el lugar del otro a mitad de camino.
/// </summary>
public sealed class BulkRescheduleConflictsTests
{
    private static readonly DateTimeOffset At10 = new(2026, 5, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset At15 = new(2026, 5, 1, 15, 0, 0, TimeSpan.Zero);
    private static readonly Guid CourtA = Guid.NewGuid();
    private static readonly Guid CourtB = Guid.NewGuid();

    private static ScheduledSlot Slot(
        Guid? venue, DateTimeOffset? at, Guid? matchId = null, Guid? home = null, Guid? away = null,
        IReadOnlyCollection<Guid>? athletes = null) =>
        new(matchId ?? Guid.NewGuid(), home ?? Guid.NewGuid(), "Local", away ?? Guid.NewGuid(), "Visitante",
            venue, at, athletes ?? []);

    [Fact]
    public void NingunMovimiento_SinNada_Que_Choque_NoDaViolaciones()
    {
        var moved = new[] { (0, Slot(CourtA, At10)) };

        var violations = BulkRescheduleConflicts.Find(moved, others: []);

        violations.Should().BeEmpty();
    }

    [Fact]
    public void DosMovimientos_MismaCancha_MismaHora_ChocanEntreSi()
    {
        var moved = new[]
        {
            (0, Slot(CourtA, At10)),
            (1, Slot(CourtA, At10)),
        };

        var violations = BulkRescheduleConflicts.Find(moved, others: []);

        violations.Should().ContainKey(0);
        violations.Should().ContainKey(1);
    }

    [Fact]
    public void DosMovimientos_MismoEquipo_MismaHora_ChocanEntreSi()
    {
        var equipo = Guid.NewGuid();
        var moved = new[]
        {
            (0, Slot(CourtA, At10, home: equipo)),
            (1, Slot(CourtB, At10, away: equipo)),
        };

        var violations = BulkRescheduleConflicts.Find(moved, others: []);

        violations.Should().ContainKey(0);
        violations.Should().ContainKey(1);
    }

    [Fact]
    public void Swap_DeDosPartidos_NoChoca()
    {
        // Exactamente el caso que motivo este archivo: A toma el lugar de B
        // (cancha A, 10hs) y B toma el lugar de A (cancha B, 15hs). Pedido
        // uno a la vez contra RescheduleMatch, el primero de los dos
        // siempre choca con el que todavia no se movio.
        var matchA = Guid.NewGuid();
        var matchB = Guid.NewGuid();
        var moved = new[]
        {
            (0, Slot(CourtA, At10, matchId: matchA)),
            (1, Slot(CourtB, At15, matchId: matchB)),
        };

        var violations = BulkRescheduleConflicts.Find(moved, others: []);

        violations.Should().BeEmpty();
    }

    [Fact]
    public void Movimiento_ChocaConUnPartidoQueNadieMueve_SeReportaSoloElMovido()
    {
        var quieto = Slot(CourtA, At10);
        var moved = new[] { (0, Slot(CourtA, At10)) };

        var violations = BulkRescheduleConflicts.Find(moved, others: [quieto]);

        violations.Should().ContainKey(0);
        // El partido quieto no esta en `moved`, asi que no hay un indice de
        // pedido para el -- nada que mover de su lado, nada que reportarle.
        violations.Should().HaveCount(1);
    }

    [Fact]
    public void DosPartidosQuietos_QueYaCoexistian_NoSeReportan()
    {
        // Dos partidos que ya convivian sin problema (canchas distintas) no
        // tienen por que aparecer en la respuesta solo porque comparten un
        // equipo con algo que si se esta moviendo a otra hora.
        var equipo = Guid.NewGuid();
        var quieto1 = Slot(CourtA, At10, home: equipo);
        var quieto2 = Slot(CourtB, At15, away: equipo);
        var moved = new[] { (0, Slot(CourtA, new DateTimeOffset(2026, 5, 2, 9, 0, 0, TimeSpan.Zero))) };

        var violations = BulkRescheduleConflicts.Find(moved, others: [quieto1, quieto2]);

        violations.Should().BeEmpty();
    }

    [Fact]
    public void DosMovimientos_EquiposDistintos_MismoDeportista_ChocanEntreSi()
    {
        // El mismo deportista, anotado bajo dos equipos distintos -- otra
        // categoria, otra competencia -- no puede estar en dos cancha a la
        // misma hora aunque los dos equipos no compartan ningun id.
        var deportista = Guid.NewGuid();
        var moved = new[]
        {
            (0, Slot(CourtA, At10, athletes: [deportista])),
            (1, Slot(CourtB, At10, athletes: [deportista])),
        };

        var violations = BulkRescheduleConflicts.Find(moved, others: []);

        violations.Should().ContainKey(0);
        violations.Should().ContainKey(1);
    }

    [Fact]
    public void DosMovimientos_EquiposDistintos_SinDeportistasEnComun_NoChocan()
    {
        var moved = new[]
        {
            (0, Slot(CourtA, At10, athletes: [Guid.NewGuid()])),
            (1, Slot(CourtB, At10, athletes: [Guid.NewGuid()])),
        };

        var violations = BulkRescheduleConflicts.Find(moved, others: []);

        violations.Should().BeEmpty();
    }

    [Fact]
    public void Movimiento_ComparteDeportistaConUnPartidoQueNadieMueve_SeReporta()
    {
        var deportista = Guid.NewGuid();
        var quieto = Slot(CourtB, At10, athletes: [deportista]);
        var moved = new[] { (0, Slot(CourtA, At10, athletes: [deportista])) };

        var violations = BulkRescheduleConflicts.Find(moved, others: [quieto]);

        violations.Should().ContainKey(0);
    }

    [Fact]
    public void MovimientoSinHora_NuncaChoca()
    {
        // Un partido que se saca del calendario (sin cancha ni hora) para
        // hacerle lugar a otro no es "un movimiento mas" que pueda chocar --
        // es justo el primer paso del baile de tres pasos que este endpoint
        // reemplaza.
        var moved = new[]
        {
            (0, Slot(venue: null, at: null)),
            (1, Slot(CourtA, At10)),
        };

        var violations = BulkRescheduleConflicts.Find(moved, others: []);

        violations.Should().BeEmpty();
    }
}

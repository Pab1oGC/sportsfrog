import { describe, expect, it } from "vitest";
import { agruparPorRonda, soloFaseEliminatoria, agruparRepechajePorRama, agruparCalendario } from "./match-rounds";
import { FASES } from "./phase-labels";

/** Un partido mínimo, con los campos que agruparPorRonda/agruparCalendario leen. */
function partido(overrides = {}) {
  return {
    id: "m1",
    phase: null,
    roundNumber: null,
    scheduledAt: null,
    status: "scheduled",
    homeTotal: null,
    awayTotal: null,
    isRepechage: false,
    repechageBranch: null,
    ...overrides,
  };
}

describe("agruparPorRonda", () => {
  it("agrupa partidos de grupos (sin fase) por jornada, clave 'j:<ronda>'", () => {
    const grupos = agruparPorRonda([
      partido({ id: "a", roundNumber: 1 }),
      partido({ id: "b", roundNumber: 1 }),
      partido({ id: "c", roundNumber: 2 }),
    ]);

    expect(grupos.map((g) => g.clave)).toEqual(["j:1", "j:2"]);
    expect(grupos[0].matches.map((m) => m.id)).toEqual(["a", "b"]);
    expect(grupos[0].titulo).toBe("Jornada 1");
  });

  it("agrupa partidos sin ronda bajo la clave 'sin' / 'Sin jornada'", () => {
    const grupos = agruparPorRonda([partido({ id: "a", roundNumber: null })]);

    expect(grupos).toHaveLength(1);
    expect(grupos[0].clave).toBe("sin");
    expect(grupos[0].titulo).toBe("Sin jornada");
  });

  it("agrupa partidos de fase por clave 'f:<ronda>:<fase>', con título de FASES", () => {
    const grupos = agruparPorRonda([
      partido({ id: "a", phase: "cuartos", roundNumber: 1 }),
      partido({ id: "b", phase: "cuartos", roundNumber: 1 }),
      partido({ id: "c", phase: "final", roundNumber: 2 }),
    ]);

    expect(grupos.map((g) => g.clave)).toEqual(["f:1:cuartos", "f:2:final"]);
    expect(grupos[0].titulo).toBe(FASES.cuartos);
    expect(grupos[1].titulo).toBe(FASES.final);
  });

  it("una fase sin nombre conocido en FASES usa el propio código como título", () => {
    const grupos = agruparPorRonda([partido({ phase: "ronda-misteriosa", roundNumber: 1 })]);
    expect(grupos[0].titulo).toBe("ronda-misteriosa");
  });

  it("misma ronda numérica en fase de grupos y de eliminatoria no se mezclan (la fase manda)", () => {
    // Una categoría que pasó de grupos a eliminatoria reinicia su numeración
    // de ronda en el cruce -- "ronda 1" de grupos y "ronda 1" de la llave no
    // son el mismo grupo, así que deben quedar en columnas separadas.
    const grupos = agruparPorRonda([
      partido({ id: "grupo", roundNumber: 1, phase: null }),
      partido({ id: "llave", roundNumber: 1, phase: "octavos" }),
    ]);

    expect(grupos).toHaveLength(2);
    expect(grupos[0].clave).toBe("j:1");
    expect(grupos[1].clave).toBe("f:1:octavos");
  });

  it("ordena: grupos (sin fase) siempre antes que cualquier fase de eliminatoria", () => {
    // roundNumber de la fase es menor, pero fase manda sobre ronda: igual va después.
    const grupos = agruparPorRonda([
      partido({ id: "final", phase: "final", roundNumber: 1 }),
      partido({ id: "jornada-3", roundNumber: 3 }),
    ]);

    expect(grupos.map((g) => g.clave)).toEqual(["j:3", "f:1:final"]);
  });

  it("dentro del mismo balde, roundNumber nulo (9999) ordena al final", () => {
    const grupos = agruparPorRonda([
      partido({ id: "sin-ronda", roundNumber: null }),
      partido({ id: "ronda-1", roundNumber: 1 }),
    ]);

    expect(grupos.map((g) => g.clave)).toEqual(["j:1", "sin"]);
  });

  it("dentro de la misma ronda, programados ordenan antes que sin programar", () => {
    const grupos = agruparPorRonda([
      partido({ id: "sin-fecha", roundNumber: 1, scheduledAt: null }),
      partido({ id: "con-fecha", roundNumber: 1, scheduledAt: "2026-05-01T10:00:00Z" }),
    ]);

    expect(grupos[0].matches.map((m) => m.id)).toEqual(["con-fecha", "sin-fecha"]);
  });

  it("dos partidos programados de la misma ronda ordenan por fecha ascendente", () => {
    const grupos = agruparPorRonda([
      partido({ id: "tarde", roundNumber: 1, scheduledAt: "2026-05-02T10:00:00Z" }),
      partido({ id: "temprano", roundNumber: 1, scheduledAt: "2026-05-01T10:00:00Z" }),
    ]);

    expect(grupos[0].matches.map((m) => m.id)).toEqual(["temprano", "tarde"]);
  });

  it("no muta el arreglo de entrada (usa slice antes de sort)", () => {
    const fixtures = [partido({ id: "b", roundNumber: 2 }), partido({ id: "a", roundNumber: 1 })];
    const original = fixtures.slice();
    agruparPorRonda(fixtures);
    expect(fixtures).toEqual(original);
  });

  describe("contadores por grupo (pendientes/jugados/terminada/enCurso)", () => {
    it("terminada=true y enCurso=false cuando no queda ningún partido pendiente", () => {
      const [g] = agruparPorRonda([
        partido({ roundNumber: 1, status: "finished", homeTotal: 2, awayTotal: 1 }),
        partido({ roundNumber: 1, status: "cancelled" }),
      ]);
      expect(g.pendientes).toBe(0);
      expect(g.terminada).toBe(true);
      expect(g.enCurso).toBe(false);
    });

    it("enCurso=true cuando hay pendientes y también hay al menos un partido con resultado", () => {
      const [g] = agruparPorRonda([
        partido({ roundNumber: 1, status: "finished", homeTotal: 2, awayTotal: 1 }),
        partido({ roundNumber: 1, status: "scheduled" }),
      ]);
      expect(g.pendientes).toBe(1);
      expect(g.jugados).toBe(1);
      expect(g.enCurso).toBe(true);
      expect(g.terminada).toBe(false);
    });

    it("enCurso=false cuando todos los partidos siguen pendientes (nada jugado aún)", () => {
      const [g] = agruparPorRonda([
        partido({ roundNumber: 1, status: "scheduled" }),
        partido({ roundNumber: 1, status: "postponed" }),
      ]);
      expect(g.jugados).toBe(0);
      expect(g.enCurso).toBe(false);
      expect(g.terminada).toBe(false);
    });

    it("in_progress cuenta como pendiente aunque ya tenga marcador en vivo", () => {
      // El marcador en vivo no es homeTotal/awayTotal (eso solo se llena al
      // cerrar el partido) -- así que in_progress sigue siendo "pendiente".
      const [g] = agruparPorRonda([partido({ roundNumber: 1, status: "in_progress" })]);
      expect(g.pendientes).toBe(1);
      expect(g.terminada).toBe(false);
    });
  });

  it("con un arreglo vacío devuelve un arreglo vacío", () => {
    expect(agruparPorRonda([])).toEqual([]);
  });
});

describe("soloFaseEliminatoria", () => {
  it("conserva solo los grupos cuya clave empieza con 'f:'", () => {
    const grupos = agruparPorRonda([
      partido({ id: "jornada", roundNumber: 1 }),
      partido({ id: "cruce", phase: "semifinal", roundNumber: 1 }),
    ]);

    const soloLlave = soloFaseEliminatoria(grupos);
    expect(soloLlave).toHaveLength(1);
    expect(soloLlave[0].clave).toBe("f:1:semifinal");
  });

  it("con solo jornadas de grupos devuelve un arreglo vacío", () => {
    const grupos = agruparPorRonda([partido({ roundNumber: 1 })]);
    expect(soloFaseEliminatoria(grupos)).toEqual([]);
  });
});

describe("agruparRepechajePorRama", () => {
  it("separa los partidos en dos ramas según repechageBranch", () => {
    const [ramaA, ramaB] = agruparRepechajePorRama([
      partido({ id: "a1", repechageBranch: 1 }),
      partido({ id: "b1", repechageBranch: 2 }),
      partido({ id: "a2", repechageBranch: 1 }),
    ]);

    expect(ramaA.clave).toBe("r:1");
    expect(ramaA.titulo).toBe("Repechaje Bronce A");
    expect(ramaA.matches.map((m) => m.id)).toEqual(["a1", "a2"]);

    expect(ramaB.clave).toBe("r:2");
    expect(ramaB.titulo).toBe("Repechaje Bronce B");
    expect(ramaB.matches.map((m) => m.id)).toEqual(["b1"]);
  });

  it("descarta en silencio los partidos sin repechageBranch reconocido (ni 1 ni 2)", () => {
    const ramas = agruparRepechajePorRama([
      partido({ id: "huerfano", repechageBranch: null }),
      partido({ id: "rama-3", repechageBranch: 3 }),
      partido({ id: "valido", repechageBranch: 1 }),
    ]);

    // Solo la rama 1 tiene partidos -- los otros dos se perdieron sin error.
    expect(ramas).toHaveLength(1);
    expect(ramas[0].matches.map((m) => m.id)).toEqual(["valido"]);
  });

  it("una rama sin ningún partido no aparece en el resultado", () => {
    const ramas = agruparRepechajePorRama([partido({ repechageBranch: 1 })]);
    expect(ramas.map((r) => r.clave)).toEqual(["r:1"]);
  });

  it("ordena los partidos de una rama por roundNumber ascendente", () => {
    const [rama] = agruparRepechajePorRama([
      partido({ id: "segundo", repechageBranch: 1, roundNumber: 2 }),
      partido({ id: "primero", repechageBranch: 1, roundNumber: 1 }),
    ]);
    expect(rama.matches.map((m) => m.id)).toEqual(["primero", "segundo"]);
  });

  it("un partido sin roundNumber en la rama se trata como 0 (va primero)", () => {
    const [rama] = agruparRepechajePorRama([
      partido({ id: "con-ronda", repechageBranch: 1, roundNumber: 1 }),
      partido({ id: "sin-ronda", repechageBranch: 1, roundNumber: null }),
    ]);
    expect(rama.matches.map((m) => m.id)).toEqual(["sin-ronda", "con-ronda"]);
  });

  it("con un arreglo vacío no devuelve ninguna rama", () => {
    expect(agruparRepechajePorRama([])).toEqual([]);
  });
});

describe("agruparCalendario", () => {
  it("sin repechaje, devuelve exactamente lo que arma agruparPorRonda", () => {
    const fixtures = [partido({ roundNumber: 1 }), partido({ roundNumber: 2, phase: "final" })];
    expect(agruparCalendario(fixtures)).toEqual(agruparPorRonda(fixtures));
  });

  it("solo con partidos de repechaje (sin normales), devuelve solo las ramas de repechaje", () => {
    const fixtures = [partido({ isRepechage: true, repechageBranch: 1 })];
    const resultado = agruparCalendario(fixtures);
    expect(resultado.map((g) => g.clave)).toEqual(["r:1"]);
  });

  it("intercala las ramas de repechaje justo antes de la final, no después", () => {
    const fixtures = [
      partido({ id: "semi", phase: "semifinal", roundNumber: 1 }),
      partido({ id: "final", phase: "final", roundNumber: 2 }),
      partido({ id: "repechaje-a", isRepechage: true, repechageBranch: 1 }),
      partido({ id: "repechaje-b", isRepechage: true, repechageBranch: 2 }),
    ];

    const resultado = agruparCalendario(fixtures);

    // semifinal, las dos ramas de repechaje, y la final al final de todo.
    expect(resultado.map((g) => g.clave)).toEqual(["f:1:semifinal", "r:1", "r:2", "f:2:final"]);
  });

  it("los partidos de repechaje nunca entran en los grupos normales de agruparPorRonda", () => {
    const fixtures = [
      partido({ id: "normal", roundNumber: 1 }),
      partido({ id: "repechaje", isRepechage: true, repechageBranch: 1, roundNumber: 1 }),
    ];

    const resultado = agruparCalendario(fixtures);
    const grupoNormal = resultado.find((g) => g.clave === "j:1");
    expect(grupoNormal.matches.map((m) => m.id)).toEqual(["normal"]);
  });

  it("con un arreglo vacío devuelve un arreglo vacío", () => {
    expect(agruparCalendario([])).toEqual([]);
  });
});

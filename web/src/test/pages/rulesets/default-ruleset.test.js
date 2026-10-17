import { describe, expect, it } from "vitest";
import { buildDefaultRulesetConfig, defaultRulesetName } from "src/pages/rulesets/default-ruleset";

const FUTBOL = {
  code: "football",
  name: "Fútbol",
  periodLabel: "tiempo",
  defaultPeriods: 2,
  periodHasClock: true,
  defaultMinutes: 45,
  defaultBreakMinutes: 15,
};

const VOLEIBOL = {
  code: "volleyball",
  name: "Vóley",
  periodLabel: "set",
  defaultPeriods: 5,
  periodHasClock: false,
  defaultMinutes: null,
  defaultBreakMinutes: null,
};

const POOMSAE = {
  code: "taekwondo_poomsae",
  name: "Taekwondo (Poomsae)",
  periodLabel: "actuación",
  defaultPeriods: 1,
  periodHasClock: false,
  defaultMinutes: null,
  defaultBreakMinutes: null,
};

describe("buildDefaultRulesetConfig", () => {
  it("un deporte con reloj saca minutos y descanso del catálogo, sin duración estimada", () => {
    expect(buildDefaultRulesetConfig(FUTBOL)).toEqual({
      periods: { count: 2, label: "Tiempo", minutes: 45, breakMinutes: 15, estimatedMinutes: null },
      points: {},
      tiebreakers: ["score_difference", "head_to_head"],
    });
  });

  it("un deporte sin reloj no manda minutos ni descanso, y sí una duración estimada", () => {
    const config = buildDefaultRulesetConfig(VOLEIBOL);

    expect(config.periods.minutes).toBeNull();
    expect(config.periods.breakMinutes).toBeNull();
    expect(config.periods.estimatedMinutes).toBe(30);
  });

  it("points siempre vacío -- sin tabla de posiciones por defecto, no por omisión", () => {
    expect(buildDefaultRulesetConfig(FUTBOL).points).toEqual({});
    expect(buildDefaultRulesetConfig(VOLEIBOL).points).toEqual({});
  });

  it("la cantidad de períodos y la etiqueta salen tal cual del catálogo, capitalizada", () => {
    const config = buildDefaultRulesetConfig(POOMSAE);

    expect(config.periods.count).toBe(1);
    expect(config.periods.label).toBe("Actuación");
  });

  it("un deporte con defaultMinutes/defaultBreakMinutes en null pero con reloj no revienta", () => {
    const sinDefaults = { ...FUTBOL, defaultMinutes: null, defaultBreakMinutes: null };

    expect(buildDefaultRulesetConfig(sinDefaults).periods).toEqual({
      count: 2, label: "Tiempo", minutes: null, breakMinutes: null, estimatedMinutes: null,
    });
  });
});

describe("defaultRulesetName", () => {
  it("nombra el reglamento por el deporte, reconocible en la lista de Reglamentos", () => {
    expect(defaultRulesetName(FUTBOL)).toBe("Fútbol — Reglamento estándar");
  });
});

import { describe, expect, it } from "vitest";
import { derivarVocabulario } from "src/pages/teams/roster-vocabulary";

describe("derivarVocabulario -- cupo", () => {
  it("sin nada declarado (ni sport ni categoría), cupo cae en 1", () => {
    expect(derivarVocabulario({ individual: true, sport: {}, categoria: {} }).cupo).toBe(1);
  });

  it("con solo el techo del deporte declarado, usa ese", () => {
    expect(derivarVocabulario({ individual: true, sport: { maxEntrySize: 3 }, categoria: {} }).cupo).toBe(3);
  });

  it("con solo el cupo de la categoría declarado, usa ese", () => {
    expect(derivarVocabulario({ individual: true, sport: {}, categoria: { maxRosterSize: 2 } }).cupo).toBe(2);
  });

  it("con los dos declarados, manda el menor -- la categoría puede achicar el techo del deporte, nunca ensancharlo", () => {
    expect(derivarVocabulario({ individual: true, sport: { maxEntrySize: 3 }, categoria: { maxRosterSize: 1 } }).cupo).toBe(1);
    expect(derivarVocabulario({ individual: true, sport: { maxEntrySize: 1 }, categoria: { maxRosterSize: 3 } }).cupo).toBe(1);
  });

  it("un cupo de 0 declarado se respeta (0 != null, no se filtra)", () => {
    expect(derivarVocabulario({ individual: true, sport: { maxEntrySize: 0 }, categoria: {} }).cupo).toBe(0);
  });
});

describe("derivarVocabulario -- soloDeportista", () => {
  it("true solo cuando es individual y el cupo es exactamente 1", () => {
    expect(derivarVocabulario({ individual: true, sport: { maxEntrySize: 1 }, categoria: {} }).soloDeportista).toBe(true);
  });

  it("false si es individual pero el cupo admite más de uno (pareja, trío)", () => {
    expect(derivarVocabulario({ individual: true, sport: { maxEntrySize: 3 }, categoria: {} }).soloDeportista).toBe(false);
  });

  it("false en un deporte de equipo, aunque el cupo termine dando 1 por algún cálculo", () => {
    expect(derivarVocabulario({ individual: false, sport: { maxEntrySize: 1 }, categoria: {} }).soloDeportista).toBe(false);
  });
});

describe("derivarVocabulario -- entityName / entityGender", () => {
  it("deporte individual con cupo 1: 'deportista', masculino", () => {
    const r = derivarVocabulario({ individual: true, sport: { maxEntrySize: 1 }, categoria: {} });
    expect(r.entityName).toBe("deportista");
    expect(r.entityGender).toBe("m");
  });

  it("deporte individual con cupo >1 (pareja/trío): 'inscripción', femenino", () => {
    const r = derivarVocabulario({ individual: true, sport: { maxEntrySize: 3 }, categoria: {} });
    expect(r.entityName).toBe("inscripción");
    expect(r.entityGender).toBe("f");
  });

  it("deporte de equipo: 'equipo', masculino", () => {
    const r = derivarVocabulario({ individual: false, sport: {}, categoria: {} });
    expect(r.entityName).toBe("equipo");
    expect(r.entityGender).toBe("m");
  });
});

import { describe, expect, it } from "vitest";
import { calcularBombos } from "src/pages/matches/draw-pots";

function equipo(overrides = {}) {
  return { name: "Equipo", groupLabel: null, seed: null, ...overrides };
}

describe("calcularBombos -- sinGrupos", () => {
  it("true en fase de grupos, con equipos, y ninguno tiene grupo asignado todavía", () => {
    const { sinGrupos } = calcularBombos({ teams: [equipo()], formato: "groups", groupCount: "", allowSamePot: false });
    expect(sinGrupos).toBe(true);
  });

  it("false si algún equipo ya tiene grupo asignado", () => {
    const { sinGrupos } = calcularBombos({ teams: [equipo({ groupLabel: "A" })], formato: "groups", groupCount: "", allowSamePot: false });
    expect(sinGrupos).toBe(false);
  });

  it("false fuera de fase de grupos, aunque no haya equipos con grupo", () => {
    const { sinGrupos } = calcularBombos({ teams: [equipo()], formato: "knockout", groupCount: "", allowSamePot: false });
    expect(sinGrupos).toBe(false);
  });

  it("false sin ningún equipo todavía cargado", () => {
    const { sinGrupos } = calcularBombos({ teams: [], formato: "groups", groupCount: "", allowSamePot: false });
    expect(sinGrupos).toBe(false);
  });
});

describe("calcularBombos -- agrupación por bombo", () => {
  it("agrupa los nombres de equipo por su número de bombo (seed)", () => {
    const { bombos, bombosNumerados } = calcularBombos({
      teams: [equipo({ name: "A", seed: 1 }), equipo({ name: "B", seed: 1 }), equipo({ name: "C", seed: 2 })],
      formato: "groups",
      groupCount: "",
      allowSamePot: false,
    });
    expect(bombos[1]).toEqual(["A", "B"]);
    expect(bombos[2]).toEqual(["C"]);
    expect(bombosNumerados).toEqual([1, 2]);
  });

  it("un equipo sin bombo (seed null) va bajo la clave 'sin', separado de los numerados", () => {
    const { bombos, bombosNumerados } = calcularBombos({
      teams: [equipo({ name: "SinBombo", seed: null }), equipo({ name: "ConBombo", seed: 1 })],
      formato: "groups",
      groupCount: "",
      allowSamePot: false,
    });
    expect(bombos.sin).toEqual(["SinBombo"]);
    expect(bombosNumerados).toEqual([1]); // 'sin' no cuenta como bombo numerado
  });

  it("bombosNumerados ordena numéricamente, no alfabéticamente (10 después de 2)", () => {
    const { bombosNumerados } = calcularBombos({
      teams: [equipo({ seed: 10 }), equipo({ seed: 2 })],
      formato: "groups",
      groupCount: "",
      allowSamePot: false,
    });
    expect(bombosNumerados).toEqual([2, 10]);
  });
});

describe("calcularBombos -- bomboExcedido", () => {
  it("true cuando un bombo tiene más equipos que la cantidad de grupos elegida", () => {
    const teams = [equipo({ seed: 1 }), equipo({ seed: 1 }), equipo({ seed: 1 })]; // 3 equipos en el bombo 1
    const { bomboExcedido } = calcularBombos({ teams, formato: "groups", groupCount: "2", allowSamePot: false });
    expect(bomboExcedido).toBe(true);
  });

  it("false cuando ningún bombo supera la cantidad de grupos", () => {
    const teams = [equipo({ seed: 1 }), equipo({ seed: 1 })];
    const { bomboExcedido } = calcularBombos({ teams, formato: "groups", groupCount: "2", allowSamePot: false });
    expect(bomboExcedido).toBe(false);
  });

  it("false sin haber elegido todavía la cantidad de grupos (groupCount vacío)", () => {
    const teams = [equipo({ seed: 1 }), equipo({ seed: 1 }), equipo({ seed: 1 })];
    const { bomboExcedido } = calcularBombos({ teams, formato: "groups", groupCount: "", allowSamePot: false });
    expect(bomboExcedido).toBe(false);
  });

  it("allowSamePot:true desactiva la restricción por completo, sin importar cuántos equipos tenga el bombo", () => {
    const teams = [equipo({ seed: 1 }), equipo({ seed: 1 }), equipo({ seed: 1 })];
    const { bomboExcedido } = calcularBombos({ teams, formato: "groups", groupCount: "2", allowSamePot: true });
    expect(bomboExcedido).toBe(false);
  });

  it("un bombo exactamente del tamaño de la cantidad de grupos no excede (solo 'más que', no 'igual o más')", () => {
    const teams = [equipo({ seed: 1 }), equipo({ seed: 1 })];
    const { bomboExcedido } = calcularBombos({ teams, formato: "groups", groupCount: "2", allowSamePot: false });
    expect(bomboExcedido).toBe(false);
  });
});

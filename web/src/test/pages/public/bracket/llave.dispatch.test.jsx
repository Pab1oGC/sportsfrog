import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";

vi.mock("src/pages/public/bracket/standard-cruce", () => ({
  StandardCruce: () => <div data-testid="cruce-standard" />,
}));
vi.mock("src/pages/public/bracket/compact-cruce", () => ({
  CompactCruce: () => <div data-testid="cruce-compact" />,
}));
vi.mock("src/pages/public/bracket/detailed-cruce", () => ({
  DetailedCruce: () => <div data-testid="cruce-detailed" />,
}));
vi.mock("src/pages/public/bracket/individual-cruce", () => ({
  IndividualCruce: () => <div data-testid="cruce-individual" />,
}));
// BracketConnectors mide las tarjetas ya renderizadas -- ajeno a qué tarjeta
// se elige, que es lo único que esta suite quiere ejercitar.
vi.mock("src/pages/public/bracket/bracket-connectors", () => ({
  BracketConnectors: () => null,
}));

import { Llave } from "src/pages/public/bracket/llave";

const gruposFixture = [
  {
    clave: "final",
    titulo: "Final",
    enCurso: false,
    matches: [{ id: "m1", homeTeamName: "A", awayTeamName: "B", status: "scheduled" }],
  },
];

describe("Llave -- despachador de variante", () => {
  it("variant 'standard' monta StandardCruce", () => {
    render(<Llave grupos={gruposFixture} variant="standard" />);
    expect(screen.getByTestId("cruce-standard")).toBeInTheDocument();
  });

  it("variant 'compact' monta CompactCruce", () => {
    render(<Llave grupos={gruposFixture} variant="compact" />);
    expect(screen.getByTestId("cruce-compact")).toBeInTheDocument();
  });

  it("variant 'detailed' monta DetailedCruce", () => {
    render(<Llave grupos={gruposFixture} variant="detailed" />);
    expect(screen.getByTestId("cruce-detailed")).toBeInTheDocument();
  });

  it("una variante desconocida cae en StandardCruce", () => {
    render(<Llave grupos={gruposFixture} variant="algo-que-no-existe" />);
    expect(screen.getByTestId("cruce-standard")).toBeInTheDocument();
  });

  it("esIndividual reemplaza la elección por completo: usa IndividualCruce aunque variant pida otra cosa", () => {
    render(<Llave grupos={gruposFixture} variant="detailed" esIndividual />);
    expect(screen.getByTestId("cruce-individual")).toBeInTheDocument();
    expect(screen.queryByTestId("cruce-detailed")).not.toBeInTheDocument();
  });

  it("sin esIndividual (false/ausente), la variante elegida sí se respeta", () => {
    render(<Llave grupos={gruposFixture} variant="compact" esIndividual={false} />);
    expect(screen.getByTestId("cruce-compact")).toBeInTheDocument();
  });
});

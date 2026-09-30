import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";

vi.mock("src/pages/public/match-card/standard-card", () => ({
  StandardCard: () => <div data-testid="card-standard" />,
}));
vi.mock("src/pages/public/match-card/compact-card", () => ({
  CompactCard: () => <div data-testid="card-compact" />,
}));
vi.mock("src/pages/public/match-card/matchup-card", () => ({
  MatchupCard: () => <div data-testid="card-matchup" />,
}));

import { Partido } from "src/pages/public/match-card/partido";

const partidoFixture = { id: "m1", homeTeamName: "Local", awayTeamName: "Visitante", status: "scheduled" };

describe("Partido -- despachador de variante", () => {
  it("variant 'standard' monta StandardCard", () => {
    render(<Partido m={partidoFixture} variant="standard" />);
    expect(screen.getByTestId("card-standard")).toBeInTheDocument();
  });

  it("variant 'compact' monta CompactCard", () => {
    render(<Partido m={partidoFixture} variant="compact" />);
    expect(screen.getByTestId("card-compact")).toBeInTheDocument();
  });

  it("variant 'matchup' monta MatchupCard", () => {
    render(<Partido m={partidoFixture} variant="matchup" />);
    expect(screen.getByTestId("card-matchup")).toBeInTheDocument();
  });

  it("una variante desconocida cae en StandardCard", () => {
    render(<Partido m={partidoFixture} variant="algo-que-no-existe" />);
    expect(screen.getByTestId("card-standard")).toBeInTheDocument();
  });

  it("sin variant declarada, también cae en StandardCard", () => {
    render(<Partido m={partidoFixture} />);
    expect(screen.getByTestId("card-standard")).toBeInTheDocument();
  });
});

import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";

/**
 * Se reemplazan las tres variantes reales (StandardBoard/CardsBoard/
 * EditorialBoard) por un doble mínimo cada una -- a propósito, para probar
 * solo la elección del despachador (qué variante se monta según
 * `props.variant`), sin necesitar entender cómo dibuja cada una sus filas.
 */
vi.mock("src/pages/public/standings/standard-board", () => ({
  StandardBoard: () => <div data-testid="board-standard" />,
}));
vi.mock("src/pages/public/standings/cards-board", () => ({
  CardsBoard: () => <div data-testid="board-cards" />,
}));
vi.mock("src/pages/public/standings/editorial-board", () => ({
  EditorialBoard: () => <div data-testid="board-editorial" />,
}));

import { StandingsView } from "src/pages/public/standings/standings-view";

const dataFixture = {
  categories: [
    { categoryId: "c1", categoryName: "Sub-15", tiebreakers: [], qualifiersPerGroup: 0, groups: [{ label: null, rows: [] }] },
  ],
};

describe("StandingsView -- despachador de variante", () => {
  it("variant 'standard' monta StandardBoard", () => {
    render(<StandingsView data={dataFixture} variant="standard" />);
    expect(screen.getByTestId("board-standard")).toBeInTheDocument();
  });

  it("variant 'cards' monta CardsBoard", () => {
    render(<StandingsView data={dataFixture} variant="cards" />);
    expect(screen.getByTestId("board-cards")).toBeInTheDocument();
  });

  it("variant 'editorial' monta EditorialBoard", () => {
    render(<StandingsView data={dataFixture} variant="editorial" />);
    expect(screen.getByTestId("board-editorial")).toBeInTheDocument();
  });

  it("una variante desconocida cae en StandardBoard, no en una pantalla en blanco", () => {
    render(<StandingsView data={dataFixture} variant="algo-que-no-existe" />);
    expect(screen.getByTestId("board-standard")).toBeInTheDocument();
  });

  it("sin variant declarada, también cae en StandardBoard", () => {
    render(<StandingsView data={dataFixture} />);
    expect(screen.getByTestId("board-standard")).toBeInTheDocument();
  });
});

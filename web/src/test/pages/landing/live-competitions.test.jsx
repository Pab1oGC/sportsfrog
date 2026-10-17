import { describe, expect, it, vi, beforeEach } from "vitest";
import { screen, cleanup } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderWithProviders } from "src/test/render";

// Se preserva el resto del módulo (SWRConfig, que renderWithProviders monta)
// y solo se mockea el hook en sí.
vi.mock("swr", async (importOriginal) => {
  const actual = await importOriginal();
  return { ...actual, default: vi.fn() };
});
import useSWR from "swr";

// vi.hoisted: vi.mock se eleva por encima de todo el archivo, así que la
// factory no puede cerrar sobre un const declarado más abajo sin esto.
const { mockNavigate } = vi.hoisted(() => ({ mockNavigate: vi.fn() }));
vi.mock("react-router", async (importOriginal) => {
  const actual = await importOriginal();
  return { ...actual, useNavigate: () => mockNavigate };
});

// La animación no es lo que esta prueba verifica. Se mockea entero,
// incluido registerPlugin -- use-scroll-reveal.js lo llama al cargar el
// módulo (ver su propio comentario), y el mock no trae esa función salvo
// que se la agreguemos.
vi.mock("gsap", () => ({
  default: {
    context: (fn) => {
      fn();
      return { revert: vi.fn() };
    },
    from: vi.fn(),
    registerPlugin: vi.fn(),
  },
}));

import { LiveCompetitions } from "src/pages/landing/live-competitions";

function competenciaFixture(overrides = {}) {
  return {
    organizationSlug: "liga-test",
    competitionSlug: "torneo-test",
    organizationName: "Liga Test",
    competitionName: "Torneo Test",
    sportName: "Fútbol",
    status: "in_progress",
    teams: 8,
    categories: 2,
    season: "2026",
    ...overrides,
  };
}

function montar(directorio) {
  cleanup();
  useSWR.mockReturnValue({ data: directorio, error: undefined, isLoading: false });
  return renderWithProviders(<LiveCompetitions />);
}

beforeEach(() => {
  vi.clearAllMocks();
});

describe("LiveCompetitions", () => {
  it("sin competencias publicadas, no renderiza nada", () => {
    montar({ total: 0, competitions: [] });
    expect(screen.queryByText("AHORA MISMO")).not.toBeInTheDocument();
  });

  it("muestra una tarjeta por cada competencia publicada, con su estado y deporte", () => {
    montar({ total: 1, competitions: [competenciaFixture()] });

    expect(screen.getByText("Torneo Test")).toBeInTheDocument();
    expect(screen.getByText("Liga Test")).toBeInTheDocument();
    expect(screen.getByText("En curso")).toBeInTheDocument();
    expect(screen.getByText("Fútbol")).toBeInTheDocument();
  });

  it("corta la lista en 6, aunque el directorio traiga más", () => {
    const competencias = Array.from({ length: 10 }, (_, i) =>
      competenciaFixture({ competitionSlug: `torneo-${i}`, competitionName: `Torneo ${i}` }),
    );
    montar({ total: 10, competitions: competencias });

    expect(screen.getByText("Torneo 0")).toBeInTheDocument();
    expect(screen.getByText("Torneo 5")).toBeInTheDocument();
    expect(screen.queryByText("Torneo 6")).not.toBeInTheDocument();
  });

  it("al hacer click en una tarjeta, navega al detalle público de esa competencia", async () => {
    const user = userEvent.setup();
    montar({ total: 1, competitions: [competenciaFixture()] });

    await user.click(screen.getByText("Torneo Test"));

    expect(mockNavigate).toHaveBeenCalledWith("/public/liga-test/torneo-test");
  });
});

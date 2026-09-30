import { describe, expect, it, vi } from "vitest";
import { render, screen, within, cleanup } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Routes, Route } from "react-router";

/**
 * CalendarView es un componente interno de public-competition.jsx (no
 * exportado) con lógica de agrupación bastante intrincada según el propio
 * plan de pruebas -- a propósito, estas pruebas se escribieron leyendo solo
 * su JSX final (qué aparece en pantalla) y la especificación del plan, sin
 * haber leído cómo arma la lista de equipos ni cómo elige la jornada por
 * defecto. Si el comportamiento real no coincide con lo que sigue, es un
 * hallazgo para reportar, no algo para acomodar en la prueba.
 */
vi.mock("swr", () => ({ default: vi.fn() }));
import useSWR from "swr";
import PublicCompetitionPage from "src/pages/public/public-competition";

const ORG_SLUG = "liga-test";
const COMP_SLUG = "torneo-test";
const COMP_URL = `/api/public/${ORG_SLUG}/${COMP_SLUG}`;
const MATCHES_URL = `${COMP_URL}/matches`;

/** Una competencia donde la ÚNICA sección visible es Calendario -- así la pestaña 0 ya es el calendario, sin tener que clickear nada. */
function compFixture(overrides = {}) {
  return {
    name: "Copa Apertura",
    season: "2026",
    organizationName: "Liga Test",
    sportName: "Fútbol",
    status: "in_progress",
    format: "groups",
    isJudged: false,
    isIndividual: false,
    portal: { theme: null, gallery: [] },
    shows: { standings: false, leaders: false, classification: false, gallery: false, bracket: false },
    ...overrides,
  };
}

function partido(overrides = {}) {
  return {
    id: "m1",
    categoryId: "cat1",
    categoryName: "Sub-15",
    phase: null,
    roundNumber: 1,
    status: "scheduled",
    homeTeamName: "Local",
    awayTeamName: "Visitante",
    homeTotal: null,
    awayTotal: null,
    scheduledAt: null,
    venueName: null,
    spaceName: null,
    ...overrides,
  };
}

function useSWRFalso({ comp, fixtures }) {
  return (key) => {
    if (key === COMP_URL) return { data: comp, error: undefined, isLoading: false };
    if (key === MATCHES_URL) return { data: { fixtures: fixtures || [] }, error: undefined, isLoading: false };
    return { data: undefined, error: undefined, isLoading: false };
  };
}

function elArbol() {
  return (
    <MemoryRouter initialEntries={[`/public/${ORG_SLUG}/${COMP_SLUG}`]}>
      <Routes>
        <Route path="/public/:orgSlug/:compSlug" element={<PublicCompetitionPage />} />
      </Routes>
    </MemoryRouter>
  );
}

function montar(comp, fixtures) {
  cleanup();
  useSWR.mockImplementation(useSWRFalso({ comp, fixtures }));
  return render(elArbol());
}

describe("CalendarView -- los nombres de equipo se repiten entre categorías", () => {
  it("dos equipos con el MISMO nombre en categorías distintas aparecen como dos opciones separadas, no una sola", async () => {
    const user = userEvent.setup();
    montar(compFixture(), [
      partido({ id: "m1", categoryId: "cat1", categoryName: "Sub-15", homeTeamName: "Deportivo Norte", awayTeamName: "Atlético Sur" }),
      partido({ id: "m2", categoryId: "cat2", categoryName: "Sub-17", homeTeamName: "Deportivo Norte", awayTeamName: "Unión Central" }),
    ]);

    await user.click(screen.getByRole("combobox", { name: /equipo/i }));
    const listbox = await screen.findByRole("listbox");
    const opciones = within(listbox).getAllByText(/Deportivo Norte/i);

    // Si el nombre se indexara solo por nombre (sin la categoría), acá
    // habría una sola opción "Deportivo Norte" en vez de dos distinguidas
    // por categoría.
    expect(opciones.length).toBeGreaterThanOrEqual(2);
    expect(within(listbox).getByText(/Deportivo Norte.*Sub-15/i)).toBeInTheDocument();
    expect(within(listbox).getByText(/Deportivo Norte.*Sub-17/i)).toBeInTheDocument();
  });
});

describe("CalendarView -- qué jornada se muestra por defecto", () => {
  it("con una jornada ya terminada y la siguiente pendiente, muestra la pendiente (la primera sin terminar)", () => {
    montar(compFixture(), [
      partido({ id: "j1", roundNumber: 1, status: "finished", homeTotal: 2, awayTotal: 1 }),
      partido({ id: "j2", roundNumber: 2, status: "scheduled" }),
    ]);

    expect(screen.getByText("Jornada 2")).toBeInTheDocument();
    expect(screen.queryByText("Jornada 1")).not.toBeInTheDocument();
  });

  it("con todas las jornadas ya jugadas, muestra alguna -- no queda en blanco ni revienta", () => {
    montar(compFixture(), [
      partido({ id: "j1", roundNumber: 1, status: "finished", homeTotal: 2, awayTotal: 1 }),
      partido({ id: "j2", roundNumber: 2, status: "finished", homeTotal: 1, awayTotal: 1 }),
    ]);

    const jornada1 = screen.queryByText("Jornada 1");
    const jornada2 = screen.queryByText("Jornada 2");
    // Exactamente una de las dos es la que se ve por defecto (deUnEquipo
    // es false, así que "visibles" es un arreglo de un solo grupo).
    expect([jornada1, jornada2].filter(Boolean)).toHaveLength(1);
  });
});

describe("CalendarView -- una elección manual de jornada no se pisa sola", () => {
  it("elegir una jornada a mano y que los mismos datos se revaliden (mismo conjunto de jornadas) no vuelve a la que elegía por defecto", async () => {
    const user = userEvent.setup();
    const fixturesIniciales = [
      partido({ id: "j1", roundNumber: 1, status: "finished", homeTotal: 2, awayTotal: 1 }),
      partido({ id: "j2", roundNumber: 2, status: "scheduled" }),
    ];
    useSWR.mockImplementation(useSWRFalso({ comp: compFixture(), fixtures: fixturesIniciales }));
    const { rerender } = render(elArbol());

    // Por defecto se ve "Jornada 2" (la pendiente) -- se elige "Jornada 1" a mano.
    await user.click(screen.getByRole("button", { name: "J1" }));
    expect(screen.getByText("Jornada 1")).toBeInTheDocument();

    // Revalidación: mismas dos jornadas, pero ahora la 2 también tiene resultado.
    useSWR.mockImplementation(useSWRFalso({
      comp: compFixture(),
      fixtures: [
        fixturesIniciales[0],
        { ...fixturesIniciales[1], status: "finished", homeTotal: 3, awayTotal: 0 },
      ],
    }));
    rerender(elArbol()); // mismo árbol/posición -> React conserva el estado de CalendarView

    expect(screen.getByText("Jornada 1")).toBeInTheDocument();
    expect(screen.queryByText("Jornada 2")).not.toBeInTheDocument();
  });
});

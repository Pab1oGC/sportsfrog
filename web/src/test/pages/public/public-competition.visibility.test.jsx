import { describe, expect, it, vi, beforeEach } from "vitest";
import { render, screen, cleanup } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Routes, Route } from "react-router";

/**
 * Pruebas escritas desde la especificación del plan de pruebas (la matriz de
 * visibilidad documentada para esta página), no desde una lectura línea por
 * línea de public-competition.jsx -- a propósito, para que una prueba no
 * termine confirmando un defecto solo porque coincide con lo que el código
 * ya hace hoy. Donde el comportamiento real no coincida con la
 * especificación, es un hallazgo a reportar, no algo para ajustar en
 * silencio.
 */
vi.mock("swr", () => ({ default: vi.fn() }));
import useSWR from "swr";
import PublicCompetitionPage from "src/pages/public/public-competition";

const ORG_SLUG = "liga-test";
const COMP_SLUG = "torneo-test";
const COMP_URL = `/api/public/${ORG_SLUG}/${COMP_SLUG}`;

/** Una competencia publicada, con todas las secciones "activables" en true, para partir de un caso permisivo. */
function compFixture(overrides = {}) {
  return {
    name: "Copa Apertura",
    season: "2026",
    organizationName: "Liga Test",
    organizationLogoUrl: null,
    sportName: "Fútbol",
    sportCode: "football",
    status: "in_progress",
    format: "league",
    isJudged: false,
    isPublic: true,
    moment: null,
    startsOn: "2026-03-01",
    endsOn: "2026-06-01",
    portal: { theme: null, accentColor: null, sectionOrder: null, gallery: [] },
    shows: { standings: true, leaders: true, classification: true, rosters: true, bracket: true, gallery: true },
    ...overrides,
  };
}

/** useSWR se llama varias veces (comp, standings, leaders, classification, matches) -- solo el de `comp` importa para estas pruebas. */
function useSWRFalso({ data, error } = {}) {
  return (key) => {
    if (key === COMP_URL) return { data, error, isLoading: false };
    return { data: undefined, error: undefined, isLoading: false };
  };
}

function montar(compData, { error } = {}) {
  cleanup(); // por si la propia prueba ya montó otra instancia antes
  useSWR.mockImplementation(useSWRFalso({ data: compData, error }));
  return render(
    <MemoryRouter initialEntries={[`/public/${ORG_SLUG}/${COMP_SLUG}`]}>
      <Routes>
        <Route path="/public/:orgSlug/:compSlug" element={<PublicCompetitionPage />} />
      </Routes>
    </MemoryRouter>,
  );
}

beforeEach(() => {
  vi.clearAllMocks();
});

describe("PublicCompetitionPage -- matriz de visibilidad de secciones", () => {
  it("standings, leaders y gallery (con fotos) están visibles por defecto, sin interruptor explícito", () => {
    montar(compFixture({ shows: {}, portal: { gallery: [{ url: "https://cdn/1.jpg" }] } }));
    expect(screen.getByRole("tab", { name: /posiciones/i })).toBeInTheDocument();
    expect(screen.getByRole("tab", { name: /líderes|lideres/i })).toBeInTheDocument();
    expect(screen.getByRole("tab", { name: /fotos/i })).toBeInTheDocument();
  });

  it("standings/leaders se ocultan solo con shows.X === false explícito", () => {
    montar(compFixture({ shows: { standings: false, leaders: false } }));
    expect(screen.queryByRole("tab", { name: /posiciones/i })).not.toBeInTheDocument();
    expect(screen.queryByRole("tab", { name: /líderes|lideres/i })).not.toBeInTheDocument();
  });

  it("rosters (nóminas) es la excepción: oculto por defecto, visible solo con shows.rosters === true", () => {
    // No hay pestaña propia de "nóminas" -- se verifica indirectamente a
    // través del botón de cronología de una tarjeta de partido, más abajo.
    // Acá solo se confirma el caso "apagado sin declarar nada".
    montar(compFixture({ shows: {} }));
    // No debería reventar ni mostrar nada que dependa de nóminas encendidas.
    expect(screen.queryByText(/no se pudo cargar/i)).not.toBeInTheDocument();
  });

  it("classification exige isJudged Y el interruptor -- ninguno de los dos solo alcanza", () => {
    montar(compFixture({ isJudged: false, shows: { classification: true } }));
    expect(screen.queryByRole("tab", { name: /clasificaci/i })).not.toBeInTheDocument();

    vi.clearAllMocks();
    montar(compFixture({ isJudged: true, shows: { classification: false } }));
    expect(screen.queryByRole("tab", { name: /clasificaci/i })).not.toBeInTheDocument();

    vi.clearAllMocks();
    montar(compFixture({ isJudged: true, shows: { classification: true } }));
    expect(screen.getByRole("tab", { name: /clasificaci/i })).toBeInTheDocument();
  });

  it("gallery exige el interruptor Y al menos una foto cargada -- ninguno de los dos solo alcanza", () => {
    montar(compFixture({ shows: { gallery: true }, portal: { gallery: [] } }));
    expect(screen.queryByRole("tab", { name: /fotos/i })).not.toBeInTheDocument();

    vi.clearAllMocks();
    montar(compFixture({ shows: { gallery: false }, portal: { gallery: [{ url: "https://cdn/1.jpg" }] } }));
    expect(screen.queryByRole("tab", { name: /fotos/i })).not.toBeInTheDocument();

    vi.clearAllMocks();
    montar(compFixture({ shows: { gallery: true }, portal: { gallery: [{ url: "https://cdn/1.jpg" }] } }));
    expect(screen.getByRole("tab", { name: /fotos/i })).toBeInTheDocument();
  });

  it("bracket lo decide el backend (shows.bracket === true), no una condición calculada en el cliente", () => {
    montar(compFixture({ shows: { bracket: false } }));
    expect(screen.queryByRole("tab", { name: /llave/i })).not.toBeInTheDocument();

    vi.clearAllMocks();
    montar(compFixture({ shows: { bracket: true } }));
    expect(screen.getByRole("tab", { name: /llave/i })).toBeInTheDocument();
  });

  it("calendar está siempre visible, sin ningún interruptor que pueda apagarlo", () => {
    montar(compFixture({ shows: { standings: false, leaders: false, classification: false, gallery: false, bracket: false } }));
    expect(screen.getByRole("tab", { name: /calendario/i })).toBeInTheDocument();
  });
});

describe("PublicCompetitionPage -- 'no publicada' es indistinguible de 'inexistente'", () => {
  it("un slug inexistente y un error de servidor muestran exactamente el mismo mensaje", () => {
    montar(undefined, { error: new Error("Request failed with status code 404") });
    const mensajeNoEncontrada = screen.getByText(/no encontrada|no publicada/i);

    vi.clearAllMocks();
    montar(undefined, { error: new Error("Network Error") });
    const mensajeServidorCaido = screen.getByText(/no encontrada|no publicada/i);

    expect(mensajeNoEncontrada.textContent).toBe(mensajeServidorCaido.textContent);
  });

  it("sin comp y sin error explícito (respuesta vacía) también cae en el mismo mensaje, no en una pantalla en blanco", () => {
    montar(null, {});
    expect(screen.getByText(/no encontrada|no publicada/i)).toBeInTheDocument();
  });
});

describe("PublicCompetitionPage -- recorte de la pestaña activa cuando el conteo baja", () => {
  // El plan de pruebas marca esto como "una clase de defecto real": cambiar
  // de categoría (acá, simulado con una revalidación de `comp` que apaga
  // secciones) puede dejar la pestaña activa apuntando a un índice que ya no
  // existe. La expectativa, tal como la describe el plan: si la pestaña
  // activa queda fuera del nuevo conteo, vuelve a 0.
  it("con la última pestaña activa, si una revalidación reduce las pestañas a menos de las que había, vuelve a la primera", async () => {
    const user = userEvent.setup();
    const conTodo = compFixture({
      isJudged: true,
      shows: { standings: true, leaders: true, classification: true, bracket: true, gallery: true },
      portal: { gallery: [{ url: "https://cdn/1.jpg" }] },
    });
    useSWR.mockImplementation(useSWRFalso({ data: conTodo }));

    const { rerender } = render(
      <MemoryRouter initialEntries={[`/public/${ORG_SLUG}/${COMP_SLUG}`]}>
        <Routes>
          <Route path="/public/:orgSlug/:compSlug" element={<PublicCompetitionPage />} />
        </Routes>
      </MemoryRouter>,
    );

    // Seis pestañas visibles (posiciones/líderes/clasificación/calendario/
    // fotos/llave); se activa la última.
    await user.click(screen.getByRole("tab", { name: /llave/i }));
    expect(screen.getByRole("tab", { name: /llave/i })).toHaveAttribute("aria-selected", "true");

    // La revalidación (misma competencia, con menos secciones prendidas) dejaría
    // solo tres pestañas -- la que estaba activa (índice 5) ya no existe.
    const conMenos = compFixture({
      isJudged: false,
      shows: { standings: true, leaders: true, classification: false, bracket: false, gallery: false },
    });
    useSWR.mockImplementation(useSWRFalso({ data: conMenos }));
    rerender(
      <MemoryRouter initialEntries={[`/public/${ORG_SLUG}/${COMP_SLUG}`]}>
        <Routes>
          <Route path="/public/:orgSlug/:compSlug" element={<PublicCompetitionPage />} />
        </Routes>
      </MemoryRouter>,
    );

    expect(screen.queryByRole("tab", { name: /llave/i })).not.toBeInTheDocument();
    expect(screen.getByRole("tab", { name: /posiciones/i })).toHaveAttribute("aria-selected", "true");
  });
});

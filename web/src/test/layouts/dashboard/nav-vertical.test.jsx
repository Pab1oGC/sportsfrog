import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, useLocation } from "react-router";
import { NavVertical } from "src/layouts/dashboard/nav-vertical";

// isActive vive dentro de NavVertical, no exportada aparte -- se ejercita a
// través del componente en vez de extraerla, comparando qué ítem queda
// marcado "Mui-selected" para cada ruta actual.
const data = [
  {
    subheader: "Principal",
    items: [
      { title: "Dashboard", path: "/dashboard", icon: "mdi:view-dashboard-outline" },
      { title: "Clubes", path: "/dashboard/clubs", icon: "mdi:office-building-outline" },
    ],
  },
];

function estaSeleccionado(titulo) {
  return screen.getByText(titulo).closest(".MuiListItemButton-root").className.includes("Mui-selected");
}

function montarEn(pathname) {
  return render(
    <MemoryRouter initialEntries={[pathname]}>
      <NavVertical data={data} collapsed={false} isDesktop mobileOpen={false} onCloseMobile={() => {}} onToggle={() => {}} />
    </MemoryRouter>,
  );
}

// Sonda para leer a dónde navegó un click, sin montar <Routes> de verdad --
// a NavVertical no le importa qué pantalla hay detrás de cada ruta, solo
// que useNavigate() la cambie.
function Ubicacion() {
  const location = useLocation();
  return <div data-testid="ubicacion">{location.pathname}</div>;
}

const dataConHijos = [
  {
    subheader: "Competencia",
    items: [
      {
        title: "Competiciones",
        path: "/dashboard/competitions",
        icon: "mdi:trophy-outline",
        children: [
          { title: "Categorias", path: "/dashboard/categories" },
          { title: "Equipos", path: "/dashboard/teams" },
        ],
      },
      { title: "Clubes", path: "/dashboard/clubs", icon: "mdi:office-building-outline" },
    ],
  },
];

function montarConHijosEn(pathname) {
  return render(
    <MemoryRouter initialEntries={[pathname]}>
      <NavVertical data={dataConHijos} collapsed={false} isDesktop mobileOpen={false} onCloseMobile={() => {}} onToggle={() => {}} />
      <Ubicacion />
    </MemoryRouter>,
  );
}

describe("NavVertical -- items con hijos", () => {
  it("clickear el texto principal navega a su propio path, no solo expande", async () => {
    const user = userEvent.setup();
    montarConHijosEn("/dashboard/clubs");
    await user.click(screen.getByText("Competiciones"));
    expect(screen.getByTestId("ubicacion")).toHaveTextContent("/dashboard/competitions");
  });

  it("arranca expandido por default, sin haber tocado nada todavía", () => {
    montarConHijosEn("/dashboard/clubs");
    expect(screen.getByText("Categorias")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Contraer" })).toBeInTheDocument();
  });

  it("clickear la flecha lo contrae sin navegar, y un segundo click lo vuelve a expandir", async () => {
    const user = userEvent.setup();
    montarConHijosEn("/dashboard/clubs");

    await user.click(screen.getByRole("button", { name: "Contraer" }));
    expect(screen.queryByText("Categorias")).not.toBeInTheDocument();
    expect(screen.getByTestId("ubicacion")).toHaveTextContent("/dashboard/clubs");

    await user.click(screen.getByRole("button", { name: "Expandir" }));
    expect(screen.getByText("Categorias")).toBeInTheDocument();
  });

  it("clickear el texto principal reabre el submenú aunque se lo haya contraído con la flecha", async () => {
    const user = userEvent.setup();
    montarConHijosEn("/dashboard/clubs");

    await user.click(screen.getByRole("button", { name: "Contraer" }));
    expect(screen.queryByText("Categorias")).not.toBeInTheDocument();

    await user.click(screen.getByText("Competiciones"));
    expect(screen.getByText("Categorias")).toBeInTheDocument();
  });

  it("estando en la ruta de una hija, la hija queda marcada y el padre no", () => {
    montarConHijosEn("/dashboard/categories");
    expect(estaSeleccionado("Categorias")).toBe(true);
    expect(estaSeleccionado("Competiciones")).toBe(false);
  });

  it("clickear una hija navega a su propio path", async () => {
    const user = userEvent.setup();
    montarConHijosEn("/dashboard/categories");
    await user.click(screen.getByText("Equipos"));
    expect(screen.getByTestId("ubicacion")).toHaveTextContent("/dashboard/teams");
  });
});

describe("NavVertical -- isActive", () => {
  it("'/dashboard' exacto marca Dashboard, y solo a Dashboard", () => {
    montarEn("/dashboard");
    expect(estaSeleccionado("Dashboard")).toBe(true);
    expect(estaSeleccionado("Clubes")).toBe(false);
  });

  it("'/dashboard' NO se marca activo en una ruta hija -- es el único ítem que exige coincidencia exacta", () => {
    montarEn("/dashboard/clubs");
    expect(estaSeleccionado("Dashboard")).toBe(false);
    expect(estaSeleccionado("Clubes")).toBe(true);
  });

  it("una ruta distinta de /dashboard sí acepta el prefijo 'path + /' (una subpágina la deja marcada)", () => {
    montarEn("/dashboard/clubs/123/edit");
    expect(estaSeleccionado("Clubes")).toBe(true);
  });

  it("no matchea por substring: una ruta que solo empieza igual pero sin el '/' de separador no activa el ítem", () => {
    // "/dashboard/clubsomethingelse" no es una subruta de "/dashboard/clubs"
    // -- comparten el prefijo de texto, pero no el segmento de ruta.
    montarEn("/dashboard/clubsomethingelse");
    expect(estaSeleccionado("Clubes")).toBe(false);
  });
});

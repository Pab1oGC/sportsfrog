import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router";
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

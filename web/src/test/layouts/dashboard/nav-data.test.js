import { describe, expect, it } from "vitest";
import { getNavData, navData } from "src/layouts/dashboard/nav-data";

// Ítems y sus hijos, todos al mismo nivel -- para los chequeos que no les
// importa de qué nivel viene cada ruta (que empiece con /dashboard, que no
// se repita), aplanar un nivel más alcanza.
function todosLosItems(secciones) {
  return secciones.flatMap((s) => s.items).flatMap((i) => [i, ...(i.children || [])]);
}

describe("getNavData", () => {
  it("sin isPlatformAdmin (por defecto), no incluye 'Crear Organización'", () => {
    const secciones = getNavData();
    expect(todosLosItems(secciones).some((i) => i.path === "/dashboard/register-organization")).toBe(false);
  });

  it("isPlatformAdmin=false explícito tampoco la incluye", () => {
    expect(todosLosItems(getNavData(false)).some((i) => i.path === "/dashboard/register-organization")).toBe(false);
  });

  it("isPlatformAdmin=true agrega 'Crear Organización' al final de la sección Sistema", () => {
    const sistema = getNavData(true).find((s) => s.subheader === "Sistema");
    expect(sistema.items.at(-1)).toEqual({
      title: "Crear Organización",
      path: "/dashboard/register-organization",
      icon: "mdi:domain-plus",
    });
  });

  it("todas las rutas, incluidas las de los hijos, empiezan con /dashboard", () => {
    expect(todosLosItems(getNavData(true)).every((i) => i.path.startsWith("/dashboard"))).toBe(true);
  });

  it("no hay dos ítems con la misma ruta, ni entre padres e hijos", () => {
    const paths = todosLosItems(getNavData(true)).map((i) => i.path);
    expect(new Set(paths).size).toBe(paths.length);
  });

  it("cada sección tiene un subheader y al menos un ítem", () => {
    getNavData(true).forEach((seccion) => {
      expect(typeof seccion.subheader).toBe("string");
      expect(seccion.items.length).toBeGreaterThan(0);
    });
  });
});

describe("navData (instancia exportada)", () => {
  it("es getNavData(false) ya resuelto -- sin la entrada de administrador de plataforma", () => {
    expect(navData).toEqual(getNavData(false));
  });
});

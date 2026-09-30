import { describe, expect, it } from "vitest";
import { getNavData, navData } from "src/layouts/dashboard/nav-data";

describe("getNavData", () => {
  it("sin isPlatformAdmin (por defecto), no incluye 'Crear Organización'", () => {
    const secciones = getNavData();
    const items = secciones.flatMap((s) => s.items);
    expect(items.some((i) => i.path === "/dashboard/register-organization")).toBe(false);
  });

  it("isPlatformAdmin=false explícito tampoco la incluye", () => {
    const items = getNavData(false).flatMap((s) => s.items);
    expect(items.some((i) => i.path === "/dashboard/register-organization")).toBe(false);
  });

  it("isPlatformAdmin=true agrega 'Crear Organización' al final de la sección Sistema", () => {
    const sistema = getNavData(true).find((s) => s.subheader === "Sistema");
    expect(sistema.items.at(-1)).toEqual({
      title: "Crear Organización",
      path: "/dashboard/register-organization",
      icon: "mdi:domain-plus",
    });
  });

  it("todas las rutas empiezan con /dashboard", () => {
    const items = getNavData(true).flatMap((s) => s.items);
    expect(items.every((i) => i.path.startsWith("/dashboard"))).toBe(true);
  });

  it("no hay dos ítems con la misma ruta", () => {
    const paths = getNavData(true).flatMap((s) => s.items).map((i) => i.path);
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

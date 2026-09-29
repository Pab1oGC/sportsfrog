import "@testing-library/jest-dom/vitest";
import { afterEach, beforeEach, vi } from "vitest";
import { cleanup } from "@testing-library/react";

// Desmonta el árbol renderizado por el test anterior. Sin esto, dos pruebas
// que rendericen la misma pantalla en el mismo archivo ven ambas copias en
// el DOM y un `getByText` que debería fallar por ambigüedad no falla.
afterEach(() => {
  cleanup();
});

// ─── Sesión y almacenamiento ────────────────────────────────────────────────
//
// localStorage/sessionStorage sobreviven entre pruebas del mismo archivo
// porque jsdom no reinicia `window` por test. Sin esto, "organizations" o el
// access token de una prueba anterior aparecen en la siguiente sin que nada
// en el propio test los haya puesto ahí.
beforeEach(() => {
  localStorage.clear();
  sessionStorage.clear();
});

// ─── APIs que jsdom no implementa ───────────────────────────────────────────

// window.matchMedia: usado por use-prefers-reduced-motion.js, lib/motion.js,
// playful-hero.jsx y el useMediaQuery de MUI en dashboard/layout.jsx. jsdom
// no lo define en absoluto, así que sin este stub cualquier componente que
// lo toque revienta con "matchMedia is not a function" antes de llegar a la
// aserción que la prueba realmente quiere hacer. Devuelve matches:false por
// defecto; una prueba puntual que necesite matches:true la sobreescribe con
// vi.spyOn(window, "matchMedia").
if (!window.matchMedia) {
  window.matchMedia = vi.fn().mockImplementation((query) => ({
    matches: false,
    media: query,
    onchange: null,
    addListener: vi.fn(), // API vieja, todavía la pide algún polyfill
    removeListener: vi.fn(),
    addEventListener: vi.fn(),
    removeEventListener: vi.fn(),
    dispatchEvent: vi.fn(),
  }));
}

// ResizeObserver: lo usan template-designer.jsx y bracket-connectors.jsx
// para reaccionar a cambios de tamaño del lienzo/las líneas del bracket.
// jsdom no mide layout real, así que el valor que reporte no importa para
// estas pruebas -- lo que importa es que exista, para que el componente no
// tire "ResizeObserver is not defined" al montarse.
class ResizeObserverStub {
  observe() {}
  unobserve() {}
  disconnect() {}
}
if (!("ResizeObserver" in window)) {
  window.ResizeObserver = ResizeObserverStub;
  globalThis.ResizeObserver = ResizeObserverStub;
}

// URL.createObjectURL / revokeObjectURL: lib/download-blob.js los usa para
// convertir la respuesta binaria de un reporte/PDF en un enlace de descarga.
// jsdom no implementa el lado de Blob de la API URL.
if (!URL.createObjectURL) {
  URL.createObjectURL = vi.fn(() => "blob:mock-url");
}
if (!URL.revokeObjectURL) {
  URL.revokeObjectURL = vi.fn();
}

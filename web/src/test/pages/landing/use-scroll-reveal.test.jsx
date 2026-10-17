import { describe, expect, it, vi, beforeEach } from "vitest";
import { render } from "@testing-library/react";

// registerPlugin no se verifica acá: se llama una sola vez al cargar el
// módulo, antes de que corra cualquier prueba, y vitest.config.js tiene
// restoreMocks: true -- eso limpia el historial de todos los mocks antes de
// CADA prueba, incluida la primera, así que esa llamada queda borrada antes
// de que una aserción pueda verla. El comportamiento real (ScrollTrigger
// registrado) ya lo confirman las pruebas de abajo, que sí logran disparar
// scrollTrigger sin que gsap se queje de un plugin no registrado.
const { mockContext, mockFrom } = vi.hoisted(() => ({
  mockContext: vi.fn((fn) => {
    fn();
    return { revert: vi.fn() };
  }),
  mockFrom: vi.fn(),
}));

vi.mock("gsap", () => ({
  default: { context: mockContext, from: mockFrom, registerPlugin: vi.fn() },
}));

import { useScrollReveal } from "src/pages/landing/use-scroll-reveal";

function Probe({ options }) {
  const ref = useScrollReveal(options);
  return <div ref={ref} data-testid="scope" />;
}

describe("useScrollReveal", () => {
  it("con movimiento permitido, anima el selector dado contra el nodo del ref", () => {
    render(<Probe options={{ selector: ".item" }} />);

    expect(mockContext).toHaveBeenCalledOnce();
    expect(mockFrom).toHaveBeenCalledWith(
      ".item",
      expect.objectContaining({
        y: 40,
        opacity: 0,
        duration: 0.6,
        scrollTrigger: expect.objectContaining({ start: "top 85%" }),
      }),
    );
  });

  it("mezcla `from` y `scrollTrigger` sobre los defaults en vez de reemplazarlos", () => {
    render(<Probe options={{ selector: ".item", from: { stagger: 0.1 }, scrollTrigger: { start: "top 70%" } }} />);

    expect(mockFrom).toHaveBeenCalledWith(
      ".item",
      expect.objectContaining({ y: 40, opacity: 0, stagger: 0.1, scrollTrigger: expect.objectContaining({ start: "top 70%" }) }),
    );
  });

  it("con prefers-reduced-motion, no anima nada", () => {
    vi.spyOn(window, "matchMedia").mockReturnValue({ matches: true });

    render(<Probe options={{ selector: ".item" }} />);

    expect(mockContext).not.toHaveBeenCalled();
    expect(mockFrom).not.toHaveBeenCalled();
  });
});

import { describe, expect, it, vi } from "vitest";
import { render } from "@testing-library/react";

// registerPlugin no se verifica acá por el mismo motivo que en
// use-scroll-reveal.test.jsx: se llama una sola vez al cargar el módulo, y
// restoreMocks: true (vitest.config.js) borra esa llamada antes de la
// primera prueba.
const { mockTimelineInstance, mockTimeline, mockContext, mockScrollTriggerCreate } = vi.hoisted(() => {
  const instance = { to: vi.fn(function () { return this; }), play: vi.fn() };
  return {
    mockTimelineInstance: instance,
    mockTimeline: vi.fn(() => instance),
    mockContext: vi.fn((fn) => {
      fn();
      return { revert: vi.fn() };
    }),
    // onEnter corre en el momento, como si el elemento ya hubiera entrado en
    // pantalla -- jsdom no mide scroll real, así que no hay "más adelante"
    // que simular.
    mockScrollTriggerCreate: vi.fn(({ onEnter }) => onEnter()),
  };
});

vi.mock("gsap", () => ({
  default: { context: mockContext, timeline: mockTimeline, registerPlugin: vi.fn() },
}));
vi.mock("gsap/ScrollTrigger", () => ({
  ScrollTrigger: { create: mockScrollTriggerCreate },
}));

import { useLoopReveal } from "src/pages/landing/use-loop-reveal";

function Probe({ buildTimeline }) {
  const ref = useLoopReveal(buildTimeline);
  return <div ref={ref} data-testid="scope" />;
}

describe("useLoopReveal", () => {
  it("con movimiento permitido, arma la timeline con repeat: -1 y la deja pausada hasta que ScrollTrigger avisa", () => {
    const buildTimeline = vi.fn();
    render(<Probe buildTimeline={buildTimeline} />);

    expect(mockContext).toHaveBeenCalledOnce();
    expect(mockTimeline).toHaveBeenCalledWith({ repeat: -1, paused: true });
    expect(buildTimeline).toHaveBeenCalledWith(mockTimelineInstance);
  });

  it("arranca (play) la timeline cuando el ScrollTrigger entra en pantalla", () => {
    render(<Probe buildTimeline={vi.fn()} />);

    expect(mockScrollTriggerCreate).toHaveBeenCalledWith(
      expect.objectContaining({ start: "top 85%", once: true }),
    );
    expect(mockTimelineInstance.play).toHaveBeenCalledOnce();
  });

  it("con prefers-reduced-motion, no arma ninguna timeline", () => {
    vi.spyOn(window, "matchMedia").mockReturnValue({ matches: true });
    const buildTimeline = vi.fn();

    render(<Probe buildTimeline={buildTimeline} />);

    expect(mockContext).not.toHaveBeenCalled();
    expect(buildTimeline).not.toHaveBeenCalled();
  });
});

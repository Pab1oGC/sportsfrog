import { describe, expect, it, vi } from "vitest";
import { screen } from "@testing-library/react";
import { renderWithProviders } from "src/test/render";

const { mockTimelineInstance } = vi.hoisted(() => ({
  mockTimelineInstance: { to: vi.fn(function () { return this; }), play: vi.fn() },
}));

vi.mock("gsap", () => ({
  default: {
    context: (fn) => {
      fn();
      return { revert: vi.fn() };
    },
    timeline: () => mockTimelineInstance,
    registerPlugin: vi.fn(),
  },
}));
vi.mock("gsap/ScrollTrigger", () => ({
  ScrollTrigger: { create: ({ onEnter }) => onEnter() },
}));

import { CredentialsVisual } from "src/pages/landing/visuals/credentials-visual";

describe("CredentialsVisual", () => {
  it("muestra el editor de plantillas (lienzo, tabs, campos) sin romper", () => {
    renderWithProviders(<CredentialsVisual />);

    expect(screen.getByText("Diseño: Credencial Sub-14")).toBeInTheDocument();
    expect(screen.getByText("Anverso")).toBeInTheDocument();
    expect(screen.getByText("Reverso")).toBeInTheDocument();
    expect(screen.getByText("Campos")).toBeInTheDocument();
    expect(screen.getByText("Foto")).toBeInTheDocument();
    expect(screen.getByText("Nombre")).toBeInTheDocument();
    expect(screen.getByText("QR")).toBeInTheDocument();
  });

  it("arma una timeline en loop para el ciclo de selección de campos", () => {
    renderWithProviders(<CredentialsVisual />);
    // 3 campos x 2 pasos (resaltar, apagar) + el respiro final = 7 llamadas a .to()
    expect(mockTimelineInstance.to).toHaveBeenCalledTimes(7);
  });
});

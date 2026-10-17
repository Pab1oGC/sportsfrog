import { describe, expect, it } from "vitest";
import { renderWithProviders } from "src/test/render";
import { VisualFrame } from "src/pages/landing/visuals/visual-frame";

describe("VisualFrame", () => {
  it("renderiza su contenido dentro del <svg>", () => {
    const { container } = renderWithProviders(
      <VisualFrame>
        <circle data-testid="contenido" cx={10} cy={10} r={5} />
      </VisualFrame>,
    );
    expect(container.querySelector('[data-testid="contenido"]')).toBeInTheDocument();
  });

  it("dos instancias montadas a la vez -- como las cinco secciones de la landing -- no repiten el id del patrón de puntos", () => {
    const { container } = renderWithProviders(
      <div>
        <VisualFrame>
          <circle />
        </VisualFrame>
        <VisualFrame>
          <circle />
        </VisualFrame>
      </div>,
    );

    const patternIds = [...container.querySelectorAll("pattern")].map((pattern) => pattern.id);
    expect(patternIds).toHaveLength(2);
    expect(new Set(patternIds).size).toBe(2);
    expect(patternIds.every(Boolean)).toBe(true);
  });
});

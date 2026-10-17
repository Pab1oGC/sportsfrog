import { describe, expect, it } from "vitest";
import { renderWithProviders } from "src/test/render";
import { useDotPattern } from "src/pages/landing/use-dot-pattern";

function Probe({ options, testId }) {
  const dots = useDotPattern(options);
  return (
    <svg>
      <defs>{dots.pattern}</defs>
      <rect data-testid={testId} fill={dots.fill} />
    </svg>
  );
}

describe("useDotPattern", () => {
  it("el fill del rect apunta al id del pattern que devuelve", () => {
    const { container } = renderWithProviders(<Probe options={{ color: "red" }} testId="r" />);
    const pattern = container.querySelector("pattern");
    const rect = container.querySelector('[data-testid="r"]');
    expect(pattern.id).toBeTruthy();
    expect(rect.getAttribute("fill")).toBe(`url(#${pattern.id})`);
  });

  it("dos instancias montadas a la vez no repiten id de patrón", () => {
    const { container } = renderWithProviders(
      <div>
        <Probe options={{ color: "red" }} testId="a" />
        <Probe options={{ color: "blue" }} testId="b" />
      </div>,
    );
    const ids = [...container.querySelectorAll("pattern")].map((p) => p.id);
    expect(ids).toHaveLength(2);
    expect(new Set(ids).size).toBe(2);
  });

  it("respeta size y radius en vez de usar siempre el default", () => {
    const { container } = renderWithProviders(<Probe options={{ color: "red", size: 40, radius: 3 }} testId="r" />);
    const pattern = container.querySelector("pattern");
    const circle = container.querySelector("circle");
    expect(pattern.getAttribute("width")).toBe("40");
    expect(circle.getAttribute("r")).toBe("3");
  });
});

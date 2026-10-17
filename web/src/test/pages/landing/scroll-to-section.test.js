import { describe, expect, it, vi, afterEach } from "vitest";
import { scrollToSection } from "src/pages/landing/scroll-to-section";

afterEach(() => {
  document.body.innerHTML = "";
});

describe("scrollToSection", () => {
  it("hace scroll al elemento con ese id", () => {
    const target = document.createElement("div");
    target.id = "reglamento";
    target.scrollIntoView = vi.fn();
    document.body.appendChild(target);

    scrollToSection("reglamento");

    expect(target.scrollIntoView).toHaveBeenCalledWith(expect.objectContaining({ block: "start" }));
  });

  it("si el id no existe en el DOM, no rompe", () => {
    expect(() => scrollToSection("no-existe")).not.toThrow();
  });

  it("respeta prefers-reduced-motion: behavior 'auto' en vez de 'smooth'", () => {
    vi.spyOn(window, "matchMedia").mockReturnValue({ matches: true });
    const target = document.createElement("div");
    target.id = "reglamento";
    target.scrollIntoView = vi.fn();
    document.body.appendChild(target);

    scrollToSection("reglamento");

    expect(target.scrollIntoView).toHaveBeenCalledWith(expect.objectContaining({ behavior: "auto" }));
  });
});

import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";

vi.mock("src/pages/public/hero/standard-hero", () => ({
  StandardHero: () => <div data-testid="hero-standard" />,
}));
vi.mock("src/pages/public/hero/scoreboard-hero", () => ({
  ScoreboardHero: () => <div data-testid="hero-scoreboard" />,
}));
vi.mock("src/pages/public/hero/editorial-hero", () => ({
  EditorialHero: () => <div data-testid="hero-editorial" />,
}));
vi.mock("src/pages/public/hero/live-hero", () => ({
  LiveHero: () => <div data-testid="hero-live" />,
}));

import { PortalHero } from "src/pages/public/portal-hero";

const compFixture = { name: "Copa Apertura", organizationName: "Liga Test", season: "2026", status: "in_progress" };

function montar(heroVariant) {
  return render(<PortalHero comp={compFixture} portal={{ theme: { heroVariant } }} onBack={() => {}} />);
}

describe("PortalHero -- despachador de variante", () => {
  it("theme.heroVariant 'standard' monta StandardHero", () => {
    montar("standard");
    expect(screen.getByTestId("hero-standard")).toBeInTheDocument();
  });

  it("theme.heroVariant 'scoreboard' monta ScoreboardHero", () => {
    montar("scoreboard");
    expect(screen.getByTestId("hero-scoreboard")).toBeInTheDocument();
  });

  it("theme.heroVariant 'editorial' monta EditorialHero", () => {
    montar("editorial");
    expect(screen.getByTestId("hero-editorial")).toBeInTheDocument();
  });

  it("theme.heroVariant 'live' monta LiveHero", () => {
    montar("live");
    expect(screen.getByTestId("hero-live")).toBeInTheDocument();
  });

  it("una variante desconocida cae en StandardHero", () => {
    montar("algo-que-no-existe");
    expect(screen.getByTestId("hero-standard")).toBeInTheDocument();
  });

  it("sin theme (portal sin personalizar), también cae en StandardHero", () => {
    render(<PortalHero comp={compFixture} portal={{}} onBack={() => {}} />);
    expect(screen.getByTestId("hero-standard")).toBeInTheDocument();
  });
});

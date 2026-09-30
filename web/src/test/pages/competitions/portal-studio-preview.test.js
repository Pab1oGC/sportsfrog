import { describe, expect, it } from "vitest";
import { previewImg, fakeComp, formToPreviewPortal, defaultSectionLabel } from "src/pages/competitions/portal-studio-preview";
import { DEFAULT_PORTAL_THEME, PORTAL_SECTIONS } from "src/lib/portal-theme";

describe("previewImg", () => {
  it("una imagen recién elegida (data URL) se muestra tal cual", () => {
    expect(previewImg("data:image/png;base64,AAAA", "https://cdn/old.png")).toBe("data:image/png;base64,AAAA");
  });

  it("una key ya guardada (no data URL) se muestra por su currentUrl firmada", () => {
    expect(previewImg("banners/x.png", "https://cdn/x.png")).toBe("https://cdn/x.png");
  });

  it("sin key, da null aunque haya un currentUrl", () => {
    expect(previewImg(null, "https://cdn/x.png")).toBeNull();
    expect(previewImg("", "https://cdn/x.png")).toBeNull();
  });
});

describe("fakeComp", () => {
  it("arma el comp mínimo que PortalHero necesita, con un nombre de organización de relleno", () => {
    const row = { name: "Copa Apertura", season: "2026", sportCode: "football", status: "draft", format: "league", startsOn: "2026-03-01", endsOn: "2026-06-01" };
    expect(fakeComp(row)).toEqual({
      name: "Copa Apertura",
      organizationName: "(tu organización)",
      season: "2026",
      sportName: "football",
      status: "draft",
      format: "league",
      startsOn: "2026-03-01",
      endsOn: "2026-06-01",
    });
  });

  it("usa sportName si viene, en vez del código crudo", () => {
    expect(fakeComp({ sportName: "Fútbol", sportCode: "football" }).sportName).toBe("Fútbol");
  });
});

describe("formToPreviewPortal", () => {
  function formBase(overrides = {}) {
    return {
      accentColor: "",
      theme: { ...DEFAULT_PORTAL_THEME },
      sectionOrder: PORTAL_SECTIONS,
      bannerKey: null, currentBannerUrl: null,
      logoKey: null, currentLogoUrl: null,
      description: "", instagram: "", facebook: "", whatsApp: "", website: "",
      sponsors: [],
      gallery: [],
      ...overrides,
    };
  }

  it("sin form todavía (null), da null -- la vista previa no tiene nada que dibujar", () => {
    expect(formToPreviewPortal(null)).toBeNull();
  });

  it("campos de texto vacíos van como null, igual que en buildPortalPayload", () => {
    const preview = formToPreviewPortal(formBase());
    expect(preview.description).toBeNull();
    expect(preview.instagram).toBeNull();
    expect(preview.accentColor).toBeNull();
  });

  it("bannerUrl/logoUrl salen de previewImg, no de la key cruda", () => {
    const preview = formToPreviewPortal(formBase({ bannerKey: "data:image/png;base64,AAAA", logoKey: "logos/x.png", currentLogoUrl: "https://cdn/x.png" }));
    expect(preview.bannerUrl).toBe("data:image/png;base64,AAAA");
    expect(preview.logoUrl).toBe("https://cdn/x.png");
  });

  it("un auspiciante sin logo resuelto (ni data URL ni currentLogoUrl) se descarta de la vista previa", () => {
    const preview = formToPreviewPortal(formBase({ sponsors: [{ name: "ACME", url: "", logoKey: "logos/sin-preview.png", currentLogoUrl: null }] }));
    expect(preview.sponsors).toEqual([]);
  });

  it("un auspiciante con logo resuelto sí aparece, con name/url en null si vinieron vacíos", () => {
    const preview = formToPreviewPortal(formBase({ sponsors: [{ name: "", url: "", logoKey: "data:image/png;base64,AAAA", currentLogoUrl: null }] }));
    expect(preview.sponsors).toEqual([{ name: null, url: null, logoUrl: "data:image/png;base64,AAAA" }]);
  });

  it("una foto de galería sin url resuelta se descarta", () => {
    const preview = formToPreviewPortal(formBase({ gallery: [{ caption: "Final", key: "", currentUrl: null }] }));
    expect(preview.gallery).toEqual([]);
  });

  it("el theme conserva los valores de forma tal cual (heroStyle, corners, etc.) sin transformarlos", () => {
    const preview = formToPreviewPortal(formBase({ theme: { ...DEFAULT_PORTAL_THEME, heroStyle: "gradient", corners: "sharp" } }));
    expect(preview.theme.heroStyle).toBe("gradient");
    expect(preview.theme.corners).toBe("sharp");
  });
});

describe("defaultSectionLabel", () => {
  it("devuelve la etiqueta por defecto de una sección conocida", () => {
    expect(defaultSectionLabel("standings")).toBe(PORTAL_SECTIONS.find((s) => s.key === "standings").label);
  });

  it("una clave desconocida se devuelve tal cual, no revienta", () => {
    expect(defaultSectionLabel("seccion-que-no-existe")).toBe("seccion-que-no-existe");
  });
});

import { describe, expect, it } from "vitest";
import { EMPTY_PORTAL_FORM, readPortalForm, buildPortalPayload } from "./portal-payload";
import { DEFAULT_PORTAL_THEME, PORTAL_SECTIONS, resolvePortalSections } from "src/lib/portal-theme";

describe("readPortalForm", () => {
  it("row null/undefined da exactamente el formulario vacío por defecto (EMPTY_PORTAL_FORM)", () => {
    expect(readPortalForm(null)).toEqual(EMPTY_PORTAL_FORM);
    expect(readPortalForm(undefined)).toEqual(EMPTY_PORTAL_FORM);
  });

  it("showStandings/showLeaders/showClassification/showGallery: prendidos salvo que digan false explícito", () => {
    const encendidosPorOmision = readPortalForm({ settings: { public: {} } });
    expect(encendidosPorOmision.showStandings).toBe(true);
    expect(encendidosPorOmision.showLeaders).toBe(true);
    expect(encendidosPorOmision.showClassification).toBe(true);
    expect(encendidosPorOmision.showGallery).toBe(true);

    const apagados = readPortalForm({
      settings: { public: { showStandings: false, showLeaders: false, showClassification: false, showGallery: false } },
    });
    expect(apagados.showStandings).toBe(false);
    expect(apagados.showLeaders).toBe(false);
    expect(apagados.showClassification).toBe(false);
    expect(apagados.showGallery).toBe(false);
  });

  it("showRosters/showAthletePhotos: apagados salvo que digan true explícito (RNF-16, polaridad invertida)", () => {
    const sinDecir = readPortalForm({ settings: { public: {} } });
    expect(sinDecir.showRosters).toBe(false);
    expect(sinDecir.showAthletePhotos).toBe(false);

    const prendidos = readPortalForm({ settings: { public: { showRosters: true, showAthletePhotos: true } } });
    expect(prendidos.showRosters).toBe(true);
    expect(prendidos.showAthletePhotos).toBe(true);
  });

  it("bannerKey/logoKey se leen de settings.public; las urls de vista previa vienen de publicPreview", () => {
    const form = readPortalForm({
      settings: { public: { bannerKey: "banners/x.png", logoKey: "logos/y.png" } },
      publicPreview: { bannerUrl: "https://cdn/banner.png", logoUrl: "https://cdn/logo.png" },
    });
    expect(form.bannerKey).toBe("banners/x.png");
    expect(form.currentBannerUrl).toBe("https://cdn/banner.png");
    expect(form.logoKey).toBe("logos/y.png");
    expect(form.currentLogoUrl).toBe("https://cdn/logo.png");
  });

  it("heroStyle sin elegir cae en 'image' si ya hay un bannerKey cargado, o 'solid' si no", () => {
    const conBanner = readPortalForm({ settings: { public: { bannerKey: "banners/x.png" } } });
    expect(conBanner.theme.heroStyle).toBe("image");

    const sinBanner = readPortalForm({ settings: { public: {} } });
    expect(sinBanner.theme.heroStyle).toBe("solid");
  });

  it("heroStyle explícito gana siempre, tenga o no banner cargado", () => {
    const form = readPortalForm({ settings: { public: { bannerKey: "x.png", theme: { heroStyle: "gradient" } } } });
    expect(form.theme.heroStyle).toBe("gradient");
  });

  it("focusX/focusY: 0 es un valor real y se conserva, no cae al default 50", () => {
    const form = readPortalForm({ settings: { public: { theme: { focusX: 0, focusY: 0 } } } });
    expect(form.theme.focusX).toBe(0);
    expect(form.theme.focusY).toBe(0);
  });

  it("focusX/focusY ausentes caen en 50 (centrado)", () => {
    expect(EMPTY_PORTAL_FORM.theme.focusX).toBe(50);
    expect(EMPTY_PORTAL_FORM.theme.focusY).toBe(50);
  });

  it("showLogoBackground: prendido salvo que diga false explícito, igual criterio que las secciones de arriba", () => {
    expect(readPortalForm({ settings: { public: {} } }).theme.showLogoBackground).toBe(true);
    expect(
      readPortalForm({ settings: { public: { theme: { showLogoBackground: false } } } }).theme.showLogoBackground,
    ).toBe(false);
  });

  it("sponsors: cada uno lleva su vista previa emparejada por logoKey", () => {
    const form = readPortalForm({
      settings: { public: { sponsors: [{ logoKey: "s1", name: "ACME", url: "https://acme.test" }] } },
      publicPreview: { sponsors: [{ logoKey: "s1", logoUrl: "https://cdn/s1.png" }] },
    });
    expect(form.sponsors).toEqual([
      { logoKey: "s1", name: "ACME", url: "https://acme.test", currentLogoUrl: "https://cdn/s1.png" },
    ]);
  });

  it("sponsors sin vista previa cargada aún: currentLogoUrl null, no undefined", () => {
    const form = readPortalForm({ settings: { public: { sponsors: [{ logoKey: "s1" }] } } });
    expect(form.sponsors[0].currentLogoUrl).toBeNull();
  });

  it("gallery: mismo emparejamiento que sponsors, pero por 'key'/'url' en vez de 'logoKey'/'logoUrl'", () => {
    const form = readPortalForm({
      settings: { public: { gallery: [{ key: "g1", caption: "Final" }] } },
      publicPreview: { gallery: [{ key: "g1", url: "https://cdn/g1.jpg" }] },
    });
    expect(form.gallery).toEqual([{ key: "g1", caption: "Final", currentUrl: "https://cdn/g1.jpg" }]);
  });

  it("sectionOrder pasa por resolvePortalSections -- siempre las seis secciones, no solo las guardadas", () => {
    const form = readPortalForm({ settings: { public: { sectionOrder: [{ key: "bracket" }] } } });
    expect(form.sectionOrder).toEqual(resolvePortalSections([{ key: "bracket" }]));
    expect(form.sectionOrder).toHaveLength(PORTAL_SECTIONS.length);
  });

  it("campos de texto libres (description, redes, accentColor) caen en cadena vacía si no vinieron", () => {
    const form = readPortalForm({ settings: { public: {} } });
    expect(form.description).toBe("");
    expect(form.instagram).toBe("");
    expect(form.facebook).toBe("");
    expect(form.whatsApp).toBe("");
    expect(form.website).toBe("");
    expect(form.accentColor).toBe("");
  });
});

describe("buildPortalPayload", () => {
  const formaBase = () => ({
    ...EMPTY_PORTAL_FORM,
    showStandings: true,
    showLeaders: true,
    showClassification: true,
    showRosters: false,
    showAthletePhotos: false,
    showGallery: true,
  });

  it("los interruptores viajan tal cual, sin transformación", () => {
    const f = { ...formaBase(), showRosters: true };
    const payload = buildPortalPayload(f);
    expect(payload.showRosters).toBe(true);
    expect(payload.showStandings).toBe(true);
  });

  it("bannerKey/logoKey vacíos van como null, no como cadena vacía", () => {
    const payload = buildPortalPayload({ ...formaBase(), bannerKey: "", logoKey: "" });
    expect(payload.bannerKey).toBeNull();
    expect(payload.logoKey).toBeNull();
  });

  it("accentColor solo viaja si es un hex válido; si no, null", () => {
    expect(buildPortalPayload({ ...formaBase(), accentColor: "#112233" }).accentColor).toBe("#112233");
    expect(buildPortalPayload({ ...formaBase(), accentColor: "no-es-color" }).accentColor).toBeNull();
    expect(buildPortalPayload({ ...formaBase(), accentColor: "" }).accentColor).toBeNull();
  });

  it("campos de texto libres vacíos van como null", () => {
    const payload = buildPortalPayload({
      ...formaBase(),
      description: "",
      instagram: "",
      facebook: "",
      whatsApp: "",
      website: "",
    });
    expect(payload.description).toBeNull();
    expect(payload.instagram).toBeNull();
    expect(payload.facebook).toBeNull();
    expect(payload.whatsApp).toBeNull();
    expect(payload.website).toBeNull();
  });

  it("campos de texto libres con contenido viajan tal cual", () => {
    const payload = buildPortalPayload({ ...formaBase(), description: "Torneo anual", website: "https://x.test" });
    expect(payload.description).toBe("Torneo anual");
    expect(payload.website).toBe("https://x.test");
  });

  describe("sponsors", () => {
    it("un arreglo vacío o ausente se manda como null, no como []", () => {
      expect(buildPortalPayload({ ...formaBase(), sponsors: [] }).sponsors).toBeNull();
      expect(buildPortalPayload({ ...formaBase(), sponsors: undefined }).sponsors).toBeNull();
    });

    it("con contenido, se mandan solo logoKey/name/url -- currentLogoUrl (solo de vista previa) no viaja", () => {
      const payload = buildPortalPayload({
        ...formaBase(),
        sponsors: [{ logoKey: "s1", name: "ACME", url: "https://acme.test", currentLogoUrl: "https://cdn/s1.png" }],
      });
      expect(payload.sponsors).toEqual([{ logoKey: "s1", name: "ACME", url: "https://acme.test" }]);
    });

    it("name/url vacíos dentro de un sponsor van como null", () => {
      const payload = buildPortalPayload({ ...formaBase(), sponsors: [{ logoKey: "s1", name: "", url: "" }] });
      expect(payload.sponsors[0]).toEqual({ logoKey: "s1", name: null, url: null });
    });
  });

  describe("gallery", () => {
    it("un arreglo vacío o ausente se manda como null", () => {
      expect(buildPortalPayload({ ...formaBase(), gallery: [] }).gallery).toBeNull();
    });

    it("con contenido, se manda solo key/caption -- currentUrl no viaja", () => {
      const payload = buildPortalPayload({
        ...formaBase(),
        gallery: [{ key: "g1", caption: "Final", currentUrl: "https://cdn/g1.jpg" }],
      });
      expect(payload.gallery).toEqual([{ key: "g1", caption: "Final" }]);
    });

    it("caption vacío va como null", () => {
      const payload = buildPortalPayload({ ...formaBase(), gallery: [{ key: "g1", caption: "" }] });
      expect(payload.gallery[0].caption).toBeNull();
    });
  });

  describe("sectionOrder", () => {
    it("ausente o vacío da null", () => {
      expect(buildPortalPayload({ ...formaBase(), sectionOrder: undefined }).sectionOrder).toBeNull();
      expect(buildPortalPayload({ ...formaBase(), sectionOrder: [] }).sectionOrder).toBeNull();
    });

    it("el orden y las etiquetas por defecto (sin tocar nada) dan null -- no marca la competencia como personalizada", () => {
      const payload = buildPortalPayload({ ...formaBase(), sectionOrder: resolvePortalSections(null) });
      expect(payload.sectionOrder).toBeNull();
    });

    it("reordenar sin cambiar etiquetas: cada fila lleva su clave, con label null (sigue siendo la de siempre)", () => {
      const reordenado = [...PORTAL_SECTIONS].reverse();
      const payload = buildPortalPayload({ ...formaBase(), sectionOrder: reordenado });
      expect(payload.sectionOrder.map((s) => s.key)).toEqual(reordenado.map((s) => s.key));
      expect(payload.sectionOrder.every((s) => s.label === null)).toBe(true);
    });

    it("una etiqueta personalizada (distinta de la de siempre) sí viaja, incluso sin reordenar", () => {
      const personalizado = PORTAL_SECTIONS.map((s) => (s.key === "standings" ? { ...s, label: "Tabla" } : s));
      const payload = buildPortalPayload({ ...formaBase(), sectionOrder: personalizado });
      const fila = payload.sectionOrder.find((s) => s.key === "standings");
      expect(fila.label).toBe("Tabla");
      // El resto, sin tocar, sigue viajando como null.
      expect(payload.sectionOrder.find((s) => s.key === "leaders").label).toBeNull();
    });
  });

  describe("theme (portalThemePayload)", () => {
    it("f.theme ausente da null", () => {
      expect(buildPortalPayload({ ...formaBase(), theme: null }).theme).toBeNull();
    });

    it("el tema en todos sus valores por defecto da null -- abrir el estudio sin tocar nada no personaliza", () => {
      const payload = buildPortalPayload({ ...formaBase(), bannerKey: null, theme: { ...DEFAULT_PORTAL_THEME } });
      expect(payload.theme).toBeNull();
    });

    it.each([
      ["primary", { primary: "#112233" }],
      ["primaryContrast", { primaryContrast: "#ffffff" }],
      ["secondary", { secondary: "#112233" }],
      ["surface", { surface: "#112233" }],
      ["headingFont", { headingFont: "oswald" }],
      ["corners", { corners: "sharp" }],
      ["heroStyle", { heroStyle: "gradient" }],
      ["heroGradientTo", { heroGradientTo: "#112233" }],
      ["focusX", { focusX: 10 }],
      ["focusY", { focusY: 90 }],
      ["density", { density: "compact" }],
      ["decoration", { decoration: "bold" }],
      ["contentFigure", { contentFigure: "wave" }],
      ["contentFigureColor", { contentFigureColor: "#112233" }],
      ["showLogoBackground", { showLogoBackground: false }],
      ["heroLayout", { heroLayout: "centered" }],
      ["heroVariant", { heroVariant: "scoreboard" }],
      ["standingsVariant", { standingsVariant: "cards" }],
      ["matchCardVariant", { matchCardVariant: "compact" }],
      ["bracketVariant", { bracketVariant: "detailed" }],
    ])("tocar solo '%s' alcanza para que el tema deje de ser null", (_campo, cambio) => {
      const payload = buildPortalPayload({
        ...formaBase(),
        bannerKey: null,
        theme: { ...DEFAULT_PORTAL_THEME, ...cambio },
      });
      expect(payload.theme).not.toBeNull();
    });

    it("al tocar un solo campo, los demás viajan como null (no repiten el valor por defecto)", () => {
      const payload = buildPortalPayload({
        ...formaBase(),
        bannerKey: null,
        theme: { ...DEFAULT_PORTAL_THEME, primary: "#112233" },
      });
      expect(payload.theme.primary).toBe("#112233");
      expect(payload.theme.headingFont).toBeNull();
      expect(payload.theme.corners).toBeNull();
      expect(payload.theme.heroStyle).toBeNull();
      expect(payload.theme.density).toBeNull();
    });

    it("showLogoBackground viaja siempre como booleano explícito una vez que el tema está personalizado", () => {
      const payload = buildPortalPayload({
        ...formaBase(),
        bannerKey: null,
        theme: { ...DEFAULT_PORTAL_THEME, primary: "#112233" }, // customización disparada por otro campo
      });
      // showLogoBackground no se tocó (sigue en true), pero al viajar el resto
      // del tema, este campo va como boolean explícito, no como null.
      expect(payload.theme.showLogoBackground).toBe(true);
    });

    it("un color inválido en cualquier campo de color no cuenta como personalización ni viaja", () => {
      const payload = buildPortalPayload({
        ...formaBase(),
        bannerKey: null,
        theme: { ...DEFAULT_PORTAL_THEME, primary: "no-es-color" },
      });
      expect(payload.theme).toBeNull();
    });

    it("heroStyle: con bannerKey cargado, el default pasa a ser 'image' -- dejar 'solid' a mano ya cuenta como personalización", () => {
      // Mismo cálculo de defaultHero que readPortalForm: una vez que hay
      // portada subida, "solid" explícito ya no es el default implícito.
      const payload = buildPortalPayload({
        ...formaBase(),
        bannerKey: "banners/x.png",
        theme: { ...DEFAULT_PORTAL_THEME, heroStyle: "solid" },
      });
      expect(payload.theme).not.toBeNull();
      expect(payload.theme.heroStyle).toBe("solid");
    });

    it("heroStyle 'image' con bannerKey cargado coincide con el default calculado: no personaliza por sí solo", () => {
      const payload = buildPortalPayload({
        ...formaBase(),
        bannerKey: "banners/x.png",
        theme: { ...DEFAULT_PORTAL_THEME, heroStyle: "image" },
      });
      expect(payload.theme).toBeNull();
    });
  });
});

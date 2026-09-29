import { describe, expect, it } from "vitest";
import { createAppTheme } from "src/theme";
import {
  PORTAL_FONTS,
  PORTAL_CORNERS,
  PORTAL_SECTIONS,
  DEFAULT_PORTAL_THEME,
  parseHex,
  isHex,
  contrastRatio,
  readableTextOn,
  lighten,
  darken,
  portalFontStack,
  portalFontHref,
  resolvePortalSections,
  heroBackground,
  buildPortalTheme,
} from "./portal-theme";

describe("parseHex / isHex", () => {
  it("acepta '#rrggbb' y descompone los tres canales", () => {
    expect(parseHex("#ff0000")).toEqual({ r: 255, g: 0, b: 0 });
    expect(parseHex("#00ff00")).toEqual({ r: 0, g: 255, b: 0 });
    expect(parseHex("#0000ff")).toEqual({ r: 0, g: 0, b: 255 });
  });

  it("acepta el hex sin '#' y con espacios alrededor", () => {
    expect(parseHex("ff0000")).toEqual({ r: 255, g: 0, b: 0 });
    expect(parseHex("  #ff0000  ")).toEqual({ r: 255, g: 0, b: 0 });
  });

  it("es insensible a mayúsculas/minúsculas", () => {
    expect(parseHex("#FF00AA")).toEqual({ r: 255, g: 0, b: 170 });
  });

  it("'#fff' (abreviado) da null -- exige los 6 dígitos, no 3", () => {
    expect(parseHex("#fff")).toBeNull();
  });

  it("un valor con caracteres no hexadecimales da null", () => {
    expect(parseHex("#gggggg")).toBeNull();
  });

  it("valores que no son string dan null, sin reventar", () => {
    expect(parseHex(null)).toBeNull();
    expect(parseHex(undefined)).toBeNull();
    expect(parseHex(123456)).toBeNull();
    expect(parseHex({})).toBeNull();
  });

  it("isHex es el mismo chequeo como booleano", () => {
    expect(isHex("#336699")).toBe(true);
    expect(isHex("no es color")).toBe(false);
    expect(isHex(null)).toBe(false);
  });
});

describe("contrastRatio", () => {
  it("negro sobre blanco da el máximo de la escala, 21:1", () => {
    expect(contrastRatio("#000000", "#ffffff")).toBeCloseTo(21, 0);
  });

  it("un color contra sí mismo da 1:1 (sin contraste)", () => {
    expect(contrastRatio("#336699", "#336699")).toBeCloseTo(1, 5);
  });

  it("es simétrico: el orden de los dos colores no cambia el resultado", () => {
    expect(contrastRatio("#112233", "#eeddcc")).toBeCloseTo(contrastRatio("#eeddcc", "#112233"), 10);
  });

  it("null si cualquiera de los dos no es un hex válido", () => {
    expect(contrastRatio("no-color", "#ffffff")).toBeNull();
    expect(contrastRatio("#ffffff", "no-color")).toBeNull();
    expect(contrastRatio(null, "#ffffff")).toBeNull();
  });
});

describe("readableTextOn", () => {
  it("sobre un fondo claro, elige el texto oscuro", () => {
    expect(readableTextOn("#ffffff")).toBe("#111315");
  });

  it("sobre un fondo oscuro, elige el texto blanco", () => {
    expect(readableTextOn("#000000")).toBe("#ffffff");
  });

  it("un fondo inválido (no hex) cae en blanco por defecto", () => {
    expect(readableTextOn("no-color")).toBe("#ffffff");
  });
});

describe("lighten / darken", () => {
  it("lighten(t=1) da blanco puro sin importar el color de partida", () => {
    expect(lighten("#000000", 1)).toBe("#ffffff");
    expect(lighten("#336699", 1)).toBe("#ffffff");
  });

  it("lighten(t=0) devuelve el mismo color", () => {
    expect(lighten("#336699", 0)).toBe("#336699");
  });

  it("lighten(t=0.5) mezcla a mitad de camino hacia blanco", () => {
    expect(lighten("#000000", 0.5)).toBe("#808080");
  });

  it("darken(t=1) da negro puro", () => {
    expect(darken("#ffffff", 1)).toBe("#000000");
  });

  it("darken(t=0.5) mezcla a mitad de camino hacia negro", () => {
    expect(darken("#ffffff", 0.5)).toBe("#808080");
  });

  it("un valor no-hex se devuelve intacto, no revienta", () => {
    expect(lighten("no-color", 0.5)).toBe("no-color");
    expect(darken("no-color", 0.5)).toBe("no-color");
  });
});

describe("portalFontStack", () => {
  it("devuelve el stack de una fuente conocida", () => {
    const oswald = PORTAL_FONTS.find((f) => f.value === "oswald");
    expect(portalFontStack("oswald")).toBe(oswald.stack);
  });

  it("un valor desconocido cae en la primera fuente de la lista (Inter)", () => {
    expect(portalFontStack("fuente-que-no-existe")).toBe(PORTAL_FONTS[0].stack);
  });

  it("'inter' es la primera de la lista, la del cuerpo", () => {
    expect(PORTAL_FONTS[0].value).toBe("inter");
  });
});

describe("portalFontHref", () => {
  it("una fuente de Google Fonts arma la url con el parámetro family", () => {
    const oswald = PORTAL_FONTS.find((f) => f.value === "oswald");
    expect(portalFontHref("oswald")).toBe(`https://fonts.googleapis.com/css2?family=${oswald.google}&display=swap`);
  });

  it("'inter' no carga nada externo: null", () => {
    expect(portalFontHref("inter")).toBeNull();
  });

  it("un valor desconocido da null", () => {
    expect(portalFontHref("fuente-que-no-existe")).toBeNull();
  });
});

describe("resolvePortalSections", () => {
  it("sin nada guardado (null/undefined/[]), devuelve las seis secciones por defecto en orden canónico", () => {
    expect(resolvePortalSections(null)).toEqual(PORTAL_SECTIONS);
    expect(resolvePortalSections(undefined)).toEqual(PORTAL_SECTIONS);
    expect(resolvePortalSections([])).toEqual(PORTAL_SECTIONS);
  });

  it("respeta un orden guardado distinto del canónico", () => {
    const guardado = [{ key: "bracket" }, { key: "standings" }];
    const resultado = resolvePortalSections(guardado);
    expect(resultado[0].key).toBe("bracket");
    expect(resultado[1].key).toBe("standings");
  });

  it("agrega al final, en orden canónico, las secciones que no vinieron guardadas", () => {
    const guardado = [{ key: "bracket" }];
    const resultado = resolvePortalSections(guardado);
    const claves = resultado.map((s) => s.key);
    expect(claves[0]).toBe("bracket");
    // El resto, en el mismo orden en que aparecen en PORTAL_SECTIONS.
    const resto = PORTAL_SECTIONS.filter((s) => s.key !== "bracket").map((s) => s.key);
    expect(claves.slice(1)).toEqual(resto);
  });

  it("descarta claves desconocidas que ya no existen en PORTAL_SECTIONS", () => {
    const guardado = [{ key: "seccion-vieja-eliminada" }, { key: "standings" }];
    const resultado = resolvePortalSections(guardado);
    expect(resultado.some((s) => s.key === "seccion-vieja-eliminada")).toBe(false);
    expect(resultado).toHaveLength(PORTAL_SECTIONS.length);
  });

  it("una clave repetida solo cuenta la primera vez", () => {
    const guardado = [{ key: "standings", label: "Primera" }, { key: "standings", label: "Segunda" }];
    const resultado = resolvePortalSections(guardado);
    expect(resultado.filter((s) => s.key === "standings")).toHaveLength(1);
    expect(resultado.find((s) => s.key === "standings").label).toBe("Primera");
  });

  it("una etiqueta en blanco cae al nombre por defecto de esa sección", () => {
    const guardado = [{ key: "standings", label: "   " }];
    const resultado = resolvePortalSections(guardado);
    const defecto = PORTAL_SECTIONS.find((s) => s.key === "standings").label;
    expect(resultado.find((s) => s.key === "standings").label).toBe(defecto);
  });

  it("una etiqueta personalizada no vacía se conserva recortada", () => {
    const guardado = [{ key: "standings", label: "  Posiciones  " }];
    const resultado = resolvePortalSections(guardado);
    expect(resultado.find((s) => s.key === "standings").label).toBe("Posiciones");
  });

  it("una entrada nula o falsy en el arreglo guardado se ignora sin reventar", () => {
    const guardado = [null, { key: "standings" }];
    expect(() => resolvePortalSections(guardado)).not.toThrow();
    expect(resolvePortalSections(guardado).some((s) => s.key === "standings")).toBe(true);
  });

  it("siempre devuelve las seis secciones, ni una más ni una menos", () => {
    expect(resolvePortalSections([{ key: "standings" }])).toHaveLength(6);
  });
});

describe("heroBackground", () => {
  it("heroStyle 'image' con bannerUrl: superpone el overlay oscuro sobre la imagen", () => {
    const r = heroBackground({ heroStyle: "image", primary: "#336699", bannerUrl: "foto.jpg", focusX: 30, focusY: 70 });
    expect(r.backgroundImage).toBe("linear-gradient(180deg, rgba(0,0,0,0.35), rgba(0,0,0,0.6)), url(foto.jpg)");
    expect(r.backgroundPosition).toBe("30% 70%");
    expect(r.backgroundColor).toBe("#336699");
  });

  it("heroStyle 'image' sin bannerUrl cae a color plano (no hay imagen que mostrar)", () => {
    const r = heroBackground({ heroStyle: "image", primary: "#336699", bannerUrl: null });
    expect(r.backgroundImage).toBeUndefined();
    expect(r.backgroundColor).toBe("#336699");
  });

  it("heroStyle 'gradient' sin gradientTo elegido usa el principal oscurecido", () => {
    const r = heroBackground({ heroStyle: "gradient", primary: "#336699" });
    expect(r.backgroundImage).toBe(`linear-gradient(135deg, #336699, ${darken("#336699", 0.32)})`);
    expect(r.backgroundPosition).toBeUndefined();
  });

  it("heroStyle 'gradient' con gradientTo elegido lo usa tal cual, sin oscurecer", () => {
    const r = heroBackground({ heroStyle: "gradient", primary: "#336699", gradientTo: "#ff0000" });
    expect(r.backgroundImage).toBe("linear-gradient(135deg, #336699, #ff0000)");
  });

  it("heroStyle 'gradient' sin un primary válido cae a color plano (sin degradado que armar)", () => {
    const r = heroBackground({ heroStyle: "gradient", primary: "" });
    expect(r.backgroundImage).toBeUndefined();
    expect(r.backgroundColor).toBeUndefined();
  });

  it("heroStyle 'solid': solo el color, sin imagen ni posición", () => {
    const r = heroBackground({ heroStyle: "solid", primary: "#336699" });
    expect(r).toEqual({ backgroundColor: "#336699", backgroundImage: undefined, backgroundPosition: undefined });
  });

  it("focusX/focusY fuera de 0..100 se recortan a los bordes", () => {
    const r = heroBackground({ heroStyle: "image", bannerUrl: "x.jpg", focusX: -20, focusY: 150 });
    expect(r.backgroundPosition).toBe("0% 100%");
  });

  it("focusX/focusY no numéricos o ausentes caen en 50 (centrado)", () => {
    const r1 = heroBackground({ heroStyle: "image", bannerUrl: "x.jpg", focusX: undefined, focusY: undefined });
    expect(r1.backgroundPosition).toBe("50% 50%");

    const r2 = heroBackground({ heroStyle: "image", bannerUrl: "x.jpg", focusX: "no-numero", focusY: 10 });
    expect(r2.backgroundPosition).toBe("50% 10%");
  });
});

describe("buildPortalTheme", () => {
  const base = createAppTheme();

  it("sin tema ni accentColor heredado, devuelve exactamente el tema base (misma referencia)", () => {
    expect(buildPortalTheme({ base, portal: null })).toBe(base);
    expect(buildPortalTheme({ base, portal: {} })).toBe(base);
    expect(buildPortalTheme({ base, portal: { theme: null, accentColor: null } })).toBe(base);
  });

  it("un accentColor heredado (sin theme) alcanza para personalizar, aunque no exista objeto theme", () => {
    const result = buildPortalTheme({ base, portal: { accentColor: "#112233" } });
    expect(result).not.toBe(base);
    expect(result.palette.primary.main).toBe("#112233");
  });

  it("theme.primary, si es un hex válido, gana sobre el accentColor heredado", () => {
    const result = buildPortalTheme({
      base,
      portal: { accentColor: "#112233", theme: { primary: "#aabbcc" } },
    });
    expect(result.palette.primary.main).toBe("#aabbcc");
  });

  it("un primary inválido cae al primary del tema base, no rompe la construcción", () => {
    const result = buildPortalTheme({ base, portal: { theme: { primary: "no-es-color", corners: "sharp" } } });
    expect(result.palette.primary.main).toBe(base.palette.primary.main);
  });

  it("secondary sin elegir sigue al primary elegido (no al secondary del tema base)", () => {
    const result = buildPortalTheme({ base, portal: { theme: { primary: "#aabbcc" } } });
    expect(result.palette.secondary.main).toBe("#aabbcc");
  });

  it("primaryContrast explícito gana sobre el contraste calculado automáticamente", () => {
    const result = buildPortalTheme({
      base,
      portal: { theme: { primary: "#000000", primaryContrast: "#ff00ff" } },
    });
    expect(result.palette.primary.contrastText).toBe("#ff00ff");
  });

  it("sin primaryContrast, el contraste se calcula para ser legible sobre el primary", () => {
    const result = buildPortalTheme({ base, portal: { theme: { primary: "#000000" } } });
    expect(result.palette.primary.contrastText).toBe(readableTextOn("#000000"));
  });

  it("corners: 'sharp' da radio 0, 'round' dan el radio de la lista, sin elegir cae en 'soft' (12px)", () => {
    const sharp = buildPortalTheme({ base, portal: { theme: { primary: "#112233", corners: "sharp" } } });
    const round = buildPortalTheme({ base, portal: { theme: { primary: "#112233", corners: "round" } } });
    const sinElegir = buildPortalTheme({ base, portal: { theme: { primary: "#112233" } } });

    expect(sharp.shape.borderRadius).toBe(0);
    expect(round.shape.borderRadius).toBe(PORTAL_CORNERS.find((c) => c.value === "round").radius);
    expect(sinElegir.shape.borderRadius).toBe(12);
  });

  it("headingFont distinto de 'inter' sobreescribe la tipografía de h1..h6", () => {
    const result = buildPortalTheme({ base, portal: { theme: { primary: "#112233", headingFont: "oswald" } } });
    const stack = portalFontStack("oswald");
    ["h1", "h2", "h3", "h4", "h5", "h6"].forEach((variant) => {
      expect(result.typography[variant].fontFamily).toBe(stack);
    });
  });

  it("headingFont 'inter' (o sin elegir) no toca la tipografía del tema base", () => {
    const result = buildPortalTheme({ base, portal: { theme: { primary: "#112233" } } });
    expect(result.typography.h1.fontFamily).toBe(base.typography.h1.fontFamily);
  });

  it("density controla el espaciado: 'compact' usa 6px por unidad, 'spacious' usa 11px", () => {
    const compact = buildPortalTheme({ base, portal: { theme: { primary: "#112233", density: "compact" } } });
    const spacious = buildPortalTheme({ base, portal: { theme: { primary: "#112233", density: "spacious" } } });

    expect(compact.spacing(1)).toBe("6px");
    expect(spacious.spacing(1)).toBe("11px");
  });

  it("density sin elegir usa 'normal', 8px por unidad -- pixel-idéntico al tema base sin portal", () => {
    const result = buildPortalTheme({ base, portal: { theme: { primary: "#112233" } } });
    expect(result.spacing(1)).toBe("8px");
  });

  it("spacing sin argumentos equivale a un factor de 1", () => {
    const result = buildPortalTheme({ base, portal: { theme: { primary: "#112233" } } });
    expect(result.spacing()).toBe("8px");
  });

  it("spacing con varios factores los une con un espacio, como theme.spacing(1, 2) de MUI", () => {
    const result = buildPortalTheme({ base, portal: { theme: { primary: "#112233" } } });
    expect(result.spacing(1, 2)).toBe("8px 16px");
  });

  it("surface inválido o ausente cae en el gris por defecto del portal", () => {
    const result = buildPortalTheme({ base, portal: { theme: { primary: "#112233", surface: "no-color" } } });
    expect(result.palette.background.default).toBe("#f4f6f8");
  });

  it("surface válido se usa como fondo de página", () => {
    const result = buildPortalTheme({ base, portal: { theme: { primary: "#112233", surface: "#eeeeee" } } });
    expect(result.palette.background.default).toBe("#eeeeee");
  });
});

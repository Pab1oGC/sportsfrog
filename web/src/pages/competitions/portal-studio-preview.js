import { PORTAL_SECTIONS } from 'src/lib/portal-theme';

/**
 * Una imagen recién elegida (todavía sin subir, vive como data URL en el
 * propio formulario) se ve tal cual; una que ya estaba guardada se ve por su
 * URL firmada (`currentUrl`), no por su key de almacenamiento.
 */
export function previewImg(key, currentUrl) {
  if (!key) return null;
  return String(key).startsWith('data:') ? key : currentUrl;
}

/** row de la API → el `comp` mínimo que PortalHero necesita. */
export function fakeComp(row) {
  return {
    name: row.name,
    organizationName: '(tu organización)',
    season: row.season,
    sportName: row.sportName || row.sportCode,
    status: row.status,
    format: row.format,
    startsOn: row.startsOn,
    endsOn: row.endsOn,
  };
}

/** form del estudio → objeto con la forma de comp.portal, para el tema y la portada de la vista previa. */
export function formToPreviewPortal(form) {
  if (!form) return null;
  const t = form.theme;
  return {
    accentColor: form.accentColor || null,
    theme: {
      primary: t.primary || null,
      primaryContrast: t.primaryContrast || null,
      secondary: t.secondary || null,
      surface: t.surface || null,
      headingFont: t.headingFont,
      corners: t.corners,
      heroStyle: t.heroStyle,
      heroGradientTo: t.heroGradientTo || null,
      focusX: t.focusX,
      focusY: t.focusY,
      density: t.density,
      decoration: t.decoration,
      contentFigure: t.contentFigure,
      contentFigureColor: t.contentFigureColor || null,
      showLogoBackground: t.showLogoBackground,
      heroLayout: t.heroLayout,
      heroVariant: t.heroVariant,
      standingsVariant: t.standingsVariant,
      matchCardVariant: t.matchCardVariant,
      bracketVariant: t.bracketVariant,
    },
    sectionOrder: form.sectionOrder,
    bannerUrl: previewImg(form.bannerKey, form.currentBannerUrl),
    logoUrl: previewImg(form.logoKey, form.currentLogoUrl),
    description: form.description || null,
    instagram: form.instagram || null,
    facebook: form.facebook || null,
    whatsApp: form.whatsApp || null,
    website: form.website || null,
    sponsors: form.sponsors
      .map((s) => ({ name: s.name || null, url: s.url || null, logoUrl: previewImg(s.logoKey, s.currentLogoUrl) }))
      .filter((s) => s.logoUrl),
    gallery: form.gallery
      .map((p) => ({ caption: p.caption || null, url: previewImg(p.key, p.currentUrl) }))
      .filter((p) => p.url),
  };
}

/** El nombre por defecto de una sección, para mostrar junto al campo que lo puede reemplazar. */
export function defaultSectionLabel(key) {
  const section = PORTAL_SECTIONS.find((s) => s.key === key);
  return section ? section.label : key;
}

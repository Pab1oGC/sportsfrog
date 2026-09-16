import { PORTAL_SECTIONS, resolvePortalSections } from 'src/lib/portal-theme';

/* ===========================================================================
   La rebanada "portal" del formulario de una competencia: cómo se lee de la
   fila que devuelve la API y cómo se arma el cuerpo `settings.public` que se
   manda de vuelta.

   Una sola pieza para que la usen igual la página de competencias
   (el acordeón con los interruptores) y el estudio de portal
   (portal-studio.jsx). Si las dos armaran el cuerpo por su cuenta, una
   guardaría un campo que la otra pisa con null.
   =========================================================================== */

/** La parte del formulario que corresponde al portal, en su estado inicial. */
export const EMPTY_PORTAL_FORM = readPortalForm(null);

/**
 * fila de la API (row.settings.public + row.publicPreview) → campos del
 * formulario. `row` puede ser null/undefined: devuelve los valores por
 * defecto, que es lo que necesita el alta de una competencia nueva.
 */
export function readPortalForm(row) {
  const pub = (row && row.settings && row.settings.public) || {};
  const preview = (row && row.publicPreview) || {};
  const previewByKey = {};
  (preview.sponsors || []).forEach((s) => { previewByKey[s.logoKey] = s.logoUrl; });
  const previewGalleryByKey = {};
  (preview.gallery || []).forEach((p) => { previewGalleryByKey[p.key] = p.url; });
  const t = pub.theme || {};

  return {
    // Igual criterio que el backend: mostradas salvo que digan explícitamente
    // que no; las nóminas al revés, ocultas salvo que se hayan publicado a
    // propósito (RNF-16).
    showStandings: pub.showStandings !== false,
    showLeaders: pub.showLeaders !== false,
    showClassification: pub.showClassification !== false,
    showRosters: !!pub.showRosters,
    showGallery: pub.showGallery !== false,

    // Las imágenes viajan como clave de almacenamiento; currentBannerUrl /
    // currentLogoUrl son solo para la vista previa y nunca se envían.
    bannerKey: pub.bannerKey || null,
    currentBannerUrl: preview.bannerUrl || null,
    logoKey: pub.logoKey || null,
    currentLogoUrl: preview.logoUrl || null,

    // Color previo al tema. Se conserva para las competencias que solo
    // pusieron esto; el tema lo lee como su color principal si no eligió uno.
    accentColor: pub.accentColor || '',

    theme: {
      primary: t.primary || '',
      primaryContrast: t.primaryContrast || '',
      secondary: t.secondary || '',
      surface: t.surface || '',
      headingFont: t.headingFont || 'inter',
      corners: t.corners || 'soft',
      // Sin estilo elegido: "imagen" si ya hay portada cargada (así una
      // competencia que subió banner con el editor viejo no lo pierde),
      // "color plano" si no. Mismo criterio que PortalTheme.Resolve.
      heroStyle: t.heroStyle || (pub.bannerKey ? 'image' : 'solid'),
      focusX: t.focusX != null ? t.focusX : 50,
      focusY: t.focusY != null ? t.focusY : 50,
      colorScheme: t.colorScheme || 'auto',
    },

    description: pub.description || '',
    instagram: pub.instagram || '',
    facebook: pub.facebook || '',
    whatsApp: pub.whatsApp || '',
    website: pub.website || '',

    sponsors: (pub.sponsors || []).map((s) => ({
      logoKey: s.logoKey, name: s.name || '', url: s.url || '',
      currentLogoUrl: previewByKey[s.logoKey] || null,
    })),

    gallery: (pub.gallery || []).map((p) => ({
      key: p.key, caption: p.caption || '',
      currentUrl: previewGalleryByKey[p.key] || null,
    })),

    // La lectura administrativa trae la lista tal como está guardada --
    // corta, desordenada o ausente --, no la resuelta que entrega el portal
    // público. resolvePortalSections hace lo mismo que PortalSection.Resolve
    // del lado del servidor: siempre las cuatro, en orden.
    sectionOrder: resolvePortalSections(pub.sectionOrder),
  };
}

/**
 * campos del formulario → cuerpo `settings.public`. Los vacíos van como null,
 * no como cadena vacía: es lo que el validador del contrato espera para
 * saltearse la regla de cada campo opcional.
 */
export function buildPortalPayload(f) {
  return {
    showStandings: f.showStandings,
    showLeaders: f.showLeaders,
    showClassification: f.showClassification,
    showRosters: f.showRosters,
    showGallery: f.showGallery,
    bannerKey: f.bannerKey || null,
    logoKey: f.logoKey || null,
    accentColor: f.accentColor || null,
    theme: portalThemePayload(f),
    description: f.description || null,
    instagram: f.instagram || null,
    facebook: f.facebook || null,
    whatsApp: f.whatsApp || null,
    website: f.website || null,
    sponsors: f.sponsors && f.sponsors.length
      ? f.sponsors.map((s) => ({ logoKey: s.logoKey, name: s.name || null, url: s.url || null }))
      : null,
    gallery: f.gallery && f.gallery.length
      ? f.gallery.map((p) => ({ key: p.key, caption: p.caption || null }))
      : null,
    sectionOrder: sectionOrderPayload(f.sectionOrder),
  };
}

/**
 * null cuando el orden y los nombres son exactamente los de siempre --
 * reordenar sin tocar nada más no debe dejar guardada una lista idéntica a
 * la que no guardar nada ya produce. Cuando algo cambió, cada fila lleva su
 * clave siempre y su nombre solo si es distinto del que le tocaba por
 * defecto en esa posición.
 */
function sectionOrderPayload(sections) {
  if (!sections || sections.length === 0) return null;

  const defaultLabelOf = (key) => (PORTAL_SECTIONS.find((s) => s.key === key) || {}).label;
  const matchesDefaultOrder = sections.every((s, i) => PORTAL_SECTIONS[i] && s.key === PORTAL_SECTIONS[i].key);
  const matchesDefaultLabels = sections.every((s) => !s.label || s.label === defaultLabelOf(s.key));

  if (matchesDefaultOrder && matchesDefaultLabels) return null;

  return sections.map((s) => ({
    key: s.key,
    label: s.label && s.label !== defaultLabelOf(s.key) ? s.label : null,
  }));
}

/**
 * El tema, o null cuando quedó en todos los valores por defecto: abrir el
 * estudio y no tocar nada no debe dejar la competencia marcada como
 * personalizada (y por lo tanto salida del tema base de SportFrog).
 */
function portalThemePayload(f) {
  const t = f.theme;
  if (!t) return null;
  // El estilo de portada por defecto depende de si hay banner: si el valor
  // del formulario coincide con ese default, no se guarda (así el tema queda
  // null cuando no se personalizó nada de verdad).
  const defaultHero = f.bannerKey ? 'image' : 'solid';
  const body = {
    primary: t.primary || null,
    primaryContrast: t.primaryContrast || null,
    secondary: t.secondary || null,
    surface: t.surface || null,
    headingFont: t.headingFont && t.headingFont !== 'inter' ? t.headingFont : null,
    corners: t.corners && t.corners !== 'soft' ? t.corners : null,
    heroStyle: t.heroStyle && t.heroStyle !== defaultHero ? t.heroStyle : null,
    focusX: t.focusX != null && t.focusX !== 50 ? t.focusX : null,
    focusY: t.focusY != null && t.focusY !== 50 ? t.focusY : null,
    colorScheme: t.colorScheme && t.colorScheme !== 'auto' ? t.colorScheme : null,
  };
  return Object.values(body).some((v) => v !== null) ? body : null;
}

import { useState, useEffect, useMemo, useCallback, useRef, useTransition } from 'react';
import { useParams, useNavigate } from 'react-router';
import Box from '@mui/material/Box';
import Paper from '@mui/material/Paper';
import Stack from '@mui/material/Stack';
import Divider from '@mui/material/Divider';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import IconButton from '@mui/material/IconButton';
import TextField from '@mui/material/TextField';
import MenuItem from '@mui/material/MenuItem';
import Switch from '@mui/material/Switch';
import FormControlLabel from '@mui/material/FormControlLabel';
import Alert from '@mui/material/Alert';
import Avatar from '@mui/material/Avatar';
import Chip from '@mui/material/Chip';
import Tabs from '@mui/material/Tabs';
import Tab from '@mui/material/Tab';
import ToggleButton from '@mui/material/ToggleButton';
import ToggleButtonGroup from '@mui/material/ToggleButtonGroup';
import CircularProgress from '@mui/material/CircularProgress';
import { ThemeProvider } from '@mui/material/styles';
import { portalBaseTheme } from 'src/theme/portal-base';
import { toast } from 'sonner';
import { Iconify } from 'src/components/iconify';
import { useApi, apiPut } from 'src/hooks/use-api';
import { endpoints } from 'src/lib/axios';
import { leerComoDataUrl } from 'src/lib/data-url';
import { slugDeOrganizacion } from 'src/lib/slug';
import {
  PORTAL_FONTS, PORTAL_CORNERS, PORTAL_HERO_STYLES, PORTAL_SECTIONS,
  PORTAL_DENSITIES, PORTAL_DECORATIONS, PORTAL_CONTENT_FIGURES, PORTAL_HERO_LAYOUTS, PORTAL_HERO_VARIANTS,
  PORTAL_STANDINGS_VARIANTS, PORTAL_MATCH_CARD_VARIANTS, PORTAL_BRACKET_VARIANTS,
  buildPortalTheme, portalFontHref, portalFontStack, contrastRatio, readableTextOn, isHex, darken,
} from 'src/lib/portal-theme';
import { readPortalForm, buildPortalPayload } from 'src/pages/competitions/portal-payload';
import { PortalHero } from 'src/pages/public/portal-hero';
import { ContentFigureBackground } from 'src/pages/public/content-figure';
import { StandingsView } from 'src/pages/public/standings/standings-view';
import { Partido } from 'src/pages/public/match-card/partido';
import { Llave } from 'src/pages/public/bracket/llave';

/* ===========================================================================
   Estudio de portal: la personalización visual del portal público de una
   competencia, con vista previa en vivo.

   El formulario vive a la izquierda; a la derecha se dibuja el portal real
   (la misma <PortalHero> y el mismo buildPortalTheme que usa
   public-competition.jsx) con el estado del formulario como datos. Lo que se
   ve acá es lo que verá el visitante.

   Guarda con PUT /competitions/{id}: el contrato es la competencia entera,
   así que se reenvían sin tocar los campos que no son del portal (nombre,
   dirección, reglamento, calendario, boletín) y solo se reescribe
   settings.public. La dirección y el reglamento igual no son editables acá.
   =========================================================================== */

const ANCHO_MOVIL = 390;

// 64px de la barra fija del panel (Header, layout.jsx) + 16 de aire -- sin
// esto "pegado arriba" quedaba tapado debajo de esa barra en vez de debajo
// de ella. Compartido por las dos columnas: en desktop cada una vive en su
// propia caja con este mismo tope y su propio scroll interno, en vez de que
// la más corta se quede esperando a que la otra termine de bajar.
const SCROLL_STICKY_TOP = 80;

export default function PortalStudioPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const { data: row, isLoading, error } = useApi(endpoints.competition(id));

  const [form, setForm] = useState(null);
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState('');
  const [device, setDevice] = useState('desktop');

  // El tema base del PORTAL, no el del panel: la vista previa y los
  // placeholders "vacío = el del tema base" tienen que mostrar lo que va a ver
  // el visitante, y el portal parte de portalBaseTheme (ver theme/portal-base.jsx).
  const appTheme = portalBaseTheme;
  const [, startFontTransition] = useTransition();

  useEffect(() => {
    if (row) setForm(readPortalForm(row));
  }, [row]);

  const previewPortal = useMemo(() => formToPreviewPortal(form), [form]);
  const previewTheme = useMemo(
    () => buildPortalTheme({ base: appTheme, portal: previewPortal }),
    [appTheme, previewPortal],
  );
  const fontHref = form ? portalFontHref(form.theme.headingFont) : null;

  const patch = useCallback((p) => setForm((f) => ({ ...f, ...p })), []);
  const patchTheme = useCallback((p) => setForm((f) => ({ ...f, theme: { ...f.theme, ...p } })), []);

  // Subir/bajar una fila del orden de secciones. Los interruptores de
  // "Secciones visibles" siguen siendo lo único que oculta una -- esto solo
  // cambia el orden y, si se tocó, el nombre.
  const moveSection = useCallback((index, direction) => {
    setForm((f) => {
      const target = index + direction;
      if (target < 0 || target >= f.sectionOrder.length) return f;
      const next = f.sectionOrder.slice();
      [next[index], next[target]] = [next[target], next[index]];
      return { ...f, sectionOrder: next };
    });
  }, []);

  const renameSection = useCallback((index, label) => {
    setForm((f) => {
      const next = f.sectionOrder.slice();
      next[index] = { ...next[index], label };
      return { ...f, sectionOrder: next };
    });
  }, []);

  // Las mismas secciones, filtradas por lo que "Secciones visibles" dejó
  // prendido -- lo que la vista previa necesita para dibujar sus pestañas de
  // a mentira, en el mismo orden que verá el visitante. La galería además
  // necesita al menos una foto, igual que en el portal real.
  const visibleSections = useMemo(() => {
    if (!form) return [];
    const visible = {
      standings: form.showStandings, leaders: form.showLeaders, classification: form.showClassification,
      calendar: true, gallery: form.showGallery && form.gallery.some((p) => p.key),
    };
    return form.sectionOrder.filter((s) => visible[s.key]);
  }, [form]);

  const save = async () => {
    setSaving(true);
    setSaveError('');
    try {
      await apiPut(endpoints.competition(id), {
        rulesetId: row.rulesetId,
        name: row.name,
        slug: row.slug,
        season: row.season,
        format: row.format,
        captureLevel: row.captureLevel,
        startsOn: row.startsOn,
        endsOn: row.endsOn,
        settings: {
          // Reenviados tal cual: este editor no los toca y el PUT reemplaza
          // el objeto entero.
          schedule: (row.settings && row.settings.schedule) || null,
          bulletin: (row.settings && row.settings.bulletin) || null,
          public: buildPortalPayload(form),
        },
      });
      toast.success('Portal actualizado.');
      navigate('/dashboard/competitions');
    } catch (e) {
      setSaveError(e.message || 'No se pudo guardar.');
    } finally {
      setSaving(false);
    }
  };

  if (isLoading || (!form && !error)) {
    return <Box sx={{ display: 'flex', justifyContent: 'center', mt: 10 }}><CircularProgress /></Box>;
  }

  if (error || !row) {
    return (
      <Box sx={{ p: 3 }}>
        <Alert severity="error">No se pudo cargar la competencia.</Alert>
        <Button sx={{ mt: 2 }} onClick={() => navigate('/dashboard/competitions')} startIcon={<Iconify icon="eva:arrow-back-outline" />}>
          Volver
        </Button>
      </Box>
    );
  }

  return (
    <Box sx={{ pb: 6 }}>
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 3, flexWrap: 'wrap' }}>
        <IconButton onClick={() => navigate('/dashboard/competitions')} aria-label="Volver">
          <Iconify icon="eva:arrow-back-outline" />
        </IconButton>
        <Box sx={{ flexGrow: 1, minWidth: 200 }}>
          <Typography variant="h4" fontWeight={700}>Estudio de portal</Typography>
          <Typography variant="body2" color="text.secondary">{row.name} · {row.season}</Typography>
        </Box>
        <Button
          variant="outlined"
          component="a"
          href={`/public/${slugDeOrganizacion()}/${row.slug}`}
          target="_blank"
          rel="noopener noreferrer"
          startIcon={<Iconify icon="eva:external-link-outline" />}
        >
          Ver portal
        </Button>
        <Button variant="contained" onClick={save} disabled={saving} startIcon={saving ? <CircularProgress size={16} color="inherit" /> : <Iconify icon="eva:save-outline" />}>
          Guardar
        </Button>
      </Box>

      {saveError && <Alert severity="error" sx={{ mb: 2 }} onClose={() => setSaveError('')}>{saveError}</Alert>}
      {!row.isPublic && (
        <Alert severity="info" sx={{ mb: 2 }}>
          Esta competencia todavía no está publicada. Podés dejar el portal listo igual; se verá cuando la publiques.
        </Alert>
      )}

      {/* En desktop cada columna vive en su propia caja "pegada" a
          SCROLL_STICKY_TOP con su propio scroll interno (maxHeight +
          overflowY) -- bajar por las herramientas no mueve ni un pixel la
          vista previa, y viceversa. En mobile (`xs`) nada de esto aplica:
          una sola columna, cada bloque en su alto natural, la página entera
          se desplaza como cualquier otra pantalla del panel. */}
      <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', md: 'minmax(0, 420px) 1fr' }, gap: 3 }}>
        {/* ---- Formulario ---- */}
        <Stack
          spacing={2.5}
          sx={{
            position: { md: 'sticky' },
            top: { md: SCROLL_STICKY_TOP },
            maxHeight: { md: `calc(100vh - ${SCROLL_STICKY_TOP}px - 24px)` },
            overflowY: { md: 'auto' },
            pr: { md: 1 },
          }}
        >
          <Section title="Colores" icon="mdi:palette-outline">
            <ColorField label="Principal" value={form.theme.primary} placeholder={appTheme.palette.primary.main}
              onChange={(v) => patchTheme({ primary: v })} />
            <ColorField label="Texto sobre el principal" value={form.theme.primaryContrast}
              placeholder={readableTextOn(form.theme.primary || appTheme.palette.primary.main)}
              onChange={(v) => patchTheme({ primaryContrast: v })} help="Vacío: se elige negro o blanco por contraste." />
            <ContrastNote
              bg={form.theme.primary || appTheme.palette.primary.main}
              fg={form.theme.primaryContrast || readableTextOn(form.theme.primary || appTheme.palette.primary.main)}
            />
            <ColorField label="Secundario" value={form.theme.secondary} placeholder={form.theme.primary || appTheme.palette.primary.main}
              onChange={(v) => patchTheme({ secondary: v })} help="Enlaces y acciones secundarias. Vacío: sigue al principal." />
            <ColorField label="Fondo de la página" value={form.theme.surface} placeholder="Automático"
              onChange={(v) => patchTheme({ surface: v })} help="Vacío: el gris muy claro (o casi negro) del tema base." />
          </Section>

          <Section title="Tipografía" icon="mdi:format-font">
            <TextField select fullWidth label="Fuente de los títulos" value={form.theme.headingFont}
              onChange={(e) => {
                // Elegir una tipografía nueva pone un <link> de stylesheet
                // con un href que React todavía no vio (más abajo, junto a
                // fontHref) -- y React 19 espera a que esa hoja de estilo
                // cargue antes de mostrar la actualización. Sin transición,
                // esa espera muestra el fallback del <Suspense> de esta
                // página perezosa (routes/sections.jsx) y la desmonta
                // entera mientras tanto -- al volver a montarse relee la
                // competencia de la API y se pierde todo lo demás que se
                // había cambiado en el formulario, no solo la fuente.
                // Envuelta en una transición, React sigue mostrando la
                // página actual hasta que la fuente termina de cargar.
                const next = e.target.value;
                startFontTransition(() => patchTheme({ headingFont: next }));
              }}>
              {PORTAL_FONTS.map((f) => (
                <MenuItem key={f.value} value={f.value} sx={{ fontFamily: f.stack }}>{f.label}</MenuItem>
              ))}
            </TextField>
            <Box sx={{ mt: 1, px: 1.5, py: 1.25, border: '1px dashed', borderColor: 'divider', borderRadius: 1 }}>
              <Typography sx={{ fontFamily: portalFontStack(form.theme.headingFont), fontWeight: 700, fontSize: 26, lineHeight: 1.15 }}>
                {row.name || 'Copa Apertura 2026'}
              </Typography>
              <Typography variant="caption" color="text.secondary">Así se ven los titulares del portal.</Typography>
            </Box>
          </Section>

          <Section title="Forma" icon="mdi:shape-outline">
            <Box>
              <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 0.5 }}>Bordes</Typography>
              <ToggleButtonGroup exclusive size="small" value={form.theme.corners} sx={{ flexWrap: 'wrap' }}
                onChange={(e, v) => v && patchTheme({ corners: v })}>
                {PORTAL_CORNERS.map((c) => <ToggleButton key={c.value} value={c.value}>{c.label}</ToggleButton>)}
              </ToggleButtonGroup>
            </Box>
            <Box>
              <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 0.5 }}>Densidad</Typography>
              <ToggleButtonGroup exclusive size="small" value={form.theme.density} sx={{ flexWrap: 'wrap' }}
                onChange={(e, v) => v && patchTheme({ density: v })}>
                {PORTAL_DENSITIES.map((d) => <ToggleButton key={d.value} value={d.value}>{d.label}</ToggleButton>)}
              </ToggleButtonGroup>
            </Box>
            <Box>
              <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 0.5 }}>Variante de portada</Typography>
              <ToggleButtonGroup exclusive size="small" value={form.theme.heroVariant} sx={{ flexWrap: 'wrap' }}
                onChange={(e, v) => v && patchTheme({ heroVariant: v })}>
                {PORTAL_HERO_VARIANTS.map((h) => <ToggleButton key={h.value} value={h.value}>{h.label}</ToggleButton>)}
              </ToggleButtonGroup>
              {form.theme.heroVariant === 'live' && (
                <Alert severity="info" sx={{ py: 0, mt: 1 }}>
                  Esta vista previa nunca simula un partido en vivo, así que acá siempre se ve como la portada estándar.
                  Solo se ve la portada "En vivo" de verdad en el portal público, mientras haya un partido en curso.
                </Alert>
              )}
            </Box>
            <TextField select fullWidth label="Portada" value={form.theme.heroStyle}
              onChange={(e) => patchTheme({ heroStyle: e.target.value })}>
              {PORTAL_HERO_STYLES.map((h) => <MenuItem key={h.value} value={h.value}>{h.label}</MenuItem>)}
            </TextField>
            {form.theme.heroStyle === 'image' && !previewPortal.bannerUrl && (
              <Alert severity="warning" sx={{ py: 0 }}>Elegiste portada con imagen pero no subiste una. Se usa el color plano.</Alert>
            )}
            {form.theme.heroStyle === 'gradient' && (
              <ColorField
                label="Segundo color del degradado" value={form.theme.heroGradientTo}
                placeholder={darken(form.theme.primary || appTheme.palette.primary.main, 0.32)}
                onChange={(v) => patchTheme({ heroGradientTo: v })}
                help="Vacío: el principal oscurecido, como hasta ahora."
              />
            )}
            {form.theme.heroVariant === 'standard' && (
              <Box>
                <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 0.5 }}>Disposición de la portada</Typography>
                <ToggleButtonGroup exclusive size="small" value={form.theme.heroLayout} sx={{ flexWrap: 'wrap' }}
                  onChange={(e, v) => v && patchTheme({ heroLayout: v })}>
                  {PORTAL_HERO_LAYOUTS.map((h) => <ToggleButton key={h.value} value={h.value}>{h.label}</ToggleButton>)}
                </ToggleButtonGroup>
              </Box>
            )}
            <Box>
              <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 0.5 }}>Decoración de la portada</Typography>
              <ToggleButtonGroup exclusive size="small" value={form.theme.decoration} sx={{ flexWrap: 'wrap' }}
                onChange={(e, v) => v && patchTheme({ decoration: v })}>
                {PORTAL_DECORATIONS.map((d) => <ToggleButton key={d.value} value={d.value}>{d.label}</ToggleButton>)}
              </ToggleButtonGroup>
            </Box>
            {/* Nunca la portada -- esta es la figura detrás del contenido
                (tabla, calendario, fotos...), independiente de la decoración
                de arriba. "Ninguna" ya resuelve el "quiero figura o no". */}
            <TextField select fullWidth label="Figura de fondo" value={form.theme.contentFigure}
              onChange={(e) => patchTheme({ contentFigure: e.target.value })}>
              {PORTAL_CONTENT_FIGURES.map((cf) => <MenuItem key={cf.value} value={cf.value}>{cf.label}</MenuItem>)}
            </TextField>
            {form.theme.contentFigure !== 'none' && (
              <ColorField
                label="Color de la figura" value={form.theme.contentFigureColor}
                placeholder={form.theme.primary || appTheme.palette.primary.main}
                onChange={(v) => patchTheme({ contentFigureColor: v })}
                help="Vacío: sigue al color principal."
              />
            )}
            <Box>
              <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 0.5 }}>Variante de tabla de posiciones</Typography>
              <ToggleButtonGroup exclusive size="small" value={form.theme.standingsVariant} sx={{ flexWrap: 'wrap' }}
                onChange={(e, v) => v && patchTheme({ standingsVariant: v })}>
                {PORTAL_STANDINGS_VARIANTS.map((s) => <ToggleButton key={s.value} value={s.value}>{s.label}</ToggleButton>)}
              </ToggleButtonGroup>
            </Box>
            <Box>
              <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 0.5 }}>Variante de tarjeta de partido</Typography>
              <ToggleButtonGroup exclusive size="small" value={form.theme.matchCardVariant} sx={{ flexWrap: 'wrap' }}
                onChange={(e, v) => v && patchTheme({ matchCardVariant: v })}>
                {PORTAL_MATCH_CARD_VARIANTS.map((m) => <ToggleButton key={m.value} value={m.value}>{m.label}</ToggleButton>)}
              </ToggleButtonGroup>
            </Box>
            <Box>
              <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 0.5 }}>Variante de llave de eliminatoria</Typography>
              <ToggleButtonGroup exclusive size="small" value={form.theme.bracketVariant} sx={{ flexWrap: 'wrap' }}
                onChange={(e, v) => v && patchTheme({ bracketVariant: v })}>
                {PORTAL_BRACKET_VARIANTS.map((b) => <ToggleButton key={b.value} value={b.value}>{b.label}</ToggleButton>)}
              </ToggleButtonGroup>
            </Box>
          </Section>

          <Section title="Secciones visibles" icon="mdi:eye-outline">
            <FormControlLabel control={<Switch checked={form.showStandings} onChange={(e) => patch({ showStandings: e.target.checked })} />} label="Tabla de posiciones" />
            <FormControlLabel control={<Switch checked={form.showLeaders} onChange={(e) => patch({ showLeaders: e.target.checked })} />} label="Líderes" />
            <FormControlLabel control={<Switch checked={form.showClassification} onChange={(e) => patch({ showClassification: e.target.checked })} />} label="Clasificación (deportes juzgados)" />
            <FormControlLabel control={<Switch checked={form.showRosters} onChange={(e) => patch({ showRosters: e.target.checked })} />} label="Nóminas" />
            <FormControlLabel
              control={<Switch checked={form.showAthletePhotos} onChange={(e) => patch({ showAthletePhotos: e.target.checked })} />}
              label="Fotos de los deportistas (llave, deportes individuales)"
            />
            <FormControlLabel control={<Switch checked={form.showGallery} onChange={(e) => patch({ showGallery: e.target.checked })} />} label="Fotos del evento" />
          </Section>

          <Section title="Orden y nombres" icon="mdi:reorder-horizontal">
            <Typography variant="caption" color="text.secondary">
              En qué orden aparecen las pestañas, y cómo se llama cada una. Ocultar una sigue siendo el interruptor de arriba.
            </Typography>
            {form.sectionOrder.map((s, i) => (
              <Box key={s.key} sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                <Stack sx={{ flexShrink: 0 }}>
                  <IconButton size="small" disabled={i === 0} onClick={() => moveSection(i, -1)} sx={{ p: 0.25 }} aria-label="Subir">
                    <Iconify icon="eva:chevron-up-fill" width={16} />
                  </IconButton>
                  <IconButton size="small" disabled={i === form.sectionOrder.length - 1} onClick={() => moveSection(i, 1)} sx={{ p: 0.25 }} aria-label="Bajar">
                    <Iconify icon="eva:chevron-down-fill" width={16} />
                  </IconButton>
                </Stack>
                <Typography variant="body2" color="text.secondary" sx={{ width: 108, flexShrink: 0 }} noWrap>
                  {defaultSectionLabel(s.key)}
                </Typography>
                <TextField
                  size="small" fullWidth value={s.label}
                  onChange={(e) => renameSection(i, e.target.value)}
                  placeholder={defaultSectionLabel(s.key)}
                  slotProps={{ htmlInput: { maxLength: 40 } }}
                />
              </Box>
            ))}
          </Section>

          <Section title="Portada e identidad" icon="mdi:image-outline">
            <ImagePicker label="Imagen de portada"
              url={previewImg(form.bannerKey, form.currentBannerUrl)}
              onPick={async (file) => {
                // Subir la imagen ya la vuelve la portada -- antes había que
                // además ir a "Portada" más abajo y elegir "Imagen" a mano;
                // si no, quedaba guardada con el estilo que ya tenía (Color
                // plano, el de por defecto) y la imagen recién subida nunca
                // se veía. Se puede seguir cambiando después.
                const bannerKey = await leerComoDataUrl(file);
                setForm((f) => ({ ...f, bannerKey, theme: { ...f.theme, heroStyle: 'image' } }));
              }}
              onClear={() => patch({ bannerKey: null, currentBannerUrl: null })}
              ratio="96 / 54"
              help="La imagen ancha detrás del nombre. Subirla la vuelve la portada." />
            {form.theme.heroStyle === 'image' && previewImg(form.bannerKey, form.currentBannerUrl) && (
              <FocalPointPicker
                url={previewImg(form.bannerKey, form.currentBannerUrl)}
                x={form.theme.focusX}
                y={form.theme.focusY}
                onChange={(x, y) => patchTheme({ focusX: x, focusY: y })}
              />
            )}
            <ImagePicker label="Logo"
              url={previewImg(form.logoKey, form.currentLogoUrl)}
              onPick={async (file) => patch({ logoKey: await leerComoDataUrl(file) })}
              onClear={() => patch({ logoKey: null, currentLogoUrl: null })}
              ratio="1 / 1"
              help="La marca de la competencia, al lado del nombre." />
            {/* Solo el logo -- ningún otro color o imagen del portal tiene
                esta opción. Prendido por defecto (el mismo look de siempre);
                se apaga para una marca que ya se ve bien sola, sin un
                fondo detrás que la enmarque otra vez. */}
            <FormControlLabel
              control={
                <Switch
                  checked={form.theme.showLogoBackground}
                  onChange={(e) => patchTheme({ showLogoBackground: e.target.checked })}
                />
              }
              label="Fondo detrás del logo"
            />
          </Section>

          <Section title="Presentación" icon="mdi:text-box-outline">
            <TextField label="Presentación" value={form.description} onChange={(e) => patch({ description: e.target.value })}
              fullWidth multiline minRows={2} slotProps={{ htmlInput: { maxLength: 500 } }} helperText={`${form.description.length}/500`} />
          </Section>

          <Section title="Redes y contacto" icon="mdi:link-variant">
            <TextField label="Instagram" value={form.instagram} onChange={(e) => patch({ instagram: e.target.value })} fullWidth placeholder="https://instagram.com/..." />
            <TextField label="Facebook" value={form.facebook} onChange={(e) => patch({ facebook: e.target.value })} fullWidth placeholder="https://facebook.com/..." />
            <TextField label="WhatsApp" value={form.whatsApp} onChange={(e) => patch({ whatsApp: e.target.value })} fullWidth placeholder="https://wa.me/59171234567" />
            <TextField label="Sitio web" value={form.website} onChange={(e) => patch({ website: e.target.value })} fullWidth placeholder="https://..." />
          </Section>

          <Section title="Auspiciantes" icon="mdi:handshake-outline"
            action={<Button size="small" startIcon={<Iconify icon="eva:plus-fill" width={16} />} onClick={() => patch({ sponsors: [...form.sponsors, { logoKey: '', name: '', url: '', currentLogoUrl: null }] })}>Agregar</Button>}>
            {form.sponsors.length === 0 && <Typography variant="body2" color="text.secondary">Sin auspiciantes.</Typography>}
            {form.sponsors.map((s, i) => (
              <Box key={i} sx={{ display: 'flex', alignItems: 'center', gap: 1.5, p: 1, border: '1px solid', borderColor: 'divider', borderRadius: 1 }}>
                <Avatar src={previewImg(s.logoKey, s.currentLogoUrl) || undefined} variant="rounded" sx={{ width: 40, height: 40, bgcolor: 'action.hover', flexShrink: 0, '& img': { objectFit: 'contain' } }}>
                  {!previewImg(s.logoKey, s.currentLogoUrl) && <Iconify icon="mdi:image-outline" width={20} sx={{ color: 'text.disabled' }} />}
                </Avatar>
                <Button variant="outlined" component="label" size="small" sx={{ flexShrink: 0 }}>
                  Logo
                  <input type="file" accept="image/png,image/jpeg,image/webp" hidden onChange={async (e) => {
                    const file = e.target.files[0];
                    if (file) editSponsor(setForm, i, { logoKey: await leerComoDataUrl(file), currentLogoUrl: null });
                  }} />
                </Button>
                <TextField label="Nombre" size="small" value={s.name} onChange={(e) => editSponsor(setForm, i, { name: e.target.value })} sx={{ flex: 1, minWidth: 90 }} />
                <TextField label="Enlace" size="small" value={s.url} onChange={(e) => editSponsor(setForm, i, { url: e.target.value })} sx={{ flex: 1, minWidth: 90 }} />
                <IconButton size="small" onClick={() => patch({ sponsors: form.sponsors.filter((_, idx) => idx !== i) })}>
                  <Iconify icon="eva:trash-2-outline" width={18} sx={{ color: 'error.main' }} />
                </IconButton>
              </Box>
            ))}
          </Section>

          <Section title="Galería del evento" icon="mdi:image-multiple-outline"
            action={
              <Button size="small" component="label" startIcon={<Iconify icon="eva:plus-fill" width={16} />}>
                Agregar fotos
                <input
                  type="file" accept="image/png,image/jpeg,image/webp" multiple hidden
                  onChange={async (e) => {
                    const files = Array.from(e.target.files || []);
                    if (files.length === 0) return;
                    const added = await Promise.all(
                      files.map(async (file) => ({ key: await leerComoDataUrl(file), caption: '', currentUrl: null })),
                    );
                    patch({ gallery: [...form.gallery, ...added] });
                  }}
                />
              </Button>
            }>
            <Typography variant="caption" color="text.secondary">
              Fotos del propio evento -- partidos, la premiación, el público. Se pueden elegir varias a la vez.
            </Typography>
            {form.gallery.length === 0 && <Typography variant="body2" color="text.secondary">Sin fotos todavía.</Typography>}
            {form.gallery.map((p, i) => (
              <Box key={i} sx={{ display: 'flex', alignItems: 'center', gap: 1.5, p: 1, border: '1px solid', borderColor: 'divider', borderRadius: 1 }}>
                <Avatar src={previewImg(p.key, p.currentUrl) || undefined} variant="rounded" sx={{ width: 48, height: 48, bgcolor: 'action.hover', flexShrink: 0, '& img': { objectFit: 'cover' } }}>
                  {!previewImg(p.key, p.currentUrl) && <Iconify icon="mdi:image-outline" width={20} sx={{ color: 'text.disabled' }} />}
                </Avatar>
                <TextField
                  label="Epígrafe (opcional)" size="small" value={p.caption}
                  onChange={(e) => editGalleryPhoto(setForm, i, { caption: e.target.value })}
                  sx={{ flex: 1 }} slotProps={{ htmlInput: { maxLength: 140 } }}
                />
                <IconButton size="small" onClick={() => patch({ gallery: form.gallery.filter((_, idx) => idx !== i) })}>
                  <Iconify icon="eva:trash-2-outline" width={18} sx={{ color: 'error.main' }} />
                </IconButton>
              </Box>
            ))}
          </Section>
        </Stack>

        {/* ---- Vista previa ---- */}
        <Box
          sx={{
            position: { md: 'sticky' },
            top: { md: SCROLL_STICKY_TOP },
            maxHeight: { md: `calc(100vh - ${SCROLL_STICKY_TOP}px - 24px)` },
            overflowY: { md: 'auto' },
          }}
        >
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 1 }}>
            <Typography variant="overline" color="text.secondary" sx={{ flexGrow: 1 }}>Vista previa</Typography>
            <ToggleButtonGroup exclusive size="small" value={device} onChange={(e, v) => v && setDevice(v)}>
              <ToggleButton value="desktop" aria-label="Escritorio"><Iconify icon="mdi:monitor" width={18} /></ToggleButton>
              <ToggleButton value="mobile" aria-label="Celular"><Iconify icon="mdi:cellphone" width={18} /></ToggleButton>
            </ToggleButtonGroup>
          </Box>
          {fontHref && <link rel="stylesheet" href={fontHref} precedence="portal-font" />}
          <Paper
            variant="outlined"
            sx={{
              overflow: 'hidden',
              mx: 'auto',
              width: device === 'mobile' ? ANCHO_MOVIL : '100%',
              maxWidth: '100%',
              transition: 'width 0.2s ease',
            }}
          >
            <ThemeProvider theme={previewTheme}>
              {/* color explícito: el texto que hereda color debe seguir al
                  tema del portal, no al del panel (mismo motivo que en
                  public-competition.jsx). */}
              <Box sx={{ bgcolor: 'background.default', color: 'text.primary' }}>
                <PortalHero comp={fakeComp(row)} portal={previewPortal} dense />
                <PreviewBody portal={previewPortal} sections={visibleSections} />
              </Box>
            </ThemeProvider>
          </Paper>
        </Box>
      </Box>
    </Box>
  );
}

/* --- helpers de datos --------------------------------------------------------- */

function previewImg(key, currentUrl) {
  if (!key) return null;
  return String(key).startsWith('data:') ? key : currentUrl;
}

function editSponsor(setForm, i, patch) {
  setForm((f) => ({ ...f, sponsors: f.sponsors.map((s, idx) => (idx === i ? { ...s, ...patch } : s)) }));
}

function editGalleryPhoto(setForm, i, patch) {
  setForm((f) => ({ ...f, gallery: f.gallery.map((p, idx) => (idx === i ? { ...p, ...patch } : p)) }));
}

/** row de la API → el `comp` mínimo que PortalHero necesita. */
function fakeComp(row) {
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

/** form del estudio → objeto con la forma de comp.portal, para el tema y la portada. */
function formToPreviewPortal(form) {
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
function defaultSectionLabel(key) {
  const section = PORTAL_SECTIONS.find((s) => s.key === key);
  return section ? section.label : key;
}

/* --- piezas de UI ----------------------------------------------------------- */

function Section({ title, icon, action, children }) {
  return (
    <Paper variant="outlined" sx={{ p: 2 }}>
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 1.5 }}>
        <Iconify icon={icon} width={18} sx={{ color: 'text.secondary' }} />
        <Typography variant="subtitle2" sx={{ flexGrow: 1 }}>{title}</Typography>
        {action}
      </Box>
      <Stack spacing={1.5}>{children}</Stack>
    </Paper>
  );
}

/**
 * El selector nativo `<input type="color">` dispara su onChange en cada tick
 * mientras se arrastra adentro del picker -- no una vez al soltar -- y cada
 * uno de esos ticks terminaba reconstruyendo el tema entero (buildPortalTheme
 * -> createTheme, la parte cara de este panel: paleta, tipografía y overrides
 * de componentes) y volviendo a pintar toda la vista previa. Eso era la
 * lentitud reportada: no un selector lento en sí, sino el resto de la página
 * recalculando algo pesado docenas de veces por segundo detrás de él.
 *
 * `draft` responde al instante -- el cuadrito y el campo de texto se sienten
 * fluidos arrastrando o tipeando -- y lo que sube al formulario (lo que
 * dispara esa reconstrucción) se demora un instante corto, así que solo
 * corre una vez cuando el usuario deja de mover el color.
 */
function ColorField({ label, value, placeholder, onChange, help }) {
  const [draft, setDraft] = useState(value);
  const timeoutRef = useRef(null);

  useEffect(() => { setDraft(value); }, [value]);
  useEffect(() => () => clearTimeout(timeoutRef.current), []);

  const commit = (v) => {
    setDraft(v);
    clearTimeout(timeoutRef.current);
    timeoutRef.current = setTimeout(() => onChange(v), 120);
  };

  const clear = () => {
    clearTimeout(timeoutRef.current);
    setDraft('');
    onChange('');
  };

  const valid = isHex(draft);
  return (
    <Box>
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
        <Box
          component="input"
          type="color"
          value={valid ? draft : (isHex(placeholder) ? placeholder : '#F50057')}
          onChange={(e) => commit(e.target.value)}
          sx={{ width: 40, height: 40, p: 0, border: '1px solid', borderColor: 'divider', borderRadius: 1, bgcolor: 'transparent', cursor: 'pointer', flexShrink: 0 }}
        />
        <TextField
          label={label}
          value={draft}
          onChange={(e) => commit(e.target.value)}
          placeholder={placeholder}
          size="small"
          fullWidth
          error={!!draft && !valid}
          helperText={!!draft && !valid ? 'Se escribe como #rrggbb.' : help}
        />
        {draft && (
          <IconButton size="small" onClick={clear} aria-label={`Quitar ${label}`}>
            <Iconify icon="eva:close-outline" width={16} />
          </IconButton>
        )}
      </Box>
    </Box>
  );
}

function ContrastNote({ bg, fg }) {
  const ratio = contrastRatio(bg, fg);
  if (ratio == null) return null;
  const ok = ratio >= 4.5;
  return (
    <Alert severity={ok ? 'success' : 'warning'} icon={false} sx={{ py: 0.25, fontSize: 13 }}>
      Contraste texto / color principal: {ratio.toFixed(2)}:1 {ok ? '· legible (AA)' : '· por debajo de 4.5:1, difícil de leer'}
    </Alert>
  );
}

function ImagePicker({ label, url, onPick, onClear, ratio, help }) {
  return (
    <Box>
      <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 0.5 }}>{label}</Typography>
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 2 }}>
        <Avatar src={url || undefined} variant="rounded" sx={{ width: 88, aspectRatio: ratio, height: 'auto', bgcolor: 'action.hover', '& img': { objectFit: 'contain' } }}>
          {!url && <Iconify icon="mdi:image-outline" width={22} sx={{ color: 'text.disabled' }} />}
        </Avatar>
        <Box sx={{ display: 'flex', flexDirection: 'column', gap: 0.5 }}>
          <Button variant="outlined" component="label" size="small" startIcon={<Iconify icon="eva:upload-outline" width={16} />}>
            {url ? 'Reemplazar' : 'Subir'}
            <input type="file" accept="image/png,image/jpeg,image/webp" hidden onChange={(e) => { const f = e.target.files[0]; if (f) onPick(f); }} />
          </Button>
          {url && <Button size="small" color="inherit" onClick={onClear}>Quitar</Button>}
        </Box>
      </Box>
      {help && <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 0.5 }}>{help}</Typography>}
    </Box>
  );
}

/**
 * Dónde no se debe recortar la imagen de portada, elegido tocando la propia
 * imagen en vez de dos campos numéricos -- ver dónde queda el punto se
 * entiende de un vistazo, calcularlo a ojo no. `x`/`y` son 0..100.
 */
function FocalPointPicker({ url, x, y, onChange }) {
  const pick = (e) => {
    const rect = e.currentTarget.getBoundingClientRect();
    const nx = Math.round(((e.clientX - rect.left) / rect.width) * 100);
    const ny = Math.round(((e.clientY - rect.top) / rect.height) * 100);
    onChange(Math.max(0, Math.min(100, nx)), Math.max(0, Math.min(100, ny)));
  };

  return (
    <Box>
      <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 0.5 }}>Punto focal</Typography>
      <Box
        onClick={pick}
        sx={{
          position: 'relative', width: '100%', aspectRatio: '96 / 54', borderRadius: 1, overflow: 'hidden',
          cursor: 'crosshair', border: '1px solid', borderColor: 'divider',
          backgroundImage: `url(${url})`, backgroundSize: 'cover', backgroundPosition: `${x}% ${y}%`,
        }}
      >
        <Box
          sx={{
            position: 'absolute', left: `${x}%`, top: `${y}%`, width: 16, height: 16, borderRadius: '50%',
            border: '2px solid #fff', boxShadow: '0 0 0 1px rgba(0,0,0,0.5), 0 1px 4px rgba(0,0,0,0.4)',
            transform: 'translate(-50%, -50%)', pointerEvents: 'none',
          }}
        />
      </Box>
      <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 0.5 }}>
        Tocá la imagen para marcar qué parte no se debe perder al recortarla en una pantalla angosta.
      </Typography>
    </Box>
  );
}

/**
 * Filas de mentira con la forma completa de ReadStandings.Row -- para que
 * las tres variantes de tabla (standard/cards/editorial) tengan algo real
 * que dibujar en la vista previa, no solo la variante `standard` como
 * antes. `qualifiersPerGroup: 2` deja ver también el resaltado de
 * clasificación en las tres.
 */
var FILAS_DE_MENTIRA = [
  { position: 1, teamId: 'p1', teamName: 'Deportivo Norte', clubLogoUrl: null, played: 6, won: 5, drawn: 1, lost: 0, scoreFor: 16, scoreAgainst: 4, scoreDifference: 12, points: 16 },
  { position: 2, teamId: 'p2', teamName: 'Atlético Sur', clubLogoUrl: null, played: 6, won: 4, drawn: 1, lost: 1, scoreFor: 13, scoreAgainst: 6, scoreDifference: 7, points: 13 },
  { position: 3, teamId: 'p3', teamName: 'Unión Central', clubLogoUrl: null, played: 6, won: 3, drawn: 1, lost: 2, scoreFor: 10, scoreAgainst: 8, scoreDifference: 2, points: 10 },
  { position: 4, teamId: 'p4', teamName: 'Estrella Roja', clubLogoUrl: null, played: 6, won: 1, drawn: 0, lost: 5, scoreFor: 3, scoreAgainst: 14, scoreDifference: -11, points: 3 },
];

var DATOS_DE_MENTIRA = {
  categories: [{
    categoryId: 'preview',
    categoryName: 'Sub-15',
    allowsDraw: true,
    tiebreakers: [],
    qualifiersPerGroup: 2,
    groups: [{ label: null, rows: FILAS_DE_MENTIRA }],
  }],
};

/**
 * Un partido de mentira para previsualizar la variante de tarjeta elegida.
 * `mostrarEventos: false` en el llamado de abajo evita que se intente
 * pedir la cronología (que necesitaría un partido y una competencia
 * reales) -- ni con "finished" aparece el botón.
 */
var PARTIDO_DE_MENTIRA = {
  id: 'preview',
  categoryName: 'Sub-15',
  homeTeamName: 'Deportivo Norte',
  homeClubLogoUrl: null,
  awayTeamName: 'Atlético Sur',
  awayClubLogoUrl: null,
  homeTotal: 2,
  awayTotal: 1,
  status: 'finished',
  scheduledAt: new Date().toISOString(),
  venueName: 'Complejo Municipal',
  spaceName: 'Cancha 1',
  venueMapsUrl: null,
};

/**
 * Una llave de mentira chica (semifinal + final) para previsualizar la
 * variante de cruce elegida -- una semifinal en vivo, para que también se
 * vea ese estado, y la final jugada, para que el banner de campeón
 * también aparezca en la preview.
 */
var LLAVE_DE_MENTIRA = [
  {
    clave: 'sf', titulo: 'Semifinal', enCurso: true,
    matches: [
      {
        id: 'sf1', homeTeamName: 'Deportivo Norte', homeClubLogoUrl: null,
        awayTeamName: 'Atlético Sur', awayClubLogoUrl: null,
        homeTotal: 2, awayTotal: 1, status: 'finished',
        penaltyHomeScore: null, penaltyAwayScore: null,
        scheduledAt: null, venueName: null, spaceName: null,
      },
      {
        id: 'sf2', homeTeamName: 'Unión Central', homeClubLogoUrl: null,
        awayTeamName: 'Estrella Roja', awayClubLogoUrl: null,
        homeTotal: null, awayTotal: null, status: 'in_progress',
        liveHomeTotal: 1, liveAwayTotal: 0,
        penaltyHomeScore: null, penaltyAwayScore: null,
        scheduledAt: null, venueName: 'Complejo Municipal', spaceName: 'Cancha 2',
      },
    ],
  },
  {
    clave: 'final', titulo: 'Final', enCurso: false,
    matches: [
      {
        id: 'final', homeTeamName: 'Deportivo Norte', homeClubLogoUrl: null,
        awayTeamName: 'Unión Central', awayClubLogoUrl: null,
        homeTotal: 3, awayTotal: 1, status: 'finished',
        penaltyHomeScore: null, penaltyAwayScore: null,
        scheduledAt: null, venueName: 'Complejo Municipal', spaceName: 'Cancha 1',
      },
    ],
  },
];

/**
 * El cuerpo de la vista previa: no es el portal entero, es una tira de las
 * superficies que el tema toca (chips, pestañas, la tabla de posiciones,
 * botones, la franja de auspiciantes) para que cada color y cada radio se
 * vea sin tener que cargar datos reales de la competencia. Las pestañas sí
 * son las reales -- en el orden y con el nombre que "Orden y nombres" dejó.
 *
 * La tabla de posiciones es el StandingsView real (no una maqueta aparte),
 * para que elegir una variante en "Variante de tabla de posiciones" se vea
 * reflejado acá mismo -- la misma garantía que ya tiene el hero. `sportInfo`
 * vacío es intencional: ni `row` ni `fakeComp(row)` traen todavía
 * isPlayedInSets/scoringUnit/periodLabel, y columnasMarcador/
 * etiquetasDesempate ya están pensadas para ese caso (caen en "puntos"/"a
 * favor"/"en contra" genéricos) -- no hace falta una llamada nueva a la API
 * solo para la vista previa.
 */
function PreviewBody({ portal, sections }) {
  return (
    <Box sx={{ p: 2, position: 'relative', overflow: 'hidden' }}>
      <ContentFigureBackground figure={portal.theme && portal.theme.contentFigure} color={portal.theme && portal.theme.contentFigureColor} />
      {/* Un solo envoltorio "position: relative" para todo lo que sigue --
          no uno por hijo -- para que pinte por delante de la figura de
          fondo (position: absolute), que si no, al venir primero en el DOM,
          quedaría por encima de cualquier hijo sin su propio posicionamiento. */}
      <Box sx={{ position: 'relative', zIndex: 1 }}>
      <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap', mb: 1.5 }}>
        <Chip label="Sub-15" color="primary" />
        <Chip label="Sub-17" variant="outlined" color="primary" />
        <Chip label="Libre" variant="outlined" color="primary" />
      </Box>
      <Tabs value={0} variant="scrollable" scrollButtons="auto" allowScrollButtonsMobile sx={{ mb: 1.5, minHeight: 36 }}>
        {sections.map((s) => <Tab key={s.key} label={s.label} sx={{ minHeight: 36 }} />)}
      </Tabs>
      <Box sx={{ mb: 2 }}>
        <StandingsView
          data={DATOS_DE_MENTIRA}
          loading={false}
          selectedCatId={null}
          sportInfo={{}}
          variant={portal.theme && portal.theme.standingsVariant}
        />
      </Box>
      <Box sx={{ mb: 2 }}>
        <Partido
          m={PARTIDO_DE_MENTIRA}
          mostrarCategoria={false}
          destacado={null}
          orgSlug="preview"
          compSlug="preview"
          mostrarEventos={false}
          variant={portal.theme && portal.theme.matchCardVariant}
        />
      </Box>
      <Box sx={{ mb: 2 }}>
        <Llave grupos={LLAVE_DE_MENTIRA} variant={portal.theme && portal.theme.bracketVariant} />
      </Box>
      <Stack direction="row" spacing={1} sx={{ mb: 2 }}>
        <Button variant="contained">Acción</Button>
        <Button variant="outlined">Secundaria</Button>
        <Button>Texto</Button>
      </Stack>
      {portal.gallery.length > 0 && (
        <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 1, mb: 2 }}>
          {portal.gallery.slice(0, 6).map((p, i) => (
            <Box key={i} component="img" src={p.url} sx={{ width: '100%', aspectRatio: '1 / 1', objectFit: 'cover', borderRadius: 1 }} />
          ))}
        </Box>
      )}
      {portal.sponsors.length > 0 && (
        <>
          <Divider sx={{ mb: 1.5 }} />
          <Typography variant="overline" color="text.secondary" sx={{ display: 'block', textAlign: 'center', mb: 1 }}>Auspician</Typography>
          <Box sx={{ display: 'flex', gap: 2, flexWrap: 'wrap', justifyContent: 'center' }}>
            {portal.sponsors.map((s, i) => (
              <Avatar key={i} src={s.logoUrl} variant="rounded" sx={{ width: 48, height: 48, bgcolor: 'action.hover', '& img': { objectFit: 'contain' } }} />
            ))}
          </Box>
        </>
      )}
      </Box>
    </Box>
  );
}

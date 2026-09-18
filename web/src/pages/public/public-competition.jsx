import { useState, useEffect, useMemo } from 'react';
import { useParams, useNavigate } from 'react-router';
import Avatar from '@mui/material/Avatar';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import Chip from '@mui/material/Chip';
import IconButton from '@mui/material/IconButton';
import Tabs from '@mui/material/Tabs';
import Tab from '@mui/material/Tab';
import AppBar from '@mui/material/AppBar';
import Toolbar from '@mui/material/Toolbar';
import Container from '@mui/material/Container';
import CircularProgress from '@mui/material/CircularProgress';
import Alert from '@mui/material/Alert';
import Divider from '@mui/material/Divider';
import Paper from '@mui/material/Paper';
import Stack from '@mui/material/Stack';
import TextField from '@mui/material/TextField';
import MenuItem from '@mui/material/MenuItem';
import Dialog from '@mui/material/Dialog';
import Fade from '@mui/material/Fade';
import { alpha, ThemeProvider, useTheme } from '@mui/material/styles';
import useSWR from 'swr';
import publicAxios from 'src/lib/public-axios';
import { Iconify } from 'src/components/iconify';
import { MedalCircle, MEDAL_COLORS } from 'src/components/medal-circle';
import { EmptyState } from 'src/components/empty-state';
import { SectionSkeleton } from 'src/components/section-skeleton';
import { usePrefersReducedMotion } from 'src/hooks/use-prefers-reduced-motion';
import { buildPortalTheme, portalFontHref } from 'src/lib/portal-theme';
import { PortalHero } from 'src/pages/public/portal-hero';
import { ContentFigureBackground } from 'src/pages/public/content-figure';
import { StandingsView } from 'src/pages/public/standings/standings-view';
import { Partido } from 'src/pages/public/match-card/partido';
import { BracketView } from 'src/pages/public/bracket/bracket-view';
import { agruparPorRonda } from 'src/lib/match-rounds';
import { etiquetasDesempate } from 'src/lib/tiebreaker-labels';

var publicFetcher = function(url) { return publicAxios.get(url).then(function(r) { return r.data; }); };
// El orden de siempre, para el instante antes de que comp llegue -- nunca se
// llega a dibujar con este valor (el "if (loadingComp) return" más abajo lo
// corta primero), pero los hooks de esta pantalla corren sin condicion, así
// que necesitan algo con que calcular mientras tanto. Una vez que comp
// llega, el orden real es comp.portal.sectionOrder -- las mismas cuatro
// claves y etiquetas por defecto que resuelve PortalSection.Resolve.
var DEFAULT_SECTION_ORDER = [
  { key: 'standings', label: 'Tabla de posiciones' },
  { key: 'leaders', label: 'Líderes' },
  { key: 'classification', label: 'Clasificación' },
  { key: 'calendar', label: 'Calendario' },
  { key: 'gallery', label: 'Fotos' },
  { key: 'bracket', label: 'Llave' },
];

export default function PublicCompetitionPage() {
  var params = useParams();
  var navigate = useNavigate();
  var orgSlug = params.orgSlug;
  var compSlug = params.compSlug;
  var [tab, setTab] = useState(0);
  var [selectedCatId, setSelectedCatId] = useState('');
  var reducirMovimiento = usePrefersReducedMotion();

  var { data: comp, isLoading: loadingComp, error: errorComp } = useSWR(
    '/api/public/' + orgSlug + '/' + compSlug, publicFetcher
  );

  // El tema del portal de esta competencia, sobre el tema de la app. Si la
  // competencia no personalizó nada, buildPortalTheme devuelve el tema base
  // intacto y la página se ve igual que siempre.
  var appTheme = useTheme();
  var portalTheme = useMemo(function() {
    return buildPortalTheme({ base: appTheme, portal: comp && comp.portal });
  }, [appTheme, comp]);
  var headingFontHref = portalFontHref(comp && comp.portal && comp.portal.theme && comp.portal.theme.headingFont);

  // El <body> lo pinta el CssBaseline del nivel de la app, fuera de este
  // ThemeProvider: si el portal cambió el fondo (surface), el que se ve al
  // hacer overscroll quedaría del otro color. Se acompaña a mano mientras
  // esta página está montada.
  useEffect(function() {
    if (portalTheme === appTheme) return undefined;
    var previo = document.body.style.backgroundColor;
    document.body.style.backgroundColor = portalTheme.palette.background.default;
    return function() { document.body.style.backgroundColor = previo; };
  }, [portalTheme, appTheme]);

  // Sin la opción de ver todas, siempre hay una categoría a la vista: se abre
  // en la primera apenas llegan. Mezclar categorías en una misma tabla o
  // calendario confundía más de lo que ayudaba — dos equipos de clubes
  // distintos comparten nombre entre divisiones.
  var primeraCat = comp && comp.categories && comp.categories.length ? comp.categories[0].id : null;
  useEffect(function() {
    if (primeraCat) setSelectedCatId(function(actual) { return actual || primeraCat; });
  }, [primeraCat]);

  var shows = (comp && comp.shows) || {};
  var showStandings = shows.standings !== false;
  var showLeaders = shows.leaders !== false;
  // Al reves que las dos anteriores: los planteles son privados salvo que se
  // hayan publicado a proposito (RNF-16), asi que su ausencia significa "no".
  var showRosters = shows.rosters === true;
  // Solo tiene sentido en un deporte juzgado (poomsae, no todavia ningun
  // otro) -- una tabla vacia en un deporte por tabla no es lo que el
  // interruptor prometia mostrar u ocultar.
  var showClassification = !!(comp && comp.isJudged) && shows.classification !== false;
  var gallery = (comp && comp.portal && comp.portal.gallery) || [];
  // Igual que Auspiciantes: una galeria vacia no tiene nada que publicar, asi
  // que el interruptor por si solo no alcanza para mostrar la pestaña.
  var showGallery = shows.gallery !== false && gallery.length > 0;

  // El orden y el nombre de cada pestaña son cosa del backend
  // (comp.portal.sectionOrder, siempre las seis secciones, en algún orden
  // -- ver PortalSection.Resolve). Acá solo se filtra por cuáles van
  // ocultas, que sigue siendo un interruptor aparte y no parte del orden:
  // ni el calendario ni la llave se filtran por un interruptor de
  // organizador -- la llave existe o no según haya de verdad un partido de
  // eliminatoria (comp.shows.bracket, ver ReadPublicCompetition), igual que
  // clasificación depende de isJudged en vez de un switch propio.
  var showBracket = shows.bracket === true;
  var sectionOrder = (comp && comp.portal && comp.portal.sectionOrder) || DEFAULT_SECTION_ORDER;
  var seccionVisible = {
    standings: showStandings, leaders: showLeaders, classification: showClassification,
    calendar: true, gallery: showGallery, bracket: showBracket,
  };
  var seccionesVisibles = sectionOrder.filter(function(s) { return seccionVisible[s.key]; });

  var tabCount = seccionesVisibles.length;
  var effectiveTab = tab;
  if (effectiveTab >= tabCount && tabCount > 0) effectiveTab = 0;

  var seccionActiva = seccionesVisibles[effectiveTab] ? seccionesVisibles[effectiveTab].key : null;

  var { data: standingsData, isLoading: loadingStandings } = useSWR(
    comp && seccionActiva === 'standings' ? '/api/public/' + orgSlug + '/' + compSlug + '/standings' : null, publicFetcher
  );

  var { data: leadersData, isLoading: loadingLeaders } = useSWR(
    comp && seccionActiva === 'leaders' ? '/api/public/' + orgSlug + '/' + compSlug + '/leaders?top=10' : null, publicFetcher
  );

  var { data: classificationData, isLoading: loadingClassification } = useSWR(
    comp && seccionActiva === 'classification' ? '/api/public/' + orgSlug + '/' + compSlug + '/classification' : null, publicFetcher
  );

  // Calendario y Llave son dos vistas del mismo fixture -- comparten este
  // mismo pedido (SWR lo cachea una sola vez por key), no uno cada una.
  var { data: calendarData, isLoading: loadingCalendar } = useSWR(
    comp && (seccionActiva === 'calendar' || seccionActiva === 'bracket') ? '/api/public/' + orgSlug + '/' + compSlug + '/matches' : null, publicFetcher
  );

  if (loadingComp) return (
    <Box sx={{ minHeight: '100vh', bgcolor: 'background.default' }}>
      <Navbar navigate={navigate} />
      <Box sx={{ display: 'flex', justifyContent: 'center', mt: 10 }}><CircularProgress /></Box>
    </Box>
  );

  if (errorComp || !comp) return (
    <Box sx={{ minHeight: '100vh', bgcolor: 'background.default' }}>
      <Navbar navigate={navigate} />
      <Container maxWidth="md" sx={{ py: 6 }}>
        <Alert severity="error">Competencia no encontrada o no publicada.</Alert>
        <Button variant="outlined" sx={{ mt: 2 }} onClick={function() { navigate('/public'); }} startIcon={<Iconify icon="eva:arrow-back-outline" />}>Volver</Button>
      </Container>
    </Box>
  );

  var cats = comp.categories || [];
  // Ausente en toda competencia que nunca abrió el estudio de portal: el
  // resto de la pagina se dibuja exactamente como siempre.
  var portal = comp.portal || {};
  var sponsors = portal.sponsors || [];

  return (
    <ThemeProvider theme={portalTheme}>
      {/* React 19 iza este <link> al <head> y lo deduplica. Sin fuente propia
          headingFontHref es null y no se dibuja nada. */}
      {headingFontHref && <link rel="stylesheet" href={headingFontHref} precedence="portal-font" />}
      {/* color: el <body> lo pinta el CssBaseline del nivel de la app y no
          sigue a este ThemeProvider, así que el texto que hereda color (un
          <Typography> sin prop `color`) se quedaría con el del panel. Fijarlo
          acá hace que todo el portal herede el color del tema del portal. */}
      <Box sx={{ minHeight: '100vh', bgcolor: 'background.default', color: 'text.primary' }}>
      <Navbar navigate={navigate} logoUrl={comp.organizationLogoUrl} />
      <PortalHero comp={comp} portal={portal} moment={comp.moment} onBack={function() { navigate('/public'); }} />
      {/* position: relative + overflow: hidden: la figura se dibuja detrás de
          todo lo que sigue (tabla, calendario, fotos...) y nunca sobre la
          portada, que ya tiene su propia decoración (PortalHero). No tiene
          una altura propia -- crece con el contenido real -- así que la
          figura, con inset 0, la cubre entera sin importar cuánto mida. */}
      <Box sx={{ position: 'relative', overflow: 'hidden' }}>
        <ContentFigureBackground figure={portal.theme && portal.theme.contentFigure} color={portal.theme && portal.theme.contentFigureColor} />
        <Container maxWidth="lg" sx={{ py: 3, position: 'relative', zIndex: 1 }}>
        {cats.length > 0 && (
          <Box sx={{ mb: 2, display: 'flex', gap: 1, flexWrap: 'wrap' }}>
            {cats.map(function(c) {
              return <Chip key={c.id} label={c.name + ' (' + c.teamCount + ')'} variant={selectedCatId === c.id ? 'filled' : 'outlined'} color="primary" onClick={function() { setSelectedCatId(c.id); }} />;
            })}
          </Box>
        )}
        <Tabs
          value={effectiveTab}
          onChange={function(e, v) { setTab(v); }}
          variant="scrollable"
          scrollButtons="auto"
          allowScrollButtonsMobile
          sx={{ mb: 3 }}
        >
          {seccionesVisibles.map(function(s) { return <Tab key={s.key} label={s.label} />; })}
        </Tabs>
        {/* Un fade corto en cada cambio de pestaña o de categoría -- el
            `key` fuerza a React a tratar el contenido como nuevo, así que
            arranca en opacidad 0 y sube, en vez del corte instantáneo de
            antes. Respeta prefers-reduced-motion (timeout 0). */}
        <Fade in key={effectiveTab + ':' + (selectedCatId || '')} timeout={reducirMovimiento ? 0 : 200}>
          <Box>
            {seccionesVisibles.map(function(s, i) {
              if (effectiveTab !== i) return null;
              switch (s.key) {
                case 'standings':
                  return (
                    <StandingsView
                      key="standings"
                      data={standingsData}
                      loading={loadingStandings}
                      selectedCatId={selectedCatId}
                      sportInfo={comp}
                      variant={portal.theme && portal.theme.standingsVariant}
                    />
                  );
                case 'leaders':
                  return <LeadersView key="leaders" data={leadersData} loading={loadingLeaders} selectedCatId={selectedCatId} />;
                case 'classification':
                  return <ClassificationView key="classification" data={classificationData} loading={loadingClassification} selectedCatId={selectedCatId} />;
                case 'calendar':
                  return (
                    <CalendarView
                      key="calendar"
                      data={calendarData}
                      loading={loadingCalendar}
                      selectedCatId={selectedCatId}
                      orgSlug={orgSlug}
                      compSlug={compSlug}
                      mostrarEventos={showRosters}
                      variant={portal.theme && portal.theme.matchCardVariant}
                      esIndividual={!!comp.isIndividual}
                    />
                  );
                case 'bracket':
                  return (
                    <BracketView
                      key="bracket"
                      data={calendarData}
                      loading={loadingCalendar}
                      selectedCatId={selectedCatId}
                      bracketVariant={portal.theme && portal.theme.bracketVariant}
                      esIndividual={!!comp.isIndividual}
                    />
                  );
                case 'gallery':
                  return <GalleryView key="gallery" photos={gallery} />;
                default:
                  return null;
              }
            })}
          </Box>
        </Fade>

        {sponsors.length > 0 && (
          <Box sx={{ mt: 5, p: 3, borderRadius: 2, bgcolor: 'action.hover' }}>
            <Typography variant="overline" color="text.secondary" sx={{ display: 'block', mb: 1.5, textAlign: 'center' }}>
              Auspician
            </Typography>
            <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 3, alignItems: 'center', justifyContent: 'center' }}>
              {sponsors.map(function(s, i) {
                var logo = <Avatar src={s.logoUrl} variant="rounded" sx={{ width: 64, height: 64, bgcolor: 'background.paper', '& img': { objectFit: 'contain' } }} />;
                return s.url ? (
                  <Box component="a" key={i} href={s.url} target="_blank" rel="noopener noreferrer" title={s.name || ''} sx={{ display: 'flex' }}>
                    {logo}
                  </Box>
                ) : (
                  <Box key={i} title={s.name || ''} sx={{ display: 'flex' }}>{logo}</Box>
                );
              })}
            </Box>
          </Box>
        )}
      </Container>
      </Box>
      </Box>
    </ThemeProvider>
  );
}

function Navbar(props) {
  return (
    <AppBar position="static" elevation={0} sx={{ bgcolor: 'background.paper', color: 'text.primary', borderBottom: '1px solid', borderColor: 'divider' }}>
      <Toolbar>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, cursor: 'pointer' }} onClick={function() { props.navigate('/'); }}>
          {props.logoUrl ? (
            <Avatar src={props.logoUrl} variant="rounded" sx={{ width: 36, height: 36, '& img': { objectFit: 'contain' } }} />
          ) : (
            <Box sx={{ width: 36, height: 36, borderRadius: 1.5, bgcolor: 'primary.main', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
              {/* `primary.contrastText`, no un 'white' fijo: un organizador
                  puede elegir un color principal claro, y ahí el texto que
                  corresponde es oscuro (mismo cálculo que ya usa el resto
                  del sistema vía readableTextOn()). */}
              <Typography sx={{ color: 'primary.contrastText', fontWeight: 800, fontSize: 16 }}>SF</Typography>
            </Box>
          )}
          <Typography variant="h6" fontWeight={700}>SportFrog</Typography>
        </Box>
        <Box sx={{ flexGrow: 1 }} />
        <Button onClick={function() { props.navigate('/public'); }} sx={{ mr: 1 }}>Competiciones</Button>
        <Button variant="outlined" onClick={function() { props.navigate('/auth/jwt/sign-in'); }}>Iniciar sesion</Button>
      </Toolbar>
    </AppBar>
  );
}

/* -------------------------------------------------------------------------
   Líderes

   Un reglamento define varias métricas y una competencia recién empezada no
   tiene datos en casi ninguna. Dibujar una grilla vacía por cada una de ellas
   no informa nada: lo que el visitante necesita saber es que todavía no se
   cargaron estadísticas, dicho una sola vez.
   ------------------------------------------------------------------------- */

function LeadersView(props) {
  var loading = props.loading;
  var selectedCatId = props.selectedCatId;

  if (loading) return <Cargando kind="rows" />;

  var cats = (props.data && props.data.categories) || [];
  if (selectedCatId) cats = cats.filter(function(c) { return c.categoryId === selectedCatId; });

  var conDatos = cats
    .map(function(c) {
      return {
        categoryId: c.categoryId,
        categoryName: c.categoryName,
        boards: (c.boards || []).filter(function(b) { return (b.leaders || []).length > 0; })
      };
    })
    .filter(function(c) { return c.boards.length > 0; });

  if (conDatos.length === 0) return (
    <EmptyState icon="mdi:trophy-outline">
      Todavía no se registraron estadísticas individuales en esta competencia.
      Los tableros aparecen a medida que se cargan los eventos de cada partido.
    </EmptyState>
  );

  var variasCategorias = conDatos.length > 1;

  return conDatos.map(function(cat) {
    return (
      <Box key={cat.categoryId} sx={{ mb: 4 }}>
        {variasCategorias && (
          <Typography variant="h6" fontWeight={700} sx={{ mb: 1.5 }}>{cat.categoryName}</Typography>
        )}
        <Box
          sx={{
            display: 'grid',
            gap: 2,
            gridTemplateColumns: { xs: '1fr', sm: 'repeat(2, 1fr)', lg: 'repeat(3, 1fr)' }
          }}
        >
          {cat.boards.map(function(board) {
            // metricCode, no metricId: el tablero combinado de puntos no
            // tiene un metricId propio (junta varios), pero su código
            // sintético "points" es igual de único y estable.
            return <Tablero key={board.metricCode} board={board} />;
          })}
        </Box>
      </Box>
    );
  });
}

function Tablero(props) {
  var board = props.board;

  return (
    <Paper variant="outlined" sx={{ p: 2, borderRadius: 2 }}>
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 1 }}>
        <Typography variant="subtitle2" fontWeight={700} sx={{ flexGrow: 1 }}>{board.metricLabel}</Typography>
        {!board.affectsScore && (
          <Chip size="small" variant="outlined" label="Disciplina" sx={{ height: 20, fontSize: 11 }} />
        )}
      </Box>
      <Stack divider={<Divider flexItem />}>
        {board.leaders.map(function(l, i) {
          var pos = l.position || (i + 1);
          var nombre = ((l.firstName || '') + ' ' + (l.lastName || '')).trim();
          var pie = (l.jerseyNumber != null ? '#' + l.jerseyNumber + ' · ' : '') + (l.teamName || '');

          return (
            <Box
              key={l.rosterEntryId || i}
              sx={{
                display: 'flex', alignItems: 'center', gap: 1.25, py: 0.9,
                bgcolor: MEDAL_COLORS[pos] ? (t) => alpha(MEDAL_COLORS[pos], 0.08) : undefined,
              }}
            >
              <MedalCircle position={pos} />
              <Box sx={{ minWidth: 0, flexGrow: 1 }}>
                <Typography variant="body2" fontWeight={600} noWrap title={nombre}>{nombre}</Typography>
                <Typography variant="caption" color="text.secondary" noWrap sx={{ display: 'block' }} title={pie}>
                  {pie}
                </Typography>
              </Box>
              <Typography
                variant="h6"
                fontWeight={800}
                sx={{ flexShrink: 0, lineHeight: 1, fontVariantNumeric: 'tabular-nums' }}
              >
                {l.total}
              </Typography>
            </Box>
          );
        })}
      </Stack>
    </Paper>
  );
}

/* -------------------------------------------------------------------------
   Clasificación

   El equivalente de la tabla de posiciones para un deporte que no se decide
   por partido sino por puntaje de jueces (poomsae, hoy el único). Cada
   competidor actúa una vez y queda ordenado por puntaje; el que todavía no
   actuó aparece al final, sin posición, en vez de faltar de la lista.
   ------------------------------------------------------------------------- */

function ClassificationView(props) {
  var loading = props.loading;
  var selectedCatId = props.selectedCatId;

  if (loading) return <Cargando kind="rows" />;

  var cats = (props.data && props.data.categories) || [];
  if (selectedCatId) cats = cats.filter(function(c) { return c.categoryId === selectedCatId; });

  if (cats.length === 0) {
    return (
      <EmptyState icon="mdi:podium-outline">
        Todavía no hay clasificación para mostrar. Aparece apenas se abre la etapa de clasificación de la categoría.
      </EmptyState>
    );
  }

  var variasCategorias = cats.length > 1;

  return cats.map(function(cat) {
    return (
      <Box key={cat.categoryId} sx={{ mb: 4 }}>
        {variasCategorias && (
          <Typography variant="h6" fontWeight={700} sx={{ mb: 1.5 }}>{cat.categoryName}</Typography>
        )}
        <Paper variant="outlined" sx={{ borderRadius: 2, overflow: 'hidden' }}>
          <Stack divider={<Divider flexItem />}>
            {cat.rows.map(function(r) {
              var sinActuar = r.status === 'pending';
              return (
                <Box
                  key={r.performanceId}
                  sx={{
                    display: 'flex', alignItems: 'center', gap: 1.5, px: 2, py: 1.1,
                    bgcolor: MEDAL_COLORS[r.position] ? (t) => alpha(MEDAL_COLORS[r.position], 0.08) : undefined,
                  }}
                >
                  <MedalCircle position={r.position} size={30} />
                  <Typography variant="body2" fontWeight={600} noWrap sx={{ flexGrow: 1, minWidth: 0 }} title={r.teamName}>
                    {r.teamName}
                  </Typography>
                  {sinActuar ? (
                    <Chip size="small" variant="outlined" label="Sin actuar" />
                  ) : (
                    <Typography variant="h6" fontWeight={800} sx={{ lineHeight: 1, fontVariantNumeric: 'tabular-nums' }}>
                      {r.score}
                    </Typography>
                  )}
                </Box>
              );
            })}
          </Stack>
        </Paper>
      </Box>
    );
  });
}

/* -------------------------------------------------------------------------
   Galería

   Fotos del propio evento -- partidos, la premiación, el público. En grilla,
   y en grande al tocar una: no hace falta abrir cada una en una pestaña
   nueva ni perder el lugar en la grilla para volver a la que sigue.
   ------------------------------------------------------------------------- */

function GalleryView(props) {
  var photos = props.photos || [];
  var [abierta, setAbierta] = useState(null);
  var reducirMovimiento = usePrefersReducedMotion();

  if (photos.length === 0) {
    return <EmptyState icon="mdi:image-multiple-outline">Todavía no hay fotos para mostrar.</EmptyState>;
  }

  var actual = abierta != null ? photos[abierta] : null;

  return (
    <>
      <Box sx={{ display: 'grid', gridTemplateColumns: { xs: 'repeat(2, 1fr)', sm: 'repeat(3, 1fr)', md: 'repeat(4, 1fr)' }, gap: 1.5 }}>
        {photos.map(function(p, i) {
          return (
            <Box
              key={i}
              onClick={function() { setAbierta(i); }}
              title={p.caption || ''}
              sx={{
                cursor: 'pointer', borderRadius: 2, overflow: 'hidden', aspectRatio: '1 / 1', bgcolor: 'action.hover',
              }}
            >
              <Box
                component="img"
                src={p.url}
                alt={p.caption || ''}
                sx={{
                  width: '100%', height: '100%', objectFit: 'cover', display: 'block',
                  transition: 'transform 0.2s ease', '&:hover': { transform: 'scale(1.05)' },
                }}
              />
            </Box>
          );
        })}
      </Box>

      <Dialog
        open={abierta != null}
        onClose={function() { setAbierta(null); }}
        maxWidth="md"
        fullWidth
        TransitionComponent={Fade}
        transitionDuration={reducirMovimiento ? 0 : 220}
      >
        {actual && (
          <Box sx={{ position: 'relative', bgcolor: 'common.black' }}>
            <IconButton
              onClick={function() { setAbierta(null); }}
              aria-label="Cerrar"
              sx={{ position: 'absolute', top: 8, right: 8, color: 'white', bgcolor: 'rgba(0,0,0,0.4)', '&:hover': { bgcolor: 'rgba(0,0,0,0.6)' } }}
            >
              <Iconify icon="eva:close-outline" />
            </IconButton>
            {abierta > 0 && (
              <IconButton
                onClick={function() { setAbierta(abierta - 1); }}
                aria-label="Anterior"
                sx={{ position: 'absolute', top: '50%', left: 8, transform: 'translateY(-50%)', color: 'white', bgcolor: 'rgba(0,0,0,0.4)', '&:hover': { bgcolor: 'rgba(0,0,0,0.6)' } }}
              >
                <Iconify icon="eva:arrow-back-outline" />
              </IconButton>
            )}
            {abierta < photos.length - 1 && (
              <IconButton
                onClick={function() { setAbierta(abierta + 1); }}
                aria-label="Siguiente"
                sx={{ position: 'absolute', top: '50%', right: 8, transform: 'translateY(-50%)', color: 'white', bgcolor: 'rgba(0,0,0,0.4)', '&:hover': { bgcolor: 'rgba(0,0,0,0.6)' } }}
              >
                <Iconify icon="eva:arrow-forward-outline" />
              </IconButton>
            )}
            <Box component="img" src={actual.url} alt={actual.caption || ''} sx={{ width: '100%', maxHeight: '80vh', objectFit: 'contain', display: 'block' }} />
            {actual.caption && (
              <Typography variant="body2" sx={{ color: 'white', p: 1.5, textAlign: 'center' }}>{actual.caption}</Typography>
            )}
          </Box>
        )}
      </Dialog>
    </>
  );
}

/* -------------------------------------------------------------------------
   Calendario

   Un calendario se lee por jornada: qué se juega, contra quién y cómo terminó.
   La sede y la hora son detalle, y solo aparecen cuando existen.

   Una liga de dieciséis equipos a dos vueltas son treinta jornadas. Apilarlas
   todas obliga a rodar la pantalla hasta encontrar la que se está jugando, que
   es justo la única que casi todo el mundo vino a ver. Así que se muestra una
   por vez, y la que abre es la relevante: la primera que todavía no terminó.
   Si esa ya se jugó entera, la siguiente cae sola por la misma regla; si el
   torneo terminó, abre en la última.
   ------------------------------------------------------------------------- */

/** Un partido que todavía se espera jugar. */
function CalendarView(props) {
  var loading = props.loading;
  var selectedCatId = props.selectedCatId;
  var orgSlug = props.orgSlug;
  var compSlug = props.compSlug;
  var mostrarEventos = props.mostrarEventos;
  var esIndividual = props.esIndividual;
  var [eleccion, setEleccion] = useState(null);
  var [equipo, setEquipo] = useState('');

  var fixtures = (props.data && props.data.fixtures) || [];
  if (selectedCatId) fixtures = fixtures.filter(function(m) { return m.categoryId === selectedCatId; });

  // Por jornada y, dentro de ella, cronológico. Los partidos sin fecha van al
  // final de su jornada en lugar de al final de todo, que es donde el orden
  // que devuelve la API los deja y donde nadie los busca.
  // Los equipos salen de los propios partidos: el calendario publico trae
  // nombres, no identificadores. Y los nombres SE REPITEN entre categorias
  // -el mismo club inscribe un Calacoto en Sub-15 y otro en Sub-17-, asi que
  // la clave es el nombre junto a su categoria, no el nombre solo. Filtrar por
  // nombre pelado mezclaba dos equipos distintos en una sola lista.
  var equipos = [];
  var vistos = {};
  fixtures.forEach(function(m) {
    [m.homeTeamName, m.awayTeamName].forEach(function(n) {
      if (!n) return;
      var clave = m.categoryId + String.fromCharCode(31) + n;
      if (vistos[clave]) return;
      vistos[clave] = true;
      equipos.push({ clave: clave, nombre: n, categoria: m.categoryName, categoryId: m.categoryId });
    });
  });
  equipos.sort(function(a, b) {
    return a.nombre.localeCompare(b.nombre) || a.categoria.localeCompare(b.categoria);
  });

  // El nombre solo alcanza cuando hay una categoria a la vista; si no, hay que
  // decir cual es para poder elegir.
  var unaSolaCategoria = equipos.length === 0||
    !equipos.some(function(e) { return e.categoryId !== equipos[0].categoryId; });

  var elEquipo = equipos.find(function(e) { return e.clave === equipo; }) || null;

  // Un equipo juega una vez por jornada, asi que verlo jornada por jornada no
  // dice nada: lo que se pide al filtrar por equipo es su fixture entero.
  var deUnEquipo = !!elEquipo;
  var partidos = elEquipo
    ? fixtures.filter(function(m) {
        return m.categoryId === elEquipo.categoryId
          && (m.homeTeamName === elEquipo.nombre || m.awayTeamName === elEquipo.nombre);
      })
    : fixtures;

  // Agrupadas por jornada o por fase de eliminatoria -- ver
  // src/lib/match-rounds.js, compartido con BracketView (la llave, que
  // ahora es su propia pestaña y ya no un sub-modo de esta).
  var grupos = agruparPorRonda(partidos);

  // La primera sin terminar. Si están todas terminadas, la última: el torneo
  // se acabó y lo que alguien viene a ver es cómo cerró.
  var actual = grupos.find(function(g) { return !g.terminada; }) || grupos[grupos.length - 1];

  // Se vuelve a la jornada relevante cuando cambia el conjunto de jornadas
  // —otra categoría, un sorteo nuevo—, no cuando SWR revalida: eso pisaría la
  // jornada que la persona acaba de elegir a mano.
  var firma = (selectedCatId || 'sin-categoria') + '::' + grupos.map(function(g) { return g.clave; }).join('|');
  useEffect(function() {
    setEleccion(null);
    setEquipo('');
  }, [selectedCatId]);

  useEffect(function() {
    setEleccion(null);
  }, [firma]);

  if (loading) return <Cargando kind="calendar" />;

  if (fixtures.length === 0) return (
    <EmptyState icon="mdi:calendar-blank-outline">
      Todavía no hay partidos en esta sección. Aparecen apenas se realiza el sorteo del fixture.
    </EmptyState>
  );

  var elegida = grupos.find(function(g) { return g.clave === eleccion; })
    || actual;
  var visibles = deUnEquipo ? grupos : [elegida];

  var jugadosTotal = partidos.filter(function(m) { return m.homeTotal != null && m.awayTotal != null; }).length;
  var variasCategorias = !selectedCatId && partidos.length > 0
    && partidos.some(function(m) { return m.categoryId !== partidos[0].categoryId; });

  return (
    <Box>
      {equipos.length > 1 && (
        <TextField
          select
          size="small"
          label={esIndividual ? 'Deportista' : 'Equipo'}
          value={equipo}
          onChange={function(e) { setEquipo(e.target.value); }}
          sx={{ mb: 2, minWidth: 260 }}
        >
          <MenuItem value="">{esIndividual ? 'Todos los deportistas' : 'Todos los equipos'}</MenuItem>
          {equipos.map(function(e) {
            return (
              <MenuItem key={e.clave} value={e.clave}>
                {unaSolaCategoria ? e.nombre : e.nombre + " - " + e.categoria}
              </MenuItem>
            );
          })}
        </TextField>
      )}

      {/* Con un equipo elegido las jornadas dejan de servir como filtro:
          juega una vez en cada una. Se muestra su fixture entero. */}
      {!deUnEquipo && grupos.length > 1 && (
            <Box sx={{ mb: 2 }}>
              <Box
                sx={{
                  display: 'flex', gap: 1, flexWrap: 'wrap', alignItems: 'center',
                  pb: 0.5
                }}
              >
                {grupos.map(function(g) {
                  var seleccionada = elegida && g.clave === elegida.clave;
                  return (
                    <Chip
                      key={g.clave}
                      label={g.titulo.replace('Jornada ', 'J')}
                      size="small"
                      onClick={function() { setEleccion(g.clave); }}
                      color={seleccionada ? 'primary' : g.enCurso ? 'warning' : 'default'}
                      variant={seleccionada || g.enCurso ? 'filled' : 'outlined'}
                      sx={{
                        fontWeight: seleccionada ? 700 : 500,
                        // Una jornada ya jugada se atenúa: sigue ahí para volver a
                        // ella, pero no compite por la atención con la que viene.
                        opacity: !seleccionada && g.terminada ? 0.55 : 1
                      }}
                    />
                  );
                })}
              </Box>
            </Box>
          )}

          <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
            {deUnEquipo
              ? elEquipo.nombre + ' · ' + partidos.length + ' ' + (partidos.length === 1 ? 'partido' : 'partidos')
                + ' · ' + jugadosTotal + ' con resultado'
              : elegida
                ? elegida.matches.length + ' ' + (elegida.matches.length === 1 ? 'partido' : 'partidos')
                  + ' · ' + elegida.jugados + ' con resultado'
                  + (elegida.terminada ? ' · jornada completa' : '')
                : ''}
          </Typography>

          {visibles.filter(Boolean).map(function(g) {
            return (
              <Box key={g.clave} sx={{ mb: 3 }}>
                <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5, mb: 1.5 }}>
                  <Box sx={{ width: 28, height: 4, borderRadius: 1, bgcolor: 'primary.main', flexShrink: 0 }} />
                  <Typography
                    variant="subtitle1"
                    sx={{ fontWeight: 800, letterSpacing: 0.4, textTransform: 'uppercase', fontSize: 15 }}
                  >
                    {g.titulo}
                  </Typography>
                  {g.enCurso && <Chip label="En juego" color="warning" size="small" sx={{ height: 20, fontSize: 11 }} />}
                </Box>
                <Stack spacing={1}>
                  {g.matches.map(function(m) {
                    return (
                      <Partido
                        key={m.id}
                        m={m}
                        mostrarCategoria={variasCategorias}
                        destacado={elEquipo ? elEquipo.nombre : null}
                        orgSlug={orgSlug}
                        compSlug={compSlug}
                        mostrarEventos={mostrarEventos}
                        variant={props.variant}
                      />
                    );
                  })}
                </Stack>
              </Box>
            );
          })}
    </Box>
  );
}

function Cargando(props) {
  return <SectionSkeleton kind={props.kind} />;
}

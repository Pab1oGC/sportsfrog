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
import ToggleButton from '@mui/material/ToggleButton';
import ToggleButtonGroup from '@mui/material/ToggleButtonGroup';
import Dialog from '@mui/material/Dialog';
import { DataGrid } from '@mui/x-data-grid';
import { alpha, ThemeProvider, useTheme } from '@mui/material/styles';
import useSWR from 'swr';
import publicAxios from 'src/lib/public-axios';
import { Iconify } from 'src/components/iconify';
import { ColorModeToggle } from 'src/components/color-mode-toggle';
import { useColorMode } from 'src/theme';
import { buildPortalTheme, portalFontHref } from 'src/lib/portal-theme';
import { PortalHero } from 'src/pages/public/portal-hero';
import { FASES } from 'src/lib/phase-labels';
import { PENDIENTE } from 'src/lib/match-status';
import { etiquetasDesempate, columnasMarcador } from 'src/lib/tiebreaker-labels';
import { embedSrc } from 'src/lib/google-maps-url';

var publicFetcher = function(url) { return publicAxios.get(url).then(function(r) { return r.data; }); };
var SC = { scheduled: 'info', in_progress: 'warning', finished: 'success', cancelled: 'error', walkover: 'warning', postponed: 'default' };
var SL = { scheduled: 'Programado', in_progress: 'En curso', finished: 'Finalizado', cancelled: 'Cancelado', walkover: 'Walkover', postponed: 'Aplazado' };
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
];

export default function PublicCompetitionPage() {
  var params = useParams();
  var navigate = useNavigate();
  var orgSlug = params.orgSlug;
  var compSlug = params.compSlug;
  var [tab, setTab] = useState(0);
  var [selectedCatId, setSelectedCatId] = useState('');

  var { data: comp, isLoading: loadingComp, error: errorComp } = useSWR(
    '/api/public/' + orgSlug + '/' + compSlug, publicFetcher
  );

  // El tema del portal de esta competencia, sobre el tema de la app. Si la
  // competencia no personalizó nada, buildPortalTheme devuelve el tema base
  // intacto y la página se ve igual que siempre. El modo del visitante entra
  // acá para que "sigue al visitante" funcione; una competencia que fijó
  // claro u oscuro lo ignora dentro de buildPortalTheme.
  var appTheme = useTheme();
  var colorMode = useColorMode();
  var portalTheme = useMemo(function() {
    return buildPortalTheme({ base: appTheme, portal: comp && comp.portal, visitorMode: colorMode.mode });
  }, [appTheme, comp, colorMode.mode]);
  var headingFontHref = portalFontHref(comp && comp.portal && comp.portal.theme && comp.portal.theme.headingFont);

  // El <body> lo pinta el CssBaseline del nivel de la app, fuera de este
  // ThemeProvider: si el portal fijó un modo distinto al del panel, el fondo
  // que se ve al hacer overscroll quedaría del otro color. Se acompaña a mano
  // mientras esta página está montada.
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
  // (comp.portal.sectionOrder, siempre las cinco secciones, en algún orden
  // -- ver PortalSection.Resolve). Acá solo se filtra por cuáles van
  // ocultas, que sigue siendo un interruptor aparte y no parte del orden: el
  // calendario nunca se filtra, no tiene interruptor.
  var sectionOrder = (comp && comp.portal && comp.portal.sectionOrder) || DEFAULT_SECTION_ORDER;
  var seccionVisible = {
    standings: showStandings, leaders: showLeaders, classification: showClassification,
    calendar: true, gallery: showGallery,
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

  var { data: calendarData, isLoading: loadingCalendar } = useSWR(
    comp && seccionActiva === 'calendar' ? '/api/public/' + orgSlug + '/' + compSlug + '/matches' : null, publicFetcher
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
  var esquemaFijo = portal.theme && portal.theme.colorScheme && portal.theme.colorScheme !== 'auto';

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
      <Navbar navigate={navigate} logoUrl={comp.organizationLogoUrl} showModeToggle={!esquemaFijo} />
      <PortalHero comp={comp} portal={portal} moment={comp.moment} onBack={function() { navigate('/public'); }} />
      <Container maxWidth="lg" sx={{ py: 3 }}>
        {cats.length > 0 && (
          <Box sx={{ mb: 2, display: 'flex', gap: 1, flexWrap: 'wrap' }}>
            {cats.map(function(c) {
              return <Chip key={c.id} label={c.name + ' (' + c.teamCount + ')'} variant={selectedCatId === c.id ? 'filled' : 'outlined'} color="primary" onClick={function() { setSelectedCatId(c.id); }} />;
            })}
          </Box>
        )}
        <Tabs value={effectiveTab} onChange={function(e, v) { setTab(v); }} sx={{ mb: 3 }}>
          {seccionesVisibles.map(function(s) { return <Tab key={s.key} label={s.label} />; })}
        </Tabs>
        {seccionesVisibles.map(function(s, i) {
          if (effectiveTab !== i) return null;
          switch (s.key) {
            case 'standings':
              return <StandingsView key="standings" data={standingsData} loading={loadingStandings} selectedCatId={selectedCatId} sportInfo={comp} />;
            case 'leaders':
              return <LeadersView key="leaders" data={leadersData} loading={loadingLeaders} selectedCatId={selectedCatId} />;
            case 'classification':
              return <ClassificationView key="classification" data={classificationData} loading={loadingClassification} selectedCatId={selectedCatId} />;
            case 'calendar':
              return <CalendarView key="calendar" data={calendarData} loading={loadingCalendar} selectedCatId={selectedCatId} orgSlug={orgSlug} compSlug={compSlug} mostrarEventos={showRosters} />;
            case 'gallery':
              return <GalleryView key="gallery" photos={gallery} />;
            default:
              return null;
          }
        })}

        {sponsors.length > 0 && (
          <Box sx={{ mt: 5, pt: 3, borderTop: '1px solid', borderColor: 'divider' }}>
            <Typography variant="overline" color="text.secondary" sx={{ display: 'block', mb: 1.5, textAlign: 'center' }}>
              Auspician
            </Typography>
            <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 3, alignItems: 'center', justifyContent: 'center' }}>
              {sponsors.map(function(s, i) {
                var logo = <Avatar src={s.logoUrl} variant="rounded" sx={{ width: 64, height: 64, bgcolor: 'action.hover', '& img': { objectFit: 'contain' } }} />;
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
              <Typography sx={{ color: 'white', fontWeight: 800, fontSize: 16 }}>SF</Typography>
            </Box>
          )}
          <Typography variant="h6" fontWeight={700}>SportFrog</Typography>
        </Box>
        <Box sx={{ flexGrow: 1 }} />
        {/* La competencia puede fijar claro u oscuro; ahí el interruptor no
            haría nada y se esconde. */}
        {props.showModeToggle !== false && <ColorModeToggle sx={{ mr: 0.5 }} />}
        <Button onClick={function() { props.navigate('/public'); }} sx={{ mr: 1 }}>Competiciones</Button>
        <Button variant="outlined" onClick={function() { props.navigate('/auth/jwt/sign-in'); }}>Iniciar sesion</Button>
      </Toolbar>
    </AppBar>
  );
}

function StandingsView(props) {
  var data = props.data;
  var loading = props.loading;
  var selectedCatId = props.selectedCatId;
  var sportInfo = props.sportInfo;

  if (loading) return <Box sx={{ display: 'flex', justifyContent: 'center', py: 4 }}><CircularProgress /></Box>;
  if (!data || !data.categories || data.categories.length === 0) return <Alert severity="info">Sin datos de posiciones.</Alert>;

  var catsToShow = data.categories;
  if (selectedCatId) catsToShow = catsToShow.filter(function(c) { return c.categoryId === selectedCatId; });

  // GF/GC en un deporte de suma, SG/SP ("sets ganados"/"sets perdidos") en
  // uno por sets — la misma unidad que arma las etiquetas de desempate abajo,
  // asi que las dos siempre coinciden.
  var columnasScore = columnasMarcador(sportInfo);

  // Las columnas dependen del reglamento de cada categoria (allowsDraw viene
  // por categoria, no por deporte: dos categorias del mismo deporte pueden
  // jugar con reglamentos distintos), asi que se arman por categoria y no una
  // sola vez para toda la vista.
  //
  // sortable: false en cada columna — una tabla de posiciones publica no es
  // una planilla: el orden lo decide el reglamento (puntos, luego los
  // criterios de desempate ya aplicados del lado del servidor), asi que
  // dejar que cualquiera la reordene con un clic mostraria una tabla que
  // "miente" sobre quien va primero.
  function columnasDe(cat) {
    return [
      { field: 'position', headerName: '#', width: 50, sortable: false },
      { field: 'teamName', headerName: 'Equipo', flex: 1, minWidth: 180, sortable: false, renderCell: function(p) {
        return (
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, height: '100%' }}>
            <Avatar src={p.row.clubLogoUrl || undefined} variant="rounded" sx={{ width: 24, height: 24, bgcolor: 'action.hover', '& img': { objectFit: 'contain' } }}>
              {!p.row.clubLogoUrl && <Iconify icon="mdi:office-building-outline" width={14} sx={{ color: 'text.disabled' }} />}
            </Avatar>
            <Typography variant="body2" noWrap>{p.value}</Typography>
          </Box>
        );
      } },
      { field: 'played', headerName: 'PJ', width: 50, sortable: false },
      { field: 'won', headerName: 'PG', width: 50, sortable: false },
      // No es que la columna siempre de cero: es que este reglamento no
      // tiene un empate que precie — bajo sets porque el modo no lo tiene,
      // en un deporte de suma porque estos organizadores no le pusieron
      // puntaje. La pregunta directamente no aplica.
      cat.allowsDraw && { field: 'drawn', headerName: 'PE', width: 50, sortable: false },
      { field: 'lost', headerName: 'PP', width: 50, sortable: false },
      { field: 'scoreFor', headerName: columnasScore.favor, width: 50, sortable: false },
      { field: 'scoreAgainst', headerName: columnasScore.contra, width: 50, sortable: false },
      { field: 'scoreDifference', headerName: 'DF', width: 50, sortable: false },
      { field: 'points', headerName: 'Pts', width: 60, sortable: false }
    ].filter(Boolean);
  }

  var etiquetas = etiquetasDesempate(sportInfo);

  return catsToShow.map(function(cat) {
    var qualifies = cat.qualifiersPerGroup || 0;
    var cols = columnasDe(cat);
    return (
      <Box key={cat.categoryId} sx={{ mb: 3 }}>
        {(cat.groups || []).map(function(grp, gi) {
          var rows = (grp.rows || []).map(function(r, i) {
            return Object.assign({}, r, { id: r.teamId || i, position: r.position || (i + 1) });
          });
          return (
            <Box key={cat.categoryId + '-' + gi} sx={{ mb: 1.5 }}>
              <Typography variant="h6" fontWeight={600} sx={{ mb: 1 }}>{cat.categoryName}{grp.label ? ' - Grupo ' + grp.label : ''}</Typography>
              <DataGrid
                rows={rows}
                columns={cols}
                autoHeight
                hideFooter
                disableRowSelectionOnClick
                disableColumnMenu
                disableColumnResize
                getRowId={function(r) { return r.id; }}
                getRowClassName={function(p) { return qualifies && p.row.position <= qualifies ? 'row-clasifica' : ''; }}
                sx={{
                  // Sin el menu de columna (que ademas de ordenar dejaba
                  // esconder columnas) el header ya no tiene nada para hacer
                  // clic — se le saca tambien el cursor y el resaltado de hover
                  // que sugerian lo contrario, para que se lea como una tabla
                  // fija y no como una planilla editable.
                  '& .MuiDataGrid-columnHeader': { cursor: 'default' },
                  '& .MuiDataGrid-columnHeader:focus, & .MuiDataGrid-columnHeader:focus-within': { outline: 'none' },
                  '& .MuiDataGrid-row:hover': { bgcolor: 'transparent' },
                  '& .MuiDataGrid-row.row-clasifica, & .MuiDataGrid-row.row-clasifica:hover': function(theme) {
                    return { bgcolor: alpha(theme.palette.success.main, 0.14) };
                  },
                }}
              />
            </Box>
          );
        })}
        {qualifies > 0 && (
          <Stack direction="row" alignItems="center" spacing={1} sx={{ mb: 1 }}>
            <Box sx={{ width: 14, height: 14, borderRadius: 0.5, bgcolor: (theme) => alpha(theme.palette.success.main, 0.35), border: '1px solid', borderColor: 'success.main', flexShrink: 0 }} />
            <Typography variant="caption" color="text.secondary">
              {qualifies === 1
                ? 'Clasifica a la siguiente ronda el primero de cada grupo.'
                : 'Clasifican a la siguiente ronda los primeros ' + qualifies + ' de cada grupo.'}
            </Typography>
          </Stack>
        )}
        {cat.tiebreakers && cat.tiebreakers.length > 0 && (
          <Typography variant="caption" color="text.secondary" sx={{ mt: 0.5, display: 'block' }}>
            Criterios de desempate: {cat.tiebreakers.map(function(code) { return etiquetas[code] || code; }).join(', ')}
          </Typography>
        )}
      </Box>
    );
  });
}

/* -------------------------------------------------------------------------
   Líderes

   Un reglamento define varias métricas y una competencia recién empezada no
   tiene datos en casi ninguna. Dibujar una grilla vacía por cada una de ellas
   no informa nada: lo que el visitante necesita saber es que todavía no se
   cargaron estadísticas, dicho una sola vez.
   ------------------------------------------------------------------------- */

var MEDALLA = { 1: '#C9A227', 2: '#8E8E93', 3: '#B87333' };

function LeadersView(props) {
  var loading = props.loading;
  var selectedCatId = props.selectedCatId;

  if (loading) return <Cargando />;

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
    <Alert severity="info">
      Todavía no se registraron estadísticas individuales en esta competencia.
      Los tableros aparecen a medida que se cargan los eventos de cada partido.
    </Alert>
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
            <Box key={l.rosterEntryId || i} sx={{ display: 'flex', alignItems: 'center', gap: 1.25, py: 0.9 }}>
              <Box
                sx={{
                  width: 24, height: 24, flexShrink: 0, borderRadius: '50%',
                  display: 'flex', alignItems: 'center', justifyContent: 'center',
                  fontSize: 12, fontWeight: 700,
                  bgcolor: MEDALLA[pos] || 'action.selected',
                  color: MEDALLA[pos] ? 'white' : 'text.secondary'
                }}
              >
                {pos}
              </Box>
              <Box sx={{ minWidth: 0, flexGrow: 1 }}>
                <Typography variant="body2" fontWeight={600} noWrap title={nombre}>{nombre}</Typography>
                <Typography variant="caption" color="text.secondary" noWrap sx={{ display: 'block' }} title={pie}>
                  {pie}
                </Typography>
              </Box>
              <Typography variant="h6" fontWeight={700} sx={{ flexShrink: 0, lineHeight: 1 }}>{l.total}</Typography>
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

  if (loading) return <Cargando />;

  var cats = (props.data && props.data.categories) || [];
  if (selectedCatId) cats = cats.filter(function(c) { return c.categoryId === selectedCatId; });

  if (cats.length === 0) {
    return (
      <Alert severity="info">
        Todavía no hay clasificación para mostrar. Aparece apenas se abre la etapa de clasificación de la categoría.
      </Alert>
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
                <Box key={r.performanceId} sx={{ display: 'flex', alignItems: 'center', gap: 1.5, px: 2, py: 1.1 }}>
                  <Box
                    sx={{
                      width: 28, height: 28, flexShrink: 0, borderRadius: '50%',
                      display: 'flex', alignItems: 'center', justifyContent: 'center',
                      fontSize: 13, fontWeight: 700,
                      bgcolor: MEDALLA[r.position] || 'action.selected',
                      color: MEDALLA[r.position] ? 'white' : 'text.secondary'
                    }}
                  >
                    {r.position || '—'}
                  </Box>
                  <Typography variant="body2" fontWeight={600} noWrap sx={{ flexGrow: 1, minWidth: 0 }} title={r.teamName}>
                    {r.teamName}
                  </Typography>
                  {sinActuar ? (
                    <Chip size="small" variant="outlined" label="Sin actuar" />
                  ) : (
                    <Typography variant="h6" fontWeight={700} sx={{ lineHeight: 1 }}>{r.score}</Typography>
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

  if (photos.length === 0) {
    return <Alert severity="info">Todavía no hay fotos para mostrar.</Alert>;
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

      <Dialog open={abierta != null} onClose={function() { setAbierta(null); }} maxWidth="md" fullWidth>
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
  var [eleccion, setEleccion] = useState(null);
  var [equipo, setEquipo] = useState('');
  // "Lista" es el default: una categoria sin eliminatoria nunca ve el
  // interruptor (hayLlave, mas abajo, lo esconde), y una que si la tiene abre
  // igual en la vista de siempre en vez de cambiarle la pantalla a nadie.
  var [vista, setVista] = useState('lista');

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

  // La fase manda antes que la ronda: una categoria que paso de grupos a
  // eliminatoria vuelve a empezar su numeracion de ronda en el cruce, igual
  // que la volvio a empezar la propia fase de grupos -asi que "ronda 1" sola
  // no distingue la primera jornada de grupos del primer cruce de la llave.
  // La fase si distingue: es null en toda la fase de grupos y no-null en
  // toda la eliminatoria.
  var orden = partidos.slice().sort(function(a, b) {
    var faseA = a.phase ? 1 : 0;
    var faseB = b.phase ? 1 : 0;
    if (faseA !== faseB) return faseA - faseB;
    var ra = a.roundNumber == null ? 9999 : a.roundNumber;
    var rb = b.roundNumber == null ? 9999 : b.roundNumber;
    if (ra !== rb) return ra - rb;
    if (a.scheduledAt && !b.scheduledAt) return -1;
    if (!a.scheduledAt && b.scheduledAt) return 1;
    if (a.scheduledAt && b.scheduledAt) return new Date(a.scheduledAt) - new Date(b.scheduledAt);
    return 0;
  });

  var grupos = [];
  var porClave = {};
  orden.forEach(function(m) {
    var clave = m.phase ? 'f:' + m.phase : (m.roundNumber != null ? 'j:' + m.roundNumber : 'sin');
    if (!porClave[clave]) {
      porClave[clave] = {
        clave: clave,
        titulo: m.phase
          ? (FASES[m.phase] || m.phase)
          : (m.roundNumber != null ? 'Jornada ' + m.roundNumber : 'Sin jornada'),
        matches: []
      };
      grupos.push(porClave[clave]);
    }
    porClave[clave].matches.push(m);
  });

  grupos.forEach(function(g) {
    g.pendientes = g.matches.filter(function(m) { return PENDIENTE[m.status]; }).length;
    g.jugados = g.matches.filter(function(m) { return m.homeTotal != null && m.awayTotal != null; }).length;
    g.terminada = g.pendientes === 0;
    g.enCurso = g.pendientes > 0 && g.jugados > 0;
  });

  // Solo las rondas de eliminatoria arman una llave — una jornada de grupos
  // no tiene cruces que dibujar como árbol. Filtrada por equipo tampoco: el
  // fixture de un solo equipo es una lista de partidos suyos, no una llave
  // entera de la que ver el resto no aporta nada.
  var gruposFase = grupos.filter(function(g) { return g.clave.indexOf('f:') === 0; });
  var hayLlave = gruposFase.length > 0 && !deUnEquipo;
  var vistaLlave = hayLlave && vista === 'llave';

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

  if (loading) return <Cargando />;

  if (fixtures.length === 0) return (
    <Alert severity="info">
      Todavía no hay partidos en esta sección. Aparecen apenas se realiza el sorteo del fixture.
    </Alert>
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
          label="Equipo"
          value={equipo}
          onChange={function(e) { setEquipo(e.target.value); }}
          sx={{ mb: 2, minWidth: 260 }}
        >
          <MenuItem value="">Todos los equipos</MenuItem>
          {equipos.map(function(e) {
            return (
              <MenuItem key={e.clave} value={e.clave}>
                {unaSolaCategoria ? e.nombre : e.nombre + " — " + e.categoria}
              </MenuItem>
            );
          })}
        </TextField>
      )}

      {hayLlave && (
        <ToggleButtonGroup
          exclusive
          size="small"
          value={vista}
          onChange={function(e, v) { if (v) setVista(v); }}
          sx={{ mb: 2, display: 'flex', width: { xs: '100%', sm: 'fit-content' } }}
        >
          <ToggleButton value="lista" sx={{ flex: { xs: 1, sm: 'initial' } }}>Lista</ToggleButton>
          <ToggleButton value="llave" sx={{ flex: { xs: 1, sm: 'initial' } }}>Llave</ToggleButton>
        </ToggleButtonGroup>
      )}

      {vistaLlave ? (
        <Llave grupos={gruposFase} orgSlug={orgSlug} compSlug={compSlug} />
      ) : (
        <>
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
                <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 1 }}>
                  <Typography
                    variant="overline"
                    color="text.secondary"
                    sx={{ fontWeight: 700, letterSpacing: 1 }}
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
                      />
                    );
                  })}
                </Stack>
              </Box>
            );
          })}
        </>
      )}
    </Box>
  );
}

/** La llave de una eliminatoria, ronda por ronda: una columna por fase, ordenadas
 * de la primera a la final. El alto de cada columna lo estira su fila (todas las
 * columnas son hijas del mismo Box en fila), y "space-around" reparte los cruces
 * dentro de ese alto — que es lo que hace que las columnas de rondas con menos
 * cruces se vean más separadas: la misma convergencia visual de un árbol, sin
 * dibujar una sola línea. */
function Llave(props) {
  var grupos = props.grupos;
  var orgSlug = props.orgSlug;
  var compSlug = props.compSlug;

  // El campeón: la última ronda, cuando quedó en un solo cruce ya jugado.
  var ultima = grupos[grupos.length - 1];
  var final = ultima && ultima.matches.length === 1 ? ultima.matches[0] : null;
  var finalJugada = final && final.homeTotal != null && final.awayTotal != null;
  var campeon = null;

  if (finalJugada) {
    var penales = final.homeTotal === final.awayTotal && final.penaltyHomeScore != null;
    var ganoLocal = penales ? final.penaltyHomeScore > final.penaltyAwayScore : final.homeTotal > final.awayTotal;
    campeon = ganoLocal ? final.homeTeamName : final.awayTeamName;
  }

  return (
    <Box>
      {campeon && (
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5, mb: 2.5, p: 1.5, borderRadius: 2, bgcolor: 'action.hover' }}>
          <Typography sx={{ fontSize: 24, lineHeight: 1 }}>🏆</Typography>
          <Box>
            <Typography variant="subtitle1" fontWeight={700} sx={{ lineHeight: 1.2 }}>{campeon}</Typography>
            <Typography variant="caption" color="text.secondary">Campeón</Typography>
          </Box>
        </Box>
      )}
      <Box sx={{ display: 'flex', gap: { xs: 2, sm: 3 }, overflowX: 'auto', pb: 1 }}>
        {grupos.map(function(g) {
          return (
            <Box key={g.clave} sx={{ minWidth: 210, width: 210, flexShrink: 0, display: 'flex', flexDirection: 'column' }}>
              <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 0.75, mb: 1 }}>
                <Typography variant="overline" color="text.secondary" sx={{ fontWeight: 700, textAlign: 'center' }}>
                  {g.titulo}
                </Typography>
                {g.enCurso && <Chip label="En juego" color="warning" size="small" sx={{ height: 18, fontSize: 10 }} />}
              </Box>
              <Box sx={{ flexGrow: 1, display: 'flex', flexDirection: 'column', justifyContent: 'space-around', gap: 2 }}>
                {g.matches.map(function(m) {
                  return <CruceLlave key={m.id} m={m} orgSlug={orgSlug} compSlug={compSlug} />;
                })}
              </Box>
            </Box>
          );
        })}
      </Box>
    </Box>
  );
}

/** Un cruce de la llave: los dos equipos, uno arriba del otro, con el marcador
 * al costado — compacto a propósito, para que quepan varios por columna. */
function CruceLlave(props) {
  var m = props.m;
  var jugado = m.homeTotal != null && m.awayTotal != null;
  var enVivo = !jugado && m.status === 'in_progress' && m.liveHomeTotal != null && m.liveAwayTotal != null;
  var huboPenales = jugado && m.homeTotal === m.awayTotal && m.penaltyHomeScore != null;
  var ganoLocal = jugado && (huboPenales ? m.penaltyHomeScore > m.penaltyAwayScore : m.homeTotal > m.awayTotal);
  var ganoVisita = jugado && (huboPenales ? m.penaltyAwayScore > m.penaltyHomeScore : m.awayTotal > m.homeTotal);

  return (
    <Paper variant="outlined" sx={{ p: 1, borderRadius: 1.5 }}>
      <FilaCruce nombre={m.homeTeamName} logo={m.homeClubLogoUrl} score={jugado ? m.homeTotal : enVivo ? m.liveHomeTotal : null} gano={ganoLocal} />
      <Divider sx={{ my: 0.5 }} />
      <FilaCruce nombre={m.awayTeamName} logo={m.awayClubLogoUrl} score={jugado ? m.awayTotal : enVivo ? m.liveAwayTotal : null} gano={ganoVisita} />
      {huboPenales && (
        <Typography variant="caption" color="text.secondary" sx={{ display: 'block', textAlign: 'center', mt: 0.5 }}>
          ({m.penaltyHomeScore}-{m.penaltyAwayScore} pen)
        </Typography>
      )}
      {enVivo && <Chip label="EN VIVO" color="warning" size="small" sx={{ mt: 0.5, width: '100%', fontSize: 10, height: 18 }} />}
      {!jugado && !enVivo && (
        <Typography variant="caption" color="text.secondary" sx={{ display: 'block', textAlign: 'center', mt: 0.5 }}>
          {m.scheduledAt ? fechaCorta(m.scheduledAt) + ' · ' + hora(m.scheduledAt) : 'Por programar'}
        </Typography>
      )}
    </Paper>
  );
}

function FilaCruce(props) {
  return (
    <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.75 }}>
      <EscudoEquipo url={props.logo} />
      <Typography variant="body2" noWrap title={props.nombre} sx={{ flexGrow: 1, minWidth: 0, fontWeight: props.gano ? 700 : 500 }}>
        {props.nombre}
      </Typography>
      <Typography variant="body2" fontWeight={700} sx={{ minWidth: 16, textAlign: 'right' }}>
        {props.score != null ? props.score : ''}
      </Typography>
    </Box>
  );
}

function Partido(props) {
  var m = props.m;
  var jugado = m.homeTotal != null && m.awayTotal != null;
  // Mientras el partido esta en curso todavia no hay Resultado cargado
  // (homeTotal/awayTotal siguen null a proposito, hasta que se cierra el
  // partido), pero puede que ya haya goles registrados como eventos: se
  // muestra ese marcador en vivo en su lugar, para no dejar la tarjeta en
  // "vs" mientras se esta jugando.
  var enVivo = !jugado && m.status === 'in_progress' && m.liveHomeTotal != null && m.liveAwayTotal != null;
  var homeMostrado = jugado ? m.homeTotal : enVivo ? m.liveHomeTotal : null;
  var awayMostrado = jugado ? m.awayTotal : enVivo ? m.liveAwayTotal : null;
  // Empatado en los 90 minutos no siempre es empatado en la llave: una
  // eliminatoria que llego a penales ya tiene quien sigue, y ese es el que se
  // resalta, no el resultado del partido en si.
  var huboPenales = jugado && m.homeTotal === m.awayTotal && m.penaltyHomeScore != null;
  var ganoLocal = jugado && (huboPenales ? m.penaltyHomeScore > m.penaltyAwayScore : m.homeTotal > m.awayTotal);
  var ganoVisita = jugado && (huboPenales ? m.penaltyAwayScore > m.penaltyHomeScore : m.awayTotal > m.homeTotal);

  // Cuando se filtra por un equipo, marcarlo dentro de la fila: de un vistazo
  // se ve si jugó de local o de visitante sin leer los dos nombres.
  var esLocal = props.destacado && m.homeTeamName === props.destacado;
  var esVisita = props.destacado && m.awayTeamName === props.destacado;

  var cancha = [m.venueName, m.spaceName].filter(Boolean).join(' · ');

  // La cronología nombra jugadores, así que sigue la misma regla que un
  // plantel: solo se ofrece cuando la competencia publicó planteles, y solo
  // tiene sentido pedirla una vez que el partido empezó a jugarse.
  var puedeVerCronologia = props.mostrarEventos && (m.status === 'in_progress' || m.status === 'finished');
  var [expandido, setExpandido] = useState(false);
  var [mapaAbierto, setMapaAbierto] = useState(false);

  return (
    <Paper
      variant="outlined"
      sx={{
        p: { xs: 1.5, sm: 2 },
        borderRadius: 2,
        display: 'flex',
        flexDirection: 'column',
        gap: { xs: 1, md: 1 }
      }}
    >
    <Box
      sx={{
        display: 'flex',
        flexDirection: { xs: 'column', md: 'row' },
        alignItems: { md: 'center' },
        gap: { xs: 1, md: 2 }
      }}
    >
      <Box sx={{ width: { md: 140 }, flexShrink: 0 }}>
        {m.scheduledAt ? (
          <>
            <Typography variant="body2" fontWeight={600} sx={{ textTransform: 'capitalize' }}>
              {fechaCorta(m.scheduledAt)}
            </Typography>
            <Typography variant="caption" color="text.secondary">{hora(m.scheduledAt)}</Typography>
          </>
        ) : (
          <Typography variant="caption" color="text.secondary" fontStyle="italic">Por programar</Typography>
        )}
        {props.mostrarCategoria && (
          <Typography variant="caption" color="text.disabled" noWrap sx={{ display: 'block' }}>
            {m.categoryName}
          </Typography>
        )}
      </Box>

      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5, flexGrow: 1, minWidth: 0 }}>
        <Box sx={{ flex: 1, minWidth: 0, display: 'flex', alignItems: 'center', justifyContent: 'flex-end', gap: 0.75 }}>
          <Typography
            variant="body2"
            noWrap
            title={m.homeTeamName}
            sx={{ minWidth: 0, textAlign: 'right', fontWeight: esLocal || ganoLocal ? 700 : 500, color: esLocal ? 'primary.main' : undefined }}
          >
            {m.homeTeamName}
          </Typography>
          <EscudoEquipo url={m.homeClubLogoUrl} />
        </Box>
        <Box
          sx={{
            flexShrink: 0, minWidth: 62, textAlign: 'center',
            px: 1.25, py: 0.5, borderRadius: 1, fontWeight: 700, fontSize: 15,
            bgcolor: jugado ? 'text.primary' : enVivo ? 'warning.main' : 'action.hover',
            color: jugado ? 'background.paper' : enVivo ? 'warning.contrastText' : 'text.secondary'
          }}
        >
          {jugado || enVivo ? homeMostrado + ' - ' + awayMostrado : 'vs'}
          {huboPenales && (
            <Typography variant="caption" component="div" sx={{ fontSize: 9, fontWeight: 600, letterSpacing: 0.3, lineHeight: 1.2, opacity: 0.8 }}>
              ({m.penaltyHomeScore}-{m.penaltyAwayScore} pen)
            </Typography>
          )}
          {enVivo && (
            <Typography variant="caption" component="div" sx={{ fontSize: 9, fontWeight: 700, letterSpacing: 0.5, lineHeight: 1.2 }}>
              EN VIVO
            </Typography>
          )}
        </Box>
        <Box sx={{ flex: 1, minWidth: 0, display: 'flex', alignItems: 'center', gap: 0.75 }}>
          <EscudoEquipo url={m.awayClubLogoUrl} />
          <Typography
            variant="body2"
            noWrap
            title={m.awayTeamName}
            sx={{ minWidth: 0, fontWeight: esVisita || ganoVisita ? 700 : 500, color: esVisita ? 'primary.main' : undefined }}
          >
            {m.awayTeamName}
          </Typography>
        </Box>
      </Box>

      <Box
        sx={{
          display: 'flex', alignItems: 'center', gap: 1, flexShrink: 0,
          justifyContent: { xs: 'space-between', md: 'flex-end' },
          minWidth: { md: 200 }
        }}
      >
        {cancha && (
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.25, minWidth: 0 }}>
            <Typography variant="caption" color="text.secondary" noWrap title={cancha} sx={{ minWidth: 0 }}>
              {cancha}
            </Typography>
            {m.venueMapsUrl && (
              <IconButton
                size="small"
                title="Ver el mapa"
                onClick={(e) => { e.stopPropagation(); setMapaAbierto(true); }}
                sx={{ p: 0.25 }}
              >
                <Iconify icon="mdi:map-marker" width={14} />
              </IconButton>
            )}
          </Box>
        )}
        <Chip
          size="small"
          label={SL[m.status] || m.status}
          color={SC[m.status] || 'default'}
          variant={m.status === 'finished' ? 'filled' : 'outlined'}
          sx={{ flexShrink: 0 }}
        />
        {puedeVerCronologia && (
          // Un icono suelto se pasaba por alto — "mas intuitivo" fue el
          // pedido, asi que ahora dice lo que hace en vez de solo insinuarlo.
          <Button
            size="small"
            color="primary"
            onClick={function() { setExpandido(!expandido); }}
            startIcon={<Iconify icon={expandido ? 'eva:chevron-up-fill' : 'eva:chevron-down-fill'} width={16} />}
            sx={{ flexShrink: 0, minWidth: 0, px: 1 }}
          >
            {expandido ? 'Ocultar' : 'Cronologia'}
          </Button>
        )}
      </Box>
    </Box>
    {puedeVerCronologia && expandido && (
      <Cronologia orgSlug={props.orgSlug} compSlug={props.compSlug} matchId={m.id} homeTeamName={m.homeTeamName} awayTeamName={m.awayTeamName} />
    )}
    {m.venueMapsUrl && (
      <MapaSedeDialog open={mapaAbierto} onClose={function() { setMapaAbierto(false); }} titulo={cancha} mapsUrl={m.venueMapsUrl} />
    )}
    </Paper>
  );
}

/**
 * El mapa de una sede, incrustado en un dialogo en vez de abrirse en otra
 * pestaña -- eso era todo lo que habia antes de que se pidiera verlo "en el
 * portal" en si. El enlace que carga el organizador puede ser cualquier
 * forma en que Google Maps entrega un lugar (un "compartir", un lugar, un
 * enlace corto): la mayoria de esas paginas rechazan mostrarse dentro de un
 * iframe ajeno, asi que en vez de usarlo tal cual se arma la URL de consulta
 * que Google si permite incrustar (?q=...&output=embed). No hay forma de
 * detectar en JavaScript si ese intento fallo -un bloqueo por iframe no
 * dispara ningun evento- asi que el enlace para abrirlo en una pestaña
 * aparte queda siempre visible debajo, no solo como respaldo silencioso.
 */
function MapaSedeDialog(props) {
  return (
    <Dialog open={props.open} onClose={props.onClose} maxWidth="sm" fullWidth>
      <Box sx={{ p: 2 }}>
        <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', mb: 1.5 }}>
          <Typography variant="subtitle1" fontWeight={600} noWrap title={props.titulo} sx={{ minWidth: 0 }}>
            {props.titulo || 'Ubicación'}
          </Typography>
          <IconButton size="small" onClick={props.onClose} aria-label="Cerrar">
            <Iconify icon="eva:close-outline" width={20} />
          </IconButton>
        </Box>
        <Box
          component="iframe"
          src={embedSrc(props.mapsUrl)}
          title={props.titulo || 'Mapa'}
          loading="lazy"
          referrerPolicy="no-referrer-when-downgrade"
          sx={{ width: '100%', height: { xs: 260, sm: 340 }, border: 0, borderRadius: 1, display: 'block' }}
        />
        <Button
          component="a"
          href={props.mapsUrl}
          target="_blank"
          rel="noopener noreferrer"
          size="small"
          startIcon={<Iconify icon="mdi:map-marker" width={16} />}
          sx={{ mt: 1 }}
        >
          Abrir en Google Maps
        </Button>
      </Box>
    </Dialog>
  );
}

/** El minuto a minuto de un partido: goles y tarjetas, en el orden en que pasaron. */
function Cronologia(props) {
  var { data, isLoading } = useSWR(
    '/api/public/' + props.orgSlug + '/' + props.compSlug + '/matches/' + props.matchId + '/events',
    publicFetcher
  );

  if (isLoading) return <Box sx={{ display: 'flex', justifyContent: 'center', py: 1.5 }}><CircularProgress size={20} /></Box>;

  var eventos = (data && data.events) || [];

  if (eventos.length === 0) {
    return (
      <Typography variant="caption" color="text.secondary" sx={{ display: 'block', pt: 1, borderTop: '1px solid', borderColor: 'divider' }}>
        Todavía no se registraron eventos en este partido.
      </Typography>
    );
  }

  return (
    <Stack spacing={0.5} divider={<Divider flexItem />} sx={{ pt: 1, borderTop: '1px solid', borderColor: 'divider' }}>
      {eventos.map(function(ev, i) {
        var marca = marcaDeEvento(ev);
        var minuto = ev.minute != null ? ev.minute + "'" + (ev.periodNumber ? ' (P' + ev.periodNumber + ')' : '') : (ev.periodNumber ? 'P' + ev.periodNumber : '');
        // Un autogol lo mete un jugador del equipo contrario al que suma:
        // se ordena del lado de a quien le sirvio, no del plantel al que
        // pertenece, que es como se lee un resultado en cualquier resumen de
        // partido — el XOR da vuelta el lado solo en ese caso.
        var esLocal = ev.isHome !== ev.countsForOpponent;
        return (
          <Box
            key={i}
            sx={{
              display: 'flex', alignItems: 'center', gap: 1, py: 0.4,
              flexDirection: esLocal ? 'row' : 'row-reverse',
              textAlign: esLocal ? 'left' : 'right'
            }}
          >
            <Typography variant="caption" color="text.secondary" sx={{ minWidth: 44, flexShrink: 0, textAlign: 'center' }}>
              {minuto || '--'}
            </Typography>
            <Typography sx={{ flexShrink: 0, fontSize: 15, lineHeight: 1 }}>{marca}</Typography>
            <Typography variant="body2" noWrap sx={{ minWidth: 0 }}>
              {ev.firstName} {ev.lastName}{ev.jerseyNumber != null ? ' (#' + ev.jerseyNumber + ')' : ''}
              {ev.quantity > 1 ? ' x' + ev.quantity : ''}
            </Typography>
            {ev.countsForOpponent && (
              <Chip label="AG" size="small" color="error" variant="outlined" sx={{ height: 18, fontSize: 10, fontWeight: 700, flexShrink: 0 }} />
            )}
          </Box>
        );
      })}
    </Stack>
  );
}

/** Un símbolo reconocible para el evento: gol, tarjeta, o un punto genérico. */
function marcaDeEvento(ev) {
  if (ev.metricCode === 'yellow_card') return '🟨';
  if (ev.metricCode === 'red_card') return '🟥';
  if (ev.affectsScore) return '⚽';
  return '•';
}

/** El escudo del club, o un icono generico cuando el club no subio uno. */
function EscudoEquipo(props) {
  return (
    <Avatar src={props.url || undefined} variant="rounded" sx={{ width: 22, height: 22, flexShrink: 0, bgcolor: 'action.hover', '& img': { objectFit: 'contain' } }}>
      {!props.url && <Iconify icon="mdi:office-building-outline" width={13} sx={{ color: 'text.disabled' }} />}
    </Avatar>
  );
}

function Cargando() {
  return <Box sx={{ display: 'flex', justifyContent: 'center', py: 4 }}><CircularProgress /></Box>;
}

function fechaCorta(iso) {
  var d = new Date(iso);
  return d.toLocaleDateString('es-BO', { weekday: 'short', day: '2-digit', month: 'short' });
}

function hora(iso) {
  var d = new Date(iso);
  return d.toLocaleTimeString('es-BO', { hour: '2-digit', minute: '2-digit' });
}

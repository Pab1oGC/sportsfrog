import { useState, useEffect } from 'react';
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
import { DataGrid } from '@mui/x-data-grid';
import { alpha } from '@mui/material/styles';
import useSWR from 'swr';
import publicAxios from 'src/lib/public-axios';
import { Iconify } from 'src/components/iconify';
import { ColorModeToggle } from 'src/components/color-mode-toggle';
import { FASES } from 'src/lib/phase-labels';
import { PENDIENTE } from 'src/lib/match-status';
import { etiquetasDesempate, columnasMarcador } from 'src/lib/tiebreaker-labels';
import { getThemeForCompetition } from 'src/context/tournament-theme-context';

var publicFetcher = function(url) { return publicAxios.get(url).then(function(r) { return r.data; }); };
var SC = { scheduled: 'info', in_progress: 'warning', finished: 'success', cancelled: 'error', walkover: 'warning', postponed: 'default' };
var SL = { scheduled: 'Programado', in_progress: 'En curso', finished: 'Finalizado', cancelled: 'Cancelado', walkover: 'Walkover', postponed: 'Aplazado' };
var SL2 = { draft: 'Borrador', scheduled: 'Programada', in_progress: 'En curso', finished: 'Finalizada', cancelled: 'Cancelada' };

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

  var tabCount = 0;
  if (showStandings) tabCount++;
  if (showLeaders) tabCount++;
  tabCount++;

  var effectiveTab = tab;
  if (effectiveTab >= tabCount && tabCount > 0) effectiveTab = 0;

  var standingsTabIdx = showStandings ? 0 : -1;
  var leadersTabIdx = showLeaders ? (showStandings ? 1 : 0) : -1;
  var calendarTabIdx = (showStandings ? 1 : 0) + (showLeaders ? 1 : 0);

  var { data: standingsData, isLoading: loadingStandings } = useSWR(
    comp && effectiveTab === standingsTabIdx && showStandings ? '/api/public/' + orgSlug + '/' + compSlug + '/standings' : null, publicFetcher
  );

  var { data: leadersData, isLoading: loadingLeaders } = useSWR(
    comp && effectiveTab === leadersTabIdx && showLeaders ? '/api/public/' + orgSlug + '/' + compSlug + '/leaders?top=10' : null, publicFetcher
  );

  var { data: calendarData, isLoading: loadingCalendar } = useSWR(
    comp && effectiveTab === calendarTabIdx ? '/api/public/' + orgSlug + '/' + compSlug + '/matches' : null, publicFetcher
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
  var portal = comp.portal || {};
  var theme = getThemeForCompetition(comp);

  var activeBanner = theme.bannerUrl || portal.bannerUrl;
  var activePrimaryColor = theme.primaryColor || portal.accentColor || '#1B8A2E';
  var activeSponsors = (theme.sponsors && theme.sponsors.length > 0) ? theme.sponsors : (portal.sponsors || []);

  var social = [
    { key: 'instagram', href: portal.instagram, icon: 'mdi:instagram' },
    { key: 'facebook', href: portal.facebook, icon: 'mdi:facebook' },
    { key: 'whatsApp', href: portal.whatsApp, icon: 'mdi:whatsapp' },
    { key: 'website', href: portal.website, icon: 'mdi:web' }
  ].filter(function(s) { return s.href; });

  return (
    <Box sx={{ minHeight: '100vh', bgcolor: 'background.default' }}>
      <Navbar navigate={navigate} logoUrl={comp.organizationLogoUrl} />
      <Box
        sx={{
          position: 'relative',
          color: 'white',
          py: { xs: 3, sm: 4 },
          px: 3,
          bgcolor: activePrimaryColor,
          backgroundImage: activeBanner
            ? 'linear-gradient(180deg, rgba(0,0,0,0.45), rgba(0,0,0,0.7)), url("' + activeBanner + '")'
            : undefined,
          backgroundSize: 'cover',
          backgroundPosition: 'center'
        }}
      >
        <Container maxWidth="lg">
          <Button size="small" sx={{ color: 'white', mb: 1, opacity: 0.8 }} onClick={function() { navigate('/public'); }} startIcon={<Iconify icon="eva:arrow-back-outline" />}>Volver</Button>
          <Typography variant="h3" fontWeight={700} sx={{ fontSize: { xs: '1.5rem', sm: '2rem', md: '2.5rem' } }}>{comp.name}</Typography>
          <Typography variant="subtitle1" sx={{ opacity: 0.9, mt: 0.5 }}>{comp.organizationName} &middot; {comp.season} &middot; {comp.sportName}</Typography>
          {portal.description && (
            <Typography variant="body2" sx={{ opacity: 0.95, mt: 1, maxWidth: 640 }}>{portal.description}</Typography>
          )}
          <Box sx={{ display: 'flex', gap: 1, mt: 1.5, flexWrap: 'wrap', alignItems: 'center' }}>
            <Chip label={SL2[comp.status] || comp.status} size="small" sx={{ bgcolor: 'rgba(255,255,255,0.2)', color: 'white' }} />
            <Chip label={comp.format === 'league' ? 'Todos vs todos' : comp.format === 'knockout' ? 'Eliminacion' : 'Grupos'} size="small" sx={{ bgcolor: 'rgba(255,255,255,0.2)', color: 'white' }} />
            {comp.startsOn && <Chip label={comp.startsOn + ' - ' + (comp.endsOn || '?')} size="small" sx={{ bgcolor: 'rgba(255,255,255,0.2)', color: 'white' }} />}
            {social.map(function(s) {
              return (
                <IconButton key={s.key} size="small" component="a" href={s.href} target="_blank" rel="noopener noreferrer" sx={{ color: 'white', bgcolor: 'rgba(255,255,255,0.15)' }}>
                  <Iconify icon={s.icon} width={16} />
                </IconButton>
              );
            })}
          </Box>
        </Container>
      </Box>
      <Container maxWidth="lg" sx={{ py: 3 }}>
        {cats.length > 0 && (
          <Box sx={{ mb: 2, display: 'flex', gap: 1, flexWrap: 'wrap' }}>
            {cats.map(function(c) {
              return <Chip key={c.id} label={c.name + ' (' + c.teamCount + ')'} variant={selectedCatId === c.id ? 'filled' : 'outlined'} color="primary" onClick={function() { setSelectedCatId(c.id); }} />;
            })}
          </Box>
        )}
        <Tabs value={effectiveTab} onChange={function(e, v) { setTab(v); }} sx={{ mb: 3 }}>
          {showStandings && <Tab label="Tabla de posiciones" />}
          {showLeaders && <Tab label="Lideres" />}
          <Tab label="Calendario" />
        </Tabs>
        {effectiveTab === standingsTabIdx && showStandings && (
          <StandingsView data={standingsData} loading={loadingStandings} selectedCatId={selectedCatId} sportInfo={comp} />
        )}
        {effectiveTab === leadersTabIdx && showLeaders && (
          <LeadersView data={leadersData} loading={loadingLeaders} selectedCatId={selectedCatId} />
        )}
        {effectiveTab === calendarTabIdx && (
          <CalendarView data={calendarData} loading={loadingCalendar} selectedCatId={selectedCatId} orgSlug={orgSlug} compSlug={compSlug} mostrarEventos={showRosters} />
        )}

        {activeSponsors.length > 0 && (
          <Box sx={{ mt: 5, pt: 3, borderTop: '1px solid', borderColor: 'divider' }}>
            <Typography variant="overline" color="text.secondary" sx={{ display: 'block', mb: 1.5, textAlign: 'center' }}>
              Auspician
            </Typography>
            <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 3, alignItems: 'center', justifyContent: 'center' }}>
              {activeSponsors.map(function(s, i) {
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
        <ColorModeToggle sx={{ mr: 0.5 }} />
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

      {/* Con un equipo elegido las jornadas dejan de servir como filtro: juega
          una vez en cada una. Se muestra su fixture entero. */}
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
          <Typography variant="caption" color="text.secondary" noWrap title={cancha} sx={{ minWidth: 0 }}>
            {cancha}
          </Typography>
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
    </Paper>
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

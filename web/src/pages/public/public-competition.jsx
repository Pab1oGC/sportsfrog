import { useState } from 'react';
import { useParams, useNavigate } from 'react-router';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import Chip from '@mui/material/Chip';
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
import { DataGrid } from '@mui/x-data-grid';
import useSWR from 'swr';
import publicAxios from 'src/lib/public-axios';
import { Iconify } from 'src/components/iconify';

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

  var shows = (comp && comp.shows) || {};
  var showStandings = shows.standings !== false;
  var showLeaders = shows.leaders !== false;

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
    <Box sx={{ minHeight: '100vh', bgcolor: 'grey.50' }}>
      <Navbar navigate={navigate} />
      <Box sx={{ display: 'flex', justifyContent: 'center', mt: 10 }}><CircularProgress /></Box>
    </Box>
  );

  if (errorComp || !comp) return (
    <Box sx={{ minHeight: '100vh', bgcolor: 'grey.50' }}>
      <Navbar navigate={navigate} />
      <Container maxWidth="md" sx={{ py: 6 }}>
        <Alert severity="error">Competencia no encontrada o no publicada.</Alert>
        <Button variant="outlined" sx={{ mt: 2 }} onClick={function() { navigate('/public'); }} startIcon={<Iconify icon="eva:arrow-back-outline" />}>Volver</Button>
      </Container>
    </Box>
  );

  var cats = comp.categories || [];

  return (
    <Box sx={{ minHeight: '100vh', bgcolor: 'grey.50' }}>
      <Navbar navigate={navigate} />
      <Box sx={{ bgcolor: 'primary.main', color: 'white', py: { xs: 3, sm: 4 }, px: 3 }}>
        <Container maxWidth="lg">
          <Button size="small" sx={{ color: 'white', mb: 1, opacity: 0.8 }} onClick={function() { navigate('/public'); }} startIcon={<Iconify icon="eva:arrow-back-outline" />}>Volver</Button>
          <Typography variant="h3" fontWeight={700} sx={{ fontSize: { xs: '1.5rem', sm: '2rem', md: '2.5rem' } }}>{comp.name}</Typography>
          <Typography variant="subtitle1" sx={{ opacity: 0.9, mt: 0.5 }}>{comp.organizationName} &middot; {comp.season} &middot; {comp.sportName}</Typography>
          <Box sx={{ display: 'flex', gap: 1, mt: 1, flexWrap: 'wrap' }}>
            <Chip label={SL2[comp.status] || comp.status} size="small" sx={{ bgcolor: 'rgba(255,255,255,0.2)', color: 'white' }} />
            <Chip label={comp.format === 'league' ? 'Todos vs todos' : comp.format === 'knockout' ? 'Eliminacion' : 'Grupos'} size="small" sx={{ bgcolor: 'rgba(255,255,255,0.2)', color: 'white' }} />
            {comp.startsOn && <Chip label={comp.startsOn + ' - ' + (comp.endsOn || '?')} size="small" sx={{ bgcolor: 'rgba(255,255,255,0.2)', color: 'white' }} />}
          </Box>
        </Container>
      </Box>
      <Container maxWidth="lg" sx={{ py: 3 }}>
        {cats.length > 0 && (
          <Box sx={{ mb: 2, display: 'flex', gap: 1, flexWrap: 'wrap' }}>
            <Chip label="Todas" variant={selectedCatId ? 'outlined' : 'filled'} color="primary" onClick={function() { setSelectedCatId(''); }} />
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
          <StandingsView data={standingsData} loading={loadingStandings} selectedCatId={selectedCatId} />
        )}
        {effectiveTab === leadersTabIdx && showLeaders && (
          <LeadersView data={leadersData} loading={loadingLeaders} selectedCatId={selectedCatId} />
        )}
        {effectiveTab === calendarTabIdx && (
          <CalendarView data={calendarData} loading={loadingCalendar} selectedCatId={selectedCatId} />
        )}
      </Container>
    </Box>
  );
}

function Navbar(props) {
  return (
    <AppBar position="static" elevation={0} sx={{ bgcolor: 'white', color: 'text.primary', borderBottom: '1px solid', borderColor: 'divider' }}>
      <Toolbar>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, cursor: 'pointer' }} onClick={function() { props.navigate('/'); }}>
          <Box sx={{ width: 36, height: 36, borderRadius: 1.5, bgcolor: 'primary.main', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
            <Typography sx={{ color: 'white', fontWeight: 800, fontSize: 16 }}>SF</Typography>
          </Box>
          <Typography variant="h6" fontWeight={700}>SportFrog</Typography>
        </Box>
        <Box sx={{ flexGrow: 1 }} />
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

  if (loading) return <Box sx={{ display: 'flex', justifyContent: 'center', py: 4 }}><CircularProgress /></Box>;
  if (!data || !data.categories || data.categories.length === 0) return <Alert severity="info">Sin datos de posiciones.</Alert>;

  var catsToShow = data.categories;
  if (selectedCatId) catsToShow = catsToShow.filter(function(c) { return c.categoryId === selectedCatId; });

  var cols = [
    { field: 'position', headerName: '#', width: 50 },
    { field: 'teamName', headerName: 'Equipo', flex: 1, minWidth: 180 },
    { field: 'played', headerName: 'PJ', width: 50 },
    { field: 'won', headerName: 'PG', width: 50 },
    { field: 'drawn', headerName: 'PE', width: 50 },
    { field: 'lost', headerName: 'PP', width: 50 },
    { field: 'scoreFor', headerName: 'GF', width: 50 },
    { field: 'scoreAgainst', headerName: 'GC', width: 50 },
    { field: 'scoreDifference', headerName: 'DF', width: 50 },
    { field: 'points', headerName: 'Pts', width: 60 }
  ];

  return catsToShow.map(function(cat) {
    return (cat.groups || []).map(function(grp, gi) {
      var rows = (grp.rows || []).map(function(r, i) {
        return Object.assign({}, r, { id: r.teamId || i, position: r.position || (i + 1) });
      });
      return (
        <Box key={cat.categoryId + '-' + gi} sx={{ mb: 3 }}>
          <Typography variant="h6" fontWeight={600} sx={{ mb: 1 }}>{cat.categoryName}{grp.label ? ' - Grupo ' + grp.label : ''}</Typography>
          <DataGrid rows={rows} columns={cols} autoHeight hideFooter disableRowSelectionOnClick getRowId={function(r) { return r.id; }} />
          {cat.tiebreakers && cat.tiebreakers.length > 0 && (
            <Typography variant="caption" color="text.secondary" sx={{ mt: 0.5, display: 'block' }}>
              Criterios de desempate: {cat.tiebreakers.join(', ')}
            </Typography>
          )}
        </Box>
      );
    });
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
      Los tableros aparecen a medida que se cargan los goles y las tarjetas de cada partido.
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
            return <Tablero key={board.metricId} board={board} />;
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
                  bgcolor: MEDALLA[pos] || 'grey.200',
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

   Nueve columnas en una grilla, cuatro de ellas llenas de "--" porque el
   partido todavía no tiene sede ni horario, es exactamente lo que no se
   entiende de un vistazo. Un calendario se lee por jornada: qué se juega,
   contra quién y cómo terminó; la sede y la hora son detalle y solo aparecen
   cuando existen.
   ------------------------------------------------------------------------- */

var FASES = {
  round_of_32: 'Dieciseisavos',
  round_of_16: 'Octavos',
  quarterfinal: 'Cuartos de final',
  semifinal: 'Semifinales',
  final: 'Final',
  third_place: 'Tercer puesto'
};

function CalendarView(props) {
  var loading = props.loading;
  var selectedCatId = props.selectedCatId;

  if (loading) return <Cargando />;

  var fixtures = (props.data && props.data.fixtures) || [];
  if (selectedCatId) fixtures = fixtures.filter(function(m) { return m.categoryId === selectedCatId; });

  if (fixtures.length === 0) return (
    <Alert severity="info">
      Todavía no hay partidos en esta sección. Aparecen apenas se realiza el sorteo del fixture.
    </Alert>
  );

  // Por jornada y, dentro de ella, cronológico. Los partidos sin fecha van al
  // final de su jornada en lugar de al final de todo, que es donde el orden
  // que devuelve la API los deja y donde nadie los busca.
  var orden = fixtures.slice().sort(function(a, b) {
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
          : (m.roundNumber != null ? 'Jornada ' + m.roundNumber : 'Sin jornada asignada'),
        matches: []
      };
      grupos.push(porClave[clave]);
    }
    porClave[clave].matches.push(m);
  });

  var jugados = fixtures.filter(function(m) { return m.homeTotal != null && m.awayTotal != null; }).length;
  var variasCategorias = !selectedCatId
    && fixtures.some(function(m) { return m.categoryId !== fixtures[0].categoryId; });

  return (
    <Box>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
        {fixtures.length} {fixtures.length === 1 ? 'partido' : 'partidos'} &middot; {jugados} con resultado
      </Typography>

      {grupos.map(function(g) {
        return (
          <Box key={g.clave} sx={{ mb: 3 }}>
            <Typography
              variant="overline"
              color="text.secondary"
              sx={{ display: 'block', fontWeight: 700, letterSpacing: 1, mb: 1 }}
            >
              {g.titulo}
            </Typography>
            <Stack spacing={1}>
              {g.matches.map(function(m) {
                return <Partido key={m.id} m={m} mostrarCategoria={variasCategorias} />;
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
  var ganoLocal = jugado && m.homeTotal > m.awayTotal;
  var ganoVisita = jugado && m.awayTotal > m.homeTotal;

  var cancha = [m.venueName, m.spaceName].filter(Boolean).join(' · ');

  return (
    <Paper
      variant="outlined"
      sx={{
        p: { xs: 1.5, sm: 2 },
        borderRadius: 2,
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
        <Typography
          variant="body2"
          noWrap
          title={m.homeTeamName}
          sx={{ flex: 1, minWidth: 0, textAlign: 'right', fontWeight: ganoLocal ? 700 : 500 }}
        >
          {m.homeTeamName}
        </Typography>
        <Box
          sx={{
            flexShrink: 0, minWidth: 62, textAlign: 'center',
            px: 1.25, py: 0.5, borderRadius: 1, fontWeight: 700, fontSize: 15,
            bgcolor: jugado ? 'text.primary' : 'grey.100',
            color: jugado ? 'background.paper' : 'text.secondary'
          }}
        >
          {jugado ? m.homeTotal + ' - ' + m.awayTotal : 'vs'}
        </Box>
        <Typography
          variant="body2"
          noWrap
          title={m.awayTeamName}
          sx={{ flex: 1, minWidth: 0, fontWeight: ganoVisita ? 700 : 500 }}
        >
          {m.awayTeamName}
        </Typography>
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
      </Box>
    </Paper>
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

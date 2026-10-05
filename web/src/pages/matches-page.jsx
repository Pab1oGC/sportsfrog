import { useState } from 'react';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import Chip from '@mui/material/Chip';
import Alert from '@mui/material/Alert';
import TextField from '@mui/material/TextField';
import MenuItem from '@mui/material/MenuItem';
import IconButton from '@mui/material/IconButton';
import Dialog from '@mui/material/Dialog';
import DialogTitle from '@mui/material/DialogTitle';
import DialogContent from '@mui/material/DialogContent';
import DialogActions from '@mui/material/DialogActions';
import Divider from '@mui/material/Divider';
import { DataGrid } from '@mui/x-data-grid';
import { Iconify } from 'src/components/iconify';
import { useApi, apiPost, apiPut, apiDelete } from 'src/hooks/use-api';
import { useCascade } from 'src/hooks/use-cascade';
import { endpoints, default as axios } from 'src/lib/axios';
import { downloadBlob } from 'src/lib/download-blob';
import { nombreFase } from 'src/lib/phase-labels';
import { fechaHora } from 'src/lib/format-date';
import { compararPartidos } from 'src/pages/matches/match-order';
import { mapaDeGrupoPorEquipo } from 'src/pages/matches/team-groups';
import {
  quedaRondaPorSortear as calcularQuedaRondaPorSortear,
  rondasDisponibles as calcularRondasDisponibles,
  motivoDeSorteoInhabilitado,
} from 'src/pages/matches/bracket-status';
import { resolverReglamentoEfectivo } from 'src/pages/matches/effective-ruleset';
import { calcularUtcOffsetMinutes } from 'src/pages/matches/schedule-time';
import { PageHeader } from 'src/components/page-header';
import { CascadeFilters } from 'src/components/cascade-filters';
import { useConfirm } from 'src/components/confirm-dialog';
import { RowActionsMenu } from 'src/components/row-actions-menu';
import { toast } from 'sonner';
import { DateField, TimeField } from 'src/components/date-field';
import { DrawDialog } from 'src/pages/matches/draw-dialog';
import { PromoteDialog } from 'src/pages/matches/promote-dialog';
import { ScheduleDialog } from 'src/pages/matches/schedule-dialog';
import { EditDialog } from 'src/pages/matches/edit-dialog';
import { ResultDialog } from 'src/pages/matches/result-dialog';
import { WalkoverDialog } from 'src/pages/matches/walkover-dialog';
import { PenaltiesDialog } from 'src/pages/matches/penalties-dialog';
import { EventsDialog } from 'src/pages/matches/events-dialog';
import { BulkRescheduleDialog } from 'src/pages/matches/bulk-reschedule-dialog';
import { SocialFlyerDialog } from 'src/pages/matches/social-flyer-dialog';
import { buildLiveActions } from 'src/pages/matches/live-actions';
import { buildFixtureActions } from 'src/pages/matches/fixture-actions';

const SC = { scheduled: 'info', in_progress: 'warning', finished: 'success', cancelled: 'error', walkover: 'warning', postponed: 'default' };
const SL = { scheduled: 'Programado', in_progress: 'En curso', finished: 'Finalizado', cancelled: 'Cancelado', walkover: 'Walkover', postponed: 'Aplazado' };

/**
 * Coordina la lista de partidos de una categoria y sus nueve diálogos —
 * programar, reprogramar, reprogramar en bloque, resultado, walkover,
 * penales, eventos, sorteo y promoción a eliminatoria. Cada diálogo vive en
 * su propio archivo bajo
 * `matches/`, con su propio formulario; lo que sigue viviendo acá es lo que
 * de verdad es de la página entera:
 *
 *   - las acciones de la columna "actions" de la grilla se arman con
 *     `buildLiveActions`/`buildFixtureActions` (matches/live-actions.js,
 *     matches/fixture-actions.js) en vez de un arreglo inline: partido en
 *     vivo y fixture son dos motivos de cambio distintos, y cada builder
 *     solo sabe del suyo.
 *
 *   - `selMatch`: que fila esta operando la fila seleccionada, porque cinco
 *     diálogos distintos (reprogramar, resultado, walkover, penales,
 *     eventos) necesitan saber sobre que partido — no tiene un dueño natural
 *     mas chico que la página.
 *   - `error`/`loading`: compartidos porque el aviso de error de arriba se
 *     esconde mientras cualquier diálogo está abierto (ver el filtro larguísimo
 *     mas abajo), y ese es un comportamiento de la página, no de un diálogo.
 *   - `drawOpen`/`drawResult`: porque "Siguiente ronda" (una acción sin
 *     diálogo propio) muestra su resultado reusando el mismo diálogo que
 *     "Sortear" — ver DrawDialog.
 */
export default function MatchesPage() {
  const confirm = useConfirm();
  const cascade = useCascade();
  const { data: competiciones } = useApi(endpoints.competitions);
  const { data: matches, mutate, isLoading } = useApi(cascade.compId ? endpoints.competitionMatches(cascade.compId) : null);
  const { data: teams, mutate: mutateTeams } = useApi(cascade.catId ? endpoints.teams(cascade.catId) : null);

  // El orden de las categorias solo importa aca: decide que bloque arma
  // primero "Generar siguiente jornada" cuando la competencia tiene mas de
  // una (ver ScheduleCalendar.FindNextJornadaAsync del lado del servidor).
  // No es un dato de la categoria en si — por eso se reordena desde este
  // dialogo, no desde la pantalla de Categorias.
  const { data: jornadaCategorias, mutate: mutateJornadaCategorias } = useApi(
    cascade.compId ? endpoints.categories(cascade.compId) : null,
  );

  const [selMatch, setSelMatch] = useState(null);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  const [schedOpen, setSchedOpen] = useState(false);
  const [editOpen, setEditOpen] = useState(false);
  const [resOpen, setResOpen] = useState(false);
  const [woOpen, setWoOpen] = useState(false);
  const [poOpen, setPoOpen] = useState(false);
  const [evOpen, setEvOpen] = useState(false);
  const [drawOpen, setDrawOpen] = useState(false);
  const [drawResult, setDrawResult] = useState(null);
  const [promoteOpen, setPromoteOpen] = useState(false);
  const [bulkOpen, setBulkOpen] = useState(false);
  const [flyersOpen, setFlyersOpen] = useState(false);
  const [jornadaRound, setJornadaRound] = useState('');

  // Una jornada por click -- una ronda de una categoria -- nunca la
  // competencia entera de un tiron (ver ScheduleCalendar del lado del
  // servidor). La fecha queda vacia por defecto para que "seguir
  // clickeando" alcance; la hora de inicio no tiene default -- a que hora
  // se puede usar la cancha es algo que el organizador ya acordo con quien
  // la administra, no algo que el sistema deba suponer.
  const [jornadaOpen, setJornadaOpen] = useState(false);
  const [jornadaForm, setJornadaForm] = useState({ from: '', startTime: '' });
  const [jornadaSaving, setJornadaSaving] = useState(false);
  const [jornadaError, setJornadaError] = useState('');

  // El grupo no es un dato del partido, es un dato del equipo -- ver
  // matches/team-groups.js para el porqué de armarlo por equipo local.
  const grupoPorEquipo = mapaDeGrupoPorEquipo(teams);

  // El orden en sí (cuatro niveles: pendiente vs decidido, jornada/fase,
  // grupo) vive aparte en matches/match-order.js -- es puro y es la lógica
  // con más ramas de toda esta página, ver el comentario ahí.
  const filtered = (matches || [])
    .filter((m) => !cascade.catId || m.categoryId === cascade.catId)
    .map((m) => ({ ...m, groupLabel: grupoPorEquipo[m.homeTeamId] || null }))
    .sort(compararPartidos);
  const comp = competiciones?.find((c) => c.id === cascade.compId);
  const formato = comp?.format;
  const catActual = (jornadaCategorias || []).find((c) => c.id === cascade.catId);
  // Que eventos aparecen para elegir depende del deporte, no de la
  // competencia: un gol o una tarjeta amarilla son del futbol, sea cual sea
  // el torneo que se esta jugando.
  const { data: sportInfo } = useApi(comp?.sportCode ? endpoints.sport(comp.sportCode) : null);

  // El reglamento y la forma de deporte efectivos para selMatch -- ver
  // matches/effective-ruleset.js para la resolución categoría-o-competencia.
  const { data: reglamentos } = useApi(endpoints.rulesets);
  const { sportInfoDelPartido } = resolverReglamentoEfectivo({ jornadaCategorias, selMatch, reglamentos, comp, sportInfo });
  const yaJugados = filtered.filter((m) => m.status === 'finished' || m.status === 'walkover').length;
  const motivoSinSorteo = motivoDeSorteoInhabilitado(comp, yaJugados);

  const doAdvance = async () => {
    if (!cascade.catId) return;
    const ok = await confirm('Sortear siguiente ronda?', { confirmLabel: 'Sortear' });
    if (!ok) return;
    setLoading(true); setError('');
    try { const r = await apiPost(endpoints.categoryAdvanceBracket(cascade.catId), {}); mutate(); setDrawResult(r); setDrawOpen(true); }
    catch (err) { setError(err.message); } finally { setLoading(false); }
  };

  // Kyorugi: en vez de un partido por el tercer puesto, quien perdio contra
  // alguno de los dos finalistas tiene derecho a repechaje -- ver
  // DrawRepechage del lado del servidor. Solo tiene sentido una vez que la
  // final ya tiene los dos nombres puestos, lo que ese mismo endpoint valida
  // -- este boton no repite esa condicion, solo la de que la categoria haya
  // activado la opcion al configurarla.
  const doRepechage = async () => {
    if (!cascade.catId) return;
    const ok = await confirm('Sortear repechaje?', { confirmLabel: 'Sortear' });
    if (!ok) return;
    setLoading(true); setError('');
    try {
      const r = await apiPost(endpoints.categoryDrawRepechage(cascade.catId), {});
      mutate();
      const nombreDe = (id) => (teams || []).find((t) => t.id === id)?.name || id;
      const bronces = (r.automaticBronzeTeamIds || []).map(nombreDe).join(', ');
      toast.success(
        `${r.created} partido(s) de repechaje sorteados.`
        + (bronces ? ` Bronce sin rival para: ${bronces}.` : ''),
      );
    } catch (err) { setError(err.message); }
    finally { setLoading(false); }
  };

  const abrirGenerarJornada = () => {
    setJornadaOpen(true);
    setJornadaForm({ from: '', startTime: '' });
    setJornadaError('');
  };

  const moverCategoriaOrden = async (categoryId, delta) => {
    const rows = jornadaCategorias || [];
    const index = rows.findIndex((row) => row.id === categoryId);
    const otherIndex = index + delta;
    if (index < 0 || otherIndex < 0 || otherIndex >= rows.length) return;

    const a = rows[index];
    const b = rows[otherIndex];
    const payloadFor = (row, displayOrder) => ({
      name: row.name,
      gender: row.gender || null,
      birthDateFrom: row.birthDateFrom || null,
      birthDateTo: row.birthDateTo || null,
      maxRosterSize: row.maxRosterSize ?? null,
      displayOrder,
      rulesetId: row.rulesetId || null,
      qualifiersPerGroup: row.qualifiersPerGroup ?? null,
      minWeightKg: row.minWeightKg ?? null,
      maxWeightKg: row.maxWeightKg ?? null,
    });

    try {
      await apiPut(endpoints.category(cascade.compId, a.id), payloadFor(a, b.displayOrder));
      await apiPut(endpoints.category(cascade.compId, b.id), payloadFor(b, a.displayOrder));
      mutateJornadaCategorias();
    } catch (err) { toast.error(err.message); }
  };

  const generarJornada = async () => {
    setJornadaSaving(true); setJornadaError('');
    try {
      // El día elegido (hoy si no se eligió ninguno) -- ver
      // matches/schedule-time.js para el porqué del cálculo de zona horaria.
      const dia = jornadaForm.from || new Date().toLocaleDateString('sv-SE');
      const r = await apiPost(endpoints.competitionSchedule(cascade.compId), {
        from: jornadaForm.from || null,
        startTime: jornadaForm.startTime || null,
        utcOffsetMinutes: calcularUtcOffsetMinutes(dia, jornadaForm.startTime),
      });
      mutate();
      setJornadaOpen(false);
      if (!r.categoryName) {
        toast.success('No queda ninguna jornada pendiente por programar.');
      } else {
        const sinLugar = r.unplaced ? `, ${r.unplaced} sin lugar` : '';
        toast.success(`Jornada ${r.roundNumber} de ${r.categoryName}: ${r.placed} partido(s) colocado(s)${sinLugar}.`);
      }
    } catch (err) { setJornadaError(err.message); }
    finally { setJornadaSaving(false); }
  };

  const descargarFixture = async () => {
    if (!cascade.compId) return;
    const url = cascade.catId ? endpoints.categoryFixturePdf(cascade.catId) : endpoints.competitionFixturePdf(cascade.compId);
    try {
      const res = await axios.get(url, { responseType: 'blob' });
      downloadBlob(res, 'fixture.pdf');
    } catch (err) {
      // El servidor rechaza el fixture completo si todavia hay partidos sin
      // fecha (ver ReadMatches.HandleCompetitionPdfAsync/HandleCategoryPdfAsync) —
      // su mensaje ya explica que conviene generar una jornada puntual en
      // cambio, asi que alcanza con mostrarlo tal cual.
      toast.error(err.message);
    }
  };

  // Ver matches/bracket-status.js para el porqué de cada uno.
  const quedaRondaPorSortear = calcularQuedaRondaPorSortear(matches, cascade.catId);
  const rondasDisponibles = calcularRondasDisponibles(matches, cascade.catId);

  const descargarJornada = async () => {
    if (!cascade.catId || jornadaRound === '') return;
    const url = `${endpoints.categoryFixturePdf(cascade.catId)}?round=${jornadaRound}`;
    try {
      const res = await axios.get(url, { responseType: 'blob' });
      downloadBlob(res, `fixture-jornada-${jornadaRound}.pdf`);
    } catch (err) { toast.error(err.message); }
  };

  const openEdit = (m) => { setSelMatch(m); setError(''); setEditOpen(true); };

  // Solo tiene sentido donde el deporte tiene algun evento que suma al
  // marcador (goles en futbol/basquet, punto/gam-jeom en taekwondo
  // kyorugi): sin eso no hay nada que sumar, y ahi sigue haciendo falta
  // cargar el resultado a mano — ver tieneEventoDeMarcador() en
  // live-actions.js, que es lo que condiciona el boton mas abajo.
  const doFinishFromEvents = async (m) => {
    const marcador = `${m.liveHomeTotal ?? 0} - ${m.liveAwayTotal ?? 0}`;
    const ok = await confirm(`Finalizar el partido ${marcador}, segun los eventos cargados?`, { confirmLabel: 'Finalizar' });
    if (!ok) return;
    try { await apiPost(endpoints.matchResultFromEvents(m.id)); mutate(); toast.success('Partido finalizado.'); }
    catch (err) { toast.error(err.message); }
  };

  const doStatus = async (id, st) => {
    const ok = await confirm(`Cambiar estado a "${SL[st] || st}"?`, { confirmLabel: 'Cambiar' });
    if (!ok) return;
    try { await apiPut(endpoints.matchStatus(id), { status: st }); mutate(); } catch (err) { toast.error(err.message); }
  };

  const delMatch = async (id) => {
    const ok = await confirm('Eliminar partido?', { confirmLabel: 'Eliminar', danger: true });
    if (!ok) return;
    // El servidor rechaza (409) borrar un partido con resultado y explica por
    // que: sin capturarlo, ese mensaje no llegaba a ningun lado y el click
    // parecia no hacer nada.
    try { await apiDelete(endpoints.match(id)); mutate(); } catch (err) { toast.error(err.message); }
  };

  const columns = [
    { field: 'scheduledAt', headerName: 'Fecha', width: 190, renderCell: ({ value }) => value ? fechaHora(value) : 'Sin fecha' },

    // Jornada solo dice algo en una liga (cada ronda es una fecha del
    // todos-contra-todos) y en la fase de grupos de un formato mixto. En una
    // eliminatoria pura no aporta nada por si sola — el numero de ronda ahi
    // vuelve a empezar en cada cruce y lo que identifica al partido es la
    // fase (Cuartos, Semifinal...), no un numero.
    // En un formato de grupos, un partido de la eliminatoria tambien trae
    // numero de ronda — pero es el conteo interno de la llave (que vuelve a
    // empezar en 1), no una jornada, y mostrarlo ahi confunde. Fase ya dice
    // cual es esa ronda con su nombre real (Cuartos, Semifinal...); Jornada
    // solo aplica mientras el partido todavia es de la fase de grupos.
    formato !== 'knockout' && { field: 'roundNumber', headerName: 'Jornada', width: 120, renderCell: ({ row }) => row.phase ? '--' : (row.roundNumber || '--') },
    { field: 'groupLabel', headerName: 'Grupo', width: 80, renderCell: ({ value }) => value ? <Chip label={value} size="small" /> : '--' },

    // Y a la inversa: Fase solo existe una vez que hay una eliminatoria de
    // por medio (pura, o la segunda mitad de una de grupos) — en una liga
    // queda vacia en cada fila, columna que nunca dice nada.
    formato !== 'league' && { field: 'phase', headerName: 'Fase', width: 130, renderCell: ({ value }) => value ? nombreFase(value) : '--' },

    // Un cuadro de eliminacion directa sorteado completo de una reserva
    // fecha y cancha para una ronda futura sin saber todavia quien la juega
    // -- homeTeamName llega null en ese caso, y homePlaceholder trae
    // "Ganador de <fase>" para no dejar la celda en blanco.
    { field: 'homeTeamName', headerName: 'Local', flex: 1, minWidth: 120, renderCell: ({ row }) => row.homeTeamName || (
      // El div propio con su altura completa fuerza el centrado vertical de
      // la celda: el line-height del tema para "body2" no coincide con el
      // que usa la grilla para un string plano, y sin esto el placeholder
      // queda unos pixeles arriba o abajo del centro en vez de a la misma
      // altura que el resto de la fila.
      <Box sx={{ display: 'flex', alignItems: 'center', height: '100%' }}>
        <Typography variant="body2" color="text.secondary" fontStyle="italic">{row.homePlaceholder || 'Por definir'}</Typography>
      </Box>
    ) },
    // El marcador oficial (homeTotal/awayTotal) queda null hasta que se
    // carga el Resultado, aunque ya haya goles cargados como eventos — son
    // dos pasos separados a proposito. Mientras el partido esta en curso, se
    // muestra el marcador en vivo (liveHomeTotal/liveAwayTotal, sumado de
    // esos eventos) para no dejar la grilla en blanco mientras se juega.
    { field: 'homeTotal', headerName: '', width: 30, renderCell: ({ row }) => row.homeTotal != null ? row.homeTotal : row.liveHomeTotal != null ? <span title="Marcador en vivo, a partir de los eventos cargados" style={{ color: 'var(--mui-palette-warning-main)', fontWeight: 600 }}>{row.liveHomeTotal}</span> : '' },
    { field: 'awayTotal', headerName: '', width: 30, renderCell: ({ row }) => row.awayTotal != null ? row.awayTotal : row.liveAwayTotal != null ? <span title="Marcador en vivo, a partir de los eventos cargados" style={{ color: 'var(--mui-palette-warning-main)', fontWeight: 600 }}>{row.liveAwayTotal}</span> : '' },
    { field: 'awayTeamName', headerName: 'Visitante', flex: 1, minWidth: 120, renderCell: ({ row }) => row.awayTeamName || (
      <Box sx={{ display: 'flex', alignItems: 'center', height: '100%' }}>
        <Typography variant="body2" color="text.secondary" fontStyle="italic">{row.awayPlaceholder || 'Por definir'}</Typography>
      </Box>
    ) },
    { field: 'penalties', headerName: '', width: 90, sortable: false, renderCell: ({ row }) =>
      row.penaltyHomeScore != null
        ? <Typography variant="caption" color="text.secondary">({row.penaltyHomeScore}-{row.penaltyAwayScore} pen)</Typography>
        : '' },
    { field: 'venueName', headerName: 'Sede', width: 160, renderCell: ({ row }) => row.venueName ? `${row.venueName}${row.spaceName ? ' — ' + row.spaceName : ''}` : '--' },
    { field: 'status', headerName: 'Estado', width: 110, renderCell: ({ value }) => <Chip label={SL[value] || value} color={SC[value] || 'default'} size="small" /> },
    { field: 'actions', headerName: 'Acciones', width: 150, align: 'center', headerAlign: 'center', renderCell: ({ row: m }) => {
      // Partido en vivo (arrancar, cargar eventos, cerrarlo) y fixture
      // (walkover, reabrir) son dos responsabilidades distintas sobre la
      // misma fila -- cada una en su propio archivo bajo matches/ (ver ahi
      // el porque), compuestas aca con un separador entre las dos para que
      // el corte tambien se note en el menu, no solo en el codigo.
      const live = buildLiveActions(m, {
        sportInfo, setSelMatch, setError, setResOpen, setPoOpen, doStatus, doFinishFromEvents,
      }).filter(Boolean);
      const fixture = buildFixtureActions(m, { setSelMatch, setError, setWoOpen, doStatus }).filter(Boolean);

      return (
        <RowActionsMenu
          primary={[
            { icon: 'eva:calendar-outline', label: 'Reprogramar', color: 'text.secondary', onClick: () => openEdit(m) },
            // Suelto, no en el "..." con el resto de live-actions: esta se
            // abre una vez por gol/tarjeta/punto mientras el partido esta en
            // curso (o se revisa terminado), no una vez por partido -- ese
            // uso repetido es lo que justifica el click de menos.
            (m.status === 'finished' || m.status === 'in_progress') && {
              icon: 'eva:film-outline', label: 'Eventos', color: 'info.main',
              onClick: () => { setSelMatch(m); setError(''); setEvOpen(true); },
            },
            { icon: 'eva:trash-2-outline', label: 'Eliminar', color: 'error.main', onClick: () => delMatch(m.id) },
          ]}
          actions={[
            ...live,
            live.length > 0 && fixture.length > 0 && { divider: true },
            ...fixture,
          ].filter(Boolean)}
        />
      );
    }},
  ].filter(Boolean);

  return (
    <Box>
      <PageHeader title="Fixtures / Partidos">
        {cascade.catId && <Button variant="outlined" startIcon={<Iconify icon="eva:shuffle-2-fill" />} onClick={() => { setDrawResult(null); setError(''); setDrawOpen(true); }} disabled={loading}>Sortear</Button>}
        {cascade.compId && comp && !['finished', 'cancelled'].includes(comp.status) && (
          <Button variant="outlined" startIcon={<Iconify icon="eva:clock-outline" />} onClick={abrirGenerarJornada} disabled={loading}>
            Generar siguiente jornada
          </Button>
        )}
        {cascade.catId && formato === 'groups' && (
          <Button variant="outlined" startIcon={<Iconify icon="eva:trending-up-outline" />} onClick={() => { setError(''); setPromoteOpen(true); }} disabled={loading}>
            Promover a eliminatoria
          </Button>
        )}
        {/* Ni un knockout puro ni una eliminatoria promovida desde grupos lo
            necesitan: los dos se sortean completos de una vez, hasta la final
            (ver Bracket.FullDraw), y cada ganador pasa solo a su lugar al
            cargar el resultado. Solo queda para una promocion hecha antes de
            eso, que dibujo unicamente la ronda 1 y todavia no llego a tener
            final -- ahi AdvanceBracket sigue siendo el unico camino. */}
        {cascade.catId && formato === 'groups' && quedaRondaPorSortear && <Button variant="outlined" startIcon={<Iconify icon="eva:arrow-forward-outline" />} onClick={doAdvance} disabled={loading}>Siguiente ronda</Button>}
        {cascade.catId && catActual?.usesRepechage && (
          <Button variant="outlined" startIcon={<Iconify icon="mdi:trophy-outline" />} onClick={doRepechage} disabled={loading}>
            Sortear repechaje
          </Button>
        )}
        {cascade.catId && <Button variant="outlined" startIcon={<Iconify icon="eva:shuffle-2-outline" />} onClick={() => { setError(''); setBulkOpen(true); }} disabled={loading}>Reprogramar en bloque</Button>}
        {cascade.catId && rondasDisponibles.length > 0 && (
          <TextField
            select size="small" label="Jornada" value={jornadaRound}
            onChange={(e) => setJornadaRound(e.target.value)}
            sx={{ minWidth: 110 }}
          >
            {rondasDisponibles.map((r) => <MenuItem key={r} value={r}>{r}</MenuItem>)}
          </TextField>
        )}
        {cascade.catId && rondasDisponibles.length > 0 && (
          <Button variant="outlined" startIcon={<Iconify icon="mdi:file-pdf-box" />} onClick={descargarJornada} disabled={jornadaRound === ''}>
            Descargar jornada
          </Button>
        )}
        {cascade.compId && <Button variant="outlined" startIcon={<Iconify icon="mdi:file-pdf-box" />} onClick={descargarFixture}>Descargar PDF completo</Button>}
        {cascade.compId && <Button variant="outlined" color="secondary" startIcon={<Iconify icon="mdi:instagram" />} onClick={() => setFlyersOpen(true)}>Crear flyers</Button>}
        <Button variant="contained" startIcon={<Iconify icon="eva:plus-fill" />} onClick={() => { setError(''); setSchedOpen(true); }} disabled={!cascade.catId}>Programar</Button>
      </PageHeader>
      {error && !evOpen && !resOpen && !woOpen && !poOpen && !drawOpen && !promoteOpen && !editOpen && !bulkOpen && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
      <CascadeFilters cascade={cascade} />
      <DataGrid rows={filtered} columns={columns} loading={isLoading} autoHeight rowHeight={56} disableRowSelectionOnClick getRowId={(r) => r.id} />

      <SocialFlyerDialog
        open={flyersOpen}
        onClose={() => setFlyersOpen(false)}
        matches={filtered}
        competition={comp}
      />

      <DrawDialog
        open={drawOpen} onClose={() => setDrawOpen(false)} result={drawResult} setResult={setDrawResult}
        cascade={cascade} formato={formato} teams={teams} mutate={mutate} mutateTeams={mutateTeams}
        motivoSinSorteo={motivoSinSorteo} loading={loading} setLoading={setLoading} error={error} setError={setError}
        onGenerarJornada={() => { setDrawOpen(false); abrirGenerarJornada(); }}
      />

      <PromoteDialog
        open={promoteOpen} onClose={() => setPromoteOpen(false)} cascade={cascade} mutate={mutate}
        loading={loading} setLoading={setLoading} error={error} setError={setError}
      />

      <ScheduleDialog
        open={schedOpen} onClose={() => setSchedOpen(false)} cascade={cascade} mutate={mutate}
        loading={loading} setLoading={setLoading} setError={setError}
      />

      <EditDialog
        open={editOpen} onClose={() => setEditOpen(false)} selMatch={selMatch} mutate={mutate}
        loading={loading} setLoading={setLoading} error={error} setError={setError}
      />

      <BulkRescheduleDialog
        open={bulkOpen} onClose={() => setBulkOpen(false)} matches={filtered} mutate={mutate}
        loading={loading} setLoading={setLoading} error={error} setError={setError}
      />

      <ResultDialog
        open={resOpen} onClose={() => setResOpen(false)} selMatch={selMatch} sportInfo={sportInfoDelPartido} mutate={mutate}
        loading={loading} setLoading={setLoading} error={error} setError={setError}
      />

      <WalkoverDialog
        open={woOpen} onClose={() => setWoOpen(false)} selMatch={selMatch} mutate={mutate}
        loading={loading} setLoading={setLoading} setError={setError}
      />

      <PenaltiesDialog
        open={poOpen} onClose={() => setPoOpen(false)} selMatch={selMatch} mutate={mutate}
        loading={loading} setLoading={setLoading} error={error} setError={setError}
      />

      <EventsDialog
        open={evOpen} onClose={() => setEvOpen(false)} selMatch={selMatch} sportInfo={sportInfoDelPartido} mutate={mutate}
        loading={loading} setLoading={setLoading} error={error} setError={setError}
      />

      <Dialog open={jornadaOpen} onClose={() => setJornadaOpen(false)} maxWidth="xs" fullWidth>
        <DialogTitle>Generar siguiente jornada</DialogTitle>
        <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: '16px !important' }}>
          {jornadaError && <Alert severity="error">{jornadaError}</Alert>}
          <Typography variant="body2" color="text.secondary">
            Coloca una sola jornada (una ronda de una categoría) por vez, siempre la próxima pendiente.
            Dejá la fecha vacía para que siga automáticamente desde donde quedó la última — la hora de
            inicio hay que indicarla siempre.
          </Typography>
          {jornadaCategorias && jornadaCategorias.length > 1 && (
            <Box>
              <Typography variant="subtitle2" sx={{ mb: 0.5 }}>Orden de las categorías</Typography>
              <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 1 }}>
                No se arma una jornada de la siguiente categoría hasta agotar todas las de esta.
              </Typography>
              {jornadaCategorias.map((cat, i) => (
                <Box key={cat.id} sx={{ display: 'flex', alignItems: 'center', gap: 1, py: 0.5 }}>
                  <Typography variant="body2" sx={{ flexGrow: 1 }}>{i + 1}. {cat.name}</Typography>
                  <IconButton size="small" disabled={i === 0} onClick={() => moverCategoriaOrden(cat.id, -1)}><Iconify icon="eva:chevron-up-fill" /></IconButton>
                  <IconButton size="small" disabled={i === jornadaCategorias.length - 1} onClick={() => moverCategoriaOrden(cat.id, 1)}><Iconify icon="eva:chevron-down-fill" /></IconButton>
                </Box>
              ))}
              <Divider sx={{ mt: 1 }} />
            </Box>
          )}
          <DateField
            label="Fecha (opcional)"
            value={jornadaForm.from}
            onChange={(e) => setJornadaForm((f) => ({ ...f, from: e.target.value }))}
            fullWidth
          />
          <TimeField
            label="Hora de inicio"
            value={jornadaForm.startTime}
            onChange={(e) => setJornadaForm((f) => ({ ...f, startTime: e.target.value }))}
            fullWidth
            required
            helperText="Ancla el primer partido de esta jornada — si hace falta más de un día, los siguientes reutilizan esta misma hora. No hay tope de hasta qué hora se puede jugar."
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setJornadaOpen(false)}>Cancelar</Button>
          <Button variant="contained" onClick={generarJornada} disabled={jornadaSaving || !jornadaForm.startTime}>Generar</Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}

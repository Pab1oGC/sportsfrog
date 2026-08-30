import { useState, useRef } from 'react';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import Chip from '@mui/material/Chip';
import Dialog from '@mui/material/Dialog';
import DialogTitle from '@mui/material/DialogTitle';
import DialogContent from '@mui/material/DialogContent';
import DialogActions from '@mui/material/DialogActions';
import TextField from '@mui/material/TextField';
import Autocomplete from '@mui/material/Autocomplete';
import MenuItem from '@mui/material/MenuItem';
import Checkbox from '@mui/material/Checkbox';
import FormControlLabel from '@mui/material/FormControlLabel';
import IconButton from '@mui/material/IconButton';
import Tooltip from '@mui/material/Tooltip';
import Alert from '@mui/material/Alert';
import Divider from '@mui/material/Divider';
import { DataGrid } from '@mui/x-data-grid';
import { Iconify } from 'src/components/iconify';
import { useApi, apiPost, apiPut, apiDelete } from 'src/hooks/use-api';
import { useCascade } from 'src/hooks/use-cascade';
import { endpoints } from 'src/lib/axios';
import { PageHeader } from 'src/components/page-header';
import { SelectionCompetition, SelectionCategory, SelectionTeam, SelectionSpace } from 'src/components/selectors';
import { useConfirm } from 'src/components/confirm-dialog';
import { toast } from 'sonner';

const SC = { scheduled: 'info', in_progress: 'warning', finished: 'success', cancelled: 'error', walkover: 'warning', postponed: 'default' };
const SL = { scheduled: 'Programado', in_progress: 'En curso', finished: 'Finalizado', cancelled: 'Cancelado', walkover: 'Walkover', postponed: 'Aplazado' };
const FORMATO_LABEL = { league: 'Todos vs todos', knockout: 'Eliminacion directa', groups: 'Fase de grupos' };

export default function MatchesPage() {
  const confirm = useConfirm();
  const cascade = useCascade();
  const { data: competiciones } = useApi(endpoints.competitions);
  const { data: matches, mutate, isLoading } = useApi(cascade.compId ? endpoints.competitionMatches(cascade.compId) : null);
  const { data: teams, mutate: mutateTeams } = useApi(cascade.catId ? endpoints.teams(cascade.catId) : null);

  const [selMatch, setSelMatch] = useState(null);
  const [schedOpen, setSchedOpen] = useState(false);
  const [resOpen, setResOpen] = useState(false);
  const [woOpen, setWoOpen] = useState(false);
  const [poOpen, setPoOpen] = useState(false);
  const [evOpen, setEvOpen] = useState(false);
  const [schedForm, setSchedForm] = useState({ homeTeamId: '', awayTeamId: '', venueSpaceId: '', scheduledAt: '', roundNumber: '', notes: '' });
  const [editOpen, setEditOpen] = useState(false);
  const [editForm, setEditForm] = useState({ venueSpaceId: '', scheduledAt: '', roundNumber: '', notes: '' });
  const [resForm, setResForm] = useState({ periodScores: [{ period: 1, home: 0, away: 0 }, { period: 2, home: 0, away: 0 }], notes: '' });
  const [woForm, setWoForm] = useState({ winnerTeamId: '', notes: '' });
  const [poForm, setPoForm] = useState({ homeScore: 0, awayScore: 0 });
  const [evForm, setEvForm] = useState({ rosterEntryId: '', metricId: '', periodNumber: '', minute: '', quantity: 1 });
  const playerFieldRef = useRef(null);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const [drawOpen, setDrawOpen] = useState(false);
  const [drawLegs, setDrawLegs] = useState(1);
  const [drawGroupCount, setDrawGroupCount] = useState('');
  const [drawAllowSamePot, setDrawAllowSamePot] = useState(false);
  const [drawResult, setDrawResult] = useState(null);
  const [promoteOpen, setPromoteOpen] = useState(false);
  const [promoteForm, setPromoteForm] = useState({ qualifiersPerGroup: 2, bestThirdPlaced: 0 });
  const [promoteResult, setPromoteResult] = useState(null);

  // El grupo no es un dato del partido, es un dato del equipo — asi que se
  // arma aca, por equipo local, para que la grilla no mezcle sin avisar los
  // partidos de grupos distintos bajo la misma "ronda" (cada grupo tiene su
  // propia ronda 1, ronda 2...).
  const grupoPorEquipo = {};
  (teams || []).forEach((t) => { grupoPorEquipo[t.id] = t.groupLabel; });

  const filtered = (matches || [])
    .filter((m) => !cascade.catId || m.categoryId === cascade.catId)
    .map((m) => ({ ...m, groupLabel: grupoPorEquipo[m.homeTeamId] || null }))
    .sort((a, b) => {
      const g = (a.groupLabel || '').localeCompare(b.groupLabel || '');
      if (g !== 0) return g;
      return (a.roundNumber || 0) - (b.roundNumber || 0);
    });
  const comp = competiciones?.find((c) => c.id === cascade.compId);
  const formato = comp?.format;
  // Que eventos aparecen para elegir depende del deporte, no de la
  // competencia: un gol o una tarjeta amarilla son del futbol, sea cual sea
  // el torneo que se esta jugando.
  const { data: sportInfo } = useApi(comp?.sportCode ? endpoints.sport(comp.sportCode) : null);
  const sorteable = comp && (comp.status === 'draft' || comp.status === 'scheduled');
  const yaJugados = filtered.filter((m) => m.status === 'finished' || m.status === 'walkover').length;
  const motivoSinSorteo = !comp ? null : !sorteable
    ? 'La competencia ya esta en curso.'
    : yaJugados > 0 ? `Ya hay ${yaJugados} partidos jugados.` : null;
  // Fase de grupos sin ningun equipo todavia sorteado en un grupo: "Sortear"
  // tiene que empezar por ahi, no fallar pidiendo algo que el propio sorteo
  // deberia resolver.
  const sinGrupos = formato === 'groups' && (teams || []).length > 0 && !(teams || []).some((t) => t.groupLabel);

  // Los bombos se cargan en Equipos, en otro momento — y por eso es facil
  // que un numero quede viejo o suelto. Se muestran aca, junto a la cantidad
  // de grupos, para que sea una sola decision y no dos separadas por dias.
  const bombos = {};
  (teams || []).forEach((t) => {
    const clave = t.seed != null ? t.seed : 'sin';
    (bombos[clave] = bombos[clave] || []).push(t.name);
  });
  const bombosNumerados = Object.keys(bombos).filter((k) => k !== 'sin').map(Number).sort((a, b) => a - b);
  const gruposElegidos = Number(drawGroupCount) || 0;
  // Si se permite que un mismo bombo se enfrente, esa restriccion deja de
  // aplicar del todo: no hay promesa que un bombo grande pueda incumplir.
  const bomboExcedido = !drawAllowSamePot && gruposElegidos > 0 && bombosNumerados.some((b) => bombos[b].length > gruposElegidos);

  const { data: events, mutate: mEv } = useApi(evOpen && selMatch ? endpoints.matchEvents(selMatch.id) : null);
  const { data: roster1 } = useApi(evOpen && selMatch ? endpoints.roster(selMatch.homeTeamId) : null);
  const { data: roster2 } = useApi(evOpen && selMatch ? endpoints.roster(selMatch.awayTeamId) : null);
  // Por posicion y despues por dorsal: buscar "el defensor numero 4" a ojo
  // en una lista sin ningun orden tactico era lo que hacia lenta la carga.
  // Sin posicion cargada queda al final, no mezclado en cualquier lado.
  const porPosicionYDorsal = (a, b) => {
    const posA = a.position || '';
    const posB = b.position || '';
    if (posA !== posB) {
      if (!posA) return 1;
      if (!posB) return -1;
      return posA.localeCompare(posB, 'es');
    }
    return (a.jerseyNumber ?? 999) - (b.jerseyNumber ?? 999);
  };
  // Dos listas, una por equipo, en vez de una sola con todos mezclados: con
  // los dos planteles completos era facil elegir sin querer a alguien del
  // equipo que no jugo el evento.
  const rosterHome = (roster1 || []).slice().sort(porPosicionYDorsal);
  const rosterAway = (roster2 || []).slice().sort(porPosicionYDorsal);

  const doDraw = async () => {
    if (!cascade.catId) return; setLoading(true); setError('');
    try {
      // Una fase de grupos sin nadie sorteado en un grupo todavia: el
      // sorteo tiene dos partes y esta es la primera. Un solo click hace
      // las dos, que es lo que alguien espera de un "sorteo".
      if (sinGrupos) {
        await apiPost(endpoints.categoryDrawGroups(cascade.catId), {
          groupCount: Number(drawGroupCount),
          respectPots: !drawAllowSamePot,
        });
        await mutateTeams();
      }
      const r = await apiPost(endpoints.categoryDraw(cascade.catId), { legs: formato === 'knockout' ? 1 : drawLegs });
      mutate(); setDrawResult(r);
    } catch (err) { setError(err.message); } finally { setLoading(false); }
  };

  const doAdvance = async () => {
    if (!cascade.catId) return;
    const ok = await confirm('Sortear siguiente ronda?', { confirmLabel: 'Sortear' });
    if (!ok) return;
    setLoading(true); setError('');
    try { const r = await apiPost(endpoints.categoryAdvanceBracket(cascade.catId), {}); mutate(); setDrawResult(r); setDrawOpen(true); }
    catch (err) { setError(err.message); } finally { setLoading(false); }
  };

  const doPromote = async () => {
    if (!cascade.catId) return; setLoading(true); setError('');
    try {
      const r = await apiPost(endpoints.categoryPromoteGroupStage(cascade.catId), {
        qualifiersPerGroup: Number(promoteForm.qualifiersPerGroup),
        bestThirdPlaced: Number(promoteForm.bestThirdPlaced),
      });
      mutate(); setPromoteResult(r);
    } catch (err) { setError(err.message); } finally { setLoading(false); }
  };

  const doSchedule = async () => {
    if (!cascade.catId) return; setLoading(true); setError('');
    try {
      await apiPost(endpoints.categoryMatches(cascade.catId), {
        homeTeamId: schedForm.homeTeamId, awayTeamId: schedForm.awayTeamId,
        venueSpaceId: schedForm.venueSpaceId || null,
        // El input da la hora local sin zona; el backend guarda un instante
        // real, asi que hay que decirle cual es antes de mandarlo.
        scheduledAt: schedForm.scheduledAt ? new Date(schedForm.scheduledAt).toISOString() : null,
        roundNumber: schedForm.roundNumber ? Number(schedForm.roundNumber) : null,
      });
      setSchedOpen(false); mutate();
    } catch (err) { setError(err.message); } finally { setLoading(false); }
  };

  // El input datetime-local quiere hora local sin zona ("2026-05-01T16:00"),
  // y lo que guarda el partido es una fecha con zona ("...Z" o "+00:00").
  // Formatear a mano evita el redondeo raro que a veces da toISOString con
  // la hora local.
  const aInputFecha = (iso) => {
    if (!iso) return '';
    const d = new Date(iso);
    const pad = (n) => String(n).padStart(2, '0');
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
  };

  const openEdit = (m) => {
    setSelMatch(m);
    setEditForm({ venueSpaceId: m.venueSpaceId || '', scheduledAt: aInputFecha(m.scheduledAt), roundNumber: m.roundNumber ?? '', notes: m.notes || '' });
    setError('');
    setEditOpen(true);
  };

  const doEdit = async () => {
    if (!selMatch) return; setLoading(true); setError('');
    try {
      await apiPut(endpoints.match(selMatch.id), {
        homeTeamId: selMatch.homeTeamId,
        awayTeamId: selMatch.awayTeamId,
        venueSpaceId: editForm.venueSpaceId || null,
        scheduledAt: editForm.scheduledAt ? new Date(editForm.scheduledAt).toISOString() : null,
        roundNumber: editForm.roundNumber !== '' ? Number(editForm.roundNumber) : null,
        phase: selMatch.phase || null,
        notes: editForm.notes || null,
      });
      setEditOpen(false); mutate(); toast.success('Partido reprogramado.');
    } catch (err) { setError(err.message); } finally { setLoading(false); }
  };

  const doResult = async () => {
    if (!selMatch) return; setLoading(true); setError('');
    try { await apiPut(endpoints.matchResult(selMatch.id), resForm); setResOpen(false); mutate(); toast.success('Resultado registrado.'); }
    catch (err) { setError(err.message); } finally { setLoading(false); }
  };

  // Solo tiene sentido donde el marcador se arma sumando goles (futbol,
  // basquet): en un deporte por sets un punto no mueve el marcador (no hay
  // nada que sumar), asi que ahi sigue haciendo falta cargar el resultado a
  // mano — ver el boton condicionado a sportInfo?.scoreMode mas abajo.
  const doFinishFromEvents = async (m) => {
    const marcador = `${m.liveHomeTotal ?? 0} - ${m.liveAwayTotal ?? 0}`;
    const ok = await confirm(`Finalizar el partido ${marcador}, segun los eventos cargados?`, { confirmLabel: 'Finalizar' });
    if (!ok) return;
    try { await apiPost(endpoints.matchResultFromEvents(m.id)); mutate(); toast.success('Partido finalizado.'); }
    catch (err) { toast.error(err.message); }
  };

  const doWalkover = async () => {
    if (!selMatch) return; setLoading(true); setError('');
    try { await apiPut(endpoints.matchWalkover(selMatch.id), woForm); setWoOpen(false); mutate(); toast.success('Walkover registrado.'); }
    catch (err) { setError(err.message); } finally { setLoading(false); }
  };

  const doPenalties = async () => {
    if (!selMatch) return; setLoading(true); setError('');
    try {
      await apiPut(endpoints.matchPenalties(selMatch.id), { homeScore: Number(poForm.homeScore), awayScore: Number(poForm.awayScore) });
      setPoOpen(false); mutate(); toast.success('Desempate por penales registrado.');
    } catch (err) { setError(err.message); } finally { setLoading(false); }
  };

  const doStatus = async (id, st) => {
    const ok = await confirm(`Cambiar estado a "${SL[st] || st}"?`, { confirmLabel: 'Cambiar' });
    if (!ok) return;
    try { await apiPut(endpoints.matchStatus(id), { status: st }); mutate(); } catch (err) { toast.error(err.message); }
  };

  const doEvent = async () => {
    if (!selMatch) return; setLoading(true); setError('');
    try {
      await apiPost(endpoints.matchEvents(selMatch.id), { ...evForm, periodNumber: evForm.periodNumber ? Number(evForm.periodNumber) : null, minute: evForm.minute ? Number(evForm.minute) : null, quantity: Number(evForm.quantity) || 1 });
      // Solo se limpia el jugador y el minuto: durante un partido en vivo el
      // tipo de evento y el periodo suelen repetirse de una carga a la otra
      // (varias tarjetas, varios goles seguidos en el mismo tiempo), asi que
      // no hace falta volver a elegirlos cada vez. El foco vuelve al buscador
      // de jugador para poder cargar el siguiente sin tocar el mouse.
      setEvForm({ ...evForm, rosterEntryId: '', minute: '' });
      mEv();
      playerFieldRef.current?.focus();
    }
    catch (err) { setError(err.message); } finally { setLoading(false); }
  };

  const delEvent = async (id) => {
    const ok = await confirm('Eliminar evento?', { confirmLabel: 'Eliminar', danger: true });
    if (!ok) return;
    try { await apiDelete(endpoints.event(id)); mEv(); } catch (err) { toast.error(err.message); }
  };

  const delMatch = async (id) => {
    const ok = await confirm('Eliminar partido?', { confirmLabel: 'Eliminar', danger: true });
    if (!ok) return;
    await apiDelete(endpoints.match(id)); mutate();
  };

  const columns = [
    { field: 'scheduledAt', headerName: 'Fecha', width: 150, renderCell: ({ value }) => value ? new Date(value).toLocaleString() : 'Sin fecha' },
    { field: 'roundNumber', headerName: '#', width: 50, renderCell: ({ value }) => value || '--' },
    { field: 'groupLabel', headerName: 'Grupo', width: 80, renderCell: ({ value }) => value ? <Chip label={value} size="small" /> : '--' },
    { field: 'phase', headerName: 'Fase', width: 100, renderCell: ({ value }) => value || '--' },
    { field: 'homeTeamName', headerName: 'Local', flex: 1, minWidth: 120 },
    // El marcador oficial (homeTotal/awayTotal) queda null hasta que se
    // carga el Resultado, aunque ya haya goles cargados como eventos — son
    // dos pasos separados a proposito. Mientras el partido esta en curso, se
    // muestra el marcador en vivo (liveHomeTotal/liveAwayTotal, sumado de
    // esos eventos) para no dejar la grilla en blanco mientras se juega.
    { field: 'homeTotal', headerName: '', width: 30, renderCell: ({ row }) => row.homeTotal != null ? row.homeTotal : row.liveHomeTotal != null ? <span title="Marcador en vivo, a partir de los eventos cargados" style={{ color: 'var(--mui-palette-warning-main)', fontWeight: 600 }}>{row.liveHomeTotal}</span> : '' },
    { field: 'awayTotal', headerName: '', width: 30, renderCell: ({ row }) => row.awayTotal != null ? row.awayTotal : row.liveAwayTotal != null ? <span title="Marcador en vivo, a partir de los eventos cargados" style={{ color: 'var(--mui-palette-warning-main)', fontWeight: 600 }}>{row.liveAwayTotal}</span> : '' },
    { field: 'awayTeamName', headerName: 'Visitante', flex: 1, minWidth: 120 },
    { field: 'penalties', headerName: '', width: 90, sortable: false, renderCell: ({ row }) =>
      row.penaltyHomeScore != null
        ? <Typography variant="caption" color="text.secondary">({row.penaltyHomeScore}-{row.penaltyAwayScore} pen)</Typography>
        : '' },
    { field: 'venueName', headerName: 'Sede', width: 160, renderCell: ({ row }) => row.venueName ? `${row.venueName}${row.spaceName ? ' — ' + row.spaceName : ''}` : '--' },
    { field: 'status', headerName: 'Estado', width: 110, renderCell: ({ value }) => <Chip label={SL[value] || value} color={SC[value] || 'default'} size="small" /> },
    // Tamaño normal (no "small") y el icono un poco mas grande: en el celular
    // los botones chicos de antes eran dificiles de tocar sin errarle al de
    // al lado.
    { field: 'actions', headerName: '', width: 340, renderCell: ({ row: m }) => (
      <Box sx={{ display: 'flex', gap: 0.25 }}>
        <Tooltip title="Reprogramar"><IconButton onClick={() => openEdit(m)}><Iconify icon="eva:calendar-outline" width={22} sx={{ color: 'text.secondary' }} /></IconButton></Tooltip>
        {m.status === 'scheduled' && <Tooltip title="Iniciar"><IconButton onClick={() => doStatus(m.id, 'in_progress')}><Iconify icon="eva:play-circle-fill" width={22} sx={{ color: 'warning.main' }} /></IconButton></Tooltip>}
        {/* Futbol, basquet: los goles ya se cargaron como eventos mientras se
            jugaba, asi que "Finalizar" cierra el partido con esa cuenta en
            un solo click — pedir el mismo numero otra vez a mano no suma
            nada. Un deporte por sets (voley) no tiene forma de derivarlo de
            los eventos (un punto no mueve el marcador ahi), asi que ese
            sigue pidiendo el resultado a mano. */}
        {m.status === 'in_progress' && sportInfo?.scoreMode === 'cumulative' && (
          <Tooltip title="Finalizar con el marcador de los eventos">
            <IconButton onClick={() => doFinishFromEvents(m)}>
              <Iconify icon="eva:checkmark-circle-fill" width={22} sx={{ color: 'success.main' }} />
            </IconButton>
          </Tooltip>
        )}
        {m.status === 'in_progress' && sportInfo?.scoreMode !== 'cumulative' && (
          <Tooltip title="Resultado">
            <IconButton onClick={() => { setSelMatch(m); setResForm({ periodScores: m.periodScores?.length ? m.periodScores.map((p) => ({ period: p.period, home: p.home, away: p.away })) : [{ period: 1, home: 0, away: 0 }, { period: 2, home: 0, away: 0 }], notes: m.notes || '' }); setError(''); setResOpen(true); }}>
              <Iconify icon="eva:checkmark-circle-fill" width={22} sx={{ color: 'success.main' }} />
            </IconButton>
          </Tooltip>
        )}
        {m.status === 'scheduled' && <Tooltip title="Walkover"><IconButton onClick={() => { setSelMatch(m); setWoForm({ winnerTeamId: m.homeTeamId, notes: '' }); setError(''); setWoOpen(true); }}><Iconify icon="eva:alert-triangle-fill" width={22} sx={{ color: 'warning.main' }} /></IconButton></Tooltip>}
        {/* Solo tiene sentido en una eliminatoria (fase != null) y con el
            partido ya empatado: en todo lo demas un empate es un resultado
            valido y no hay nada que desempatar. */}
        {m.status === 'finished' && m.phase && m.homeTotal === m.awayTotal && (
          <Tooltip title="Desempate por penales">
            <IconButton onClick={() => { setSelMatch(m); setPoForm({ homeScore: m.penaltyHomeScore ?? 0, awayScore: m.penaltyAwayScore ?? 0 }); setError(''); setPoOpen(true); }}>
              <Iconify icon="eva:radio-button-on-outline" width={22} sx={{ color: 'secondary.main' }} />
            </IconButton>
          </Tooltip>
        )}
        {(m.status === 'finished' || m.status === 'in_progress') && <Tooltip title="Eventos"><IconButton onClick={() => { setSelMatch(m); setEvForm({ rosterEntryId: '', metricId: '', periodNumber: '', minute: '', quantity: 1 }); setError(''); setEvOpen(true); }}><Iconify icon="eva:film-outline" width={22} sx={{ color: 'info.main' }} /></IconButton></Tooltip>}
        {m.status === 'in_progress' && <Tooltip title="Cancelar"><IconButton onClick={() => doStatus(m.id, 'cancelled')}><Iconify icon="eva:close-circle-fill" width={22} sx={{ color: 'error.main' }} /></IconButton></Tooltip>}
        {['cancelled', 'walkover', 'postponed'].includes(m.status) && <Tooltip title="Reabrir (vuelve a programado)"><IconButton onClick={() => doStatus(m.id, 'scheduled')}><Iconify icon="eva:refresh-outline" width={22} sx={{ color: 'info.main' }} /></IconButton></Tooltip>}
        <Tooltip title="Eliminar"><IconButton onClick={() => delMatch(m.id)}><Iconify icon="eva:trash-2-outline" width={22} sx={{ color: 'error.main' }} /></IconButton></Tooltip>
      </Box>
    )},
  ];

  return (
    <Box>
      <PageHeader title="Fixtures / Partidos">
        {cascade.catId && <Button variant="outlined" startIcon={<Iconify icon="eva:shuffle-2-fill" />} onClick={() => { setDrawLegs(1); setDrawGroupCount(''); setDrawAllowSamePot(false); setDrawResult(null); setError(''); setDrawOpen(true); }} disabled={loading}>Sortear</Button>}
        {cascade.catId && formato === 'groups' && (
          <Button variant="outlined" startIcon={<Iconify icon="eva:trending-up-outline" />} onClick={() => { setPromoteForm({ qualifiersPerGroup: 2, bestThirdPlaced: 0 }); setPromoteResult(null); setError(''); setPromoteOpen(true); }} disabled={loading}>
            Promover a eliminatoria
          </Button>
        )}
        {cascade.catId && (formato === 'knockout' || formato === 'groups') && <Button variant="outlined" startIcon={<Iconify icon="eva:arrow-forward-outline" />} onClick={doAdvance} disabled={loading}>Siguiente ronda</Button>}
        <Button variant="contained" startIcon={<Iconify icon="eva:plus-fill" />} onClick={() => { setSchedForm({ homeTeamId: '', awayTeamId: '', scheduledAt: '', roundNumber: '', notes: '' }); setError(''); setSchedOpen(true); }} disabled={!cascade.catId}>Programar</Button>
      </PageHeader>
      {error && !evOpen && !resOpen && !woOpen && !poOpen && !drawOpen && !promoteOpen && !editOpen && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
      <Box sx={{ display: 'flex', gap: 2, mb: 3, maxWidth: 700, flexWrap: 'wrap' }}>
        <Box sx={{ flex: 1, minWidth: 200 }}><SelectionCompetition value={cascade.compId} onChange={(e) => cascade.setCompId(e.target.value)} required /></Box>
        <Box sx={{ flex: 1, minWidth: 200 }}><SelectionCategory competitionId={cascade.compId} value={cascade.catId} onChange={(e) => cascade.setCatId(e.target.value)} required /></Box>
      </Box>
      <DataGrid rows={filtered} columns={columns} loading={isLoading} autoHeight rowHeight={56} disableRowSelectionOnClick getRowId={(r) => r.id} />

      {/* Draw dialog */}
      <Dialog open={drawOpen} onClose={() => setDrawOpen(false)} maxWidth="sm" fullWidth>
        <DialogTitle>{drawResult ? 'Sorteo realizado' : 'Sortear fixture'}</DialogTitle>
        <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: '16px !important' }}>
          {drawResult ? (
            <>
              {drawResult.champion ? <Alert severity="success"><strong>{drawResult.championName}</strong> es el campeon.</Alert>
                : <Alert severity="success">{drawResult.created} partidos creados{drawResult.rounds ? ` en ${drawResult.rounds} jornadas` : ''}{drawResult.round ? ` (ronda ${drawResult.round})` : ''}.</Alert>}
              {drawResult.replaced > 0 && <Alert severity="warning">Se reemplazaron {drawResult.replaced} partidos.</Alert>}
              {drawResult.byes > 0 && <Alert severity="info">{drawResult.byes} equipo(s) pasa(n) sin jugar.</Alert>}
            </>
          ) : (
            <>
              {error && <Alert severity="error">{error}</Alert>}
              {motivoSinSorteo && <Alert severity="error">{motivoSinSorteo}</Alert>}
              <Typography variant="body2">Formato: <strong>{FORMATO_LABEL[formato] || formato}</strong></Typography>
              {sinGrupos && (
                <>
                  <Alert severity="info">
                    Todavia ningun equipo tiene grupo asignado. Este sorteo primero los reparte en grupos al azar, y despues arma el fixture de cada uno.
                  </Alert>
                  <TextField
                    label="Numero de grupos"
                    type="number"
                    value={drawGroupCount}
                    onChange={(e) => setDrawGroupCount(e.target.value)}
                    fullWidth
                    required
                    helperText="Si algun equipo tiene bombo asignado, el sorteo respeta que ninguno se repita en un mismo grupo."
                    slotProps={{ htmlInput: { min: 2, max: 26 } }}
                  />
                  {bombosNumerados.length > 0 && (
                    <FormControlLabel
                      control={<Checkbox checked={drawAllowSamePot} onChange={(e) => setDrawAllowSamePot(e.target.checked)} />}
                      label="Permitir que equipos del mismo bombo se enfrenten (formato tipo liga de Champions)"
                    />
                  )}
                  {bombosNumerados.length > 0 && (
                    <Box sx={{ p: 1.5, border: '1px solid', borderColor: 'divider', borderRadius: 1 }}>
                      <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 0.5 }}>
                        Bombos cargados en Equipos
                      </Typography>
                      {bombosNumerados.map((b) => {
                        const equipos = bombos[b];
                        const excede = !drawAllowSamePot && gruposElegidos > 0 && equipos.length > gruposElegidos;
                        return (
                          <Typography key={b} variant="body2" sx={{ color: excede ? 'error.main' : undefined, fontWeight: excede ? 700 : 400 }}>
                            Bombo {b} ({equipos.length}): {equipos.join(', ')}
                            {excede ? ' — supera la cantidad de grupos' : ''}
                          </Typography>
                        );
                      })}
                      {bombos.sin && (
                        <Typography variant="body2" color="text.secondary">
                          Sin bombo ({bombos.sin.length}): {bombos.sin.join(', ')}
                        </Typography>
                      )}
                    </Box>
                  )}
                </>
              )}
              {formato !== 'knockout' && (
                <TextField select label="Vueltas" value={drawLegs} onChange={(e) => setDrawLegs(Number(e.target.value))} fullWidth>
                  <MenuItem value={1}>Una vuelta</MenuItem><MenuItem value={2}>Ida y vuelta</MenuItem>
                </TextField>
              )}
            </>
          )}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setDrawOpen(false)}>{drawResult ? 'Listo' : 'Cancelar'}</Button>
          {!drawResult && <Button variant="contained" onClick={doDraw} disabled={loading || !!motivoSinSorteo || (sinGrupos && (!drawGroupCount || bomboExcedido))}>{loading ? 'Sorteando...' : 'Sortear'}</Button>}
        </DialogActions>
      </Dialog>

      {/* Promote group stage to knockout dialog */}
      <Dialog open={promoteOpen} onClose={() => setPromoteOpen(false)} maxWidth="sm" fullWidth>
        <DialogTitle>{promoteResult ? 'Eliminatoria sorteada' : 'Promover a eliminatoria'}</DialogTitle>
        <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: '16px !important' }}>
          {promoteResult ? (
            <>
              <Alert severity="success">
                {promoteResult.created} partido(s) creados — {promoteResult.direct} clasificado(s) directo(s)
                {promoteResult.wildcards > 0 ? ` + ${promoteResult.wildcards} mejor(es) ubicado(s)` : ''}.
              </Alert>
              {promoteResult.byes > 0 && <Alert severity="info">{promoteResult.byes} equipo(s) pasa(n) sin jugar la primera ronda.</Alert>}
              {promoteResult.replaced > 0 && <Alert severity="warning">Se reemplazo una eliminatoria sorteada antes ({promoteResult.replaced} partidos).</Alert>}
              {promoteResult.repeatedMatchups > 0 && (
                <Alert severity="warning">
                  {promoteResult.repeatedMatchups} cruce(s) repite(n) un partido de la fase de grupos: los numeros no daban para evitarlo.
                </Alert>
              )}
            </>
          ) : (
            <>
              {error && <Alert severity="error">{error}</Alert>}
              <Typography variant="body2" color="text.secondary">
                Arma la llave con los mejores de cada grupo, una vez que todos los partidos de grupos tengan resultado.
              </Typography>
              <TextField
                label="Clasifican por grupo"
                type="number"
                value={promoteForm.qualifiersPerGroup}
                onChange={(e) => setPromoteForm({ ...promoteForm, qualifiersPerGroup: e.target.value })}
                fullWidth
                slotProps={{ htmlInput: { min: 1, max: 8 } }}
              />
              <TextField
                label="Mejores ubicados adicionales (mejores terceros, etc.)"
                type="number"
                value={promoteForm.bestThirdPlaced}
                onChange={(e) => setPromoteForm({ ...promoteForm, bestThirdPlaced: e.target.value })}
                fullWidth
                helperText="Opcional: cupos extra para los mejores equipos que no clasificaron directo, comparados entre grupos."
                slotProps={{ htmlInput: { min: 0, max: 16 } }}
              />
            </>
          )}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setPromoteOpen(false)}>{promoteResult ? 'Listo' : 'Cancelar'}</Button>
          {!promoteResult && <Button variant="contained" onClick={doPromote} disabled={loading}>{loading ? 'Sorteando...' : 'Sortear eliminatoria'}</Button>}
        </DialogActions>
      </Dialog>

      {/* Schedule dialog */}
      <Dialog open={schedOpen} onClose={() => setSchedOpen(false)} maxWidth="sm" fullWidth>
        <DialogTitle>Programar Partido</DialogTitle>
        <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: '16px !important' }}>
          <SelectionTeam categoryId={cascade.catId} value={schedForm.homeTeamId} onChange={(e) => setSchedForm({ ...schedForm, homeTeamId: e.target.value })} label="Local" required />
          <SelectionTeam categoryId={cascade.catId} value={schedForm.awayTeamId} onChange={(e) => setSchedForm({ ...schedForm, awayTeamId: e.target.value })} label="Visitante" required />
          <SelectionSpace value={schedForm.venueSpaceId} onChange={(e) => setSchedForm({ ...schedForm, venueSpaceId: e.target.value })} />
          <TextField label="Fecha y hora" type="datetime-local" value={schedForm.scheduledAt} onChange={(e) => setSchedForm({ ...schedForm, scheduledAt: e.target.value })} fullWidth slotProps={{ inputLabel: { shrink: true } }} />
          <TextField label="Ronda" type="number" value={schedForm.roundNumber} onChange={(e) => setSchedForm({ ...schedForm, roundNumber: e.target.value })} fullWidth />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setSchedOpen(false)}>Cancelar</Button>
          <Button variant="contained" onClick={doSchedule} disabled={loading}>Guardar</Button>
        </DialogActions>
      </Dialog>

      {/* Edit / reschedule dialog */}
      <Dialog open={editOpen} onClose={() => setEditOpen(false)} maxWidth="sm" fullWidth>
        <DialogTitle>Reprogramar Partido</DialogTitle>
        <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: '16px !important' }}>
          {error && <Alert severity="error">{error}</Alert>}
          {selMatch && <Alert severity="info">{selMatch.homeTeamName} vs {selMatch.awayTeamName}</Alert>}
          <SelectionSpace value={editForm.venueSpaceId} onChange={(e) => setEditForm({ ...editForm, venueSpaceId: e.target.value })} />
          <TextField label="Fecha y hora" type="datetime-local" value={editForm.scheduledAt} onChange={(e) => setEditForm({ ...editForm, scheduledAt: e.target.value })} fullWidth slotProps={{ inputLabel: { shrink: true } }} />
          <TextField
            label="Ronda"
            type="number"
            value={editForm.roundNumber}
            onChange={(e) => setEditForm({ ...editForm, roundNumber: e.target.value })}
            fullWidth
            helperText="Cambiar la ronda es lo que reordena en que jornada se juega este partido."
          />
          <TextField label="Notas" value={editForm.notes} onChange={(e) => setEditForm({ ...editForm, notes: e.target.value })} fullWidth multiline minRows={2} />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setEditOpen(false)}>Cancelar</Button>
          <Button variant="contained" onClick={doEdit} disabled={loading}>Guardar</Button>
        </DialogActions>
      </Dialog>

      {/* Result dialog */}
      <Dialog open={resOpen} onClose={() => setResOpen(false)} maxWidth="sm" fullWidth>
        <DialogTitle>Registrar Resultado</DialogTitle>
        <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: '16px !important' }}>
          {selMatch && <Alert severity="info">{selMatch.homeTeamName} vs {selMatch.awayTeamName}</Alert>}
          {resForm.periodScores.map((ps, i) => (
            <Box key={i} sx={{ display: 'flex', gap: 2, alignItems: 'center' }}>
              <Typography variant="body2" sx={{ minWidth: 70 }}>P{ps.period}</Typography>
              <TextField label="Loc" type="number" value={ps.home} onChange={(e) => { const s = [...resForm.periodScores]; s[i] = { ...s[i], home: Number(e.target.value) }; setResForm({ ...resForm, periodScores: s }); }} size="small" sx={{ flex: 1 }} />
              <Typography>-</Typography>
              <TextField label="Vis" type="number" value={ps.away} onChange={(e) => { const s = [...resForm.periodScores]; s[i] = { ...s[i], away: Number(e.target.value) }; setResForm({ ...resForm, periodScores: s }); }} size="small" sx={{ flex: 1 }} />
            </Box>
          ))}
          <Divider />
          <Typography fontWeight={600}>Total: {resForm.periodScores.reduce((s, p) => s + p.home, 0)} - {resForm.periodScores.reduce((s, p) => s + p.away, 0)}</Typography>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setResOpen(false)}>Cancelar</Button>
          <Button variant="contained" color="success" onClick={doResult} disabled={loading}>Registrar</Button>
        </DialogActions>
      </Dialog>

      {/* Walkover dialog */}
      <Dialog open={woOpen} onClose={() => setWoOpen(false)} maxWidth="sm" fullWidth>
        <DialogTitle>Walkover</DialogTitle>
        <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: '16px !important' }}>
          {selMatch && <Alert severity="warning">Se asignara el partido.</Alert>}
          <TextField select label="Ganador" value={woForm.winnerTeamId} onChange={(e) => setWoForm({ ...woForm, winnerTeamId: e.target.value })} fullWidth>
            {selMatch && [<MenuItem key="h" value={selMatch.homeTeamId}>{selMatch.homeTeamName} (Local)</MenuItem>, <MenuItem key="a" value={selMatch.awayTeamId}>{selMatch.awayTeamName} (Visitante)</MenuItem>]}
          </TextField>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setWoOpen(false)}>Cancelar</Button>
          <Button variant="contained" color="warning" onClick={doWalkover} disabled={loading}>Confirmar</Button>
        </DialogActions>
      </Dialog>

      {/* Penalties dialog */}
      <Dialog open={poOpen} onClose={() => setPoOpen(false)} maxWidth="sm" fullWidth>
        <DialogTitle>Desempate por penales</DialogTitle>
        <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: '16px !important' }}>
          {error && <Alert severity="error">{error}</Alert>}
          {selMatch && (
            <Alert severity="info">
              {selMatch.homeTeamName} {selMatch.homeTotal} - {selMatch.awayTotal} {selMatch.awayTeamName}: se juega la
              siguiente ronda con quien gane los penales.
            </Alert>
          )}
          <Box sx={{ display: 'flex', gap: 2, alignItems: 'center' }}>
            <TextField label={selMatch?.homeTeamName || 'Local'} type="number" value={poForm.homeScore} onChange={(e) => setPoForm({ ...poForm, homeScore: e.target.value })} fullWidth />
            <Typography>-</Typography>
            <TextField label={selMatch?.awayTeamName || 'Visitante'} type="number" value={poForm.awayScore} onChange={(e) => setPoForm({ ...poForm, awayScore: e.target.value })} fullWidth />
          </Box>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setPoOpen(false)}>Cancelar</Button>
          <Button variant="contained" color="secondary" onClick={doPenalties} disabled={loading}>Confirmar</Button>
        </DialogActions>
      </Dialog>

      {/* Events dialog: cargar y ver la lista en el mismo lugar, sin abrir un
          segundo dialogo por cada evento — eso era la mitad de la demora al
          cargar varios goles o tarjetas seguidos durante un partido en vivo. */}
      <Dialog open={evOpen} onClose={() => setEvOpen(false)} maxWidth="lg" fullWidth>
        <DialogTitle>Eventos{selMatch ? ` - ${selMatch.homeTeamName} vs ${selMatch.awayTeamName}` : ''}</DialogTitle>
        <DialogContent>
          {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
          <Box sx={{ display: 'flex', gap: 1.5, alignItems: 'flex-start', flexWrap: 'wrap', mb: 2, p: 1.5, borderRadius: 1, bgcolor: 'action.hover' }}>
            <Autocomplete
              openOnFocus
              options={rosterHome}
              getOptionLabel={(o) => `#${o.jerseyNumber ?? '?'} ${o.lastName}, ${o.firstName}${o.position ? ' — ' + o.position : ''}`}
              isOptionEqualToValue={(o, v) => o.id === v.id}
              value={rosterHome.find((r) => r.id === evForm.rosterEntryId) || null}
              onChange={(_, v) => setEvForm({ ...evForm, rosterEntryId: v?.id || '' })}
              sx={{ width: 230 }}
              renderInput={(params) => <TextField {...params} inputRef={playerFieldRef} label={selMatch?.homeTeamName || 'Local'} placeholder="Nombre, dorsal o posicion" autoFocus />}
            />
            <Autocomplete
              openOnFocus
              options={rosterAway}
              getOptionLabel={(o) => `#${o.jerseyNumber ?? '?'} ${o.lastName}, ${o.firstName}${o.position ? ' — ' + o.position : ''}`}
              isOptionEqualToValue={(o, v) => o.id === v.id}
              value={rosterAway.find((r) => r.id === evForm.rosterEntryId) || null}
              onChange={(_, v) => setEvForm({ ...evForm, rosterEntryId: v?.id || '' })}
              sx={{ width: 230 }}
              renderInput={(params) => <TextField {...params} label={selMatch?.awayTeamName || 'Visitante'} placeholder="Nombre, dorsal o posicion" />}
            />
            <TextField select label="Evento" value={evForm.metricId} onChange={(e) => setEvForm({ ...evForm, metricId: e.target.value })} sx={{ width: 160 }}>
              <MenuItem value="">Seleccionar</MenuItem>
              {(sportInfo?.metrics || []).map((m) => <MenuItem key={m.id} value={m.id}>{m.label}</MenuItem>)}
            </TextField>
            <TextField select label="Periodo" value={evForm.periodNumber} onChange={(e) => setEvForm({ ...evForm, periodNumber: e.target.value })} sx={{ width: 130 }}>
              <MenuItem value="">--</MenuItem>
              {Array.from({ length: sportInfo?.defaultPeriods || 0 }, (_, i) => i + 1).map((n) => (
                <MenuItem key={n} value={n}>{sportInfo.periodLabel} {n}</MenuItem>
              ))}
            </TextField>
            <TextField label="Minuto" type="number" value={evForm.minute} onChange={(e) => setEvForm({ ...evForm, minute: e.target.value })} sx={{ width: 90 }} />
            <TextField label="Cant." type="number" value={evForm.quantity} onChange={(e) => setEvForm({ ...evForm, quantity: e.target.value })} sx={{ width: 80 }} />
            {/* Habilitado solo con los cinco datos cargados — jugador,
                evento, periodo y minuto incluidos, no solo jugador y evento
                — para que no se pueda cargar un evento a medio llenar. */}
            <Button
              variant="contained"
              startIcon={<Iconify icon="eva:plus-fill" />}
              onClick={doEvent}
              disabled={loading || !evForm.rosterEntryId || !evForm.metricId || !evForm.periodNumber || !evForm.minute}
              sx={{ height: 56 }}
            >
              Agregar
            </Button>
          </Box>
          <DataGrid rows={events || []} columns={[
            { field: 'minute', headerName: 'Min', width: 60, renderCell: ({ value }) => value != null ? `${value}'` : '--' },
            { field: 'periodNumber', headerName: 'Per', width: 50 },
            { field: 'firstName', headerName: 'Jugador', flex: 1, renderCell: ({ row }) => (
              <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
                <span>{row.firstName || ''} {row.lastName || ''}</span>
                {row.countsForOpponent && <Chip label="AG" size="small" color="error" variant="outlined" sx={{ height: 18, '& .MuiChip-label': { px: 0.6, fontSize: 10, fontWeight: 700 } }} />}
              </Box>
            ) },
            { field: 'jerseyNumber', headerName: '#', width: 50 },
            // Un autogol lo carga un jugador del equipo contrario al que se
            // le atribuye: se muestra el equipo al que le sirvio (igual que
            // el marcador en vivo ya lo cuenta), no el plantel del jugador —
            // la etiqueta AG de al lado aclara quien lo metio realmente.
            { field: 'teamName', headerName: 'Equipo', width: 120, renderCell: ({ row }) => {
              if (!row.countsForOpponent || !selMatch) return row.teamName;
              return row.teamId === selMatch.homeTeamId ? selMatch.awayTeamName : selMatch.homeTeamName;
            } },
            { field: 'metricLabel', headerName: 'Evento', width: 120 },
            { field: 'quantity', headerName: 'Cant.', width: 60 },
            { field: 'actions', headerName: '', width: 60, renderCell: ({ row }) => <IconButton size="small" onClick={() => delEvent(row.id)}><Iconify icon="eva:trash-2-outline" width={16} sx={{ color: 'error.main' }} /></IconButton> },
          ]} autoHeight hideFooter disableRowSelectionOnClick getRowId={(r) => r.id} />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setEvOpen(false)}>Cerrar</Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
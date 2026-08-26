import { useState } from 'react';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import Chip from '@mui/material/Chip';
import Dialog from '@mui/material/Dialog';
import DialogTitle from '@mui/material/DialogTitle';
import DialogContent from '@mui/material/DialogContent';
import DialogActions from '@mui/material/DialogActions';
import TextField from '@mui/material/TextField';
import MenuItem from '@mui/material/MenuItem';
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
import { SelectionCompetition, SelectionCategory, SelectionTeam } from 'src/components/selectors';
import { toast } from 'sonner';

const SC = { scheduled: 'info', in_progress: 'warning', finished: 'success', cancelled: 'error', walkover: 'warning', postponed: 'default' };
const SL = { scheduled: 'Programado', in_progress: 'En curso', finished: 'Finalizado', cancelled: 'Cancelado', walkover: 'Walkover', postponed: 'Aplazado' };
const FORMATO_LABEL = { league: 'Todos vs todos', knockout: 'Eliminacion directa', groups: 'Fase de grupos' };

export default function MatchesPage() {
  const cascade = useCascade();
  const { data: competiciones } = useApi(endpoints.competitions);
  const { data: matches, mutate, isLoading } = useApi(cascade.compId ? endpoints.competitionMatches(cascade.compId) : null);
  const { data: teams } = useApi(cascade.catId ? endpoints.teams(cascade.catId) : null);

  const [selMatch, setSelMatch] = useState(null);
  const [schedOpen, setSchedOpen] = useState(false);
  const [resOpen, setResOpen] = useState(false);
  const [woOpen, setWoOpen] = useState(false);
  const [evOpen, setEvOpen] = useState(false);
  const [evFormOpen, setEvFormOpen] = useState(false);
  const [schedForm, setSchedForm] = useState({ homeTeamId: '', awayTeamId: '', scheduledAt: '', roundNumber: '', notes: '' });
  const [resForm, setResForm] = useState({ periodScores: [{ period: 1, home: 0, away: 0 }, { period: 2, home: 0, away: 0 }], notes: '' });
  const [woForm, setWoForm] = useState({ winnerTeamId: '', notes: '' });
  const [evForm, setEvForm] = useState({ rosterEntryId: '', metricId: '', periodNumber: '', minute: '', quantity: 1 });
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const [drawOpen, setDrawOpen] = useState(false);
  const [drawLegs, setDrawLegs] = useState(1);
  const [drawResult, setDrawResult] = useState(null);

  const filtered = (matches || []).filter((m) => !cascade.catId || m.categoryId === cascade.catId);
  const comp = competiciones?.find((c) => c.id === cascade.compId);
  const formato = comp?.format;
  const sorteable = comp && (comp.status === 'draft' || comp.status === 'scheduled');
  const yaJugados = filtered.filter((m) => m.status === 'finished' || m.status === 'walkover').length;
  const motivoSinSorteo = !comp ? null : !sorteable
    ? 'La competencia ya esta en curso.'
    : yaJugados > 0 ? `Ya hay ${yaJugados} partidos jugados.` : null;

  const { data: events, mutate: mEv } = useApi(evOpen && selMatch ? endpoints.matchEvents(selMatch.id) : null);
  const { data: roster1 } = useApi(evOpen && selMatch ? endpoints.roster(selMatch.homeTeamId) : null);
  const { data: roster2 } = useApi(evOpen && selMatch ? endpoints.roster(selMatch.awayTeamId) : null);
  const allRoster = [...(roster1 || []), ...(roster2 || [])];

  const doDraw = async () => {
    if (!cascade.catId) return; setLoading(true); setError('');
    try { const r = await apiPost(endpoints.categoryDraw(cascade.catId), { legs: formato === 'knockout' ? 1 : drawLegs }); mutate(); setDrawResult(r); }
    catch (err) { setError(err.message); } finally { setLoading(false); }
  };

  const doAdvance = async () => {
    if (!cascade.catId || !confirm('Sortear siguiente ronda?')) return;
    setLoading(true); setError('');
    try { const r = await apiPost(endpoints.categoryAdvanceBracket(cascade.catId), {}); mutate(); setDrawResult(r); setDrawOpen(true); }
    catch (err) { setError(err.message); } finally { setLoading(false); }
  };

  const doSchedule = async () => {
    if (!cascade.catId) return; setLoading(true); setError('');
    try {
      await apiPost(endpoints.categoryMatches(cascade.catId), {
        homeTeamId: schedForm.homeTeamId, awayTeamId: schedForm.awayTeamId,
        scheduledAt: schedForm.scheduledAt || null, roundNumber: schedForm.roundNumber ? Number(schedForm.roundNumber) : null,
      });
      setSchedOpen(false); mutate();
    } catch (err) { setError(err.message); } finally { setLoading(false); }
  };

  const doResult = async () => {
    if (!selMatch) return; setLoading(true); setError('');
    try { await apiPut(endpoints.matchResult(selMatch.id), resForm); setResOpen(false); mutate(); toast.success('Resultado registrado.'); }
    catch (err) { setError(err.message); } finally { setLoading(false); }
  };

  const doWalkover = async () => {
    if (!selMatch) return; setLoading(true); setError('');
    try { await apiPut(endpoints.matchWalkover(selMatch.id), woForm); setWoOpen(false); mutate(); toast.success('Walkover registrado.'); }
    catch (err) { setError(err.message); } finally { setLoading(false); }
  };

  const doStatus = async (id, st) => {
    if (!confirm(`Cambiar estado a "${SL[st] || st}"?`)) return;
    try { await apiPut(endpoints.matchStatus(id), { status: st }); mutate(); } catch (err) { toast.error(err.message); }
  };

  const doEvent = async () => {
    if (!selMatch) return; setLoading(true); setError('');
    try { await apiPost(endpoints.matchEvents(selMatch.id), { ...evForm, periodNumber: evForm.periodNumber ? Number(evForm.periodNumber) : null, minute: evForm.minute ? Number(evForm.minute) : null, quantity: Number(evForm.quantity) || 1 }); setEvFormOpen(false); mEv(); }
    catch (err) { setError(err.message); } finally { setLoading(false); }
  };

  const delEvent = async (id) => {
    if (!confirm('Eliminar evento?')) return;
    try { await apiDelete(endpoints.event(id)); mEv(); } catch (err) { toast.error(err.message); }
  };

  const columns = [
    { field: 'scheduledAt', headerName: 'Fecha', width: 150, renderCell: ({ value }) => value ? new Date(value).toLocaleString() : 'Sin fecha' },
    { field: 'roundNumber', headerName: '#', width: 50, renderCell: ({ value }) => value || '--' },
    { field: 'homeTeamName', headerName: 'Local', flex: 1, minWidth: 120 },
    { field: 'homeTotal', headerName: '', width: 25, renderCell: ({ value }) => value != null ? value : '' },
    { field: 'awayTotal', headerName: '', width: 25, renderCell: ({ value }) => value != null ? value : '' },
    { field: 'awayTeamName', headerName: 'Visitante', flex: 1, minWidth: 120 },
    { field: 'venueName', headerName: 'Sede', width: 100, renderCell: ({ value }) => value || '--' },
    { field: 'status', headerName: 'Estado', width: 110, renderCell: ({ value }) => <Chip label={SL[value] || value} color={SC[value] || 'default'} size="small" /> },
    { field: 'actions', headerName: '', width: 220, renderCell: ({ row: m }) => (
      <Box sx={{ display: 'flex' }}>
        {m.status === 'scheduled' && <Tooltip title="Iniciar"><IconButton size="small" onClick={() => doStatus(m.id, 'in_progress')}><Iconify icon="eva:play-fill" width={18} sx={{ color: 'warning.main' }} /></IconButton></Tooltip>}
        {m.status === 'in_progress' && <Tooltip title="Resultado"><IconButton size="small" onClick={() => { setSelMatch(m); setResForm({ periodScores: m.periodScores?.length ? m.periodScores.map((p) => ({ period: p.period, home: p.home, away: p.away })) : [{ period: 1, home: 0, away: 0 }, { period: 2, home: 0, away: 0 }], notes: m.notes || '' }); setError(''); setResOpen(true); }}><Iconify icon="eva:checkmark-circle-fill" width={18} sx={{ color: 'success.main' }} /></IconButton></Tooltip>}
        {m.status === 'scheduled' && <Tooltip title="Walkover"><IconButton size="small" onClick={() => { setSelMatch(m); setWoForm({ winnerTeamId: m.homeTeamId, notes: '' }); setError(''); setWoOpen(true); }}><Iconify icon="eva:alert-triangle-fill" width={18} sx={{ color: 'warning.main' }} /></IconButton></Tooltip>}
        {(m.status === 'finished' || m.status === 'in_progress') && <Tooltip title="Eventos"><IconButton size="small" onClick={() => { setSelMatch(m); setEvForm({ rosterEntryId: '', metricId: '', periodNumber: '', minute: '', quantity: 1 }); setError(''); setEvOpen(true); }}><Iconify icon="eva:film-outline" width={18} sx={{ color: 'info.main' }} /></IconButton></Tooltip>}
        {m.status === 'in_progress' && <Tooltip title="Cancelar"><IconButton size="small" onClick={() => doStatus(m.id, 'cancelled')}><Iconify icon="eva:close-circle-fill" width={18} sx={{ color: 'error.main' }} /></IconButton></Tooltip>}
        {['cancelled', 'walkover', 'postponed'].includes(m.status) && <Tooltip title="Reprogramar"><IconButton size="small" onClick={() => doStatus(m.id, 'scheduled')}><Iconify icon="eva:refresh-outline" width={18} sx={{ color: 'info.main' }} /></IconButton></Tooltip>}
        <Tooltip title="Eliminar"><IconButton size="small" onClick={() => { if (confirm('Eliminar partido?')) apiDelete(endpoints.match(m.id)).then(mutate); }}><Iconify icon="eva:trash-2-outline" width={18} sx={{ color: 'error.main' }} /></IconButton></Tooltip>
      </Box>
    )},
  ];

  return (
    <Box>
      <PageHeader title="Fixtures / Partidos">
        {cascade.catId && <Button variant="outlined" startIcon={<Iconify icon="eva:shuffle-2-fill" />} onClick={() => { setDrawLegs(1); setDrawResult(null); setError(''); setDrawOpen(true); }} disabled={loading}>Sortear</Button>}
        {cascade.catId && formato === 'knockout' && <Button variant="outlined" startIcon={<Iconify icon="eva:arrow-forward-outline" />} onClick={doAdvance} disabled={loading}>Siguiente ronda</Button>}
        <Button variant="contained" startIcon={<Iconify icon="eva:plus-fill" />} onClick={() => { setSchedForm({ homeTeamId: '', awayTeamId: '', scheduledAt: '', roundNumber: '', notes: '' }); setError(''); setSchedOpen(true); }} disabled={!cascade.catId}>Programar</Button>
      </PageHeader>
      {error && !evOpen && !resOpen && !woOpen && !drawOpen && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
      <Box sx={{ display: 'flex', gap: 2, mb: 3, maxWidth: 700 }}>
        <Box sx={{ flex: 1 }}><SelectionCompetition value={cascade.compId} onChange={(e) => cascade.setCompId(e.target.value)} required /></Box>
        <Box sx={{ flex: 1 }}><SelectionCategory competitionId={cascade.compId} value={cascade.catId} onChange={(e) => cascade.setCatId(e.target.value)} required /></Box>
      </Box>
      <DataGrid rows={filtered} columns={columns} loading={isLoading} autoHeight disableRowSelectionOnClick getRowId={(r) => r.id} />

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
          {!drawResult && <Button variant="contained" onClick={doDraw} disabled={loading || !!motivoSinSorteo}>{loading ? 'Sorteando...' : 'Sortear'}</Button>}
        </DialogActions>
      </Dialog>

      {/* Schedule dialog */}
      <Dialog open={schedOpen} onClose={() => setSchedOpen(false)} maxWidth="sm" fullWidth>
        <DialogTitle>Programar Partido</DialogTitle>
        <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: '16px !important' }}>
          <SelectionTeam categoryId={cascade.catId} value={schedForm.homeTeamId} onChange={(e) => setSchedForm({ ...schedForm, homeTeamId: e.target.value })} label="Local" required />
          <SelectionTeam categoryId={cascade.catId} value={schedForm.awayTeamId} onChange={(e) => setSchedForm({ ...schedForm, awayTeamId: e.target.value })} label="Visitante" required />
          <TextField label="Fecha y hora" type="datetime-local" value={schedForm.scheduledAt} onChange={(e) => setSchedForm({ ...schedForm, scheduledAt: e.target.value })} fullWidth slotProps={{ inputLabel: { shrink: true } }} />
          <TextField label="Ronda" type="number" value={schedForm.roundNumber} onChange={(e) => setSchedForm({ ...schedForm, roundNumber: e.target.value })} fullWidth />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setSchedOpen(false)}>Cancelar</Button>
          <Button variant="contained" onClick={doSchedule} disabled={loading}>Guardar</Button>
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

      {/* Events dialog */}
      <Dialog open={evOpen} onClose={() => setEvOpen(false)} maxWidth="lg" fullWidth>
        <DialogTitle sx={{ display: 'flex', justifyContent: 'space-between' }}>
          <span>Eventos{selMatch ? ` - ${selMatch.homeTeamName} vs ${selMatch.awayTeamName}` : ''}</span>
          <Button variant="contained" size="small" startIcon={<Iconify icon="eva:plus-fill" />} onClick={() => { setEvForm({ rosterEntryId: '', metricId: '', periodNumber: '', minute: '', quantity: 1 }); setError(''); setEvFormOpen(true); }}>Agregar</Button>
        </DialogTitle>
        <DialogContent>
          <DataGrid rows={events || []} columns={[
            { field: 'minute', headerName: 'Min', width: 60, renderCell: ({ value }) => value != null ? `${value}'` : '--' },
            { field: 'periodNumber', headerName: 'Per', width: 50 },
            { field: 'firstName', headerName: 'Jugador', flex: 1, renderCell: ({ row }) => `${row.firstName || ''} ${row.lastName || ''}` },
            { field: 'jerseyNumber', headerName: '#', width: 50 },
            { field: 'teamName', headerName: 'Equipo', width: 120 },
            { field: 'metricLabel', headerName: 'Evento', width: 120 },
            { field: 'quantity', headerName: 'Cant.', width: 60 },
            { field: 'actions', headerName: '', width: 60, renderCell: ({ row }) => <IconButton size="small" onClick={() => delEvent(row.id)}><Iconify icon="eva:trash-2-outline" width={16} sx={{ color: 'error.main' }} /></IconButton> },
          ]} autoHeight hideFooter disableRowSelectionOnClick getRowId={(r) => r.id} />
        </DialogContent>
      </Dialog>

      {/* Record event dialog */}
      <Dialog open={evFormOpen} onClose={() => setEvFormOpen(false)} maxWidth="sm" fullWidth>
        <DialogTitle>Registrar evento</DialogTitle>
        <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: '16px !important' }}>
          <TextField select label="Jugador" value={evForm.rosterEntryId} onChange={(e) => setEvForm({ ...evForm, rosterEntryId: e.target.value })} fullWidth required>
            <MenuItem value="">Seleccionar</MenuItem>
            {allRoster.map((r) => <MenuItem key={r.id} value={r.id}>#{r.jerseyNumber || '?'} {r.lastName}, {r.firstName}</MenuItem>)}
          </TextField>
          <TextField label="Metrica" value={evForm.metricId} onChange={(e) => setEvForm({ ...evForm, metricId: e.target.value })} fullWidth required />
          <TextField label="Periodo" type="number" value={evForm.periodNumber} onChange={(e) => setEvForm({ ...evForm, periodNumber: e.target.value })} fullWidth />
          <TextField label="Minuto" type="number" value={evForm.minute} onChange={(e) => setEvForm({ ...evForm, minute: e.target.value })} fullWidth />
          <TextField label="Cantidad" type="number" value={evForm.quantity} onChange={(e) => setEvForm({ ...evForm, quantity: e.target.value })} fullWidth />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setEvFormOpen(false)}>Cancelar</Button>
          <Button variant="contained" onClick={doEvent} disabled={loading}>Registrar</Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
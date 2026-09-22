import { useEffect, useRef, useState } from 'react';
import Box from '@mui/material/Box';
import Chip from '@mui/material/Chip';
import Dialog from '@mui/material/Dialog';
import DialogTitle from '@mui/material/DialogTitle';
import DialogContent from '@mui/material/DialogContent';
import DialogActions from '@mui/material/DialogActions';
import TextField from '@mui/material/TextField';
import Autocomplete from '@mui/material/Autocomplete';
import MenuItem from '@mui/material/MenuItem';
import IconButton from '@mui/material/IconButton';
import Button from '@mui/material/Button';
import Alert from '@mui/material/Alert';
import Typography from '@mui/material/Typography';
import { DataGrid } from '@mui/x-data-grid';
import { Iconify } from 'src/components/iconify';
import { useApi, apiPost, apiDelete } from 'src/hooks/use-api';
import { endpoints } from 'src/lib/axios';
import { useConfirm } from 'src/components/confirm-dialog';
import { esIndividual } from 'src/lib/sport-shape';
import { toast } from 'sonner';

const EMPTY_FORM = { rosterEntryId: '', metricId: '', periodNumber: '', minute: '', quantity: 1 };

// Por posicion y despues por dorsal: buscar "el defensor numero 4" a ojo en
// una lista sin ningun orden tactico era lo que hacia lenta la carga. Sin
// posicion cargada queda al final, no mezclado en cualquier lado.
function porPosicionYDorsal(a, b) {
  const posA = a.position || '';
  const posB = b.position || '';
  if (posA !== posB) {
    if (!posA) return 1;
    if (!posB) return -1;
    return posA.localeCompare(posB, 'es');
  }
  return (a.jerseyNumber ?? 999) - (b.jerseyNumber ?? 999);
}

/**
 * Carga y ver la lista de eventos de un partido en el mismo lugar, sin abrir
 * un segundo dialogo por cada evento — eso era la mitad de la demora al
 * cargar varios goles o tarjetas seguidos durante un partido en vivo.
 *
 * No recibe `mutate` de la lista de partidos: agregar o borrar un evento
 * refresca esta lista (mEv) pero no el marcador en vivo de la grilla de
 * partidos, exactamente como ya hacia MatchesPage antes de este archivo
 * existir.
 */
export function EventsDialog({ open, onClose, selMatch, sportInfo, loading, setLoading, error, setError }) {
  const confirm = useConfirm();
  const [form, setForm] = useState(EMPTY_FORM);
  const playerFieldRef = useRef(null);
  const individual = esIndividual(sportInfo);

  const { data: events, mutate: mEv } = useApi(open && selMatch ? endpoints.matchEvents(selMatch.id) : null);
  const { data: roster1 } = useApi(open && selMatch ? endpoints.roster(selMatch.homeTeamId) : null);
  const { data: roster2 } = useApi(open && selMatch ? endpoints.roster(selMatch.awayTeamId) : null);

  // Dos listas, una por equipo, en vez de una sola con todos mezclados: con
  // los dos planteles completos era facil elegir sin querer a alguien del
  // equipo que no jugo el evento.
  const rosterHome = (roster1 || []).slice().sort(porPosicionYDorsal);
  const rosterAway = (roster2 || []).slice().sort(porPosicionYDorsal);

  useEffect(() => {
    if (open) setForm(EMPTY_FORM);
  }, [open]);

  const doEvent = async () => {
    if (!selMatch) return;
    setLoading(true); setError('');
    try {
      await apiPost(endpoints.matchEvents(selMatch.id), { ...form, periodNumber: form.periodNumber ? Number(form.periodNumber) : null, minute: form.minute ? Number(form.minute) : null, quantity: Number(form.quantity) || 1 });
      // Solo se limpia el jugador y el minuto: durante un partido en vivo el
      // tipo de evento y el periodo suelen repetirse de una carga a la otra
      // (varias tarjetas, varios goles seguidos en el mismo tiempo), asi que
      // no hace falta volver a elegirlos cada vez. El foco vuelve al
      // buscador de jugador para poder cargar el siguiente sin tocar el
      // mouse.
      setForm({ ...form, rosterEntryId: '', minute: '' });
      mEv();
      playerFieldRef.current?.focus();
    }
    catch (err) { setError(err.message); }
    finally { setLoading(false); }
  };

  const delEvent = async (id) => {
    const ok = await confirm('Eliminar evento?', { confirmLabel: 'Eliminar', danger: true });
    if (!ok) return;
    try { await apiDelete(endpoints.event(id)); mEv(); } catch (err) { toast.error(err.message); }
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="lg" fullWidth>
      <DialogTitle>Eventos{selMatch ? ` - ${selMatch.homeTeamName} vs ${selMatch.awayTeamName}` : ''}</DialogTitle>
      <DialogContent>
        {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
        <Box sx={{ display: 'flex', gap: 1.5, alignItems: 'flex-start', flexWrap: 'wrap', mb: 2, p: 1.5, borderRadius: 1, bgcolor: 'action.hover' }}>
          {individual ? (
            // Un deporte individual (Kyorugi hoy) enfrenta a un deportista
            // contra otro, no a un plantel contra otro: no hay dorsal ni
            // posicion que buscar, y cada lado tiene un solo nombre para
            // elegir. Un buscador para una sola opcion es friccion de mas
            // en un combate que se decide en segundos — dos chips para
            // tocar cumplen lo mismo sin el paso de abrir un desplegable.
            [
              { key: 'home', label: selMatch?.homeTeamName || 'Rojo', list: rosterHome },
              { key: 'away', label: selMatch?.awayTeamName || 'Azul', list: rosterAway },
            ].map(({ key, label, list }) => (
              <Box key={key} sx={{ minWidth: 180 }}>
                <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 0.5 }}>{label}</Typography>
                <Box sx={{ display: 'flex', gap: 0.5, flexWrap: 'wrap' }}>
                  {list.length === 0 && <Chip label="Sin inscripción" size="small" variant="outlined" disabled />}
                  {list.map((r) => (
                    <Chip
                      key={r.id}
                      label={`${r.lastName}, ${r.firstName}`}
                      color={form.rosterEntryId === r.id ? 'primary' : 'default'}
                      variant={form.rosterEntryId === r.id ? 'filled' : 'outlined'}
                      onClick={() => setForm({ ...form, rosterEntryId: r.id })}
                    />
                  ))}
                </Box>
              </Box>
            ))
          ) : (
            <>
              <Autocomplete
                openOnFocus
                options={rosterHome}
                getOptionLabel={(o) => `#${o.jerseyNumber ?? '?'} ${o.lastName}, ${o.firstName}${o.position ? ' — ' + o.position : ''}`}
                isOptionEqualToValue={(o, v) => o.id === v.id}
                value={rosterHome.find((r) => r.id === form.rosterEntryId) || null}
                onChange={(_, v) => setForm({ ...form, rosterEntryId: v?.id || '' })}
                sx={{ width: 230 }}
                renderInput={(params) => <TextField {...params} inputRef={playerFieldRef} label={selMatch?.homeTeamName || 'Local'} placeholder="Nombre, dorsal o posicion" autoFocus />}
              />
              <Autocomplete
                openOnFocus
                options={rosterAway}
                getOptionLabel={(o) => `#${o.jerseyNumber ?? '?'} ${o.lastName}, ${o.firstName}${o.position ? ' — ' + o.position : ''}`}
                isOptionEqualToValue={(o, v) => o.id === v.id}
                value={rosterAway.find((r) => r.id === form.rosterEntryId) || null}
                onChange={(_, v) => setForm({ ...form, rosterEntryId: v?.id || '' })}
                sx={{ width: 230 }}
                renderInput={(params) => <TextField {...params} label={selMatch?.awayTeamName || 'Visitante'} placeholder="Nombre, dorsal o posicion" />}
              />
            </>
          )}
          <TextField select label="Evento" value={form.metricId} onChange={(e) => setForm({ ...form, metricId: e.target.value })} sx={{ width: 160 }}>
            <MenuItem value="">Seleccionar</MenuItem>
            {(sportInfo?.metrics || []).map((m) => <MenuItem key={m.id} value={m.id}>{m.label}</MenuItem>)}
          </TextField>
          <TextField select label="Periodo" value={form.periodNumber} onChange={(e) => setForm({ ...form, periodNumber: e.target.value })} sx={{ width: 130 }}>
            <MenuItem value="">--</MenuItem>
            {Array.from({ length: sportInfo?.defaultPeriods || 0 }, (_, i) => i + 1).map((n) => (
              <MenuItem key={n} value={n}>{sportInfo.periodLabel} {n}</MenuItem>
            ))}
          </TextField>
          <TextField label="Minuto" type="number" value={form.minute} onChange={(e) => setForm({ ...form, minute: e.target.value })} sx={{ width: 90 }} />
          <TextField label="Cant." type="number" value={form.quantity} onChange={(e) => setForm({ ...form, quantity: e.target.value })} sx={{ width: 80 }} />
          {/* Habilitado con jugador y evento cargados — minuto y periodo son opcionales según el deporte. */}
          <Button
            variant="contained"
            startIcon={<Iconify icon="eva:plus-fill" />}
            onClick={doEvent}
            disabled={loading || !form.rosterEntryId || !form.metricId}
            sx={{ height: 56 }}
          >
            Agregar
          </Button>
        </Box>
        <DataGrid rows={events || []} columns={[
          { field: 'minute', headerName: 'Min', width: 60, renderCell: ({ value }) => value != null ? `${value}'` : '--' },
          { field: 'periodNumber', headerName: 'Per', width: 50 },
          { field: 'firstName', headerName: individual ? 'Deportista' : 'Jugador', flex: 1, renderCell: ({ row }) => (
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
              <span>{row.firstName || ''} {row.lastName || ''}</span>
              {/* Generico, no "AG" (autogol): countsForOpponent es el mismo
                  mecanismo para un autogol en futbol que para un gam-jeom en
                  taekwondo, donde "autogol" no significa nada. */}
              {row.countsForOpponent && <Chip label="RIVAL" size="small" color="error" variant="outlined" sx={{ height: 18, '& .MuiChip-label': { px: 0.6, fontSize: 10, fontWeight: 700 } }} />}
            </Box>
          ) },
          // Dorsal y equipo no dicen nada en un deporte individual: no hay
          // camiseta numerada, y el "equipo" es el mismo nombre que ya
          // muestra la columna del deportista (IndividualTeamName en el
          // backend) — repetirlo aca solo ocupa lugar de la grilla.
          !individual && { field: 'jerseyNumber', headerName: 'Dorsal', width: 65 },
          // countsForOpponent lo carga alguien de un equipo pero cuenta para
          // el otro (autogol en futbol, gam-jeom en taekwondo): se muestra
          // el equipo al que le sirvio (igual que el marcador en vivo ya lo
          // cuenta), no el plantel de quien lo cargo — la etiqueta RIVAL de
          // al lado aclara quien lo hizo realmente.
          !individual && { field: 'teamName', headerName: 'Equipo', width: 120, renderCell: ({ row }) => {
            if (!row.countsForOpponent || !selMatch) return row.teamName;
            return row.teamId === selMatch.homeTeamId ? selMatch.awayTeamName : selMatch.homeTeamName;
          } },
          { field: 'metricLabel', headerName: 'Evento', width: 120 },
          { field: 'quantity', headerName: 'Cant.', width: 60 },
          { field: 'actions', headerName: 'Acciones', width: 90, align: 'center', headerAlign: 'center', renderCell: ({ row }) => <IconButton size="small" onClick={() => delEvent(row.id)}><Iconify icon="eva:trash-2-outline" width={16} sx={{ color: 'error.main' }} /></IconButton> },
        ].filter(Boolean)} autoHeight hideFooter disableRowSelectionOnClick getRowId={(r) => r.id} />
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cerrar</Button>
      </DialogActions>
    </Dialog>
  );
}

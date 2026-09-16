import { useEffect, useState } from 'react';
import Dialog from '@mui/material/Dialog';
import DialogTitle from '@mui/material/DialogTitle';
import DialogContent from '@mui/material/DialogContent';
import DialogActions from '@mui/material/DialogActions';
import TextField from '@mui/material/TextField';
import Button from '@mui/material/Button';
import IconButton from '@mui/material/IconButton';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import { Iconify } from 'src/components/iconify';
import { apiPost } from 'src/hooks/use-api';
import { endpoints } from 'src/lib/axios';
import { aFechaInput } from 'src/lib/format-date';
import { SelectionField, SelectionSpace } from 'src/components/selectors';
import { toast } from 'sonner';

/**
 * Mueve varios partidos a la vez, validados como la disposición final en la
 * que quedarían juntos — no uno a la vez. RescheduleMatch (el diálogo
 * "Reprogramar" de siempre) ya impide pisar una cancha ocupada, y eso mismo
 * es lo que vuelve tedioso un intercambio a mano: mover el partido A al
 * horario del B choca porque el B todavía está ahí, así que hay que
 * adivinar que primero hay que sacar al B a un lugar libre. Acá se cargan
 * los movimientos que se quieran hacer juntos y se validan todos contra la
 * disposición en la que terminarían — un intercambio entre dos partidos
 * nunca choca a mitad de camino. Ver BulkRescheduleConflicts del lado del
 * backend.
 *
 * Deliberadamente no permite cambiar los equipos de un partido — eso sigue
 * siendo trabajo de EditDialog, uno a la vez.
 */
export function BulkRescheduleDialog({ open, onClose, matches, mutate, loading, setLoading, error, setError }) {
  const [rows, setRows] = useState([]);

  useEffect(() => {
    if (open) setRows([]);
  }, [open]);

  const disponibles = (matches || []).filter((m) => !rows.some((r) => r.matchId === m.id));
  const opcionesPartido = disponibles.map((m) => ({
    value: m.id,
    label: `${m.homeTeamName} vs ${m.awayTeamName} — ${m.scheduledAt ? aFechaInput(m.scheduledAt).replace('T', ' ') : 'sin fecha'}`,
  }));

  const agregarPartido = (e) => {
    const matchId = e.target.value;
    if (!matchId) return;
    const m = matches.find((x) => x.id === matchId);
    if (!m) return;
    setRows((rs) => [...rs, {
      matchId,
      homeTeamName: m.homeTeamName,
      awayTeamName: m.awayTeamName,
      venueSpaceId: m.venueSpaceId || '',
      scheduledAt: aFechaInput(m.scheduledAt),
      roundNumber: m.roundNumber ?? '',
    }]);
  };

  const quitarFila = (i) => setRows((rs) => rs.filter((_, idx) => idx !== i));
  const editarFila = (i, patch) => setRows((rs) => rs.map((r, idx) => (idx === i ? { ...r, ...patch } : r)));

  const doReschedule = async () => {
    setLoading(true); setError('');
    try {
      const r = await apiPost(endpoints.matchesRescheduleBulk, {
        moves: rows.map((row) => ({
          matchId: row.matchId,
          venueSpaceId: row.venueSpaceId || null,
          // Mismo criterio que EditDialog: el input da la hora local sin
          // zona, y lo que guarda el partido es un instante real.
          scheduledAt: row.scheduledAt ? new Date(row.scheduledAt).toISOString() : null,
          roundNumber: row.roundNumber !== '' ? Number(row.roundNumber) : null,
        })),
      });
      onClose(); mutate(); toast.success(`Partidos reprogramados: ${r.moved}.`);
    } catch (err) { setError(err.message); }
    finally { setLoading(false); }
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="md" fullWidth>
      <DialogTitle>Reprogramar en bloque</DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: '16px !important' }}>
        {error && <Alert severity="error">{error}</Alert>}
        <Alert severity="info">
          Agregá los partidos que quiera mover juntos y editá su cancha, fecha o jornada. Se validan como la
          disposición final entre todos ellos — un intercambio de horarios entre dos partidos no choca con el
          que todavía no se movió.
        </Alert>

        <SelectionField
          label="Agregar partido"
          value=""
          onChange={agregarPartido}
          options={opcionesPartido}
          emptyLabel={opcionesPartido.length ? 'Elegir...' : 'No hay mas partidos para agregar'}
        />

        {rows.map((row, i) => (
          <Box
            key={row.matchId}
            sx={{ display: 'flex', gap: 1, alignItems: 'flex-start', flexWrap: 'wrap', border: '1px solid', borderColor: 'divider', borderRadius: 1, p: 1.5 }}
          >
            <Typography variant="body2" sx={{ minWidth: 160, flex: '0 0 auto', mt: 1.5 }}>
              {row.homeTeamName} vs {row.awayTeamName}
            </Typography>
            <Box sx={{ flex: 1, minWidth: 180 }}>
              <SelectionSpace value={row.venueSpaceId} onChange={(e) => editarFila(i, { venueSpaceId: e.target.value })} size="small" />
            </Box>
            <TextField
              label="Fecha y hora" type="datetime-local" size="small" value={row.scheduledAt}
              onChange={(e) => editarFila(i, { scheduledAt: e.target.value })}
              sx={{ flex: 1, minWidth: 200 }} slotProps={{ inputLabel: { shrink: true } }}
            />
            <TextField
              label="Ronda" type="number" size="small" value={row.roundNumber}
              onChange={(e) => editarFila(i, { roundNumber: e.target.value })} sx={{ width: 90 }}
            />
            <IconButton size="small" onClick={() => quitarFila(i)} sx={{ mt: 0.5 }}>
              <Iconify icon="eva:trash-2-outline" width={18} sx={{ color: 'error.main' }} />
            </IconButton>
          </Box>
        ))}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancelar</Button>
        <Button variant="contained" onClick={doReschedule} disabled={loading || rows.length === 0}>Guardar</Button>
      </DialogActions>
    </Dialog>
  );
}

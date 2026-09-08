import { useEffect, useState } from 'react';
import Dialog from '@mui/material/Dialog';
import DialogTitle from '@mui/material/DialogTitle';
import DialogContent from '@mui/material/DialogContent';
import DialogActions from '@mui/material/DialogActions';
import TextField from '@mui/material/TextField';
import Button from '@mui/material/Button';
import Alert from '@mui/material/Alert';
import { apiPut } from 'src/hooks/use-api';
import { endpoints } from 'src/lib/axios';
import { SelectionSpace } from 'src/components/selectors';
import { toast } from 'sonner';

// El input datetime-local quiere hora local sin zona ("2026-05-01T16:00"), y
// lo que guarda el partido es una fecha con zona ("...Z" o "+00:00").
// Formatear a mano evita el redondeo raro que a veces da toISOString con la
// hora local.
function aInputFecha(iso) {
  if (!iso) return '';
  const d = new Date(iso);
  const pad = (n) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

/** Reprograma un partido: sede, fecha y ronda. */
export function EditDialog({ open, onClose, selMatch, mutate, loading, setLoading, error, setError }) {
  const [form, setForm] = useState({ venueSpaceId: '', scheduledAt: '', roundNumber: '', notes: '' });

  useEffect(() => {
    if (open && selMatch) {
      setForm({
        venueSpaceId: selMatch.venueSpaceId || '',
        scheduledAt: aInputFecha(selMatch.scheduledAt),
        roundNumber: selMatch.roundNumber ?? '',
        notes: selMatch.notes || '',
      });
    }
  }, [open, selMatch]);

  const doEdit = async () => {
    if (!selMatch) return;
    setLoading(true); setError('');
    try {
      await apiPut(endpoints.match(selMatch.id), {
        homeTeamId: selMatch.homeTeamId,
        awayTeamId: selMatch.awayTeamId,
        venueSpaceId: form.venueSpaceId || null,
        scheduledAt: form.scheduledAt ? new Date(form.scheduledAt).toISOString() : null,
        roundNumber: form.roundNumber !== '' ? Number(form.roundNumber) : null,
        phase: selMatch.phase || null,
        notes: form.notes || null,
      });
      onClose(); mutate(); toast.success('Partido reprogramado.');
    } catch (err) { setError(err.message); }
    finally { setLoading(false); }
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Reprogramar Partido</DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: '16px !important' }}>
        {error && <Alert severity="error">{error}</Alert>}
        {selMatch && <Alert severity="info">{selMatch.homeTeamName} vs {selMatch.awayTeamName}</Alert>}
        <SelectionSpace value={form.venueSpaceId} onChange={(e) => setForm({ ...form, venueSpaceId: e.target.value })} />
        <TextField label="Fecha y hora" type="datetime-local" value={form.scheduledAt} onChange={(e) => setForm({ ...form, scheduledAt: e.target.value })} fullWidth slotProps={{ inputLabel: { shrink: true } }} />
        <TextField
          label="Ronda"
          type="number"
          value={form.roundNumber}
          onChange={(e) => setForm({ ...form, roundNumber: e.target.value })}
          fullWidth
          helperText="Cambiar la ronda es lo que reordena en que jornada se juega este partido."
        />
        <TextField label="Notas" value={form.notes} onChange={(e) => setForm({ ...form, notes: e.target.value })} fullWidth multiline minRows={2} />
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancelar</Button>
        <Button variant="contained" onClick={doEdit} disabled={loading}>Guardar</Button>
      </DialogActions>
    </Dialog>
  );
}

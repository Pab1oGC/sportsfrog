import { useEffect, useState } from 'react';
import Dialog from '@mui/material/Dialog';
import DialogTitle from '@mui/material/DialogTitle';
import DialogContent from '@mui/material/DialogContent';
import DialogActions from '@mui/material/DialogActions';
import TextField from '@mui/material/TextField';
import Button from '@mui/material/Button';
import { apiPost } from 'src/hooks/use-api';
import { endpoints } from 'src/lib/axios';
import { SelectionTeam, SelectionSpace } from 'src/components/selectors';
import { DateTimeField } from 'src/components/date-field';

const EMPTY_FORM = { homeTeamId: '', awayTeamId: '', venueSpaceId: '', scheduledAt: '', roundNumber: '', notes: '' };

/** Programa un partido nuevo entre dos equipos de la categoria elegida. */
export function ScheduleDialog({ open, onClose, cascade, mutate, loading, setLoading, setError }) {
  const [form, setForm] = useState(EMPTY_FORM);

  useEffect(() => {
    if (open) setForm(EMPTY_FORM);
  }, [open]);

  const doSchedule = async () => {
    setLoading(true); setError('');
    try {
      await apiPost(endpoints.categoryMatches(cascade.catId), {
        homeTeamId: form.homeTeamId, awayTeamId: form.awayTeamId,
        venueSpaceId: form.venueSpaceId || null,
        // El input da la hora local sin zona; el backend guarda un instante
        // real, asi que hay que decirle cual es antes de mandarlo.
        scheduledAt: form.scheduledAt ? new Date(form.scheduledAt).toISOString() : null,
        roundNumber: form.roundNumber ? Number(form.roundNumber) : null,
      });
      onClose(); mutate();
    } catch (err) { setError(err.message); }
    finally { setLoading(false); }
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Programar Partido</DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: '16px !important' }}>
        <SelectionTeam categoryId={cascade.catId} value={form.homeTeamId} onChange={(e) => setForm({ ...form, homeTeamId: e.target.value })} label="Local" required />
        <SelectionTeam categoryId={cascade.catId} value={form.awayTeamId} onChange={(e) => setForm({ ...form, awayTeamId: e.target.value })} label="Visitante" required />
        <SelectionSpace value={form.venueSpaceId} onChange={(e) => setForm({ ...form, venueSpaceId: e.target.value })} />
        <DateTimeField label="Fecha y hora" value={form.scheduledAt} onChange={(e) => setForm({ ...form, scheduledAt: e.target.value })} fullWidth />
        <TextField label="Ronda" type="number" value={form.roundNumber} onChange={(e) => setForm({ ...form, roundNumber: e.target.value })} fullWidth />
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancelar</Button>
        <Button variant="contained" onClick={doSchedule} disabled={loading}>Guardar</Button>
      </DialogActions>
    </Dialog>
  );
}

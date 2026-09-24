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
import { bloquearNoEnteros, soloDigitos } from 'src/lib/entero-sin-signo';
import { SelectionSpace } from 'src/components/selectors';
import { DateField } from 'src/components/date-field';
import { toast } from 'sonner';

const EMPTY_FORM = { venueSpaceId: '', scheduledOn: '', orderNumber: '' };

/**
 * Ubica a un competidor en el orden de turno de una clasificacion: que
 * tapete, que dia, y que numero de turno dentro de ese tapete ese dia.
 *
 * Sin hora exacta a proposito: una actuacion de poomsae dura apenas un
 * minuto, una detras de otra, asi que una hora que nadie va a respetar no
 * es mas honesta que no tener ninguna. Ver PerformancePolicy del lado del
 * backend.
 */
export function ScheduleDialog({ open, onClose, row, mutate }) {
  const [form, setForm] = useState(EMPTY_FORM);
  const [error, setError] = useState('');
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (open && row) {
      setForm({
        venueSpaceId: row.venueSpaceId || '',
        scheduledOn: row.scheduledOn || '',
        orderNumber: row.orderNumber ?? '',
      });
      setError('');
    }
  }, [open, row]);

  const doSchedule = async () => {
    if (!row) return;
    setSaving(true); setError('');
    try {
      await apiPut(endpoints.performanceSchedule(row.performanceId), {
        venueSpaceId: form.venueSpaceId || null,
        scheduledOn: form.scheduledOn || null,
        orderNumber: form.orderNumber !== '' ? Number(form.orderNumber) : null,
      });
      onClose(); mutate(); toast.success('Turno guardado.');
    } catch (err) { setError(err.message); }
    finally { setSaving(false); }
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Ubicar en el orden de turno</DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: '16px !important' }}>
        {error && <Alert severity="error">{error}</Alert>}
        {row && <Alert severity="info">{row.teamName}</Alert>}
        <SelectionSpace value={form.venueSpaceId} onChange={(e) => setForm({ ...form, venueSpaceId: e.target.value })} />
        <DateField
          label="Día" value={form.scheduledOn}
          onChange={(e) => setForm({ ...form, scheduledOn: e.target.value })}
          fullWidth
        />
        <TextField
          label="Turno" type="number" value={form.orderNumber}
          onChange={(e) => setForm({ ...form, orderNumber: soloDigitos(e.target.value) })}
          onKeyDown={bloquearNoEnteros}
          slotProps={{ htmlInput: { min: 1, step: 1 } }}
          fullWidth helperText="Su posición en el orden de ese tapete, ese día."
        />
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancelar</Button>
        <Button variant="contained" onClick={doSchedule} disabled={saving}>Guardar</Button>
      </DialogActions>
    </Dialog>
  );
}

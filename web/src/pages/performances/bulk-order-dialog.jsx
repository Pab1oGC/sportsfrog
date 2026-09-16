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
import { SelectionField, SelectionSpace } from 'src/components/selectors';
import { DateField } from 'src/components/date-field';
import { toast } from 'sonner';

/**
 * Arma el orden de turno de varios competidores a la vez, validado como la
 * disposición final en la que quedarían juntos — no uno a la vez. Mismo
 * problema que Matches/BulkRescheduleDialog resuelve para un calendario:
 * intercambiar el turno de dos competidores pidiendo un ScheduleDialog por
 * vez siempre choca, porque el primero intenta tomar el lugar del segundo
 * mientras el segundo todavía lo tiene. Ver PerformanceOrderConflicts del
 * lado del backend.
 */
export function BulkOrderDialog({ open, onClose, rows, mutate }) {
  const [items, setItems] = useState([]);
  const [error, setError] = useState('');
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (open) { setItems([]); setError(''); }
  }, [open]);

  const disponibles = (rows || []).filter((r) => !items.some((i) => i.performanceId === r.performanceId));
  const opcionesCompetidor = disponibles.map((r) => ({
    value: r.performanceId,
    label: r.orderNumber ? `${r.teamName} — turno ${r.orderNumber}` : `${r.teamName} — sin turno`,
  }));

  const agregarCompetidor = (e) => {
    const performanceId = e.target.value;
    if (!performanceId) return;
    const r = rows.find((x) => x.performanceId === performanceId);
    if (!r) return;
    setItems((its) => [...its, {
      performanceId,
      teamName: r.teamName,
      venueSpaceId: r.venueSpaceId || '',
      scheduledOn: r.scheduledOn || '',
      orderNumber: r.orderNumber ?? '',
    }]);
  };

  const quitarFila = (i) => setItems((its) => its.filter((_, idx) => idx !== i));
  const editarFila = (i, patch) => setItems((its) => its.map((it, idx) => (idx === i ? { ...it, ...patch } : it)));

  const doReorder = async () => {
    setSaving(true); setError('');
    try {
      const r = await apiPost(endpoints.performancesRescheduleBulk, {
        moves: items.map((it) => ({
          performanceId: it.performanceId,
          venueSpaceId: it.venueSpaceId || null,
          scheduledOn: it.scheduledOn || null,
          orderNumber: it.orderNumber !== '' ? Number(it.orderNumber) : null,
        })),
      });
      onClose(); mutate(); toast.success(`Turnos ubicados: ${r.moved}.`);
    } catch (err) { setError(err.message); }
    finally { setSaving(false); }
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="md" fullWidth>
      <DialogTitle>Reprogramar en bloque</DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: '16px !important' }}>
        {error && <Alert severity="error">{error}</Alert>}
        <Alert severity="info">
          Agregá los competidores que quiera ubicar juntos y editá su tapete, día o turno. Se validan
          como el orden final entre todos ellos — intercambiar el turno de dos competidores no choca
          con el que todavía no se movió.
        </Alert>

        <SelectionField
          label="Agregar competidor"
          value=""
          onChange={agregarCompetidor}
          options={opcionesCompetidor}
          emptyLabel={opcionesCompetidor.length ? 'Elegir...' : 'No hay mas competidores para agregar'}
        />

        {items.map((it, i) => (
          <Box
            key={it.performanceId}
            sx={{ display: 'flex', gap: 1, alignItems: 'flex-start', flexWrap: 'wrap', border: '1px solid', borderColor: 'divider', borderRadius: 1, p: 1.5 }}
          >
            <Typography variant="body2" sx={{ minWidth: 140, flex: '0 0 auto', mt: 1.5 }}>
              {it.teamName}
            </Typography>
            <Box sx={{ flex: 1, minWidth: 180 }}>
              <SelectionSpace value={it.venueSpaceId} onChange={(e) => editarFila(i, { venueSpaceId: e.target.value })} size="small" />
            </Box>
            <DateField
              label="Día" size="small" value={it.scheduledOn}
              onChange={(e) => editarFila(i, { scheduledOn: e.target.value })}
              sx={{ flex: 1, minWidth: 160 }}
            />
            <TextField
              label="Turno" type="number" size="small" value={it.orderNumber}
              onChange={(e) => editarFila(i, { orderNumber: e.target.value })} sx={{ width: 90 }}
            />
            <IconButton size="small" onClick={() => quitarFila(i)} sx={{ mt: 0.5 }}>
              <Iconify icon="eva:trash-2-outline" width={18} sx={{ color: 'error.main' }} />
            </IconButton>
          </Box>
        ))}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancelar</Button>
        <Button variant="contained" onClick={doReorder} disabled={saving || items.length === 0}>Guardar</Button>
      </DialogActions>
    </Dialog>
  );
}

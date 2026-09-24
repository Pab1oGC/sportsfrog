import { useEffect, useState } from 'react';
import Box from '@mui/material/Box';
import Dialog from '@mui/material/Dialog';
import DialogTitle from '@mui/material/DialogTitle';
import DialogContent from '@mui/material/DialogContent';
import DialogActions from '@mui/material/DialogActions';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import Alert from '@mui/material/Alert';
import { apiPut } from 'src/hooks/use-api';
import { endpoints } from 'src/lib/axios';
import { bloquearNoEnteros, soloDigitos } from 'src/lib/entero-sin-signo';
import { toast } from 'sonner';

/** Carga el desempate por penales de un cruce de eliminatoria empatado. */
export function PenaltiesDialog({ open, onClose, selMatch, mutate, loading, setLoading, error, setError }) {
  const [form, setForm] = useState({ homeScore: 0, awayScore: 0 });

  useEffect(() => {
    if (open && selMatch) setForm({ homeScore: selMatch.penaltyHomeScore ?? 0, awayScore: selMatch.penaltyAwayScore ?? 0 });
  }, [open, selMatch]);

  const doPenalties = async () => {
    if (!selMatch) return;
    setLoading(true); setError('');
    try {
      await apiPut(endpoints.matchPenalties(selMatch.id), { homeScore: Number(form.homeScore), awayScore: Number(form.awayScore) });
      onClose(); mutate(); toast.success('Desempate por penales registrado.');
    } catch (err) { setError(err.message); }
    finally { setLoading(false); }
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
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
          <TextField
            label={selMatch?.homeTeamName || 'Local'} type="number" value={form.homeScore} fullWidth
            onChange={(e) => setForm({ ...form, homeScore: soloDigitos(e.target.value) })}
            onKeyDown={bloquearNoEnteros}
            slotProps={{ htmlInput: { min: 0, step: 1 } }}
          />
          <Typography>-</Typography>
          <TextField
            label={selMatch?.awayTeamName || 'Visitante'} type="number" value={form.awayScore} fullWidth
            onChange={(e) => setForm({ ...form, awayScore: soloDigitos(e.target.value) })}
            onKeyDown={bloquearNoEnteros}
            slotProps={{ htmlInput: { min: 0, step: 1 } }}
          />
        </Box>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancelar</Button>
        <Button variant="contained" color="secondary" onClick={doPenalties} disabled={loading}>Confirmar</Button>
      </DialogActions>
    </Dialog>
  );
}

import { useEffect, useState } from 'react';
import Dialog from '@mui/material/Dialog';
import DialogTitle from '@mui/material/DialogTitle';
import DialogContent from '@mui/material/DialogContent';
import DialogActions from '@mui/material/DialogActions';
import TextField from '@mui/material/TextField';
import MenuItem from '@mui/material/MenuItem';
import Button from '@mui/material/Button';
import Alert from '@mui/material/Alert';
import { apiPut } from 'src/hooks/use-api';
import { endpoints } from 'src/lib/axios';
import { toast } from 'sonner';

/**
 * Otorga un partido por walkover.
 *
 * `selMatch`, `loading` y `error` vienen de MatchesPage: los tres se
 * comparten con el resto de los dialogos de la pantalla (que fila esta
 * seleccionada, si hay una accion en curso, el error a mostrar), asi que no
 * tiene sentido que este dialogo tenga su propia copia.
 */
export function WalkoverDialog({ open, onClose, selMatch, mutate, loading, setLoading, setError }) {
  const [form, setForm] = useState({ winnerTeamId: '', notes: '' });

  useEffect(() => {
    if (open && selMatch) setForm({ winnerTeamId: selMatch.homeTeamId, notes: '' });
  }, [open, selMatch]);

  const doWalkover = async () => {
    if (!selMatch) return;
    setLoading(true); setError('');
    try {
      await apiPut(endpoints.matchWalkover(selMatch.id), form);
      onClose(); mutate(); toast.success('Walkover registrado.');
    } catch (err) { setError(err.message); }
    finally { setLoading(false); }
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Walkover</DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: '16px !important' }}>
        {selMatch && <Alert severity="warning">Se asignara el partido.</Alert>}
        <TextField select label="Ganador" value={form.winnerTeamId} onChange={(e) => setForm({ ...form, winnerTeamId: e.target.value })} fullWidth>
          {selMatch && [<MenuItem key="h" value={selMatch.homeTeamId}>{selMatch.homeTeamName} (Local)</MenuItem>, <MenuItem key="a" value={selMatch.awayTeamId}>{selMatch.awayTeamName} (Visitante)</MenuItem>]}
        </TextField>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancelar</Button>
        <Button variant="contained" color="warning" onClick={doWalkover} disabled={loading}>Confirmar</Button>
      </DialogActions>
    </Dialog>
  );
}

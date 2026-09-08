import { useEffect, useState } from 'react';
import Dialog from '@mui/material/Dialog';
import DialogTitle from '@mui/material/DialogTitle';
import DialogContent from '@mui/material/DialogContent';
import DialogActions from '@mui/material/DialogActions';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import Alert from '@mui/material/Alert';
import { apiPost } from 'src/hooks/use-api';
import { endpoints } from 'src/lib/axios';

const EMPTY_FORM = { qualifiersPerGroup: 2, bestThirdPlaced: 0 };

/** Arma la llave de eliminatoria a partir de una fase de grupos terminada. */
export function PromoteDialog({ open, onClose, cascade, mutate, loading, setLoading, error, setError }) {
  const [form, setForm] = useState(EMPTY_FORM);
  const [result, setResult] = useState(null);

  useEffect(() => {
    if (open) { setForm(EMPTY_FORM); setResult(null); }
  }, [open]);

  const doPromote = async () => {
    setLoading(true); setError('');
    try {
      const r = await apiPost(endpoints.categoryPromoteGroupStage(cascade.catId), {
        qualifiersPerGroup: Number(form.qualifiersPerGroup),
        bestThirdPlaced: Number(form.bestThirdPlaced),
      });
      mutate(); setResult(r);
    } catch (err) { setError(err.message); }
    finally { setLoading(false); }
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>{result ? 'Eliminatoria sorteada' : 'Promover a eliminatoria'}</DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: '16px !important' }}>
        {result ? (
          <>
            <Alert severity="success">
              {result.created} partido(s) creados — {result.direct} clasificado(s) directo(s)
              {result.wildcards > 0 ? ` + ${result.wildcards} mejor(es) ubicado(s)` : ''}.
            </Alert>
            {result.byes > 0 && <Alert severity="info">{result.byes} equipo(s) pasa(n) sin jugar la primera ronda.</Alert>}
            {result.replaced > 0 && <Alert severity="warning">Se reemplazo una eliminatoria sorteada antes ({result.replaced} partidos).</Alert>}
            {result.repeatedMatchups > 0 && (
              <Alert severity="warning">
                {result.repeatedMatchups} cruce(s) repite(n) un partido de la fase de grupos: los numeros no daban para evitarlo.
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
              value={form.qualifiersPerGroup}
              onChange={(e) => setForm({ ...form, qualifiersPerGroup: e.target.value })}
              fullWidth
              slotProps={{ htmlInput: { min: 1, max: 8 } }}
            />
            <TextField
              label="Mejores ubicados adicionales (mejores terceros, etc.)"
              type="number"
              value={form.bestThirdPlaced}
              onChange={(e) => setForm({ ...form, bestThirdPlaced: e.target.value })}
              fullWidth
              helperText="Opcional: cupos extra para los mejores equipos que no clasificaron directo, comparados entre grupos."
              slotProps={{ htmlInput: { min: 0, max: 16 } }}
            />
          </>
        )}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>{result ? 'Listo' : 'Cancelar'}</Button>
        {!result && <Button variant="contained" onClick={doPromote} disabled={loading}>{loading ? 'Sorteando...' : 'Sortear eliminatoria'}</Button>}
      </DialogActions>
    </Dialog>
  );
}

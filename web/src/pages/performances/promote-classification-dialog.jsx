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
import { nombreFase } from 'src/lib/phase-labels';

const MINIMO_CLASIFICAN = 2;

/**
 * Arma la llave de eliminatoria a partir de una clasificacion terminada --
 * el equivalente juzgado de PromoteDialog (matches/promote-dialog.jsx) para
 * PromoteGroupStage, con el mismo molde: formulario, despues resultado en el
 * mismo dialogo.
 */
export function PromoteClassificationDialog({ open, onClose, catId, onPromoted }) {
  const [qualifiers, setQualifiers] = useState(MINIMO_CLASIFICAN);
  const [result, setResult] = useState(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    if (open) { setQualifiers(MINIMO_CLASIFICAN); setResult(null); setError(''); }
  }, [open]);

  const doPromote = async () => {
    setLoading(true); setError('');
    try {
      const r = await apiPost(endpoints.categoryPromoteClassification(catId), { qualifiers: Number(qualifiers) });
      onPromoted();
      setResult(r);
    } catch (err) { setError(err.message); }
    finally { setLoading(false); }
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>{result ? 'Eliminatoria sorteada' : 'Sortear eliminatoria'}</DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: '16px !important' }}>
        {result ? (
          <>
            <Alert severity="success">
              {result.created} partido(s) creados{result.phase ? ` (${nombreFase(result.phase)})` : ''} — {result.qualified} clasificado(s).
            </Alert>
            {/* ClassificationAdvancement nunca parte un empate en el corte:
                pedir 4 puede clasificar 5 si el cuarto y el quinto empataron
                en puntaje. No es un error, es la regla -- se dice aca en vez
                de dejar que el numero de mas parezca un fallo del sorteo. */}
            {result.qualified > qualifiers && (
              <Alert severity="info">
                Clasificaron {result.qualified} en vez de {qualifiers}: hubo un empate en el corte, y no se
                reparte -- todos los que empataron en ese puesto pasan.
              </Alert>
            )}
            {result.byes > 0 && <Alert severity="info">{result.byes} competidor(es) pasa(n) sin jugar la primera ronda.</Alert>}
            {result.replaced > 0 && <Alert severity="warning">Se reemplazó una eliminatoria sorteada antes ({result.replaced} partidos).</Alert>}
          </>
        ) : (
          <>
            {error && <Alert severity="error">{error}</Alert>}
            <Typography variant="body2" color="text.secondary">
              Arma la llave con los mejores puntajes de la clasificación, una vez que todos los competidores
              tengan puntaje cargado.
            </Typography>
            <TextField
              label="Cuántos clasifican"
              type="number"
              value={qualifiers}
              onChange={(e) => setQualifiers(e.target.value)}
              fullWidth
              helperText="Un empate en el corte nunca se parte: puede clasificar alguno más que este número."
              slotProps={{ htmlInput: { min: MINIMO_CLASIFICAN } }}
            />
          </>
        )}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>{result ? 'Listo' : 'Cancelar'}</Button>
        {!result && (
          <Button variant="contained" onClick={doPromote} disabled={loading || Number(qualifiers) < MINIMO_CLASIFICAN}>
            {loading ? 'Sorteando...' : 'Sortear'}
          </Button>
        )}
      </DialogActions>
    </Dialog>
  );
}

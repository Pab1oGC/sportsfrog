import { useEffect, useState } from 'react';
import Box from '@mui/material/Box';
import Dialog from '@mui/material/Dialog';
import DialogTitle from '@mui/material/DialogTitle';
import DialogContent from '@mui/material/DialogContent';
import DialogActions from '@mui/material/DialogActions';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import IconButton from '@mui/material/IconButton';
import Alert from '@mui/material/Alert';
import Divider from '@mui/material/Divider';
import { Iconify } from 'src/components/iconify';
import { apiPut } from 'src/hooks/use-api';
import { endpoints } from 'src/lib/axios';
import { toast } from 'sonner';

// Un deporte de suma (futbol, basquet) siempre juega todos sus periodos
// configurados: la cantidad del deporte es la cantidad correcta. Uno por
// sets rara vez llega al maximo (una mejor-de-cinco que termina 3-0 solo jugo
// tres), asi que ahi se arranca en uno y el dialogo deja agregar los que
// hagan falta.
function periodScoresIniciales(selMatch, sportInfo) {
  if (selMatch.periodScores?.length) {
    return selMatch.periodScores.map((p) => ({ period: p.period, home: p.home, away: p.away }));
  }
  const cantidad = sportInfo?.isPlayedInSets ? 1 : (sportInfo?.defaultPeriods || 2);
  return Array.from({ length: cantidad }, (_, i) => ({ period: i + 1, home: 0, away: 0 }));
}

/** Registra el resultado de un partido, periodo por periodo. */
export function ResultDialog({ open, onClose, selMatch, sportInfo, mutate, loading, setLoading, setError }) {
  const [form, setForm] = useState({ periodScores: [], notes: '' });

  // Deliberadamente sin sportInfo en las dependencias: esto arma el
  // formulario una sola vez, al abrir para este partido, igual que hacia el
  // manejador de la fila original. Si dependiera de sportInfo, una
  // revalidacion de SWR mientras el dialogo esta abierto borraria lo que se
  // esta cargando.
  useEffect(() => {
    if (open && selMatch) {
      setForm({ periodScores: periodScoresIniciales(selMatch, sportInfo), notes: selMatch.notes || '' });
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, selMatch]);

  // Solo se ofrecen en un deporte por sets: uno de suma siempre juega
  // exactamente los periodos configurados, asi que ahi la cantidad de filas
  // no es algo que el organizador deba tocar.
  const agregarPeriodo = () => {
    const siguiente = form.periodScores.length + 1;
    setForm({ ...form, periodScores: [...form.periodScores, { period: siguiente, home: 0, away: 0 }] });
  };
  const quitarPeriodo = (i) => {
    if (form.periodScores.length <= 1) return;
    setForm({
      ...form,
      periodScores: form.periodScores.filter((_, idx) => idx !== i).map((p, idx) => ({ ...p, period: idx + 1 })),
    });
  };

  const doResult = async () => {
    if (!selMatch) return;
    setLoading(true); setError('');
    try {
      await apiPut(endpoints.matchResult(selMatch.id), form);
      onClose(); mutate(); toast.success('Resultado registrado.');
    } catch (err) { setError(err.message); }
    finally { setLoading(false); }
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Registrar Resultado</DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: '16px !important' }}>
        {selMatch && <Alert severity="info">{selMatch.homeTeamName} vs {selMatch.awayTeamName}</Alert>}
        {form.periodScores.map((ps, i) => (
          <Box key={i} sx={{ display: 'flex', gap: 2, alignItems: 'center' }}>
            <Typography variant="body2" sx={{ minWidth: 70 }}>P{ps.period}</Typography>
            <TextField label="Loc" type="number" value={ps.home} onChange={(e) => { const s = [...form.periodScores]; s[i] = { ...s[i], home: Number(e.target.value) }; setForm({ ...form, periodScores: s }); }} size="small" sx={{ flex: 1 }} />
            <Typography>-</Typography>
            <TextField label="Vis" type="number" value={ps.away} onChange={(e) => { const s = [...form.periodScores]; s[i] = { ...s[i], away: Number(e.target.value) }; setForm({ ...form, periodScores: s }); }} size="small" sx={{ flex: 1 }} />
            {sportInfo?.isPlayedInSets && (
              <IconButton size="small" disabled={form.periodScores.length <= 1} onClick={() => quitarPeriodo(i)}>
                <Iconify icon="eva:trash-2-outline" sx={{ color: 'error.main' }} />
              </IconButton>
            )}
          </Box>
        ))}
        {sportInfo?.isPlayedInSets && (
          // Uno de suma juega siempre la misma cantidad de periodos, asi que
          // ahi no hay "agregar" que ofrecer.
          <Button size="small" onClick={agregarPeriodo} startIcon={<Iconify icon="eva:plus-fill" />} sx={{ alignSelf: 'flex-start' }}>
            Agregar {(sportInfo.periodLabel || 'período').toLowerCase()}
          </Button>
        )}
        <Divider />
        {/* Bajo sets el marcador del partido es la cantidad de periodos
            ganados por cada lado (ver ScoreConsolidation en el backend), no
            la suma de los puntos de cada set — sumar 25-20, 22-25, 25-18 no
            da un numero que signifique algo. */}
        <Typography fontWeight={600}>
          Total: {sportInfo?.isPlayedInSets
            ? `${form.periodScores.filter((p) => p.home > p.away).length} - ${form.periodScores.filter((p) => p.away > p.home).length}`
            : `${form.periodScores.reduce((s, p) => s + p.home, 0)} - ${form.periodScores.reduce((s, p) => s + p.away, 0)}`}
        </Typography>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancelar</Button>
        <Button variant="contained" color="success" onClick={doResult} disabled={loading}>Registrar</Button>
      </DialogActions>
    </Dialog>
  );
}

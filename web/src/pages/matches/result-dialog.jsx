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
import { esJuzgado, aPuntaje, dePuntaje } from 'src/lib/sport-shape';
import { toast } from 'sonner';

// El backend guarda cada periodo como {p,h,a} -- una letra por clave porque
// PeriodScore documenta que son cientos de estos guardados por temporada
// (ver el tipo en el dominio). Este dialogo lee y escribe {period,home,away}
// puertas adentro, mas legible para el resto del archivo; estas dos
// funciones son el unico lugar donde una forma se convierte en la otra.
//
// Sin esta conversion, PUT /matches/{id}/result nunca funciono: mandaba
// {period,home,away} contra un contrato que exige p/h/a como campos
// obligatorios, y System.Text.Json lo rechaza de entrada (verificado contra
// el tipo real del backend, no asumido). No es parte del modo juzgado -- es
// un bug preexistente en cualquier deporte, encontrado al tocar este mismo
// archivo para agregarlo.
function deApiAPeriodo(p) {
  return { period: p.p, home: p.h, away: p.a };
}
function aPeriodoApi(p) {
  return { p: p.period, h: p.home, a: p.away };
}

// Un deporte de suma (futbol, basquet) siempre juega todos sus periodos
// configurados: la cantidad del deporte es la cantidad correcta. Uno por
// sets rara vez llega al maximo (una mejor-de-cinco que termina 3-0 solo jugo
// tres), asi que ahi se arranca en uno y el dialogo deja agregar los que
// hagan falta. Uno juzgado es siempre una sola actuacion por lado
// (JudgedRulesetShape ya lo exige al guardar el reglamento) -- misma
// respuesta que ya da un deporte por sets, por la misma razon: no hay mas
// que un periodo que jugar.
function periodScoresIniciales(selMatch, sportInfo) {
  if (selMatch.periodScores?.length) {
    return selMatch.periodScores.map(deApiAPeriodo);
  }
  const cantidad = (sportInfo?.isPlayedInSets || esJuzgado(sportInfo)) ? 1 : (sportInfo?.defaultPeriods || 2);
  return Array.from({ length: cantidad }, (_, i) => ({ period: i + 1, home: 0, away: 0 }));
}

// El puntaje de un juez, con su propio borrador de texto -- convertir a
// entero (dePuntaje) en cada tecla y volver a mostrar el resultado
// redondeado (aPuntaje) cortaria "7.65" a "7.60" antes de terminar de
// escribirlo. El campo no se resincroniza con `value` despues del primer
// render: el dialogo entero se desmonta al cerrarse (Dialog sin
// keepMounted), asi que cada apertura ya arranca con un componente nuevo.
function JudgedScoreField({ label, value, onChange }) {
  const [draft, setDraft] = useState(() => aPuntaje(value));

  return (
    <TextField
      label={label}
      type="number"
      value={draft}
      onChange={(e) => {
        setDraft(e.target.value);
        const puntaje = dePuntaje(e.target.value);
        if (puntaje !== null) onChange(puntaje);
      }}
      placeholder="0.00"
      size="small"
      slotProps={{ htmlInput: { min: 0, step: 0.01 } }}
      sx={{ flex: 1 }}
    />
  );
}

/** Registra el resultado de un partido, periodo por periodo. */
export function ResultDialog({ open, onClose, selMatch, sportInfo, mutate, loading, setLoading, error, setError }) {
  const [form, setForm] = useState({ periodScores: [], notes: '' });
  const juzgado = esJuzgado(sportInfo);

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

  const actualizarPeriodo = (i, campo, valor) => {
    const s = [...form.periodScores];
    s[i] = { ...s[i], [campo]: valor };
    setForm({ ...form, periodScores: s });
  };

  const doResult = async () => {
    if (!selMatch) return;
    setLoading(true); setError('');
    try {
      await apiPut(endpoints.matchResult(selMatch.id), {
        periodScores: form.periodScores.map(aPeriodoApi),
        notes: form.notes,
      });
      onClose(); mutate(); toast.success('Resultado registrado.');
    } catch (err) { setError(err.message); }
    finally { setLoading(false); }
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Registrar Resultado</DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: '16px !important' }}>
        {/* JudgedResultShape rechaza un puntaje empatado ("los jueces tienen
            que resolver el empate antes de cargar el resultado") -- el
            unico dialogo de partido sin este Alert todavia, porque hasta
            ahora ningun modo tenia una forma tan facil de pisarlo. */}
        {error && <Alert severity="error">{error}</Alert>}
        {selMatch && <Alert severity="info">{selMatch.homeTeamName} vs {selMatch.awayTeamName}</Alert>}
        {form.periodScores.map((ps, i) => (
          <Box key={i} sx={{ display: 'flex', gap: 2, alignItems: 'center' }}>
            <Typography variant="body2" sx={{ minWidth: 70 }}>P{ps.period}</Typography>
            {juzgado ? (
              <>
                <JudgedScoreField label="Loc" value={ps.home} onChange={(v) => actualizarPeriodo(i, 'home', v)} />
                <Typography>-</Typography>
                <JudgedScoreField label="Vis" value={ps.away} onChange={(v) => actualizarPeriodo(i, 'away', v)} />
              </>
            ) : (
              <>
                <TextField label="Loc" type="number" value={ps.home} onChange={(e) => actualizarPeriodo(i, 'home', Number(e.target.value))} size="small" sx={{ flex: 1 }} />
                <Typography>-</Typography>
                <TextField label="Vis" type="number" value={ps.away} onChange={(e) => actualizarPeriodo(i, 'away', Number(e.target.value))} size="small" sx={{ flex: 1 }} />
              </>
            )}
            {sportInfo?.isPlayedInSets && (
              <IconButton size="small" disabled={form.periodScores.length <= 1} onClick={() => quitarPeriodo(i)}>
                <Iconify icon="eva:trash-2-outline" sx={{ color: 'error.main' }} />
              </IconButton>
            )}
          </Box>
        ))}
        {sportInfo?.isPlayedInSets && (
          // Uno de suma juega siempre la misma cantidad de periodos, asi que
          // ahi no hay "agregar" que ofrecer. Uno juzgado tampoco -- siempre
          // es uno solo, y esta rama no lo incluye.
          <Button size="small" onClick={agregarPeriodo} startIcon={<Iconify icon="eva:plus-fill" />} sx={{ alignSelf: 'flex-start' }}>
            Agregar {(sportInfo.periodLabel || 'período').toLowerCase()}
          </Button>
        )}
        <Divider />
        {/* Bajo sets el marcador del partido es la cantidad de periodos
            ganados por cada lado (ver ScoreConsolidation en el backend), no
            la suma de los puntos de cada set — sumar 25-20, 22-25, 25-18 no
            da un numero que signifique algo. Bajo juzgado es directamente el
            puntaje de cada juez, decodificado -- una sola actuacion, nada
            que consolidar. */}
        <Typography fontWeight={600}>
          Total: {sportInfo?.isPlayedInSets
            ? `${form.periodScores.filter((p) => p.home > p.away).length} - ${form.periodScores.filter((p) => p.away > p.home).length}`
            : juzgado
              ? `${aPuntaje(form.periodScores[0]?.home)} - ${aPuntaje(form.periodScores[0]?.away)}`
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

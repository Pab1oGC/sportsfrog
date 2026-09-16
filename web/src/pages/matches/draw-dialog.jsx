import { useEffect, useState } from 'react';
import Box from '@mui/material/Box';
import Dialog from '@mui/material/Dialog';
import DialogTitle from '@mui/material/DialogTitle';
import DialogContent from '@mui/material/DialogContent';
import DialogActions from '@mui/material/DialogActions';
import TextField from '@mui/material/TextField';
import MenuItem from '@mui/material/MenuItem';
import Checkbox from '@mui/material/Checkbox';
import FormControlLabel from '@mui/material/FormControlLabel';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import Alert from '@mui/material/Alert';
import { apiPost } from 'src/hooks/use-api';
import { endpoints } from 'src/lib/axios';

const FORMATO_LABEL = { league: 'Todos vs todos', knockout: 'Eliminacion directa', groups: 'Fase de grupos' };

/**
 * Sortea el fixture de una categoria, y muestra tambien el resultado de
 * "Siguiente ronda" (MatchesPage.doAdvance): las dos acciones terminan en el
 * mismo tipo de resultado ("se crearon N partidos"), asi que comparten este
 * dialogo en vez de tener uno cada una. Por eso `result` llega como prop en
 * vez de vivir aca adentro — MatchesPage lo escribe desde los dos lugares.
 */
export function DrawDialog({ open, onClose, result, setResult, cascade, formato, teams, mutate, mutateTeams, motivoSinSorteo, loading, setLoading, error, setError, onGenerarJornada }) {
  const [legs, setLegs] = useState(1);
  const [groupCount, setGroupCount] = useState('');
  const [allowSamePot, setAllowSamePot] = useState(false);

  useEffect(() => {
    if (open) { setLegs(1); setGroupCount(''); setAllowSamePot(false); }
  }, [open]);

  // Fase de grupos sin ningun equipo todavia sorteado en un grupo: "Sortear"
  // tiene que empezar por ahi, no fallar pidiendo algo que el propio sorteo
  // deberia resolver.
  const sinGrupos = formato === 'groups' && (teams || []).length > 0 && !(teams || []).some((t) => t.groupLabel);

  // Los bombos se cargan en Equipos, en otro momento — y por eso es facil
  // que un numero quede viejo o suelto. Se muestran aca, junto a la cantidad
  // de grupos, para que sea una sola decision y no dos separadas por dias.
  const bombos = {};
  (teams || []).forEach((t) => {
    const clave = t.seed != null ? t.seed : 'sin';
    (bombos[clave] = bombos[clave] || []).push(t.name);
  });
  const bombosNumerados = Object.keys(bombos).filter((k) => k !== 'sin').map(Number).sort((a, b) => a - b);
  const gruposElegidos = Number(groupCount) || 0;
  // Si se permite que un mismo bombo se enfrente, esa restriccion deja de
  // aplicar del todo: no hay promesa que un bombo grande pueda incumplir.
  const bomboExcedido = !allowSamePot && gruposElegidos > 0 && bombosNumerados.some((b) => bombos[b].length > gruposElegidos);

  const doDraw = async () => {
    if (!cascade.catId) return;
    setLoading(true); setError('');
    try {
      // Una fase de grupos sin nadie sorteado en un grupo todavia: el sorteo
      // tiene dos partes y esta es la primera. Un solo click hace las dos,
      // que es lo que alguien espera de un "sorteo".
      if (sinGrupos) {
        await apiPost(endpoints.categoryDrawGroups(cascade.catId), {
          groupCount: Number(groupCount),
          respectPots: !allowSamePot,
        });
        await mutateTeams();
      }
      const r = await apiPost(endpoints.categoryDraw(cascade.catId), { legs: formato === 'knockout' ? 1 : legs });
      mutate(); setResult(r);
    } catch (err) { setError(err.message); }
    finally { setLoading(false); }
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>{result ? 'Sorteo realizado' : 'Sortear fixture'}</DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: '16px !important' }}>
        {result ? (
          <>
            {result.champion ? <Alert severity="success"><strong>{result.championName}</strong> es el campeon.</Alert>
              : <Alert severity="success">{result.created} partidos creados{result.rounds ? ` en ${result.rounds} jornadas` : ''}{result.round ? ` (ronda ${result.round})` : ''}.</Alert>}
            {result.replaced > 0 && <Alert severity="warning">Se reemplazaron {result.replaced} partidos.</Alert>}
            {result.byes > 0 && <Alert severity="info">{result.byes} equipo(s) pasa(n) sin jugar.</Alert>}
          </>
        ) : (
          <>
            {error && <Alert severity="error">{error}</Alert>}
            {motivoSinSorteo && <Alert severity="error">{motivoSinSorteo}</Alert>}
            <Typography variant="body2">Formato: <strong>{FORMATO_LABEL[formato] || formato}</strong></Typography>
            {sinGrupos && (
              <>
                <Alert severity="info">
                  Todavia ningun equipo tiene grupo asignado. Este sorteo primero los reparte en grupos al azar, y despues arma el fixture de cada uno.
                </Alert>
                <TextField
                  label="Numero de grupos"
                  type="number"
                  value={groupCount}
                  onChange={(e) => setGroupCount(e.target.value)}
                  fullWidth
                  required
                  helperText="Si algun equipo tiene bombo asignado, el sorteo respeta que ninguno se repita en un mismo grupo."
                  slotProps={{ htmlInput: { min: 2, max: 26 } }}
                />
                {bombosNumerados.length > 0 && (
                  <FormControlLabel
                    control={<Checkbox checked={allowSamePot} onChange={(e) => setAllowSamePot(e.target.checked)} />}
                    label="Permitir que equipos del mismo bombo se enfrenten (formato tipo liga de Champions)"
                  />
                )}
                {bombosNumerados.length > 0 && (
                  <Box sx={{ p: 1.5, border: '1px solid', borderColor: 'divider', borderRadius: 1 }}>
                    <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 0.5 }}>
                      Bombos cargados en Equipos
                    </Typography>
                    {bombosNumerados.map((b) => {
                      const equipos = bombos[b];
                      const excede = !allowSamePot && gruposElegidos > 0 && equipos.length > gruposElegidos;
                      return (
                        <Typography key={b} variant="body2" sx={{ color: excede ? 'error.main' : undefined, fontWeight: excede ? 700 : 400 }}>
                          Bombo {b} ({equipos.length}): {equipos.join(', ')}
                          {excede ? ' — supera la cantidad de grupos' : ''}
                        </Typography>
                      );
                    })}
                    {bombos.sin && (
                      <Typography variant="body2" color="text.secondary">
                        Sin bombo ({bombos.sin.length}): {bombos.sin.join(', ')}
                      </Typography>
                    )}
                  </Box>
                )}
              </>
            )}
            {formato !== 'knockout' && (
              <TextField select label="Vueltas" value={legs} onChange={(e) => setLegs(Number(e.target.value))} fullWidth>
                <MenuItem value={1}>Una vuelta</MenuItem><MenuItem value={2}>Ida y vuelta</MenuItem>
              </TextField>
            )}
          </>
        )}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>{result ? 'Listo' : 'Cancelar'}</Button>
        {!result && <Button variant="contained" onClick={doDraw} disabled={loading || !!motivoSinSorteo || (sinGrupos && (!groupCount || bomboExcedido))}>{loading ? 'Sorteando...' : 'Sortear'}</Button>}
        {/* Un fixture recien armado casi siempre necesita fecha y cancha a
            continuacion -- separarlo en un segundo click, en otro boton, era
            justo la friccion que llevaba a sortear una y otra vez sin encontrar
            nunca la forma de programar el calendario. No tiene sentido si ya
            se corono un campeon: ahi no queda nada por programar. */}
        {result && !result.champion && (
          <Button variant="contained" onClick={onGenerarJornada}>Generar jornada</Button>
        )}
      </DialogActions>
    </Dialog>
  );
}

import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import IconButton from '@mui/material/IconButton';
import TextField from '@mui/material/TextField';
import MenuItem from '@mui/material/MenuItem';
import Switch from '@mui/material/Switch';
import FormControlLabel from '@mui/material/FormControlLabel';
import { DataGrid } from '@mui/x-data-grid';
import { Iconify } from 'src/components/iconify';
import { useApi } from 'src/hooks/use-api';
import { useCrudDialog } from 'src/hooks/use-crud';
import { endpoints } from 'src/lib/axios';
import { PageHeader } from 'src/components/page-header';
import { CrudDialog } from 'src/components/crud-dialog';
import { EditDeleteActions } from 'src/components/edit-delete-actions';
import { TIEBREAKER_CODES, etiquetasDesempate } from 'src/lib/tiebreaker-labels';
import { esJuzgado } from 'src/lib/sport-shape';

const EMPTY_FORM = {
  name: '',
  sportCode: 'football',
  config: {
    periods: { count: 2, label: 'Tiempo', minutes: 45, breakMinutes: 15, estimatedMinutes: null },
    // Vacio: armar tabla de posiciones es opcional (ver habilitarPuntos mas
    // abajo) y la mayoria de los reglamentos nuevos no la necesitan hasta que
    // el organizador la pide.
    points: {},
    tiebreakers: ['score_difference', 'head_to_head'],
  },
};

// Mismo calculo que SportFrog.Domain.Rules.SetsMatchOutcomeRules.RequiredOutcomes
// en el backend: un partido a la mejor de `count` se gana en toWin =
// ceil(count/2), y cada desenlace posible se nombra desde los dos lados.
// Se recalcula aca (en vez de pedirselo al backend) porque `count` cambia
// mientras el organizador todavia esta escribiendo el formulario — lo que
// terminara guardado siempre pasa por RulesetPolicy en el servidor, asi que
// una diferencia aca en el peor caso muestra el campo equivocado, nunca
// guarda un reglamento invalido.
function desenlacesDeSets(count) {
  const toWin = Math.ceil((count || 1) / 2);
  const desenlaces = [];
  for (let lost = 0; lost < toWin; lost++) {
    desenlaces.push(`win_${toWin}_${lost}`);
    desenlaces.push(`loss_${lost}_${toWin}`);
  }
  return desenlaces;
}

// React no resincroniza el valor de un <input type="number"> mientras esta
// enfocado (para no interrumpir mientras alguien termina de escribir "1." o
// "-5"), asi que un cero que ya estaba en el campo se queda pegado en
// pantalla hasta que se pierde el foco: se escribe "1" y se ve "01". Se
// corrige a mano sobre el nodo del DOM -- eso si toma efecto de inmediato,
// porque no pasa por la reconciliacion de React.
function limpiarCeroInicial(e) {
  const limpio = e.target.value.replace(/^0+(?=\d)/, '');
  if (limpio !== e.target.value) e.target.value = limpio;
  return limpio;
}

// "win_2_0" -> "Pts 2-0 (ganado)"; "loss_0_2" -> "Pts 0-2 (perdido)".
function etiquetaDesenlace(code) {
  if (code === 'win') return 'Pts victoria';
  if (code === 'draw') return 'Pts empate';
  if (code === 'loss') return 'Pts derrota';
  const m = /^(win|loss)_(\d+)_(\d+)$/.exec(code);
  if (!m) return code;
  const [, resultado, propio, rival] = m;
  return `Pts ${propio}-${rival} (${resultado === 'win' ? 'ganado' : 'perdido'})`;
}

export default function RulesetsPage() {
  const { data: sports } = useApi(endpoints.sports);
  const { rows, isLoading, open, editId, form, setForm, error, saving, openCreate, openEdit, close, save, remove } = useCrudDialog({
    resourceUrl: endpoints.rulesets,
    emptyForm: { ...EMPTY_FORM },
    entityName: 'reglamento',
    mapToForm: (row) => ({ name: row.name, sportCode: row.sportCode, config: row.config }),
  });

  const updateConfig = (path, value) => {
    const c = { ...form.config };
    const keys = path.split('.');
    let obj = c;
    for (let i = 0; i < keys.length - 1; i++) obj = { ...obj[keys[i]] };
    obj[keys[keys.length - 1]] = value;
    setForm({ ...form, config: c });
  };

  const tiebreakers = form.config.tiebreakers || [];
  const tiebreakersDisponibles = TIEBREAKER_CODES.filter((code) => !tiebreakers.includes(code));

  const agregarDesempate = (code) => {
    if (!code) return;
    updateConfig('tiebreakers', [...tiebreakers, code]);
  };
  const quitarDesempate = (i) => updateConfig('tiebreakers', tiebreakers.filter((_, idx) => idx !== i));
  const moverDesempate = (i, delta) => {
    const j = i + delta;
    if (j < 0 || j >= tiebreakers.length) return;
    const next = [...tiebreakers];
    [next[i], next[j]] = [next[j], next[i]];
    updateConfig('tiebreakers', next);
  };

  // En deportes que se juegan por sets el marcador de un walkover no es
  // negociable: son los sets que hacen falta para ganar, igual que si se
  // hubieran jugado. Ver RulesetPolicy.InspectWalkover en el backend, que
  // rechaza cualquier otro numero para esos deportes.
  const sportInfo = (sports || []).find((s) => s.code === form.sportCode);
  const esPorSets = Boolean(sportInfo?.isPlayedInSets);
  const juzgado = esJuzgado(sportInfo);
  // No es lo mismo que esPorSets: un set de voley se decide por marcador y
  // no corre reloj, pero un asalto de Kyorugi tambien se decide por sets
  // ganados y SI corre uno de dos minutos. periodHasClock es la pregunta que
  // en verdad decide si mostrar el campo de duracion, no isPlayedInSets. Sin
  // datos del catalogo todavia (sportInfo === undefined) se asume que si hay
  // reloj, para no ocultar el campo del deporte por defecto (futbol) mientras
  // /sports esta cargando.
  const tieneReloj = sportInfo ? Boolean(sportInfo.periodHasClock) : true;
  const setsParaGanar = Math.ceil((form.config.periods.count || 1) / 2);
  const walkover = form.config.walkover || null;

  // Que desenlaces hay que poner precio: en un deporte de suma son siempre
  // win/loss (mas el draw opcional), tal cual los expone el catalogo. En uno
  // por sets dependen de la cantidad de periodos que el organizador esta
  // escribiendo ahora mismo, asi que no pueden venir fijos del catalogo —
  // ver desenlacesDeSets arriba. Un deporte juzgado (poomsae) tambien cae
  // en esta rama: el catalogo ya expone win/loss sin empate para el
  // (JudgedMatchOutcomeRules), asi que no hace falta una tercera rama aca.
  const desenlacesRequeridos = esPorSets
    ? desenlacesDeSets(form.config.periods.count)
    : (sportInfo?.requiredOutcomes || ['win', 'loss']);
  const desenlacesOpcionales = esPorSets ? [] : (sportInfo?.optionalOutcomes || ['draw']);
  const etiquetas = etiquetasDesempate(sportInfo);

  const habilitarWalkover = (activo) => {
    if (!activo) { updateConfig('walkover', null); return; }
    updateConfig('walkover', esPorSets
      ? { winnerScore: setsParaGanar, loserScore: 0 }
      : { winnerScore: 3, loserScore: 0 });
  };

  // Vacio es una eleccion valida (ver RulesetPolicy.InspectPoints en el
  // backend): una llave de eliminacion directa nunca arma tabla, asi que
  // nadie deberia tener que inventarle puntaje a un desenlace que ninguna
  // tabla va a leer. El switch existe para que esa eleccion sea explicita en
  // vez de una fila de campos vacios que dan la impresion de ser obligatorios.
  const tienePuntos = Object.keys(form.config.points || {}).length > 0;

  const habilitarPuntos = (activo) => {
    if (!activo) { updateConfig('points', {}); return; }
    if (esPorSets) {
      const enCero = {};
      desenlacesRequeridos.forEach((code) => { enCero[code] = 0; });
      updateConfig('points', enCero);
      return;
    }
    const porDefecto = { win: 3, loss: 0 };
    if (desenlacesOpcionales.includes('draw')) porDefecto.draw = 1;
    updateConfig('points', porDefecto);
  };

  const columns = [
    { field: 'name', headerName: 'Nombre', flex: 1, minWidth: 200 },
    { field: 'sportCode', headerName: 'Deporte', width: 120, renderCell: ({ value }) => (sports || []).find((s) => s.code === value)?.name || value },
    { field: 'actions', headerName: 'Acciones', width: 90, align: 'center', headerAlign: 'center', renderCell: ({ row }) => (
      <EditDeleteActions onEdit={() => openEdit(row)} onDelete={() => remove(row.id)} />
    )},
  ];

  return (
    <div>
      <PageHeader title="Reglamentos" actionLabel="Nuevo" onAction={openCreate} />
      <DataGrid rows={rows} columns={columns} loading={isLoading} autoHeight disableRowSelectionOnClick />
      <CrudDialog open={open} editId={editId} entityName="Reglamento" error={error} saving={saving} onClose={close} onSave={save}>
        <TextField label="Nombre" value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} fullWidth />
        <TextField
          select
          label="Deporte"
          value={form.sportCode}
          onChange={(e) => {
            // Los desenlaces que hay que tarifar dependen del deporte
            // (win/loss no significan nada en un deporte por sets, y
            // viceversa), asi que un puntaje ya cargado para el anterior no
            // tiene sentido conservarlo. Periodos tambien vuelve al default
            // del deporte nuevo — el de antes podria ser par en un deporte
            // que ahora se juega por sets, donde eso no es valido. La
            // duracion tambien: el estandar del deporte nuevo, o nada si no
            // corre reloj (ahi el campo ni se muestra — ver mas abajo). Ojo,
            // no es lo mismo que "se juega por sets": Kyorugi se decide por
            // sets Y corre reloj. estimatedMinutes siempre vuelve a null: no
            // hay un estandar de catalogo del que precargarlo (a diferencia
            // de minutes), asi que cambiar de deporte no deja pegado un
            // numero que puede no tener nada que ver con el nuevo.
            const nuevoDeporte = (sports || []).find((s) => s.code === e.target.value);
            setForm({
              ...form,
              sportCode: e.target.value,
              config: {
                ...form.config,
                points: {},
                walkover: null,
                periods: nuevoDeporte
                  ? {
                      count: nuevoDeporte.defaultPeriods,
                      label: nuevoDeporte.periodLabel.charAt(0).toUpperCase() + nuevoDeporte.periodLabel.slice(1),
                      minutes: nuevoDeporte.periodHasClock ? (nuevoDeporte.defaultMinutes ?? null) : null,
                      breakMinutes: nuevoDeporte.periodHasClock ? (nuevoDeporte.defaultBreakMinutes ?? null) : null,
                      estimatedMinutes: null,
                    }
                  : form.config.periods,
              },
            });
          }}
          fullWidth
        >
          {(sports || []).map((s) => <MenuItem key={s.code} value={s.code}>{s.name}</MenuItem>)}
        </TextField>
        <TextField
          label="Periodos"
          type="number"
          value={form.config.periods.count}
          // Un deporte juzgado se decide en una sola actuacion por lado —
          // JudgedRulesetShape.InspectPeriods rechaza cualquier otro numero
          // al guardar. Bloqueado aca en vez de dejar que el organizador lo
          // escriba y se entere recien al guardar.
          disabled={juzgado}
          helperText={juzgado ? `${sportInfo.name} se decide en una sola actuación por lado.` : undefined}
          onChange={(e) => {
            const count = +limpiarCeroInicial(e);
            const nuevoWalkover = esPorSets && walkover
              ? { ...walkover, winnerScore: Math.ceil((count || 1) / 2) }
              : walkover;
            // Bajo sets, cambiar la cantidad de periodos cambia que
            // desenlaces existen (best-of-3 y best-of-5 no comparten
            // claves) — lo que ya estaba tarifado para la cantidad anterior
            // no aplica a la nueva.
            const nuevosPoints = esPorSets ? {} : form.config.points;
            setForm({
              ...form,
              config: { ...form.config, periods: { ...form.config.periods, count }, points: nuevosPoints, walkover: nuevoWalkover },
            });
          }}
          fullWidth
        />
        <TextField label="Nombre del período" value={form.config.periods.label} onChange={(e) => setForm({ ...form, config: { ...form.config, periods: { ...form.config.periods, label: e.target.value } } })} fullWidth helperText='Como se llama uno: "Tiempo", "Cuarto", "Set"...' />
        {tieneReloj && (
          <TextField
            label="Duración del período (minutos)"
            type="number"
            value={form.config.periods.minutes ?? ''}
            onChange={(e) => { const v = limpiarCeroInicial(e); setForm({ ...form, config: { ...form.config, periods: { ...form.config.periods, minutes: v === '' ? null : +v } } }); }}
            fullWidth
            // Precargada con el estandar del deporte elegido (ver el onChange
            // de "Deporte" mas arriba); el organizador la pisa si su liga
            // juega distinto.
            helperText="Se sugiere según el deporte — ajustala si tu competencia juega distinto."
          />
        )}
        {tieneReloj && (
          <TextField
            label="Descanso entre períodos (minutos)"
            type="number"
            value={form.config.periods.breakMinutes ?? ''}
            onChange={(e) => { const v = limpiarCeroInicial(e); setForm({ ...form, config: { ...form.config, periods: { ...form.config.periods, breakMinutes: v === '' ? null : +v } } }); }}
            fullWidth
            helperText="Cuánto se descansa entre uno y el siguiente. Se usa junto a la duración para calcular solo cuánto ocupa un partido completo al armar el calendario."
          />
        )}
        {!tieneReloj && (
          // El unico lugar del sistema donde vive esta duracion: sin reloj
          // no hay nada que MatchDuration.From pueda calcular sola, asi que
          // hace falta declararla a mano — ver el remark de
          // RulesetConfiguration.PeriodRules.EstimatedMinutes en el backend.
          // Antes esto se pedia en cada competencia; vivir aca en cambio
          // deja que una categoria mas chica juegue partidos mas cortos con
          // solo darle su propio reglamento, igual que ya podia hacerlo un
          // deporte con reloj.
          <TextField
            label="Duración total del partido (minutos)"
            type="number"
            value={form.config.periods.estimatedMinutes ?? ''}
            onChange={(e) => { const v = limpiarCeroInicial(e); setForm({ ...form, config: { ...form.config, periods: { ...form.config.periods, estimatedMinutes: v === '' ? null : +v } } }); }}
            fullWidth
            required
            helperText={`${sportInfo?.name || 'Este deporte'} no corre por reloj — no hay forma de calcularla sola, así que hace falta cargarla para poder armar el calendario.`}
          />
        )}
        <Box>
          <FormControlLabel
            control={<Switch checked={tienePuntos} onChange={(e) => habilitarPuntos(e.target.checked)} />}
            label="Armar tabla de posiciones"
          />
          <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 1 }}>
            {juzgado
              // Ganar o perder sigue decidiendo quien avanza en la
              // eliminatoria aunque esto quede apagado — esto solo decide si
              // ademas se arma una tabla (ver ClassificationRanking en el
              // backend para como se ordena una fase de clasificacion, que
              // no usa nada de esto).
              ? 'Quien gana o pierde un cruce sigue decidiendo quien avanza aunque esto este apagado. Prendelo solo si, ademas, este reglamento va a correr una liga o una fase de grupos.'
              : 'Le pone puntaje a cada resultado para ordenar una tabla. Una llave de eliminación directa nunca la necesita — solo hace falta si la competencia juega todos contra todos, o tiene una fase de grupos antes de la llave.'}
          </Typography>
          {tienePuntos && (
            <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2, mt: 1 }}>
              {esPorSets && (
                <Typography variant="caption" color="text.secondary">
                  Uno por cada marcador de sets con el que un partido puede terminar, visto desde los dos lados.
                </Typography>
              )}
              {[...desenlacesRequeridos, ...desenlacesOpcionales].map((code) => (
                <TextField
                  key={code}
                  label={etiquetaDesenlace(code)}
                  type="number"
                  value={form.config.points[code] ?? ''}
                  onChange={(e) => updateConfig(`points.${code}`, +limpiarCeroInicial(e))}
                  fullWidth
                />
              ))}
            </Box>
          )}
        </Box>

        {tienePuntos && (
          <Box>
            <Typography variant="subtitle2" sx={{ mb: 0.5 }}>Criterios de desempate</Typography>
            <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 1 }}>
              En el orden en que se aplican cuando dos o mas equipos quedan igualados en puntos.
            </Typography>
            {tiebreakers.map((code, i) => (
              <Box key={code} sx={{ display: 'flex', alignItems: 'center', gap: 1, py: 0.5 }}>
                <Typography variant="body2" sx={{ flexGrow: 1 }}>{i + 1}. {etiquetas[code] || code}</Typography>
                <IconButton size="small" disabled={i === 0} onClick={() => moverDesempate(i, -1)}><Iconify icon="eva:chevron-up-fill" /></IconButton>
                <IconButton size="small" disabled={i === tiebreakers.length - 1} onClick={() => moverDesempate(i, 1)}><Iconify icon="eva:chevron-down-fill" /></IconButton>
                <IconButton size="small" disabled={tiebreakers.length <= 1} onClick={() => quitarDesempate(i)}><Iconify icon="eva:trash-2-outline" sx={{ color: 'error.main' }} /></IconButton>
              </Box>
            ))}
            {tiebreakersDisponibles.length > 0 && (
              <TextField
                select
                label="Agregar criterio"
                value=""
                onChange={(e) => agregarDesempate(e.target.value)}
                fullWidth
                size="small"
                sx={{ mt: 1 }}
              >
                {tiebreakersDisponibles.map((code) => <MenuItem key={code} value={code}>{etiquetas[code]}</MenuItem>)}
              </TextField>
            )}
          </Box>
        )}

        <Box>
          <FormControlLabel
            control={<Switch checked={Boolean(walkover)} onChange={(e) => habilitarWalkover(e.target.checked)} />}
            label="Definir puntaje de walkover"
          />
          <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 1 }}>
            Cuanto se le acredita al equipo que se presenta cuando el rival no lo hace. Sin esto,
            un walkover no se puede otorgar.
          </Typography>
          {walkover && (
            <Box sx={{ display: 'flex', gap: 2 }}>
              <TextField
                label="Puntaje del ganador"
                type="number"
                value={walkover.winnerScore}
                disabled={esPorSets}
                helperText={esPorSets ? `Fijo: se gana en ${setsParaGanar} sets.` : undefined}
                onChange={(e) => updateConfig('walkover', { ...walkover, winnerScore: +limpiarCeroInicial(e) })}
                fullWidth
              />
              <TextField
                label="Puntaje del que no se presento"
                type="number"
                value={walkover.loserScore}
                onChange={(e) => updateConfig('walkover', { ...walkover, loserScore: +limpiarCeroInicial(e) })}
                fullWidth
              />
            </Box>
          )}
        </Box>
      </CrudDialog>
    </div>
  );
}
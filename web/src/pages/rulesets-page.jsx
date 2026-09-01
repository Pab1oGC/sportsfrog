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

// Mismos codigos que SportFrog.Domain.Rules.Tiebreaker, en el orden en que
// se ofrecen para agregar. El orden en que quedan aplicados es el que el
// organizador les da en la lista, no este.
const TIEBREAKER_LABELS = {
  score_difference: 'Diferencia de gol',
  score_for: 'Goles a favor',
  score_against: 'Goles en contra',
  wins: 'Partidos ganados',
  head_to_head: 'Enfrentamiento directo',
};
const TIEBREAKER_CODES = Object.keys(TIEBREAKER_LABELS);

const EMPTY_FORM = {
  name: '',
  sportCode: 'football',
  config: {
    periods: { count: 2, label: 'Tiempo', minutes: 45 },
    points: { win: 3, draw: 1, loss: 0 },
    tiebreakers: ['score_difference', 'head_to_head'],
  },
};

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
  const esPorSets = sportInfo?.scoreMode === 'sets';
  const setsParaGanar = Math.ceil((form.config.periods.count || 1) / 2);
  const walkover = form.config.walkover || null;

  const habilitarWalkover = (activo) => {
    if (!activo) { updateConfig('walkover', null); return; }
    updateConfig('walkover', esPorSets
      ? { winnerScore: setsParaGanar, loserScore: 0 }
      : { winnerScore: 3, loserScore: 0 });
  };

  const columns = [
    { field: 'name', headerName: 'Nombre', flex: 1, minWidth: 200 },
    { field: 'sportCode', headerName: 'Deporte', width: 120, renderCell: ({ value }) => (sports || []).find((s) => s.code === value)?.name || value },
    { field: 'actions', headerName: '', width: 100, renderCell: ({ row }) => (
      <div style={{ display: 'flex', gap: 4 }}>
        <Iconify icon="eva:edit-fill" sx={{ cursor: 'pointer', color: 'text.secondary' }} onClick={() => openEdit(row)} />
        <Iconify icon="eva:trash-2-outline" sx={{ cursor: 'pointer', color: 'error.main' }} onClick={() => remove(row.id)} />
      </div>
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
          onChange={(e) => setForm({ ...form, sportCode: e.target.value, config: { ...form.config, walkover: null } })}
          fullWidth
        >
          {(sports || []).map((s) => <MenuItem key={s.code} value={s.code}>{s.name}</MenuItem>)}
        </TextField>
        <TextField
          label="Periodos"
          type="number"
          value={form.config.periods.count}
          onChange={(e) => {
            const count = +e.target.value;
            const nuevoWalkover = esPorSets && walkover
              ? { ...walkover, winnerScore: Math.ceil((count || 1) / 2) }
              : walkover;
            setForm({ ...form, config: { ...form.config, periods: { ...form.config.periods, count }, walkover: nuevoWalkover } });
          }}
          fullWidth
        />
        <TextField label="Nombre del período" value={form.config.periods.label} onChange={(e) => setForm({ ...form, config: { ...form.config, periods: { ...form.config.periods, label: e.target.value } } })} fullWidth helperText='Como se llama uno: "Tiempo", "Cuarto", "Set"...' />
        <TextField
          label="Duración del período (minutos)"
          type="number"
          value={form.config.periods.minutes ?? ''}
          onChange={(e) => setForm({ ...form, config: { ...form.config, periods: { ...form.config.periods, minutes: e.target.value === '' ? null : +e.target.value } } })}
          fullWidth
          helperText="Dejar vacío en deportes que terminan por sets en vez de reloj (ej. vóley)."
        />
        <TextField label="Pts victoria" type="number" value={form.config.points.win} onChange={(e) => setForm({ ...form, config: { ...form.config, points: { ...form.config.points, win: +e.target.value } } })} fullWidth />
        <TextField label="Pts empate" type="number" value={form.config.points.draw} onChange={(e) => setForm({ ...form, config: { ...form.config, points: { ...form.config.points, draw: +e.target.value } } })} fullWidth />
        <TextField label="Pts derrota" type="number" value={form.config.points.loss} onChange={(e) => setForm({ ...form, config: { ...form.config, points: { ...form.config.points, loss: +e.target.value } } })} fullWidth />

        <Box>
          <Typography variant="subtitle2" sx={{ mb: 0.5 }}>Criterios de desempate</Typography>
          <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 1 }}>
            En el orden en que se aplican cuando dos o mas equipos quedan igualados en puntos.
          </Typography>
          {tiebreakers.map((code, i) => (
            <Box key={code} sx={{ display: 'flex', alignItems: 'center', gap: 1, py: 0.5 }}>
              <Typography variant="body2" sx={{ flexGrow: 1 }}>{i + 1}. {TIEBREAKER_LABELS[code] || code}</Typography>
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
              {tiebreakersDisponibles.map((code) => <MenuItem key={code} value={code}>{TIEBREAKER_LABELS[code]}</MenuItem>)}
            </TextField>
          )}
        </Box>

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
                onChange={(e) => updateConfig('walkover', { ...walkover, winnerScore: +e.target.value })}
                fullWidth
              />
              <TextField
                label="Puntaje del que no se presento"
                type="number"
                value={walkover.loserScore}
                onChange={(e) => updateConfig('walkover', { ...walkover, loserScore: +e.target.value })}
                fullWidth
              />
            </Box>
          )}
        </Box>
      </CrudDialog>
    </div>
  );
}
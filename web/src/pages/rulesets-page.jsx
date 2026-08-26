import TextField from '@mui/material/TextField';
import MenuItem from '@mui/material/MenuItem';
import { DataGrid } from '@mui/x-data-grid';
import { Iconify } from 'src/components/iconify';
import { useApi } from 'src/hooks/use-api';
import { useCrudDialog } from 'src/hooks/use-crud';
import { endpoints } from 'src/lib/axios';
import { PageHeader } from 'src/components/page-header';
import { CrudDialog } from 'src/components/crud-dialog';

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

  const columns = [
    { field: 'name', headerName: 'Nombre', flex: 1, minWidth: 200 },
    { field: 'sportCode', headerName: 'Deporte', width: 120 },
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
        <TextField select label="Deporte" value={form.sportCode} onChange={(e) => setForm({ ...form, sportCode: e.target.value })} fullWidth>
          {(sports || []).map((s) => <MenuItem key={s.code} value={s.code}>{s.name}</MenuItem>)}
        </TextField>
        <TextField label="Periodos" type="number" value={form.config.periods.count} onChange={(e) => setForm({ ...form, config: { ...form.config, periods: { ...form.config.periods, count: +e.target.value } } })} fullWidth />
        <TextField label="Pts victoria" type="number" value={form.config.points.win} onChange={(e) => setForm({ ...form, config: { ...form.config, points: { ...form.config.points, win: +e.target.value } } })} fullWidth />
        <TextField label="Pts empate" type="number" value={form.config.points.draw} onChange={(e) => setForm({ ...form, config: { ...form.config, points: { ...form.config.points, draw: +e.target.value } } })} fullWidth />
        <TextField label="Pts derrota" type="number" value={form.config.points.loss} onChange={(e) => setForm({ ...form, config: { ...form.config, points: { ...form.config.points, loss: +e.target.value } } })} fullWidth />
      </CrudDialog>
    </div>
  );
}
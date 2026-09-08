import { useSearchParams } from 'react-router';
import { useLastCompetition } from 'src/hooks/use-last-competition';
import { useCrudDialog } from 'src/hooks/use-crud';
import Box from '@mui/material/Box';
import TextField from '@mui/material/TextField';
import MenuItem from '@mui/material/MenuItem';
import { DataGrid } from '@mui/x-data-grid';
import { Iconify } from 'src/components/iconify';
import { useApi } from 'src/hooks/use-api';
import { endpoints } from 'src/lib/axios';
import { PageHeader } from 'src/components/page-header';
import { CrudDialog } from 'src/components/crud-dialog';
import { EditDeleteActions } from 'src/components/edit-delete-actions';
import { SelectionCompetition, SelectionField } from 'src/components/selectors';
import { esIndividual } from 'src/lib/sport-shape';

const emptyForm = () => ({ name: '', gender: '', birthDateFrom: '', birthDateTo: '', maxRosterSize: '', displayOrder: 0, rulesetId: '', qualifiersPerGroup: '', minWeightKg: '', maxWeightKg: '' });

export default function CategoriesPage() {
  const [searchParams] = useSearchParams();
  const { data: comps } = useApi(endpoints.competitions);
  // Preseleccionada al llegar desde "crear competencia": ese flujo manda para
  // acá porque una competencia sin categorías no tiene nada que sortear. Sin
  // esa señal en la URL, useLastCompetition elige la última usada en
  // cualquier pantalla, o si es la primera vez, la que está en curso.
  const [compId, setCompId] = useLastCompetition(comps, searchParams.get('competition'));
  const { data: rulesets } = useApi(endpoints.rulesets);
  const { data: sports } = useApi(endpoints.sports);

  const {
    rows: data, isLoading, open, editId, form, setForm, error, saving, openCreate, openEdit, close, save, remove,
  } = useCrudDialog({
    resourceUrl: compId ? endpoints.categories(compId) : null,
    emptyForm,
    entityName: 'categoria',
    entityGender: 'f',
    savedMessage: 'Categoria guardada.',
    buildUrl: (base, id) => endpoints.category(compId, id),
    mapToForm: (row) => ({ name: row.name || '', gender: row.gender || '', birthDateFrom: row.birthDateFrom || '', birthDateTo: row.birthDateTo || '', maxRosterSize: row.maxRosterSize || '', displayOrder: row.displayOrder || 0, rulesetId: row.rulesetId || '', qualifiersPerGroup: row.qualifiersPerGroup || '', minWeightKg: row.minWeightKg ?? '', maxWeightKg: row.maxWeightKg ?? '' }),
    mapToSend: (f) => ({
      name: f.name, gender: f.gender || null, birthDateFrom: f.birthDateFrom || null, birthDateTo: f.birthDateTo || null,
      maxRosterSize: f.maxRosterSize ? Number(f.maxRosterSize) : null, displayOrder: Number(f.displayOrder),
      rulesetId: f.rulesetId || null, qualifiersPerGroup: f.qualifiersPerGroup ? Number(f.qualifiersPerGroup) : null,
      minWeightKg: f.minWeightKg !== '' ? Number(f.minWeightKg) : null,
      maxWeightKg: f.maxWeightKg !== '' ? Number(f.maxWeightKg) : null,
    }),
  });

  const comp = comps?.find((c) => c.id === compId);
  const possibleRulesets = (rulesets || []).filter((r) => comp && r.sportCode === comp.sportCode);
  const sport = sports?.find((s) => comp && s.code === comp.sportCode);
  const sportName = sport?.name || comp?.sportCode || '';

  const columns = [
    { field: 'name', headerName: 'Nombre', flex: 1, minWidth: 180 },
    { field: 'gender', headerName: 'Genero', width: 90, renderCell: ({ value }) => value === 'M' ? 'Masculino' : value === 'F' ? 'Femenino' : 'Abierto' },
    { field: 'birthDateFrom', headerName: 'Nac. desde', width: 120 },
    { field: 'birthDateTo', headerName: 'Nac. hasta', width: 120 },
    { field: 'maxRosterSize', headerName: 'Max nomina', width: 100, renderCell: ({ value }) => value || '--' },
    { field: 'actions', headerName: 'Acciones', width: 90, align: 'center', headerAlign: 'center', renderCell: ({ row }) => (
      <EditDeleteActions onEdit={() => openEdit(row)} onDelete={() => remove(row.id)} />
    )},
  ];

  return (
    <div>
      <PageHeader title="Categorias" actionLabel="Nueva categoria" onAction={openCreate} actionDisabled={!compId} />
      <Box sx={{ display: 'flex', gap: 2, mb: 3, maxWidth: 700, flexWrap: 'wrap' }}>
        <Box sx={{ flex: 1, minWidth: 200 }}><SelectionCompetition value={compId} onChange={(e) => setCompId(e.target.value)} required /></Box>
      </Box>
      <DataGrid rows={data || []} columns={columns} loading={isLoading} autoHeight disableRowSelectionOnClick getRowId={(r) => r.id} />
      <CrudDialog open={open} editId={editId} entityName="Categoria" error={error} saving={saving} onClose={close} onSave={save}>
        <TextField label="Nombre" value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} fullWidth required />
        <SelectionField label="Genero" value={form.gender} onChange={(e) => setForm({ ...form, gender: e.target.value })} emptyLabel="Abierto" options={[{ value: 'M', label: 'Masculino' }, { value: 'F', label: 'Femenino' }]} />
        <TextField label="Nac. desde" type="date" value={form.birthDateFrom} onChange={(e) => setForm({ ...form, birthDateFrom: e.target.value })} fullWidth slotProps={{ inputLabel: { shrink: true } }} />
        <TextField label="Nac. hasta" type="date" value={form.birthDateTo} onChange={(e) => setForm({ ...form, birthDateTo: e.target.value })} fullWidth slotProps={{ inputLabel: { shrink: true } }} />
        <TextField label="Max. nomina" type="number" value={form.maxRosterSize} onChange={(e) => setForm({ ...form, maxRosterSize: e.target.value })} fullWidth />
        {comp?.format === 'groups' && (
          <TextField
            label="Clasifican por grupo"
            type="number"
            value={form.qualifiersPerGroup}
            onChange={(e) => setForm({ ...form, qualifiersPerGroup: e.target.value })}
            helperText="Cuantos equipos de cada grupo pasan a la siguiente ronda. Se deja vacio para no resaltar nada en el portal publico."
            fullWidth
          />
        )}
        {esIndividual(sport) && (
          // Solo tiene sentido donde el que se inscribe es un deportista, no
          // un club: RosterPolicy compara el peso del deportista contra esta
          // ventana al inscribirlo. Ambos extremos quedan abiertos si se
          // dejan vacios, igual que la ventana de fecha de nacimiento de arriba.
          <Box sx={{ display: 'flex', gap: 2 }}>
            <TextField
              label="Peso minimo (kg)"
              type="number"
              value={form.minWeightKg}
              onChange={(e) => setForm({ ...form, minWeightKg: e.target.value })}
              fullWidth
              slotProps={{ htmlInput: { min: 0, step: 0.1 } }}
            />
            <TextField
              label="Peso maximo (kg)"
              type="number"
              value={form.maxWeightKg}
              onChange={(e) => setForm({ ...form, maxWeightKg: e.target.value })}
              fullWidth
              slotProps={{ htmlInput: { min: 0, step: 0.1 } }}
            />
          </Box>
        )}
        <TextField select label="Reglamento propio" value={form.rulesetId} onChange={(e) => setForm({ ...form, rulesetId: e.target.value })} fullWidth disabled={possibleRulesets.length === 0}>
          <MenuItem value="">Usar el de la competencia</MenuItem>
          {possibleRulesets.map((r) => <MenuItem key={r.id} value={r.id}>{r.name}</MenuItem>)}
        </TextField>
      </CrudDialog>
    </div>
  );
}
import { useSearchParams } from 'react-router';
import { useLastCompetition } from 'src/hooks/use-last-competition';
import { useCrudDialog } from 'src/hooks/use-crud';
import Box from '@mui/material/Box';
import TextField from '@mui/material/TextField';
import MenuItem from '@mui/material/MenuItem';
import Checkbox from '@mui/material/Checkbox';
import FormControlLabel from '@mui/material/FormControlLabel';
import FormHelperText from '@mui/material/FormHelperText';
import { DataGrid } from '@mui/x-data-grid';
import { useApi } from 'src/hooks/use-api';
import { endpoints } from 'src/lib/axios';
import { PageHeader } from 'src/components/page-header';
import { CrudDialog } from 'src/components/crud-dialog';
import { EditDeleteActions } from 'src/components/edit-delete-actions';
import { SelectionCompetition, SelectionField } from 'src/components/selectors';
import { DateField } from 'src/components/date-field';
import { esIndividual } from 'src/lib/sport-shape';
import { bloquearNoEnteros, soloDigitos } from 'src/lib/entero-sin-signo';
import { bloquearNegativos, soloDecimales } from 'src/lib/entero-sin-signo';

const emptyForm = () => ({ name: '', gender: '', birthDateFrom: '', birthDateTo: '', maxRosterSize: '', displayOrder: 0, rulesetId: '', qualifiersPerGroup: '', minWeightKg: '', maxWeightKg: '', usesRepechage: false });

// Un deporte de equipo tiene tres niveles -- club, equipo, jugadores -- y
// "Max. nomina" pregunta por el ultimo: cuantos jugadores entran en el
// plantel. Un deporte individual, para quien lo usa, tiene dos: el club y
// sus deportistas. La unidad que compite (una persona, o la pareja de
// Poomsae) es una capa del modelo que el usuario no tiene por que conocer,
// asi que preguntarle "cuantos entran en la nomina" lo hace adivinar sobre
// un nivel que en su cabeza no existe -- y lo mas probable es que conteste
// pensando en cuantos deportistas puede anotar el club, que es otra cosa.
//
// Misma columna por debajo (Category.MaxRosterSize, que RosterPolicy ya hace
// cumplir), otra pregunta arriba: como se compite esta categoria.
const MODALIDADES = [
  { value: '1', label: 'Individual' },
  { value: '2', label: 'Pareja' },
  { value: '3', label: 'Trío' },
];

const modalidadDe = (value) => MODALIDADES.find((m) => m.value === String(value))?.label;

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
    mapToForm: (row) => ({ name: row.name || '', gender: row.gender || '', birthDateFrom: row.birthDateFrom || '', birthDateTo: row.birthDateTo || '', maxRosterSize: row.maxRosterSize || '', displayOrder: row.displayOrder || 0, rulesetId: row.rulesetId || '', qualifiersPerGroup: row.qualifiersPerGroup || '', minWeightKg: row.minWeightKg ?? '', maxWeightKg: row.maxWeightKg ?? '', usesRepechage: row.usesRepechage || false }),
    mapToSend: (f) => ({
      name: f.name, gender: f.gender || null, birthDateFrom: f.birthDateFrom || null, birthDateTo: f.birthDateTo || null,
      maxRosterSize: f.maxRosterSize ? Number(f.maxRosterSize) : null, displayOrder: Number(f.displayOrder),
      rulesetId: f.rulesetId || null, qualifiersPerGroup: f.qualifiersPerGroup ? Number(f.qualifiersPerGroup) : null,
      minWeightKg: f.minWeightKg !== '' ? Number(f.minWeightKg) : null,
      maxWeightKg: f.maxWeightKg !== '' ? Number(f.maxWeightKg) : null,
      usesRepechage: !!f.usesRepechage,
    }),
  });

  const comp = comps?.find((c) => c.id === compId);
  const possibleRulesets = (rulesets || []).filter((r) => comp && r.sportCode === comp.sportCode);
  const sport = sports?.find((s) => comp && s.code === comp.sportCode);
  const sportName = sport?.name || comp?.sportCode || '';
  const individual = esIndividual(sport);

  const columns = [
    { field: 'name', headerName: 'Nombre', flex: 1, minWidth: 180 },
    { field: 'gender', headerName: 'Genero', width: 90, renderCell: ({ value }) => value === 'M' ? 'Masculino' : value === 'F' ? 'Femenino' : 'Abierto' },
    { field: 'birthDateFrom', headerName: 'Nac. desde', width: 120 },
    { field: 'birthDateTo', headerName: 'Nac. hasta', width: 120 },
    individual
      ? { field: 'maxRosterSize', headerName: 'Modalidad', width: 110, renderCell: ({ value }) => modalidadDe(value) || '--' }
      : { field: 'maxRosterSize', headerName: 'Max nomina', width: 100, renderCell: ({ value }) => value || '--' },
    { field: 'actions', headerName: 'Acciones', width: 90, align: 'center', headerAlign: 'center', renderCell: ({ row }) => (
      <EditDeleteActions onEdit={() => openEdit(row)} onDelete={() => remove(row.id)} />
    )},
  ];

  return (
    <div>
      <PageHeader title="Categorías" actionLabel="Nueva categoría" onAction={openCreate} actionDisabled={!compId} />
      <Box sx={{ display: 'flex', gap: 2, mb: 3, maxWidth: 700, flexWrap: 'wrap' }}>
        <Box sx={{ flex: 1, minWidth: 200 }}><SelectionCompetition value={compId} onChange={(e) => setCompId(e.target.value)} required /></Box>
      </Box>
      <DataGrid rows={data || []} columns={columns} loading={isLoading} autoHeight disableRowSelectionOnClick getRowId={(r) => r.id} />
      <CrudDialog open={open} editId={editId} entityName="Categoria" entityGender="f" error={error} saving={saving} onClose={close} onSave={save}>
        <TextField label="Nombre" value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} fullWidth required />
        <SelectionField label="Genero" value={form.gender} onChange={(e) => setForm({ ...form, gender: e.target.value })} emptyLabel="Abierto" options={[{ value: 'M', label: 'Masculino' }, { value: 'F', label: 'Femenino' }]} />
        <DateField label="Nac. desde" value={form.birthDateFrom} onChange={(e) => setForm({ ...form, birthDateFrom: e.target.value })} fullWidth />
        <DateField label="Nac. hasta" value={form.birthDateTo} onChange={(e) => setForm({ ...form, birthDateTo: e.target.value })} fullWidth />
        {individual ? (
          // String a los dos lados: mapToForm deja un numero cuando la
          // categoria ya tenia cupo, y compararlo contra un option de texto
          // dejaria el desplegable en blanco sobre una categoria ya cargada.
          // mapToSend lo vuelve a Number antes de mandarlo, como siempre.
          <SelectionField
            label="Modalidad"
            value={form.maxRosterSize === '' ? '' : String(form.maxRosterSize)}
            onChange={(e) => setForm({ ...form, maxRosterSize: e.target.value })}
            emptyLabel="Sin definir"
            // El deporte pone el techo (Sport.MaxEntrySize) y acá solo se
            // puede elegir por debajo: en Kyorugi la única opción es
            // Individual, así que no hay forma de inventar una dupla que la
            // categoría no puede tener. Sin techo declarado se ofrecen las
            // tres y manda lo que diga la categoría.
            options={sport?.maxEntrySize != null
              ? MODALIDADES.filter((m) => Number(m.value) <= sport.maxEntrySize)
              : MODALIDADES}
            helperText={sport?.maxEntrySize === 1
              ? `${sportName} se compite de a uno, así que la categoría no admite otra modalidad.`
              : 'Cuántos compiten juntos en esta categoría.'}
          />
        ) : (
          <TextField
            label="Max. nomina" type="number" value={form.maxRosterSize} fullWidth
            onChange={(e) => setForm({ ...form, maxRosterSize: soloDigitos(e.target.value) })}
            onKeyDown={bloquearNoEnteros}
            slotProps={{ htmlInput: { min: 1, step: 1 } }}
          />
        )}
        {comp?.format === 'groups' && (
          <TextField
            label="Clasifican por grupo"
            type="number"
            value={form.qualifiersPerGroup}
            onChange={(e) => setForm({ ...form, qualifiersPerGroup: soloDigitos(e.target.value) })}
            onKeyDown={bloquearNoEnteros}
            slotProps={{ htmlInput: { min: 1, step: 1 } }}
            helperText="Cuantos equipos de cada grupo pasan a la siguiente ronda. Se deja vacio para no resaltar nada en el portal publico."
            fullWidth
          />
        )}
        {individual && (
          // Solo tiene sentido donde el que se inscribe es un deportista, no
          // un club: RosterPolicy compara el peso del deportista contra esta
          // ventana al inscribirlo. Ambos extremos quedan abiertos si se
          // dejan vacios, igual que la ventana de fecha de nacimiento de arriba.
          <Box sx={{ display: 'flex', gap: 2 }}>
            <TextField
              label="Peso minimo (kg)"
              type="number"
              value={form.minWeightKg}
              onChange={(e) => setForm({ ...form, minWeightKg: soloDecimales(e.target.value) })}
              onKeyDown={bloquearNegativos}
              fullWidth
              slotProps={{ htmlInput: { min: 0, step: 0.1 } }}
            />
            <TextField
              label="Peso maximo (kg)"
              type="number"
              value={form.maxWeightKg}
              onChange={(e) => setForm({ ...form, maxWeightKg: soloDecimales(e.target.value) })}
              onKeyDown={bloquearNegativos}
              fullWidth
              slotProps={{ htmlInput: { min: 0, step: 0.1 } }}
            />
          </Box>
        )}
        <TextField select label="Reglamento propio" value={form.rulesetId} onChange={(e) => setForm({ ...form, rulesetId: e.target.value })} fullWidth disabled={possibleRulesets.length === 0}>
          <MenuItem value="">Usar el de la competencia</MenuItem>
          {possibleRulesets.map((r) => <MenuItem key={r.id} value={r.id}>{r.name}</MenuItem>)}
        </TextField>
        {comp?.format !== 'league' && (
          // No restringido a un deporte puntual del lado del servidor
          // (DrawRepechage lo valida por la forma del cuadro, no por el
          // deporte), pero kyorugi es el motivo de que esto exista: ahi no
          // se juega un partido por el tercer puesto, se sortea un repechaje
          // aparte para cada mitad de la llave. Sin sentido en una liga, que
          // nunca llega a un cuadro de eliminacion directa.
          <Box>
            <FormControlLabel
              control={
                <Checkbox
                  checked={!!form.usesRepechage}
                  onChange={(e) => setForm({ ...form, usesRepechage: e.target.checked })}
                />
              }
              label="Definir el tercer puesto por repechaje"
            />
            <FormHelperText sx={{ mt: -0.5, ml: 4 }}>
              En vez de un partido por el tercer puesto, quienes perdieron contra alguno de los dos
              finalistas se juegan dos bronces aparte, uno por cada mitad de la llave (kyorugi).
            </FormHelperText>
          </Box>
        )}
      </CrudDialog>
    </div>
  );
}

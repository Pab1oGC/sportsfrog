import { useCrudDialog } from 'src/hooks/use-crud';
import Box from '@mui/material/Box';
import Stack from '@mui/material/Stack';
import Button from '@mui/material/Button';
import TextField from '@mui/material/TextField';
import MenuItem from '@mui/material/MenuItem';
import Checkbox from '@mui/material/Checkbox';
import FormControlLabel from '@mui/material/FormControlLabel';
import FormHelperText from '@mui/material/FormHelperText';
import { DataGrid } from '@mui/x-data-grid';
import { Iconify } from 'src/components/iconify';
import { useApi } from 'src/hooks/use-api';
import { endpoints } from 'src/lib/axios';
import { CrudDialog } from 'src/components/crud-dialog';
import { EditDeleteActions } from 'src/components/edit-delete-actions';
import { SelectionField } from 'src/components/selectors';
import { DateField } from 'src/components/date-field';
import { esIndividual } from 'src/lib/sport-shape';
import { bloquearNoEnteros, soloDigitos } from 'src/lib/entero-sin-signo';
import { bloquearNegativos, soloDecimales } from 'src/lib/entero-sin-signo';
import { modalidadDe, modalidadesDisponibles } from 'src/pages/categories/modalidad';

const emptyForm = () => ({ name: '', gender: '', birthDateFrom: '', birthDateTo: '', maxRosterSize: '', displayOrder: 0, rulesetId: '', qualifiersPerGroup: '', minWeightKg: '', maxWeightKg: '', usesRepechage: false });

/**
 * La grilla y el alta/edición de las categorías de UNA competencia, ya
 * elegida por quien llama -- a diferencia de CategoriesPage (la pantalla de
 * siempre, con su propio selector porque ahí nadie llegó con una ya en
 * mente), esto no sabe elegir ninguna, solo usarla.
 *
 * Nace de sacarle a CategoriesPage todo lo que no era el selector de arriba,
 * para que la pestaña "Categorías" del resumen de una competencia
 * (CompetitionOverviewPage) pueda mostrar exactamente lo mismo sin abrir una
 * pantalla nueva ni reimplementar el alta/edición por su cuenta. Las dos
 * envuelven este mismo componente.
 */
export function CategoriesPanel({ competitionId }) {
  const { data: rulesets } = useApi(endpoints.rulesets);
  const { data: sports } = useApi(endpoints.sports);
  const { data: comps } = useApi(endpoints.competitions);

  const {
    rows: data, isLoading, open, editId, form, setForm, error, saving, openCreate, openEdit, close, save, remove,
  } = useCrudDialog({
    resourceUrl: competitionId ? endpoints.categories(competitionId) : null,
    emptyForm,
    entityName: 'categoria',
    entityGender: 'f',
    savedMessage: 'Categoria guardada.',
    buildUrl: (base, id) => endpoints.category(competitionId, id),
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

  const comp = comps?.find((c) => c.id === competitionId);
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
    <Box>
      <Stack direction="row" justifyContent="flex-end" sx={{ mb: 2 }}>
        <Button
          variant="contained"
          startIcon={<Iconify icon="eva:plus-fill" />}
          onClick={openCreate}
          disabled={!competitionId}
        >
          Nueva categoría
        </Button>
      </Stack>
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
            options={modalidadesDisponibles(sport?.maxEntrySize)}
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
    </Box>
  );
}

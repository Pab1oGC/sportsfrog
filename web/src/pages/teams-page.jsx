import Box from '@mui/material/Box';
import Chip from '@mui/material/Chip';
import FormControlLabel from '@mui/material/FormControlLabel';
import Switch from '@mui/material/Switch';
import TextField from '@mui/material/TextField';
import { DataGrid } from '@mui/x-data-grid';
import { Iconify } from 'src/components/iconify';
import { useApi } from 'src/hooks/use-api';
import { useCascade } from 'src/hooks/use-cascade';
import { useCrudDialog } from 'src/hooks/use-crud';
import { endpoints } from 'src/lib/axios';
import { esIndividual } from 'src/lib/sport-shape';
import { PageHeader } from 'src/components/page-header';
import { CrudDialog } from 'src/components/crud-dialog';
import { EditDeleteActions } from 'src/components/edit-delete-actions';
import { CascadeFilters } from 'src/components/cascade-filters';
import { SelectionClub, SelectionAthletes } from 'src/components/selectors';

const emptyForm = () => ({ clubId: '', name: '', groupLabel: '', seed: '', isActive: true, athleteIds: [] });

export default function TeamsPage() {
  const cascade = useCascade();
  const { data: sports } = useApi(endpoints.sports);

  // De que deporte es esta categoria decide toda la forma del alta: un
  // deporte individual (Sport.IsIndividual, taekwondo hoy) inscribe
  // deportistas via EnrollIndividual, no clubes via CreateTeam -- el backend
  // ya lo rechaza con un 409 si se lo pide al reves.
  const comp = cascade.competiciones.find((c) => c.id === cascade.compId);
  const sport = sports?.find((s) => comp && s.code === comp.sportCode);
  const individual = esIndividual(sport);

  const {
    rows: teams, isLoading, open, editId, form, setForm, error, openCreate, openEdit, close, save, remove,
  } = useCrudDialog({
    resourceUrl: cascade.catId ? endpoints.teams(cascade.catId) : null,
    createUrl: individual ? endpoints.categoryIndividuals(cascade.catId) : endpoints.teams(cascade.catId),
    emptyForm,
    entityName: 'equipo',
    savedMessage: 'Equipo guardado.',
    buildUrl: (base, id) => endpoints.team(id),
    // La edicion no cambia con el deporte: UpdateTeam es el mismo contrato
    // para los dos casos, y ni club ni deportistas se tocan aca (el club no
    // se puede corregir una vez inscripto -- ver el comentario de
    // UpdateTeam en el backend -- y los deportistas se agregan o quitan
    // desde Nomina, no desde aca).
    mapToForm: (row) => ({ clubId: row.clubId || '', name: row.name || '', groupLabel: row.groupLabel || '', seed: row.seed ?? '', isActive: row.isActive !== false, athleteIds: [] }),
    mapToSend: (f, wasEdit) => {
      if (wasEdit) {
        return { name: f.name || null, groupLabel: f.groupLabel || null, seed: f.seed !== '' ? Number(f.seed) : null, isActive: f.isActive };
      }
      const seed = f.seed !== '' ? Number(f.seed) : null;
      // Sin club elegido, EnrollIndividual entra al deportista bajo el club
      // no afiliado de la organizacion -- una forma completamente ordinaria
      // de inscribirse en un deporte donde un equipo no tiene por que
      // pertenecer a ninguna delegacion.
      return individual
        ? { athleteIds: f.athleteIds, clubId: f.clubId || null, groupLabel: f.groupLabel || null, seed }
        : { clubId: f.clubId, name: f.name || null, groupLabel: f.groupLabel || null, seed, isActive: f.isActive };
    },
  });

  const columns = [
    { field: 'name', headerName: 'Nombre', flex: 1, minWidth: 200 },
    { field: 'clubName', headerName: 'Club', width: 160 },
    { field: 'groupLabel', headerName: 'Grupo', width: 100, renderCell: ({ value }) => value ? <Chip label={value} size="small" /> : '--' },
    { field: 'seed', headerName: 'Bombo', width: 90, renderCell: ({ value }) => value != null ? value : '--' },
    { field: 'isActive', headerName: 'Activo', width: 80, renderCell: ({ value }) => <Chip label={value ? 'Si' : 'No'} color={value ? 'success' : 'default'} size="small" variant="outlined" /> },
    { field: 'actions', headerName: 'Acciones', width: 90, align: 'center', headerAlign: 'center', renderCell: ({ row }) => (
      <EditDeleteActions onEdit={() => openEdit(row)} onDelete={() => remove(row.id)} />
    )},
  ];

  return (
    <Box>
      <PageHeader title="Equipos" actionLabel={individual ? 'Inscribir' : 'Nuevo equipo'} onAction={openCreate} actionDisabled={!cascade.catId} />
      <CascadeFilters cascade={cascade} />
      <DataGrid rows={teams || []} columns={columns} loading={isLoading} autoHeight disableRowSelectionOnClick getRowId={(r) => r.id} />
      <CrudDialog open={open} editId={editId} entityName="Equipo" error={error} onClose={close} onSave={save}>
        {/* El club no se puede corregir editando -- UpdateTeam no lo acepta,
            así que mostrarlo ahí sería un campo que parece editable y no lo
            es. Al crear, sigue siendo obligatorio para un club (CreateTeam
            lo exige); para un deporte individual es la delegación, y queda
            opcional -- sin elegir ninguno, EnrollIndividual entra al
            deportista bajo el club no afiliado. */}
        {!editId && (
          <SelectionClub
            value={form.clubId}
            onChange={(e) => setForm({ ...form, clubId: e.target.value })}
            required={!individual}
            emptyLabel={individual ? 'Sin delegación' : undefined}
          />
        )}
        {individual && !editId && (
          <SelectionAthletes
            value={form.athleteIds}
            onChange={(athleteIds) => setForm({ ...form, athleteIds })}
            helperText="Uno para un deportista solo, dos para una pareja, mas para un equipo mas grande."
          />
        )}
        {/* El nombre lo arma el backend a partir de los deportistas elegidos
            (IndividualTeamName) mientras no hay ninguno inscripto todavia --
            mostrar un campo de texto aca no tendria nada que editar. Al
            corregir, en cambio, es el mismo campo de siempre para cualquier
            equipo. */}
        {(!individual || editId) && (
          <TextField
            label={editId ? 'Nombre del equipo' : 'Nombre del equipo (vacio = nombre del club)'}
            value={form.name}
            onChange={(e) => setForm({ ...form, name: e.target.value })}
            fullWidth
            // UpdateTeam exige el nombre al corregir -- a diferencia de
            // CreateTeam, ahi no hay club al que recurrir si se deja vacio.
            helperText={editId ? undefined : 'Dejar vacio para usar el nombre del club'}
          />
        )}
        {/* El grupo ya no se tipea a mano: lo completa el sorteo de grupos
            (ver Fixtures/Partidos). El campo seguia en el formulario aunque
            eso ya lo resolvia solo — se saca de aca, pero el valor que ya
            tenga un equipo (asignado por un sorteo anterior) no se toca:
            form.groupLabel sigue viajando en el guardado, solo que ya no hay
            forma de escribirlo a mano. */}
        <TextField
          label="Bombo"
          type="number"
          value={form.seed}
          onChange={(e) => setForm({ ...form, seed: e.target.value })}
          fullWidth
          helperText="Opcional. Para un sorteo de grupos por bombos: equipos del mismo bombo nunca caen en el mismo grupo."
          slotProps={{ htmlInput: { min: 1, max: 26 } }}
        />
        {editId && <FormControlLabel control={<Switch checked={form.isActive} onChange={(e) => setForm({ ...form, isActive: e.target.checked })} />} label="Activo" />}
      </CrudDialog>
    </Box>
  );
}
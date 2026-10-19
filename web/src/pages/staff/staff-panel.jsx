import Box from '@mui/material/Box';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import Alert from '@mui/material/Alert';
import TextField from '@mui/material/TextField';
import MenuItem from '@mui/material/MenuItem';
import { DataGrid } from '@mui/x-data-grid';
import { Iconify } from 'src/components/iconify';
import { useApi, apiPut, apiDelete } from 'src/hooks/use-api';
import { useRememberedChild } from 'src/hooks/use-remembered-child';
import { useCrudDialog } from 'src/hooks/use-crud';
import { endpoints } from 'src/lib/axios';
import { CrudDialog } from 'src/components/crud-dialog';
import { RowActionsMenu } from 'src/components/row-actions-menu';
import { EstadoChip } from 'src/components/estado-chip';
import { SelectionCategory, SelectionTeam, SelectionAthletes } from 'src/components/selectors';
import { useConfirm } from 'src/components/confirm-dialog';
import { toast } from 'sonner';

// Los siete cargos de StaffRole, en el orden en que el backend los declara
// -- que es la jerarquia con la que se lee un banco de suplentes, no el
// alfabeto. El orden de este arreglo es el de los MenuItem; la grilla la
// ordena el servidor por el mismo criterio.
const ROLES = [
  { value: 'head_coach', label: 'Director técnico' },
  { value: 'assistant_coach', label: 'Asistente técnico' },
  { value: 'physical_trainer', label: 'Preparador físico' },
  { value: 'physician', label: 'Médico' },
  { value: 'physiotherapist', label: 'Kinesiólogo' },
  { value: 'delegate', label: 'Delegado' },
  { value: 'director', label: 'Dirigente' },
];

const ROLE_LABEL = Object.fromEntries(ROLES.map((role) => [role.value, role.label]));

const emptyForm = () => ({ athleteId: '', role: 'head_coach' });

/**
 * El cuerpo técnico y los dirigentes de un equipo de UNA competencia ya
 * elegida por quien llama -- el hermano de RosterPanel para la gente que no
 * juega, y deliberadamente un panel aparte en vez de una columna más en
 * aquel: el plantel tiene dorsal, posición, cupo y límites de edad, y nada
 * de eso significa algo para un médico.
 *
 * La persona se elige de /athletes, que es la tabla de personas de la
 * organización y no sólo de competidores: un DT se crea con el mismo
 * formulario y el mismo validador de fotos que un deportista, y lo único
 * propio de esta pantalla es el vínculo con el equipo y el cargo.
 */
export function StaffPanel({ competitionId }) {
  const confirm = useConfirm();
  const { data: categorias } = useApi(competitionId ? endpoints.categories(competitionId) : null);
  const [catId, setCatId] = useRememberedChild(categorias, competitionId, 'sportfrog:lastCategoryId:');
  const { data: equipos } = useApi(catId ? endpoints.teams(catId) : null);
  const [teamId, setTeamId] = useRememberedChild(equipos, catId, 'sportfrog:lastTeamId:');

  const staffUrl = teamId ? endpoints.teamStaff(teamId) : null;

  const { rows: staff, mutate, isLoading, open, editId, form, setForm, error, openCreate, openEdit, close, save } =
    useCrudDialog({
      resourceUrl: staffUrl,
      emptyForm,
      entityName: 'integrante',
      // Editar un integrante es corregirle el cargo y nada más: quién es no
      // se corrige, se anula el registro y se hace de nuevo -- la misma
      // división que hacen UpdateStaffRole y StrikeStaff del lado del API.
      buildUrl: (base, id) => endpoints.staffRole(id),
      mapToForm: (member) => ({ athleteId: member.athleteId, role: member.role }),
      mapToSend: (sent, wasEdit) =>
        wasEdit ? { role: sent.role } : { athleteId: sent.athleteId, role: sent.role },
    });

  const activos = staff.filter((member) => !member.withdrawnAt);

  const withdraw = async (member, withdrawn) => {
    try {
      await apiPut(endpoints.staffWithdrawal(member.id), { withdrawn });
      mutate();
    } catch (err) { toast.error(err.message); }
  };

  const strike = async (member) => {
    const ok = await confirm('Anular registro?', { confirmLabel: 'Anular', danger: true });
    if (!ok) return;

    try {
      await apiDelete(endpoints.staffMember(member.id));
      mutate();
    } catch (err) { toast.error(err.message); }
  };

  const columns = [
    { field: 'lastName', headerName: 'Apellido', flex: 1, minWidth: 120 },
    { field: 'firstName', headerName: 'Nombre', flex: 1, minWidth: 120 },
    { field: 'documentId', headerName: 'Documento', width: 120 },
    { field: 'role', headerName: 'Cargo', width: 160, renderCell: ({ value }) => ROLE_LABEL[value] || value },
    { field: 'withdrawnAt', headerName: 'Estado', width: 110, renderCell: ({ value, row }) => (
      <EstadoChip
        activo={!value}
        offLabel="Retirado" offColor="warning"
        onTooltip="Retirar" offTooltip="Reintegrar"
        onClick={() => withdraw(row, !value)}
      />
    )},
    { field: 'actions', headerName: 'Acciones', width: 90, align: 'center', headerAlign: 'center', renderCell: ({ row: member }) => {
      const retirado = !!member.withdrawnAt;
      return (
        <RowActionsMenu
          primary={{ icon: 'eva:edit-fill', label: 'Cambiar cargo', onClick: () => openEdit(member) }}
          actions={[
            { icon: retirado ? 'eva:undo-fill' : 'eva:person-done-outline', label: retirado ? 'Reintegrar' : 'Retirar', color: retirado ? 'info.main' : 'warning.main', onClick: () => withdraw(member, !retirado) },
            !retirado && { icon: 'eva:close-circle-outline', label: 'Anular', color: 'error.main', onClick: () => strike(member) },
          ].filter(Boolean)}
        />
      );
    }},
  ];

  return (
    <Box>
      <Stack direction="row" justifyContent="flex-end" sx={{ mb: 2 }}>
        <Button variant="contained" startIcon={<Iconify icon="eva:plus-fill" />} onClick={openCreate} disabled={!teamId}>
          Registrar
        </Button>
      </Stack>

      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}

      <Box sx={{ display: 'flex', gap: 2, mb: 3, flexWrap: 'wrap' }}>
        <Box sx={{ flex: 1, minWidth: 200 }}>
          <SelectionCategory competitionId={competitionId} value={catId} onChange={(e) => setCatId(e.target.value)} required />
        </Box>
        <Box sx={{ flex: 1, minWidth: 200 }}>
          <SelectionTeam categoryId={catId} value={teamId} onChange={(e) => setTeamId(e.target.value)} required />
        </Box>
      </Box>

      {teamId && (
        <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
          {activos.length} activo(s) / {staff.length} total
        </Typography>
      )}

      <DataGrid rows={staff} columns={columns} loading={isLoading} autoHeight disableRowSelectionOnClick getRowId={(row) => row.id} />

      <CrudDialog open={open} editId={editId} entityName="Integrante" error={error} onClose={close} onSave={save}>
        {!editId && (
          <SelectionAthletes
            label="Persona"
            multiple={false}
            value={form.athleteId}
            onChange={(athleteId) => setForm({ ...form, athleteId })}
            helperText="Si todavía no está cargada, creala primero en Deportistas: es la misma ficha de personas."
            required
          />
        )}
        <TextField
          select
          label="Cargo"
          value={form.role}
          onChange={(e) => setForm({ ...form, role: e.target.value })}
          fullWidth
          required
        >
          {ROLES.map((role) => <MenuItem key={role.value} value={role.value}>{role.label}</MenuItem>)}
        </TextField>
      </CrudDialog>
    </Box>
  );
}

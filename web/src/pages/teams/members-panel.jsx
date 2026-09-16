import Box from '@mui/material/Box';
import Paper from '@mui/material/Paper';
import Chip from '@mui/material/Chip';
import Button from '@mui/material/Button';
import Typography from '@mui/material/Typography';
import { DataGrid } from '@mui/x-data-grid';
import { toast } from 'sonner';
import { Iconify } from 'src/components/iconify';
import { apiPut, apiDelete } from 'src/hooks/use-api';
import { useCrudDialog } from 'src/hooks/use-crud';
import { useConfirm } from 'src/components/confirm-dialog';
import { CrudDialog } from 'src/components/crud-dialog';
import { RowActionsMenu } from 'src/components/row-actions-menu';
import { SelectionAthletes } from 'src/components/selectors';
import { endpoints } from 'src/lib/axios';

const emptyForm = () => ({ athleteIds: [] });

/**
 * Quiénes componen una inscripción de deporte individual, y las acciones
 * sobre cada uno.
 *
 * Es el panel de detalle de InscripcionesPage, con la misma forma que ya usa
 * Sedes para sus espacios: una lista a la izquierda, lo que cuelga de la fila
 * elegida a la derecha. En un deporte individual la mayoría de las
 * inscripciones son de una sola persona, así que este panel se lee como "la
 * ficha de quien está seleccionado"; el caso de varias filas es la pareja o
 * el trío de Poomsae, que es justo lo único que vuelve necesaria la
 * distinción entre la inscripción y quien la compone.
 *
 * "Sumar integrante" vive acá, en la fila, y no como un botón de cabecera:
 * sumás a *esta* inscripción, la que estás mirando, en vez de a la que un
 * desplegable haya dejado elegida. Ese desplegable era precisamente lo que
 * hacía que el alta pareciera imposible cuando la categoría ya tenía a
 * alguien cargado.
 */
export function MembersPanel({ team, max, onChanged }) {
  const confirm = useConfirm();

  // Una sola fuente para la lista y para el alta: useCrudDialog ya lee la
  // colección que recibe, así que no hace falta un useApi aparte contra la
  // misma dirección.
  const {
    rows: roster, isLoading, mutate, open, form, setForm, error, saving, openCreate, close, save,
  } = useCrudDialog({
    resourceUrl: team ? endpoints.roster(team.id) : null,
    // Igual que en Nómina: uno solo pasa por RegisterPlayer, varios por
    // RegisterPlayersBulk (todo o nada). Función y no string porque a qué
    // dirección ir depende de cuántos terminen elegidos, y eso recién se
    // sabe al guardar.
    createUrl: (f) => (team
      ? (f.athleteIds.length > 1 ? endpoints.rosterRegisterBulk(team.id) : endpoints.roster(team.id))
      : null),
    emptyForm,
    entityName: 'integrante',
    savedMessage: 'Integrante sumado.',
    mapToSend: (f) => (f.athleteIds.length > 1
      ? { athleteIds: f.athleteIds }
      : { athleteId: f.athleteIds[0] }),
    // El nombre de una inscripción individual se recalcula a partir de
    // quiénes la componen (IndividualTeamName), así que sumar a alguien
    // cambia lo que muestra la grilla de la izquierda.
    onSaved: () => onChanged?.(),
  });

  const withdraw = async (entry, retirar) => {
    try {
      await apiPut(endpoints.rosterWithdrawal(entry.id), { withdrawn: retirar });
      mutate();
      onChanged?.();
    } catch (err) { toast.error(err.message); }
  };

  const strike = async (entry) => {
    const ok = await confirm('Anular registro?', { confirmLabel: 'Anular', danger: true });
    if (!ok) return;
    try {
      await apiDelete(endpoints.rosterEntry(entry.id));
      mutate();
      onChanged?.();
    } catch (err) { toast.error(err.message); }
  };

  // El cupo de la categoria (Category.MaxRosterSize) ya lo hace cumplir
  // RosterPolicy del lado del servidor; adelantarlo aca es para no ofrecer
  // un boton cuya unica respuesta posible es un rechazo. Solo cuentan los
  // activos: quien se retiro dejo su lugar libre, que es exactamente lo que
  // cuenta la politica.
  const activos = (roster || []).filter((e) => !e.withdrawnAt).length;
  const completa = max != null && activos >= max;

  const columns = [
    { field: 'lastName', headerName: 'Apellido', flex: 1, minWidth: 110 },
    { field: 'firstName', headerName: 'Nombre', flex: 1, minWidth: 110 },
    { field: 'documentId', headerName: 'Documento', width: 120 },
    { field: 'withdrawnAt', headerName: 'Estado', width: 110, renderCell: ({ value }) => value
      ? <Chip label="Retirado" color="warning" size="small" />
      : <Chip label="Activo" color="success" size="small" /> },
    { field: 'actions', headerName: 'Acciones', width: 90, align: 'center', headerAlign: 'center', renderCell: ({ row: entry }) => {
      const retirado = !!entry.withdrawnAt;
      return (
        <RowActionsMenu
          actions={[
            { icon: retirado ? 'eva:undo-fill' : 'eva:person-done-outline', label: retirado ? 'Reintegrar' : 'Retirar', color: retirado ? 'info.main' : 'warning.main', onClick: () => withdraw(entry, !retirado) },
            !retirado && { icon: 'eva:close-circle-outline', label: 'Anular', color: 'error.main', onClick: () => strike(entry) },
          ].filter(Boolean)}
        />
      );
    }},
  ];

  return (
    <Paper variant="outlined" sx={{ flex: 1, p: 2 }}>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 1.5, gap: 1, flexWrap: 'wrap' }}>
        <Typography variant="h6" fontWeight={600}>
          {team ? `Integrantes de ${team.name}` : 'Integrantes'}
        </Typography>
        {team && (
          <Button
            variant="contained"
            size="small"
            startIcon={<Iconify icon="eva:person-add-outline" width={16} />}
            onClick={openCreate}
            disabled={completa}
          >
            Sumar integrante
          </Button>
        )}
      </Box>

      {team && completa && (
        <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 1 }}>
          Esta categoría admite {max} {max === 1 ? 'integrante' : 'integrantes'}, y ya está completa.
        </Typography>
      )}

      {team ? (
        <DataGrid rows={roster || []} columns={columns} loading={isLoading} autoHeight disableRowSelectionOnClick getRowId={(r) => r.id} />
      ) : (
        <Typography color="text.secondary" sx={{ py: 3 }}>
          Elegí una inscripción para ver quiénes la componen.
        </Typography>
      )}

      <CrudDialog open={open} editId={null} entityName="Integrante" error={error} saving={saving} onClose={close} onSave={save}>
        <SelectionAthletes
          label="Deportistas"
          value={form.athleteIds}
          onChange={(athleteIds) => setForm({ ...form, athleteIds })}
          helperText="Para completar una pareja o un trío ya inscripto."
          required
        />
      </CrudDialog>
    </Paper>
  );
}

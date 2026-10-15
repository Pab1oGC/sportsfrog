import { useState } from 'react';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import TextField from '@mui/material/TextField';
import Button from '@mui/material/Button';
import { DataGrid } from '@mui/x-data-grid';
import { toast } from 'sonner';
import { Iconify } from 'src/components/iconify';
import { useCrudDialog } from 'src/hooks/use-crud';
import { apiPut, apiDelete } from 'src/hooks/use-api';
import { endpoints } from 'src/lib/axios';
import { CrudDialog } from 'src/components/crud-dialog';
import { RowActionsMenu } from 'src/components/row-actions-menu';
import { EstadoChip } from 'src/components/estado-chip';
import { useConfirm } from 'src/components/confirm-dialog';
import { SelectionAthletes } from 'src/components/selectors';
import { bloquearNegativos, soloDecimales } from 'src/lib/entero-sin-signo';
import { fechaHora } from 'src/lib/format-date';
import { TuitionAccountDialog } from 'src/pages/courses/tuition-account-dialog';
import { crearUrlDeInscripcion, mapearEnvioDeInscripcion } from 'src/pages/courses/enrollment-mapping';

const emptyEnrollmentForm = () => ({ athleteIds: [] });
const emptyFeeForm = () => ({ feeOverrideBob: '', feeOverrideReason: '' });

/**
 * Quien esta inscripto en un curso: altas, bajas/reintegros, anulacion y la
 * pension propia de cada inscripcion. Separado de CoursesPage porque es la
 * pestaña que crece -- mismo motivo que AccreditationItemsTab/
 * AccreditationCategoriesTab viven aparte de AccreditationCatalogPage.
 */
export function EnrollmentsTab({ course }) {
  const confirm = useConfirm();

  const {
    rows: enrollments, isLoading, mutate,
    open, form, setForm, error, saving,
    openCreate, close, save,
  } = useCrudDialog({
    resourceUrl: endpoints.courseEnrollments(course.id),
    // Uno solo pasa por EnrollStudent, varios por EnrollStudentsBulk (todo
    // o nada) -- mismo patrón que ya usa Nómina para un equipo, ver
    // enrollment-mapping.js. Función y no string porque a qué dirección ir
    // depende de cuántos terminen elegidos, y eso recién se sabe al guardar.
    createUrl: (f) => crearUrlDeInscripcion(course.id, f),
    emptyForm: emptyEnrollmentForm,
    entityName: 'inscripcion',
    entityGender: 'f',
    // El alta en bloque responde con `enrollmentIds` (plural); el alta
    // individual con un solo `id` -- distinguirlos por la forma de la
    // respuesta en vez de por el form evita depender de un tercer
    // argumento que useCrudDialog.save no le pasa a savedMessage.
    savedMessage: (result) => (result.enrollmentIds
      ? `${result.enrollmentIds.length} estudiantes inscriptos.`
      : 'Estudiante inscripto.'),
    mapToSend: mapearEnvioDeInscripcion,
  });

  const withdraw = async (entry, withdrawn) => {
    try {
      await apiPut(endpoints.enrollmentWithdrawal(entry.id), { withdrawn });
      mutate();
    } catch (err) { toast.error(err.message); }
  };

  const strikeEnrollment = async (entry) => {
    const ok = await confirm(
      `Anular la inscripción de ${entry.firstName} ${entry.lastName}? Solo corresponde si fue un error -- si ya tomó clases, usá "Retirar" en su lugar.`,
      { confirmLabel: 'Anular', danger: true },
    );
    if (!ok) return;
    try {
      await apiDelete(endpoints.enrollment(entry.id));
      mutate();
    } catch (err) { toast.error(err.message); }
  };

  // Pensión por inscripción --------------------------------------------

  const [feeDialog, setFeeDialog] = useState({ open: false, entry: null });
  const [feeForm, setFeeForm] = useState(emptyFeeForm());
  const [feeSaving, setFeeSaving] = useState(false);
  const [feeError, setFeeError] = useState('');

  const openFeeDialog = (entry) => {
    setFeeForm({ feeOverrideBob: entry.feeOverrideBob ?? '', feeOverrideReason: entry.feeOverrideReason || '' });
    setFeeError('');
    setFeeDialog({ open: true, entry });
  };

  const saveFee = async () => {
    setFeeSaving(true);
    setFeeError('');
    try {
      await apiPut(endpoints.enrollmentFee(feeDialog.entry.id), {
        feeOverrideBob: feeForm.feeOverrideBob !== '' ? Number(feeForm.feeOverrideBob) : null,
        feeOverrideReason: feeForm.feeOverrideReason || null,
      });
      setFeeDialog({ open: false, entry: null });
      mutate();
    } catch (err) { setFeeError(err.message); } finally { setFeeSaving(false); }
  };

  // Pensión devengada --------------------------------------------------

  const [tuitionEntry, setTuitionEntry] = useState(null);

  const columns = [
    { field: 'firstName', headerName: 'Nombre', flex: 1, minWidth: 110 },
    { field: 'lastName', headerName: 'Apellido', flex: 1, minWidth: 110 },
    { field: 'documentId', headerName: 'Documento', width: 110 },
    { field: 'enrolledAt', headerName: 'Inscripto', width: 150, renderCell: ({ value }) => fechaHora(value) },
    { field: 'feeOverrideBob', headerName: 'Pensión propia', width: 130, renderCell: ({ value }) => (value == null ? '—' : `Bs ${value}`) },
    { field: 'withdrawnAt', headerName: 'Estado', width: 110, sortable: false, renderCell: ({ value, row }) => (
      <EstadoChip
        activo={!value}
        offLabel="Retirado" offColor="warning"
        onTooltip="Retirar" offTooltip="Reintegrar"
        onClick={() => withdraw(row, !value)}
      />
    )},
    { field: 'actions', headerName: 'Acciones', width: 90, align: 'center', headerAlign: 'center', renderCell: ({ row: entry }) => {
      const withdrawnRow = !!entry.withdrawnAt;
      return (
        <RowActionsMenu
          actions={[
            { icon: withdrawnRow ? 'eva:undo-fill' : 'eva:person-done-outline', label: withdrawnRow ? 'Reintegrar' : 'Retirar', color: withdrawnRow ? 'info.main' : 'warning.main', onClick: () => withdraw(entry, !withdrawnRow) },
            { icon: 'mdi:currency-usd', label: 'Pensión propia', onClick: () => openFeeDialog(entry) },
            { icon: 'mdi:receipt-text-outline', label: 'Ver pensión', onClick: () => setTuitionEntry(entry) },
            { icon: 'eva:close-circle-outline', label: 'Anular', color: 'error.main', onClick: () => strikeEnrollment(entry), hidden: withdrawnRow },
          ]}
        />
      );
    }},
  ];

  return (
    <Box>
      <Box sx={{ display: 'flex', justifyContent: 'flex-end', mb: 1.5 }}>
        <Button variant="contained" size="small" startIcon={<Iconify icon="eva:plus-fill" width={16} />} onClick={openCreate}>
          Inscribir
        </Button>
      </Box>
      <DataGrid rows={enrollments || []} columns={columns} loading={isLoading} autoHeight disableRowSelectionOnClick getRowId={(r) => r.id} />

      <CrudDialog open={open} editId={null} entityName="Inscripcion" entityGender="f" error={error} saving={saving} onClose={close} onSave={save}>
        <SelectionAthletes
          label="Estudiantes"
          value={form.athleteIds}
          onChange={(athleteIds) => setForm({ ...form, athleteIds })}
          helperText="Elegí uno para inscribirlo solo, o varios para inscribirlos juntos de una. Si el estudiante es nuevo, registralo primero desde Deportistas."
          required
        />
      </CrudDialog>

      <CrudDialog
        open={feeDialog.open}
        editId={feeDialog.entry?.id}
        entityName="Pensión propia"
        error={feeError}
        saving={feeSaving}
        onClose={() => setFeeDialog({ open: false, entry: null })}
        onSave={saveFee}
      >
        <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
          Reemplaza la pensión del curso para esta inscripción puntual — una beca o un descuento. Vacío para que vuelva a pagar lo que cobra el curso.
        </Typography>
        <TextField
          label="Monto (Bs)" type="number" value={feeForm.feeOverrideBob} fullWidth
          onChange={(e) => setFeeForm({ ...feeForm, feeOverrideBob: soloDecimales(e.target.value) })}
          onKeyDown={bloquearNegativos}
          slotProps={{ htmlInput: { min: 0, step: 0.01 } }}
          helperText="0 para una beca completa."
        />
        <TextField
          label="Motivo" value={feeForm.feeOverrideReason}
          onChange={(e) => setFeeForm({ ...feeForm, feeOverrideReason: e.target.value })}
          fullWidth required={feeForm.feeOverrideBob !== ''}
          helperText="Obligatorio si se pone un monto."
        />
      </CrudDialog>

      <TuitionAccountDialog enrollment={tuitionEntry} onClose={() => setTuitionEntry(null)} />
    </Box>
  );
}

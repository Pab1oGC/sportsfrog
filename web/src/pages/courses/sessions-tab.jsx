import { useState, lazy, Suspense } from 'react';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import TextField from '@mui/material/TextField';
import Button from '@mui/material/Button';
import CircularProgress from '@mui/material/CircularProgress';
import { toast } from 'sonner';
import { Iconify } from 'src/components/iconify';
import { useApi, apiPost, apiPut } from 'src/hooks/use-api';
import { endpoints } from 'src/lib/axios';
import { CrudDialog } from 'src/components/crud-dialog';
import { useConfirm } from 'src/components/confirm-dialog';
import { DateField, TimeField } from 'src/components/date-field';
import { AttendanceSheetDialog } from 'src/pages/courses/attendance-sheet-dialog';

// Carga diferida: FullCalendar (y sus plugins) pesan bastante mas que el
// resto de CoursesPage junta, y nadie los necesita hasta que entra acá --
// ver CalendarLoading mas abajo, que es lo que se ve mientras ese chunk
// aparte termina de bajar.
const SessionsCalendar = lazy(() =>
  import('src/pages/courses/sessions-calendar').then((m) => ({ default: m.SessionsCalendar })));

function CalendarLoading() {
  return <Box sx={{ display: 'flex', justifyContent: 'center', py: 6 }}><CircularProgress /></Box>;
}

const emptyGenerateForm = () => ({ from: '', to: '' });
const emptyCancelForm = () => ({ reason: '' });

/**
 * El calendario de un curso: generarlo desde el horario semanal, cancelar o
 * reintegrar una clase puntual, y reprogramarla a otra fecha u hora. Pestaña
 * propia por el mismo motivo que EnrollmentsTab lo es.
 *
 * El calendario en sí (la grilla visual, el menú por sesión, el arrastrar
 * para reprogramar) vive en SessionsCalendar -- esta pestaña sigue siendo
 * la dueña de los diálogos y de cuándo se guarda cada cosa, igual que
 * antes de que la tabla se volviera calendario.
 */
export function SessionsTab({ course }) {
  const confirm = useConfirm();
  const { data: sessions, isLoading, mutate } = useApi(endpoints.courseSessions(course.id));

  const [genOpen, setGenOpen] = useState(false);
  const [genForm, setGenForm] = useState(emptyGenerateForm());
  const [genSaving, setGenSaving] = useState(false);
  const [genError, setGenError] = useState('');

  const openGenerate = () => {
    setGenForm(emptyGenerateForm());
    setGenError('');
    setGenOpen(true);
  };

  const generate = async () => {
    setGenSaving(true);
    setGenError('');
    try {
      const result = await apiPost(endpoints.courseSessions(course.id), { from: genForm.from, to: genForm.to });
      setGenOpen(false);
      mutate();
      toast.success(
        result.alreadyScheduled > 0
          ? `${result.generated} sesión(es) nueva(s), ${result.alreadyScheduled} ya existían.`
          : `${result.generated} sesión(es) generada(s).`,
      );
    } catch (err) { setGenError(err.message); } finally { setGenSaving(false); }
  };

  // Cancelar pide motivo (CancelSession.Validator lo exige); reintegrar no.
  const [cancelDialog, setCancelDialog] = useState({ open: false, session: null });
  const [cancelForm, setCancelForm] = useState(emptyCancelForm());
  const [cancelSaving, setCancelSaving] = useState(false);
  const [cancelError, setCancelError] = useState('');

  const openCancelDialog = (session) => {
    setCancelForm(emptyCancelForm());
    setCancelError('');
    setCancelDialog({ open: true, session });
  };

  const saveCancel = async () => {
    setCancelSaving(true);
    setCancelError('');
    try {
      await apiPut(endpoints.sessionCancellation(cancelDialog.session.id), { cancelled: true, reason: cancelForm.reason });
      setCancelDialog({ open: false, session: null });
      mutate();
    } catch (err) { setCancelError(err.message); } finally { setCancelSaving(false); }
  };

  const reinstate = async (session) => {
    const ok = await confirm(`Reintegrar la sesión del ${session.sessionDate}?`);
    if (!ok) return;
    try {
      await apiPut(endpoints.sessionCancellation(session.id), { cancelled: false, reason: null });
      mutate();
    } catch (err) { toast.error(err.message); }
  };

  const [rescheduleDialog, setRescheduleDialog] = useState({ open: false, session: null });
  const [rescheduleForm, setRescheduleForm] = useState({ sessionDate: '', startsAt: '', endsAt: '' });
  const [rescheduleSaving, setRescheduleSaving] = useState(false);
  const [rescheduleError, setRescheduleError] = useState('');

  const openReschedule = (session) => {
    setRescheduleForm({ sessionDate: session.sessionDate, startsAt: session.startsAt, endsAt: session.endsAt });
    setRescheduleError('');
    setRescheduleDialog({ open: true, session });
  };

  const saveReschedule = async () => {
    setRescheduleSaving(true);
    setRescheduleError('');
    try {
      await apiPut(endpoints.session(rescheduleDialog.session.id), rescheduleForm);
      setRescheduleDialog({ open: false, session: null });
      mutate();
    } catch (err) { setRescheduleError(err.message); } finally { setRescheduleSaving(false); }
  };

  // Arrastrar un evento en SessionsCalendar es el mismo PUT que saveReschedule
  // ya hacía, con la fecha/horario que FullCalendar calculó del arrastre en
  // vez de los que trae el formulario -- sin diálogo, porque arrastrar ya es
  // la confirmación. `revert` (que FullCalendar da) devuelve el evento a su
  // lugar si el servidor lo rechaza (otra sesión ya en esa fecha y hora).
  const dragReschedule = async (session, patch, revert) => {
    try {
      await apiPut(endpoints.session(session.id), patch);
      mutate();
    } catch (err) {
      toast.error(err.message);
      revert();
    }
  };

  // Asistencia de una sesión puntual -----------------------------------

  const [attendanceSession, setAttendanceSession] = useState(null);

  const closeAttendance = (saved) => {
    setAttendanceSession(null);
    // Guardar puede pasar la sesión de Programada a Dictada -- la grilla
    // tiene que enterarse.
    if (saved) mutate();
  };

  return (
    <Box>
      <Box sx={{ display: 'flex', justifyContent: 'flex-end', mb: 1.5 }}>
        <Button variant="contained" size="small" startIcon={<Iconify icon="eva:calendar-outline" width={16} />} onClick={openGenerate}>
          Generar calendario
        </Button>
      </Box>

      {isLoading && (
        <Box sx={{ display: 'flex', justifyContent: 'center', py: 6 }}><CircularProgress /></Box>
      )}

      {!isLoading && (
        <Suspense fallback={<CalendarLoading />}>
          <SessionsCalendar
            sessions={sessions}
            onAttendance={setAttendanceSession}
            onReschedule={openReschedule}
            onCancel={openCancelDialog}
            onReinstate={reinstate}
            onDragReschedule={dragReschedule}
          />
        </Suspense>
      )}

      <CrudDialog open={genOpen} editId={null} entityName="calendario" error={genError} saving={genSaving} onClose={() => setGenOpen(false)} onSave={generate}>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
          Genera una sesión por cada franja del horario semanal dentro del rango. Las que ya existen no se duplican, y el rango se recorta a la vida del curso.
        </Typography>
        <DateField label="Desde" value={genForm.from} onChange={(e) => setGenForm({ ...genForm, from: e.target.value })} fullWidth required />
        <DateField label="Hasta" value={genForm.to} onChange={(e) => setGenForm({ ...genForm, to: e.target.value })} fullWidth required />
      </CrudDialog>

      <CrudDialog open={cancelDialog.open} editId={cancelDialog.session?.id} entityName="sesión" error={cancelError} saving={cancelSaving} onClose={() => setCancelDialog({ open: false, session: null })} onSave={saveCancel}>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
          {cancelDialog.session && `Cancelar la sesión del ${cancelDialog.session.sessionDate}.`}
        </Typography>
        <TextField
          label="Motivo" value={cancelForm.reason}
          onChange={(e) => setCancelForm({ reason: e.target.value })}
          fullWidth required multiline minRows={2}
        />
      </CrudDialog>

      <CrudDialog open={rescheduleDialog.open} editId={rescheduleDialog.session?.id} entityName="sesión" error={rescheduleError} saving={rescheduleSaving} onClose={() => setRescheduleDialog({ open: false, session: null })} onSave={saveReschedule}>
        <DateField label="Fecha" value={rescheduleForm.sessionDate} onChange={(e) => setRescheduleForm({ ...rescheduleForm, sessionDate: e.target.value })} fullWidth required />
        <TimeField label="Desde" value={rescheduleForm.startsAt} onChange={(e) => setRescheduleForm({ ...rescheduleForm, startsAt: e.target.value })} fullWidth required />
        <TimeField label="Hasta" value={rescheduleForm.endsAt} onChange={(e) => setRescheduleForm({ ...rescheduleForm, endsAt: e.target.value })} fullWidth required />
      </CrudDialog>

      <AttendanceSheetDialog session={attendanceSession} onClose={closeAttendance} />
    </Box>
  );
}

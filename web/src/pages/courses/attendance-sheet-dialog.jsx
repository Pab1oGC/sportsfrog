import { useEffect, useState } from 'react';
import Dialog from '@mui/material/Dialog';
import DialogTitle from '@mui/material/DialogTitle';
import DialogContent from '@mui/material/DialogContent';
import DialogActions from '@mui/material/DialogActions';
import Button from '@mui/material/Button';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import TextField from '@mui/material/TextField';
import ToggleButton from '@mui/material/ToggleButton';
import ToggleButtonGroup from '@mui/material/ToggleButtonGroup';
import CircularProgress from '@mui/material/CircularProgress';
import Alert from '@mui/material/Alert';
import { useApi, apiPut } from 'src/hooks/use-api';
import { endpoints } from 'src/lib/axios';

// Los cuatro valores de AttendanceState (SnakeCaseEnumConverter del lado
// del servidor), tal cual viajan.
const ESTADOS = [
  { value: 'present', label: 'Presente' },
  { value: 'late', label: 'Tarde' },
  { value: 'excused', label: 'Con aviso' },
  { value: 'absent', label: 'Ausente' },
];

/**
 * La planilla de una sesion: una fila por estudiante, el estado se elige
 * tocando un boton grande -- pensada para completarse desde una tablet en
 * la cancha, no tipeando. Un solo "Guardar" manda la planilla entera
 * (RecordAttendance.Request) de una, no una marca a la vez.
 *
 * `session` nulo mantiene el dialogo cerrado y sin pedir nada -- el mismo
 * patron que ya usan los dialogos de EnrollmentsTab (`feeDialog.entry`).
 * `onClose(guardado)` avisa al padre si hay que refrescar la grilla de
 * sesiones (guardar puede pasar la sesión de Programada a Dictada).
 */
export function AttendanceSheetDialog({ session, onClose }) {
  const open = !!session;
  const { data: sheet, isLoading } = useApi(open ? endpoints.sessionAttendance(session.id) : null);
  const [marks, setMarks] = useState({});
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');

  // Precarga lo que ya estaba marcado cada vez que se abre una sesion
  // distinta -- no al tipear, por eso `sheet` y no `marks` en las deps.
  useEffect(() => {
    const initial = {};
    (sheet || []).forEach((row) => {
      if (row.status) initial[row.enrollmentId] = { status: row.status, note: row.note || '' };
    });
    setMarks(initial);
    setError('');
  }, [sheet]);

  const setStatus = (enrollmentId, status) => {
    setMarks((current) => {
      if (!status) {
        const { [enrollmentId]: _omit, ...rest } = current;
        return rest;
      }
      return { ...current, [enrollmentId]: { status, note: current[enrollmentId]?.note || '' } };
    });
  };

  const setNote = (enrollmentId, note) => {
    setMarks((current) => (current[enrollmentId] ? { ...current, [enrollmentId]: { ...current[enrollmentId], note } } : current));
  };

  const save = async () => {
    setSaving(true);
    setError('');
    try {
      await apiPut(endpoints.sessionAttendance(session.id), {
        marks: Object.entries(marks).map(([enrollmentId, mark]) => ({
          enrollmentId, status: mark.status, note: mark.note || null,
        })),
      });
      onClose(true);
    } catch (err) { setError(err.message); } finally { setSaving(false); }
  };

  const marcados = Object.keys(marks).length;
  const roster = sheet || [];

  return (
    <Dialog open={open} onClose={() => onClose(false)} maxWidth="sm" fullWidth>
      <DialogTitle>Asistencia{session ? ` — ${session.sessionDate}` : ''}</DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 1.5, pt: '16px !important' }}>
        {error && <Alert severity="error">{error}</Alert>}
        {isLoading && (
          <Box sx={{ display: 'flex', justifyContent: 'center', py: 3 }}><CircularProgress /></Box>
        )}
        {!isLoading && roster.length === 0 && (
          <Typography color="text.secondary">Este curso no tiene estudiantes activos en esta fecha.</Typography>
        )}
        {roster.map((row) => (
          <Box key={row.enrollmentId} sx={{ display: 'flex', flexDirection: 'column', gap: 1, p: 1.5, border: '1px solid', borderColor: 'divider', borderRadius: 1 }}>
            <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 1 }}>
              <Typography fontWeight={600}>{row.lastName} {row.firstName}</Typography>
              <ToggleButtonGroup
                value={marks[row.enrollmentId]?.status || null}
                exclusive
                size="small"
                onChange={(_, value) => setStatus(row.enrollmentId, value)}
              >
                {ESTADOS.map((estado) => (
                  <ToggleButton key={estado.value} value={estado.value} sx={{ px: 1.5 }}>
                    {estado.label}
                  </ToggleButton>
                ))}
              </ToggleButtonGroup>
            </Box>
            {marks[row.enrollmentId] && (
              <TextField
                size="small" placeholder="Nota (opcional)" value={marks[row.enrollmentId].note}
                onChange={(e) => setNote(row.enrollmentId, e.target.value)}
                fullWidth
              />
            )}
          </Box>
        ))}
      </DialogContent>
      <DialogActions>
        <Button onClick={() => onClose(false)} disabled={saving}>Cancelar</Button>
        <Button variant="contained" onClick={save} disabled={saving || marcados === 0}>
          {saving ? <CircularProgress size={20} sx={{ mr: 1 }} /> : null}
          Guardar ({marcados}/{roster.length})
        </Button>
      </DialogActions>
    </Dialog>
  );
}

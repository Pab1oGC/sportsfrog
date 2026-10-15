import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import { DataGrid } from '@mui/x-data-grid';
import { useApi } from 'src/hooks/use-api';
import { endpoints } from 'src/lib/axios';

/**
 * El porcentaje de asistencia de cada estudiante inscripto, hasta hoy.
 * Pestaña propia por el mismo motivo que EnrollmentsTab/SessionsTab lo son.
 */
export function AttendanceSummaryTab({ course }) {
  const { data: summary, isLoading } = useApi(endpoints.courseAttendanceSummary(course.id));

  const columns = [
    { field: 'firstName', headerName: 'Nombre', flex: 1, minWidth: 110 },
    { field: 'lastName', headerName: 'Apellido', flex: 1, minWidth: 110 },
    { field: 'documentId', headerName: 'Documento', width: 110 },
    { field: 'recorded', headerName: 'Clases registradas', width: 150 },
    { field: 'present', headerName: 'Presente', width: 90 },
    { field: 'late', headerName: 'Tarde', width: 80 },
    { field: 'excused', headerName: 'Con aviso', width: 100 },
    { field: 'absent', headerName: 'Ausente', width: 90 },
    { field: 'rate', headerName: 'Asistencia', width: 110, renderCell: ({ value }) => (value == null ? '—' : `${Math.round(value * 100)}%`) },
  ];

  return (
    <Box>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 1.5 }}>
        Sobre las clases dictadas y registradas — una sin tomar no cuenta como ausencia, y una cancelada no cuenta en absoluto.
      </Typography>
      <DataGrid rows={summary || []} columns={columns} loading={isLoading} autoHeight disableRowSelectionOnClick getRowId={(r) => r.enrollmentId} />
    </Box>
  );
}

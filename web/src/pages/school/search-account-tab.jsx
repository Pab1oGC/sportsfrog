import { useState } from 'react';
import Box from '@mui/material/Box';
import TextField from '@mui/material/TextField';
import MenuItem from '@mui/material/MenuItem';
import Typography from '@mui/material/Typography';
import { DataGrid } from '@mui/x-data-grid';
import { useApi } from 'src/hooks/use-api';
import { endpoints } from 'src/lib/axios';
import { RowActionsMenu } from 'src/components/row-actions-menu';
import { fechaHora } from 'src/lib/format-date';
import { TuitionAccountDialog } from 'src/pages/courses/tuition-account-dialog';

/**
 * El estado de cuenta por estudiante: elegís un curso, elegís una
 * inscripción, y se abre su pensión -- la misma TuitionAccountDialog que
 * EnrollmentsTab ya usa desde Cursos. Pestaña propia dentro de Cobranza,
 * separada de "En mora", porque cobrar una pensión puntual no depende de
 * administrar el curso en sí -- ver RecordPayment.cs del lado del
 * servidor, que por eso mismo no exige scope de curso en ninguno de sus
 * endpoints.
 */
export function SearchAccountTab() {
  const { data: courses, isLoading: loadingCourses } = useApi(endpoints.courses);
  const [courseId, setCourseId] = useState('');
  const { data: enrollments, isLoading: loadingEnrollments } =
    useApi(courseId ? endpoints.courseEnrollments(courseId) : null);
  const [tuitionEntry, setTuitionEntry] = useState(null);

  const columns = [
    { field: 'firstName', headerName: 'Nombre', flex: 1, minWidth: 110 },
    { field: 'lastName', headerName: 'Apellido', flex: 1, minWidth: 110 },
    { field: 'documentId', headerName: 'Documento', width: 110 },
    { field: 'enrolledAt', headerName: 'Inscripto', width: 150, renderCell: ({ value }) => fechaHora(value) },
    { field: 'withdrawnAt', headerName: 'Estado', width: 100, sortable: false,
      renderCell: ({ value }) => (value ? 'Retirado' : 'Activo') },
    { field: 'actions', headerName: 'Pensión', width: 90, align: 'center', headerAlign: 'center',
      renderCell: ({ row }) => (
        <RowActionsMenu
          primary={{ icon: 'mdi:receipt-text-outline', label: 'Ver pensión', onClick: () => setTuitionEntry(row) }}
        />
      ) },
  ];

  return (
    <Box>
      <TextField
        select label="Curso" value={courseId} fullWidth sx={{ maxWidth: 360, mb: 2 }}
        disabled={loadingCourses}
        onChange={(e) => setCourseId(e.target.value)}
        helperText="Elegí un curso para ver sus inscripciones y gestionar sus pensiones."
      >
        {(courses || []).map((course) => (
          <MenuItem key={course.id} value={course.id}>{course.name}</MenuItem>
        ))}
      </TextField>

      {!courseId && (
        <Typography color="text.secondary" sx={{ py: 1 }}>
          Elegí un curso arriba para buscar la cuenta de un estudiante.
        </Typography>
      )}

      {courseId && (
        <DataGrid
          rows={enrollments || []} columns={columns} loading={loadingEnrollments}
          autoHeight disableRowSelectionOnClick getRowId={(r) => r.id}
          onRowClick={(p) => setTuitionEntry(p.row)} sx={{ cursor: 'pointer' }}
        />
      )}

      <TuitionAccountDialog enrollment={tuitionEntry} onClose={() => setTuitionEntry(null)} />
    </Box>
  );
}

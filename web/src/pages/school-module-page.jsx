import Box from '@mui/material/Box';
import Card from '@mui/material/Card';
import CardActionArea from '@mui/material/CardActionArea';
import CardContent from '@mui/material/CardContent';
import Chip from '@mui/material/Chip';
import Grid from '@mui/material/Grid';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import { Link as RouterLink } from 'react-router';
import { Iconify } from 'src/components/iconify';
import { PageHeader } from 'src/components/page-header';

// El backend ya protege estos flujos con las mismas reglas de organización y
// de inscripción que necesita una escuela. Crear tablas paralelas de alumnos
// y cursos duplicaría identidad, fotos y validaciones; esta capa traduce el
// vocabulario escolar hacia el modelo único del sistema.
export const schoolModuleLinks = [
  {
    title: 'Registro de alumnos',
    description: 'Alta, datos personales, tutor y fotografía de cada alumno.',
    path: '/dashboard/athletes',
    icon: 'mdi:school-outline',
    badge: 'Alumno = deportista',
  },
  {
    title: 'Cursos e inscripciones',
    description: 'Definí cursos por categoría y asigná a cada alumno a su inscripción.',
    path: '/dashboard/categories',
    icon: 'mdi:account-school-outline',
    badge: 'Curso = categoría',
  },
  {
    title: 'Asignar alumnos',
    description: 'Completá los grupos o equipos de cada curso para una competencia.',
    path: '/dashboard/teams',
    icon: 'mdi:account-plus-outline',
    badge: 'Inscripción',
  },
  {
    title: 'Competencias',
    description: 'Revisá categorías, participantes y el estado de cada competencia.',
    path: '/dashboard/competitions',
    icon: 'mdi:trophy-outline',
    badge: 'Organización',
  },
  {
    title: 'Credenciales',
    description: 'Ajustá la plantilla, fotografía e impresión de las credenciales.',
    path: '/dashboard/documents',
    icon: 'mdi:card-account-details-outline',
    badge: 'Impresión',
  },
  {
    title: 'Fixtures y reportes',
    description: 'Consultá horarios, partidos, participantes y reportes de equipos.',
    path: '/dashboard/matches',
    icon: 'mdi:calendar-clock-outline',
    badge: 'Calendario',
  },
];

export default function SchoolModulePage() {
  return (
    <Box>
      <PageHeader title="Gestión escolar" />

      <Card variant="outlined" sx={{ mb: 3, bgcolor: 'primary.lighter' }}>
        <CardContent>
          <Stack direction="row" spacing={1} alignItems="center" sx={{ mb: 1 }}>
            <Iconify icon="mdi:information-outline" width={22} />
            <Typography variant="subtitle1" fontWeight={700}>Un solo padrón, sin duplicar datos</Typography>
          </Stack>
          <Typography variant="body2">
            En este módulo, un alumno se administra como deportista y un curso se organiza mediante categorías e inscripciones.
            Así, su foto, datos y participación se mantienen consistentes en toda la competencia.
          </Typography>
        </CardContent>
      </Card>

      <Grid container spacing={2}>
        {schoolModuleLinks.map((item) => (
          <Grid key={item.path} size={{ xs: 12, sm: 6, lg: 4 }}>
            <Card variant="outlined" sx={{ height: '100%' }}>
              <CardActionArea component={RouterLink} to={item.path} sx={{ height: '100%', alignItems: 'stretch' }}>
                <CardContent>
                  <Stack direction="row" justifyContent="space-between" spacing={1} sx={{ mb: 2 }}>
                    <Iconify icon={item.icon} width={28} color="primary.main" />
                    <Chip label={item.badge} size="small" color="primary" variant="outlined" />
                  </Stack>
                  <Typography variant="subtitle1" fontWeight={700} sx={{ mb: 0.5 }}>{item.title}</Typography>
                  <Typography variant="body2" color="text.secondary">{item.description}</Typography>
                </CardContent>
              </CardActionArea>
            </Card>
          </Grid>
        ))}
      </Grid>
    </Box>
  );
}

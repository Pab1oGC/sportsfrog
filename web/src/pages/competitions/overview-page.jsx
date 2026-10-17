import { useRef, useState } from 'react';
import { useParams, useNavigate, Link as RouterLink } from 'react-router';
import Box from '@mui/material/Box';
import Paper from '@mui/material/Paper';
import Grid from '@mui/material/Grid';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import Chip from '@mui/material/Chip';
import Button from '@mui/material/Button';
import Divider from '@mui/material/Divider';
import Tabs from '@mui/material/Tabs';
import Tab from '@mui/material/Tab';
import Alert from '@mui/material/Alert';
import CircularProgress from '@mui/material/CircularProgress';
import { Iconify } from 'src/components/iconify';
import { StatCard } from 'src/components/stat-card';
import { EstadoChip } from 'src/components/estado-chip';
import { useApi } from 'src/hooks/use-api';
import { endpoints } from 'src/lib/axios';
import {
  STATUS_LABEL, STATUS_COLOR, STATUS_ICON, NEXT_STATUS, FORMAT_LABEL,
  motivoPublicacionInhabilitada,
} from 'src/pages/competitions/competition-lifecycle';
import { useCompetitionActions } from 'src/pages/competitions/use-competition-actions';
import { SetupChecklist } from 'src/pages/competitions/setup-checklist';
import { CategoriesPanel } from 'src/pages/categories/categories-panel';
import { TeamsPanel } from 'src/pages/teams/teams-panel';
import { RosterPanel } from 'src/pages/roster/roster-panel';

// "aaaa-mm-dd" (DateOnly del servidor) a "dd/mm/aaaa". Nada de Date() de por
// medio: un DateOnly no tiene hora ni zona, y pasarlo por Date lo
// interpretaría a medianoche UTC -- un día antes en cualquier huso al oeste
// de Greenwich.
const dateOnly = (iso) => (iso ? iso.split('-').reverse().join('/') : null);

/**
 * El "centro de mando" de una competencia: lo que hoy vive repartido entre
 * la grilla de Competencias, Categorías y Partidos, visto desde un solo
 * lugar. El encabezado (nombre, estado, publicación, "siguiente paso") es
 * fijo -- igual que CollectionSummary en Cobranza -- y las pestañas de abajo
 * son lo que hoy todavía son pantallas aparte.
 *
 * "Categorías", "Equipos" y "Nómina" ya se mudaron de pantalla propia a
 * pestaña (ver CategoriesPanel/TeamsPanel/RosterPanel, de donde sale su
 * contenido real): configurar una competencia entera -- sus divisiones, sus
 * equipos, sus planteles -- no exige salir de esta página. "Partidos" sigue
 * siendo una tarjeta que lleva a su pantalla de siempre -- a propósito: es
 * la única isla que queda sin migrar.
 *
 * Todo lo que se muestra sale de datos que ya existen (GET /competitions/
 * {id} y GET /competitions/{id}/matches): nada nuevo que pedirle al
 * servidor, solo una forma de leerlo junta.
 */
export default function CompetitionOverviewPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [tab, setTab] = useState('resumen');
  const publicarRef = useRef(null);

  const { data: comp, isLoading, error, mutate } = useApi(endpoints.competition(id));
  const { data: sports } = useApi(endpoints.sports);
  const { data: matches } = useApi(endpoints.competitionMatches(id));

  const { changeStatus, togglePublish, descargarConvocatoria } = useCompetitionActions(mutate);

  if (isLoading || (!comp && !error)) {
    return <Box sx={{ display: 'flex', justifyContent: 'center', mt: 10 }}><CircularProgress /></Box>;
  }

  if (error || !comp) {
    return <Alert severity="error">No se pudo cargar la competencia.</Alert>;
  }

  const sportName = sports?.find((s) => s.code === comp.sportCode)?.name || comp.sportCode;
  const motivoSinPublicar = motivoPublicacionInhabilitada(comp);
  const sinProgramar = (matches || []).filter((m) => !m.scheduledAt).length;
  const hayCalendario = (matches || []).length > 0;

  // Refleja, para orientar, las mismas condiciones que ChangeCompetitionStatus
  // ya hace cumplir del lado del servidor -- no las repite para validar nada,
  // solo para que se vean antes de intentar el paso que las necesita.
  const pasos = [
    {
      label: 'Al menos una categoría',
      hecho: comp.categoryCount > 0,
      pendiente: 'Sin categorías no hay nada que sortear ni programar.',
      onClick: () => setTab('categorias'),
    },
    {
      label: 'Calendario sorteado',
      hecho: hayCalendario,
      pendiente: 'Sorteá el cuadro o generá la primera jornada desde Partidos.',
      onClick: () => navigate(`/dashboard/matches?competition=${comp.id}`),
    },
    {
      label: 'Publicada',
      hecho: comp.isPublic,
      pendiente: motivoSinPublicar || 'Activá "Pública" cuando esté lista para mostrarse.',
      onClick: () => publicarRef.current?.scrollIntoView({ behavior: 'smooth', block: 'center' }),
    },
  ];

  const siguientesEstados = NEXT_STATUS[comp.status] || [];

  return (
    <Box>
      <Button
        component={RouterLink}
        to="/dashboard/competitions"
        size="small"
        startIcon={<Iconify icon="eva:arrow-back-fill" width={18} />}
        sx={{ mb: 2 }}
      >
        Competencias
      </Button>

      <Paper variant="outlined" sx={{ p: 3, mb: 3, borderRadius: 2 }}>
        <Stack direction="row" justifyContent="space-between" alignItems="flex-start" flexWrap="wrap" spacing={2}>
          <Box>
            <Stack direction="row" alignItems="center" spacing={1.5} flexWrap="wrap">
              <Typography variant="h4" fontWeight={700}>{comp.name}</Typography>
              <Chip
                label={STATUS_LABEL[comp.status] || comp.status}
                color={STATUS_COLOR[comp.status] || 'default'}
                icon={<Iconify icon={STATUS_ICON[comp.status]} width={16} />}
              />
            </Stack>
            <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
              {comp.season} · {sportName} · {FORMAT_LABEL[comp.format] || comp.format}
              {(comp.startsOn || comp.endsOn) && (
                <> · {dateOnly(comp.startsOn) || '¿?'} – {dateOnly(comp.endsOn) || '¿?'}</>
              )}
            </Typography>
          </Box>

          <Stack direction="row" alignItems="center" spacing={1} ref={publicarRef}>
            <EstadoChip
              activo={comp.isPublic}
              onClick={() => togglePublish(comp.id, comp.isPublic)}
              onLabel="Pública"
              offLabel="No pública"
              onIcon="eva:globe-fill"
              offIcon="eva:eye-off-outline"
              onTooltip="Despublicar"
              offTooltip="Publicar"
              disabled={!!motivoSinPublicar}
              disabledTooltip={motivoSinPublicar}
            />
          </Stack>
        </Stack>

        {siguientesEstados.length > 0 && (
          <>
            <Divider sx={{ my: 2 }} />
            <Stack direction="row" alignItems="center" spacing={1.5} flexWrap="wrap">
              <Typography variant="body2" color="text.secondary">Siguiente paso:</Typography>
              {siguientesEstados.map((next) => (
                <Button
                  key={next}
                  size="small"
                  variant="outlined"
                  color={STATUS_COLOR[next] === 'default' ? 'inherit' : STATUS_COLOR[next]}
                  startIcon={<Iconify icon={STATUS_ICON[next]} width={16} />}
                  onClick={() => changeStatus(comp.id, next)}
                >
                  {STATUS_LABEL[next]}
                </Button>
              ))}
            </Stack>
          </>
        )}
      </Paper>

      <Tabs value={tab} onChange={(_, v) => setTab(v)} sx={{ mb: 2 }}>
        <Tab value="resumen" label="Resumen" />
        <Tab value="categorias" label="Categorías" />
        <Tab value="equipos" label="Equipos" />
        <Tab value="nomina" label="Nómina" />
      </Tabs>

      {tab === 'resumen' && (
        <>
          <SetupChecklist items={pasos} />

          <Typography variant="subtitle2" color="text.secondary" sx={{ mb: 1 }}>Recomendado</Typography>
          <Grid container spacing={2} sx={{ mb: 3 }}>
            <Grid size={{ xs: 12, sm: 3 }}>
              <StatCard
                title="Categorías"
                value={comp.categoryCount}
                icon="mdi:shape-outline"
                color="primary"
                // La primera en mudarse a pestaña: cambia de pestaña en vez
                // de navegar a otra pantalla -- ver el comentario de arriba
                // del componente.
                onClick={() => setTab('categorias')}
              />
            </Grid>
            <Grid size={{ xs: 12, sm: 3 }}>
              <StatCard
                title={sinProgramar > 0 ? `Partidos (${sinProgramar} sin programar)` : 'Partidos'}
                value={(matches || []).length}
                icon="eva:list-outline"
                color="info"
                onClick={() => navigate(`/dashboard/matches?competition=${comp.id}`)}
              />
            </Grid>
            <Grid size={{ xs: 12, sm: 3 }}>
              {/*
                Sin `value`: a diferencia de Categorías/Partidos/Portal, no
                hay ningún dato ya cargado que diga "cuántos equipos tienen
                nómina" -- inventar un número acá sería fingir una métrica
                que no existe. StatCard ya sabe mostrar "--" en ese caso.
              */}
              <StatCard
                title="Nómina"
                icon="mdi:account-group-outline"
                color="secondary"
                onClick={() => setTab('nomina')}
              />
            </Grid>
            <Grid size={{ xs: 12, sm: 3 }}>
              <StatCard
                title="Portal público"
                value={comp.isPublic ? 'Publicado' : 'Sin publicar'}
                icon="mdi:palette-outline"
                color={comp.isPublic ? 'success' : 'warning'}
                onClick={() => navigate(`/dashboard/competitions/${comp.id}/portal`)}
              />
            </Grid>
          </Grid>

          <Typography variant="subtitle2" color="text.secondary" sx={{ mb: 1 }}>Convocatoria</Typography>
          <Paper variant="outlined" sx={{ p: 2, borderRadius: 2 }}>
            <Stack direction="row" spacing={1} flexWrap="wrap">
              <Button
                variant="outlined"
                size="small"
                startIcon={<Iconify icon="mdi:file-pdf-box" width={18} />}
                onClick={() => descargarConvocatoria(comp.id, 'pdf')}
              >
                Descargar PDF
              </Button>
              <Button
                variant="outlined"
                size="small"
                startIcon={<Iconify icon="mdi:file-word-box" width={18} />}
                onClick={() => descargarConvocatoria(comp.id, 'docx')}
              >
                Descargar Word
              </Button>
            </Stack>
          </Paper>
        </>
      )}

      {tab === 'categorias' && <CategoriesPanel competitionId={comp.id} />}
      {tab === 'equipos' && <TeamsPanel competitionId={comp.id} />}
      {tab === 'nomina' && <RosterPanel competitionId={comp.id} />}
    </Box>
  );
}

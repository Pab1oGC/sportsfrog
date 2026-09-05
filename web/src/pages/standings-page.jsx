import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import { DataGrid } from '@mui/x-data-grid';
import { useApi } from 'src/hooks/use-api';
import { useCascade } from 'src/hooks/use-cascade';
import { endpoints } from 'src/lib/axios';
import { PageHeader } from 'src/components/page-header';
import { SelectionCompetition, SelectionCategory } from 'src/components/selectors';
import { columnasMarcador } from 'src/lib/tiebreaker-labels';

export default function StandingsPage() {
  const cascade = useCascade();
  const { data, isLoading } = useApi(cascade.catId ? endpoints.standings(cascade.catId) : null);
  const { data: sports } = useApi(endpoints.sports);

  const groups = data?.groups || [];
  const sportInfo = (sports || []).find((s) => s.code === data?.sportCode);
  const columnasScore = columnasMarcador(sportInfo);

  const cols = [
    { field: 'position', headerName: '#', width: 50 },
    { field: 'teamName', headerName: 'Equipo', flex: 1, minWidth: 200 },
    { field: 'played', headerName: 'PJ', width: 50 },
    { field: 'won', headerName: 'PG', width: 50 },
    // No es que la columna siempre de cero: es que este reglamento no tiene
    // un empate que precie -- bajo sets porque el modo no lo tiene, en un
    // deporte de suma porque estos organizadores no le pusieron puntaje. La
    // pregunta directamente no aplica.
    data?.allowsDraw && { field: 'drawn', headerName: 'PE', width: 50 },
    { field: 'lost', headerName: 'PP', width: 50 },
    { field: 'scoreFor', headerName: columnasScore.favor, width: 50 },
    { field: 'scoreAgainst', headerName: columnasScore.contra, width: 50 },
    { field: 'scoreDifference', headerName: 'DF', width: 50 },
    { field: 'points', headerName: 'Pts', width: 60 },
  ].filter(Boolean);

  return (
    <Box>
      <PageHeader title="Tabla de Posiciones" />
      <CascadeFilters cascade={cascade} />
      {!cascade.catId && <Typography color="text.secondary">Selecciona una competicion y categoria.</Typography>}
      {cascade.catId && !isLoading && groups.length === 0 && (
        <Typography color="text.secondary">Todavia no hay partidos jugados en esta categoria.</Typography>
      )}
      {/* Una tabla por grupo, no solo la primera: una fase de grupos tiene
          tantas tablas independientes como grupos, y mostrar una sola
          escondía el resto detras de una etiqueta que ni siquiera se leia
          bien. */}
      {groups.map((g, i) => {
        const rows = (g.rows || []).map((r, idx) => ({ ...r, id: r.teamId, position: idx + 1 }));
        return (
          <Box key={g.label || i} sx={{ mb: 3 }}>
            {groups.length > 1 && (
              <Typography variant="h6" fontWeight={700} sx={{ mb: 1 }}>
                Grupo {g.label || i + 1}
              </Typography>
            )}
            <DataGrid rows={rows} columns={cols} loading={isLoading} autoHeight hideFooter disableRowSelectionOnClick getRowId={(r) => r.id} />
          </Box>
        );
      })}
    </Box>
  );
}

function CascadeFilters({ cascade }) {
  return (
    <Box sx={{ display: 'flex', gap: 2, mb: 3, maxWidth: 700, flexWrap: 'wrap' }}>
      <Box sx={{ flex: 1, minWidth: 200 }}><SelectionCompetition value={cascade.compId} onChange={(e) => cascade.setCompId(e.target.value)} required /></Box>
      <Box sx={{ flex: 1, minWidth: 200 }}><SelectionCategory competitionId={cascade.compId} value={cascade.catId} onChange={(e) => cascade.setCatId(e.target.value)} required /></Box>
    </Box>
  );
}

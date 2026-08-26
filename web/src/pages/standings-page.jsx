import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import { DataGrid } from '@mui/x-data-grid';
import { useApi } from 'src/hooks/use-api';
import { useCascade } from 'src/hooks/use-cascade';
import { endpoints } from 'src/lib/axios';
import { PageHeader } from 'src/components/page-header';
import { SelectionCompetition, SelectionCategory } from 'src/components/selectors';

const COLS = [
  { field: 'position', headerName: '#', width: 50 },
  { field: 'teamName', headerName: 'Equipo', flex: 1, minWidth: 200 },
  { field: 'played', headerName: 'PJ', width: 50 },
  { field: 'won', headerName: 'PG', width: 50 },
  { field: 'drawn', headerName: 'PE', width: 50 },
  { field: 'lost', headerName: 'PP', width: 50 },
  { field: 'scoreFor', headerName: 'GF', width: 50 },
  { field: 'scoreAgainst', headerName: 'GC', width: 50 },
  { field: 'scoreDifference', headerName: 'DF', width: 50 },
  { field: 'points', headerName: 'Pts', width: 60 },
];

export default function StandingsPage() {
  const cascade = useCascade();
  const { data, isLoading } = useApi(cascade.catId ? endpoints.standings(cascade.catId) : null);

  const groups = data?.groups || [];
  const firstGroup = groups[0];
  const rows = (firstGroup?.rows || []).map((r, i) => ({ ...r, id: r.teamId, position: i + 1 }));

  return (
    <Box>
      <PageHeader title="Tabla de Posiciones" />
      <CascadeFilters cascade={cascade} />
      {!cascade.catId && <Typography color="text.secondary">Selecciona una competicion y categoria.</Typography>}
      {groups.length > 1 && (
        <Box sx={{ mb: 2, display: 'flex', gap: 1, flexWrap: 'wrap' }}>
          {groups.map((g, i) => (
            <Typography key={i} variant="body2" fontWeight={600}>Grupo: {g.groupLabel || (i + 1)}</Typography>
          ))}
        </Box>
      )}
      <DataGrid rows={rows} columns={COLS} loading={isLoading} autoHeight hideFooter disableRowSelectionOnClick getRowId={(r) => r.id} />
    </Box>
  );
}

function CascadeFilters({ cascade }) {
  return (
    <Box sx={{ display: 'flex', gap: 2, mb: 3, maxWidth: 700 }}>
      <Box sx={{ flex: 1 }}><SelectionCompetition value={cascade.compId} onChange={(e) => cascade.setCompId(e.target.value)} required /></Box>
      <Box sx={{ flex: 1 }}><SelectionCategory competitionId={cascade.compId} value={cascade.catId} onChange={(e) => cascade.setCatId(e.target.value)} required /></Box>
    </Box>
  );
}
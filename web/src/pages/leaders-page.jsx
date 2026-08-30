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
  { field: 'playerName', headerName: 'Jugador', flex: 1, minWidth: 200 },
  { field: 'teamName', headerName: 'Equipo', flex: 1, minWidth: 150 },
  { field: 'total', headerName: 'Total', width: 80 },
];

export default function LeadersPage() {
  const cascade = useCascade();
  const { data, isLoading } = useApi(cascade.catId ? endpoints.leaders(cascade.catId) : null);

  const boards = data?.boards || [];

  return (
    <Box>
      <PageHeader title="Lideres" />
      <CascadeFilters cascade={cascade} />
      {!cascade.catId && <Typography color="text.secondary">Selecciona una competicion y categoria.</Typography>}
      {boards.map((board) => {
        const rows = (board.leaders || []).map((l, i) => ({
          ...l, id: l.rosterEntryId, playerName: `${l.firstName} ${l.lastName}`, position: i + 1,
        }));
        return (
          <Box key={board.metricId} sx={{ mb: 3 }}>
            <Typography variant="h6" fontWeight={600} sx={{ mb: 1 }}>{board.metricLabel}</Typography>
            <DataGrid rows={rows} columns={COLS} autoHeight hideFooter disableRowSelectionOnClick getRowId={(r) => r.id} />
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
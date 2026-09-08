import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import { DataGrid } from '@mui/x-data-grid';
import { useApi } from 'src/hooks/use-api';
import { useCascade } from 'src/hooks/use-cascade';
import { endpoints } from 'src/lib/axios';
import { PageHeader } from 'src/components/page-header';
import { CascadeFilters } from 'src/components/cascade-filters';

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
          // metricCode, no metricId: el tablero combinado de puntos no tiene
          // un metricId propio (junta varios), pero su código sintético
          // "points" es igual de único y estable.
          <Box key={board.metricCode} sx={{ mb: 3 }}>
            <Typography variant="h6" fontWeight={600} sx={{ mb: 1 }}>{board.metricLabel}</Typography>
            <DataGrid rows={rows} columns={COLS} autoHeight hideFooter disableRowSelectionOnClick getRowId={(r) => r.id} />
          </Box>
        );
      })}
    </Box>
  );
}
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import { useNavigate } from 'react-router';
import { DataGrid } from '@mui/x-data-grid';
import { Iconify } from 'src/components/iconify';
import { useApi } from 'src/hooks/use-api';
import { useCascade } from 'src/hooks/use-cascade';
import { endpoints } from 'src/lib/axios';
import { esJuzgado } from 'src/lib/sport-shape';
import { PageHeader } from 'src/components/page-header';
import { CascadeFilters } from 'src/components/cascade-filters';
import { columnasMarcador } from 'src/lib/tiebreaker-labels';

export default function StandingsPage() {
  const navigate = useNavigate();
  const cascade = useCascade();
  const { data, isLoading } = useApi(cascade.catId ? endpoints.standings(cascade.catId) : null);
  const { data: sports } = useApi(endpoints.sports);

  const groups = data?.groups || [];
  const sportInfo = (sports || []).find((s) => s.code === data?.sportCode);
  // Un deporte juzgado (poomsae) no arma tabla de posiciones nunca -- ni
  // vacia (StandingsCalculator.Build no tiene contendientes hasta que se
  // inscribe alguien) ni con datos (una vez inscriptos, cada fila daria
  // cero: la etapa de clasificacion no pasa por Matches). "Todavia no hay
  // partidos jugados" seria enganoso en cualquiera de los dos casos, asi
  // que esta pantalla ni lo intenta -- ver ClassificationRanking en el
  // backend, que es donde este deporte se ordena de verdad.
  const juzgado = esJuzgado(sportInfo);
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
      {cascade.catId && !isLoading && juzgado && (
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5, flexWrap: 'wrap' }}>
          <Typography color="text.secondary">
            {sportInfo.name} no arma tabla de posiciones: se decide por puntaje de jueces.
          </Typography>
          <Button
            size="small"
            startIcon={<Iconify icon="eva:trending-up-outline" />}
            onClick={() => navigate(`/dashboard/performances?competition=${cascade.compId}`)}
          >
            Ver clasificación
          </Button>
        </Box>
      )}
      {cascade.catId && !isLoading && !juzgado && groups.length === 0 && (
        <Typography color="text.secondary">Todavia no hay partidos jugados en esta categoria.</Typography>
      )}
      {/* Una tabla por grupo, no solo la primera: una fase de grupos tiene
          tantas tablas independientes como grupos, y mostrar una sola
          escondía el resto detras de una etiqueta que ni siquiera se leia
          bien. Ninguna se arma para un deporte juzgado -- el bloque de
          arriba ya se hizo cargo. */}
      {!juzgado && groups.map((g, i) => {
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

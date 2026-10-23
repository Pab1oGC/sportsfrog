import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import { useNavigate } from 'react-router';
import { DataGrid } from '@mui/x-data-grid';
import { Iconify } from 'src/components/iconify';
import { useApi } from 'src/hooks/use-api';
import { useCascade } from 'src/hooks/use-cascade';
import { endpoints } from 'src/lib/axios';
import { esJuzgado, esIndividual } from 'src/lib/sport-shape';
import { PageHeader } from 'src/components/page-header';
import { CascadeFilters } from 'src/components/cascade-filters';
import { columnasMarcador } from 'src/lib/tiebreaker-labels';
// BracketView/Llave viven bajo pages/public porque ahi nacieron (el portal
// publico), pero son presentacion pura -- ningun fetch propio, ningun
// import de publicAxios -- y reciben los fixtures por props exactamente en
// la forma que ReadMatches ya devuelve. Reusarlos aca es traer la misma
// llave que ya se ve en el portal, no reimplementarla para el dashboard.
import { BracketView } from 'src/pages/public/bracket/bracket-view';

export default function StandingsPage() {
  const navigate = useNavigate();
  const cascade = useCascade();
  const { data: sports } = useApi(endpoints.sports);

  // De la competencia, no de la respuesta de /standings: antes esta pantalla
  // recien sabia si el deporte era juzgado despues de que /standings
  // contestara (y para un deporte juzgado, lo unico que iba a contestar era
  // un 404). Leerlo de la competencia misma evita ese pedido de mas.
  const comp = cascade.competiciones.find((c) => c.id === cascade.compId);
  const sportInfo = (sports || []).find((s) => s.code === comp?.sportCode);
  // Un deporte juzgado (poomsae) no arma tabla de posiciones nunca -- ni
  // vacia (StandingsCalculator.Build no tiene contendientes hasta que se
  // inscribe alguien) ni con datos (una vez inscriptos, cada fila daria
  // cero: la etapa de clasificacion no pasa por Matches). "Todavia no hay
  // partidos jugados" seria enganoso en cualquiera de los dos casos, asi
  // que esta pantalla ni lo intenta -- ver ClassificationRanking en el
  // backend, que es donde este deporte se ordena de verdad.
  const juzgado = esJuzgado(sportInfo);

  // El formato de la competencia (no de la categoria: Format vive en
  // Competition, ver StandingsQuery del lado del servidor) decide que
  // secciones tienen sentido. Un "knockout" puro nunca arma tabla --
  // StandingsQuery.ForCategoryAsync contesta null ahi, siempre, lo haya
  // jugado o no -- asi que ni se pide; "groups" tiene las dos cosas a la
  // vez (la tabla de la fase de grupos, la llave de la eliminatoria que
  // sigue); "league" solo tabla.
  const formato = comp?.format;
  const mostrarTabla = !juzgado && formato !== 'knockout';
  const mostrarLlave = !juzgado && (formato === 'knockout' || formato === 'groups');

  const { data, isLoading } = useApi(
    cascade.catId && mostrarTabla ? endpoints.standings(cascade.catId) : null,
  );
  // Mismo endpoint que ya usa Fixtures/Partidos (ReadMatches), no uno nuevo:
  // esta pantalla solo lo lee, nunca lo modifica. Agrupar por ronda/fase es
  // trabajo de match-rounds.js, el mismo que ya usa BracketView en el
  // portal publico -- PhaseNumber/RoundNumber son los mismos campos ahi y
  // aca.
  const { data: matches, isLoading: loadingMatches } = useApi(
    cascade.catId && mostrarLlave ? endpoints.categoryMatches(cascade.catId) : null,
  );

  const groups = data?.groups || [];
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
      <PageHeader title="Resultados" />
      <CascadeFilters cascade={cascade} />
      {!cascade.catId && <Typography color="text.secondary">Selecciona una competicion y categoria.</Typography>}
      {cascade.catId && juzgado && (
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

      {cascade.catId && mostrarTabla && (
        <Box sx={{ mb: mostrarLlave ? 4 : 0 }}>
          {/* El titulo de seccion solo aparece junto a la llave: si esta
              competencia nunca va a tener una (format=league), la tabla es
              toda la pantalla y repetir "Resultados" arriba y "Tabla de
              grupos" acá sería ruido. */}
          {mostrarLlave && (
            <Typography variant="h6" fontWeight={700} sx={{ mb: 1.5 }}>Tabla de grupos</Typography>
          )}
          {!isLoading && groups.length === 0 && (
            <Typography color="text.secondary">Todavia no hay partidos jugados en esta categoria.</Typography>
          )}
          {/* Una tabla por grupo, no solo la primera: una fase de grupos
              tiene tantas tablas independientes como grupos, y mostrar una
              sola escondía el resto detras de una etiqueta que ni siquiera
              se leia bien. */}
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
      )}

      {cascade.catId && mostrarLlave && (
        <Box>
          {mostrarTabla && (
            <Typography variant="h6" fontWeight={700} sx={{ mb: 1.5 }}>Llave de eliminatoria</Typography>
          )}
          <BracketView
            data={{ fixtures: matches || [] }}
            loading={loadingMatches}
            selectedCatId={cascade.catId}
            esIndividual={esIndividual(sportInfo)}
          />
        </Box>
      )}
    </Box>
  );
}

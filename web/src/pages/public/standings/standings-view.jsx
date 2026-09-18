import Box from '@mui/material/Box';
import Alert from '@mui/material/Alert';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import { alpha } from '@mui/material/styles';
import { SectionSkeleton } from 'src/components/section-skeleton';
import { etiquetasDesempate } from 'src/lib/tiebreaker-labels';
import { StandardBoard } from './standard-board';
import { CardsBoard } from './cards-board';
import { EditorialBoard } from './editorial-board';

var BOARDS = { standard: StandardBoard, cards: CardsBoard, editorial: EditorialBoard };

/**
 * La tabla de posiciones -- despachador. El loop categoría→grupo, la
 * leyenda de clasificación y el pie de criterios de desempate son iguales
 * sin importar cómo se dibujen las filas, así que viven acá y no se
 * duplican en cada variante (standings/standard-board.jsx,
 * cards-board.jsx, editorial-board.jsx). Cada `Board` solo recibe las
 * filas ya resueltas y decide cómo mostrarlas.
 */
export function StandingsView(props) {
  var data = props.data;
  var loading = props.loading;
  var selectedCatId = props.selectedCatId;
  var sportInfo = props.sportInfo;
  var variant = props.variant || 'standard';
  var Board = BOARDS[variant] || StandardBoard;

  if (loading) return <SectionSkeleton kind="rows" />;
  if (!data || !data.categories || data.categories.length === 0) return <Alert severity="info">Sin datos de posiciones.</Alert>;

  var catsToShow = data.categories;
  if (selectedCatId) catsToShow = catsToShow.filter(function(c) { return c.categoryId === selectedCatId; });

  var etiquetas = etiquetasDesempate(sportInfo);

  return catsToShow.map(function(cat) {
    var qualifies = cat.qualifiersPerGroup || 0;

    return (
      <Box key={cat.categoryId} sx={{ mb: 3 }}>
        {(cat.groups || []).map(function(grp, gi) {
          var rows = (grp.rows || []).map(function(r, i) {
            return Object.assign({}, r, { id: r.teamId || i, position: r.position || (i + 1) });
          });
          return (
            <Box key={cat.categoryId + '-' + gi} sx={{ mb: 1.5 }}>
              <Typography variant="h6" fontWeight={600} sx={{ mb: 1 }}>{cat.categoryName}{grp.label ? ' - Grupo ' + grp.label : ''}</Typography>
              <Board rows={rows} cat={cat} sportInfo={sportInfo} qualifies={qualifies} />
            </Box>
          );
        })}
        {qualifies > 0 && (
          <Stack direction="row" alignItems="center" spacing={1} sx={{ mb: 1 }}>
            <Box sx={{ width: 14, height: 14, borderRadius: 0.5, bgcolor: (theme) => alpha(theme.palette.success.main, 0.35), border: '1px solid', borderColor: 'success.main', flexShrink: 0 }} />
            <Typography variant="caption" color="text.secondary">
              {qualifies === 1
                ? 'Clasifica a la siguiente ronda el primero de cada grupo.'
                : 'Clasifican a la siguiente ronda los primeros ' + qualifies + ' de cada grupo.'}
            </Typography>
          </Stack>
        )}
        {cat.tiebreakers && cat.tiebreakers.length > 0 && (
          <Typography variant="caption" color="text.secondary" sx={{ mt: 0.5, display: 'block' }}>
            Criterios de desempate: {cat.tiebreakers.map(function(code) { return etiquetas[code] || code; }).join(', ')}
          </Typography>
        )}
      </Box>
    );
  });
}

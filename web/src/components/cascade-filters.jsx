import Box from '@mui/material/Box';
import { SelectionCompetition, SelectionCategory } from 'src/components/selectors';

/**
 * Competicion + categoria, encadenados via useCascade.
 *
 * Estaba definido -identico, caracter por caracter- en leaders-page,
 * standings-page y teams-page: cada pantalla que filtra por categoria volvia
 * a escribir la misma pareja de selects. Una cuarta pantalla (Clasificacion,
 * para categorias juzgadas) iba a ser la cuarta copia.
 */
export function CascadeFilters({ cascade }) {
  return (
    <Box sx={{ display: 'flex', gap: 2, mb: 3, maxWidth: 700, flexWrap: 'wrap' }}>
      <Box sx={{ flex: 1, minWidth: 200 }}><SelectionCompetition value={cascade.compId} onChange={(e) => cascade.setCompId(e.target.value)} required /></Box>
      <Box sx={{ flex: 1, minWidth: 200 }}><SelectionCategory competitionId={cascade.compId} value={cascade.catId} onChange={(e) => cascade.setCatId(e.target.value)} required /></Box>
    </Box>
  );
}

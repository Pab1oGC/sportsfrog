import { useSearchParams } from 'react-router';
import { useLastCompetition } from 'src/hooks/use-last-competition';
import Box from '@mui/material/Box';
import { useApi } from 'src/hooks/use-api';
import { endpoints } from 'src/lib/axios';
import { PageHeader } from 'src/components/page-header';
import { SelectionCompetition } from 'src/components/selectors';
import { CategoriesPanel } from 'src/pages/categories/categories-panel';

/**
 * La entrada de siempre al menú: elegí una competencia (o dejá que
 * useLastCompetition elija por vos) y mirá sus categorías. El alta/edición
 * en sí vive en CategoriesPanel, compartido con la pestaña "Categorías" del
 * resumen de una competencia (CompetitionOverviewPage) -- ahí no hace falta
 * este selector porque la competencia ya está fija en la URL.
 */
export default function CategoriesPage() {
  const [searchParams] = useSearchParams();
  const { data: comps } = useApi(endpoints.competitions);
  // Preseleccionada al llegar desde "crear competencia": ese flujo manda para
  // acá porque una competencia sin categorías no tiene nada que sortear. Sin
  // esa señal en la URL, useLastCompetition elige la última usada en
  // cualquier pantalla, o si es la primera vez, la que está en curso.
  const [compId, setCompId] = useLastCompetition(comps, searchParams.get('competition'));

  return (
    <div>
      <PageHeader title="Categorías" />
      <Box sx={{ display: 'flex', gap: 2, mb: 3, maxWidth: 700, flexWrap: 'wrap' }}>
        <Box sx={{ flex: 1, minWidth: 200 }}><SelectionCompetition value={compId} onChange={(e) => setCompId(e.target.value)} required /></Box>
      </Box>
      <CategoriesPanel competitionId={compId} />
    </div>
  );
}

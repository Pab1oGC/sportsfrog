import { useSearchParams } from 'react-router';
import Box from '@mui/material/Box';
import { useApi } from 'src/hooks/use-api';
import { useLastCompetition } from 'src/hooks/use-last-competition';
import { endpoints } from 'src/lib/axios';
import { PageHeader } from 'src/components/page-header';
import { SelectionCompetition } from 'src/components/selectors';
import { TeamsPanel } from 'src/pages/teams/teams-panel';

/**
 * La entrada de siempre al menú: elegí una competencia (o dejá que
 * useLastCompetition elija por vos) y mirá sus equipos. El alta/edición en
 * sí vive en TeamsPanel, compartido con la pestaña "Equipos" del resumen de
 * una competencia (CompetitionOverviewPage) -- ahí no hace falta este
 * selector porque la competencia ya está fija en la URL.
 */
export default function TeamsPage() {
  // Un id que vino por link -- Nomina manda para aca cuando la competencia
  // elegida es de un deporte individual, porque ahi esta pantalla es la
  // unica que administra inscripciones.
  const [searchParams] = useSearchParams();
  const { data: comps } = useApi(endpoints.competitions);
  const [compId, setCompId] = useLastCompetition(comps, searchParams.get('competition'));

  return (
    <Box>
      <PageHeader title="Equipos" />
      <Box sx={{ display: 'flex', gap: 2, mb: 3, maxWidth: 700, flexWrap: 'wrap' }}>
        <Box sx={{ flex: 1, minWidth: 200 }}><SelectionCompetition value={compId} onChange={(e) => setCompId(e.target.value)} required /></Box>
      </Box>
      <TeamsPanel competitionId={compId} />
    </Box>
  );
}

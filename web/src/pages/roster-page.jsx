import { useApi } from 'src/hooks/use-api';
import { useLastCompetition } from 'src/hooks/use-last-competition';
import Box from '@mui/material/Box';
import { endpoints } from 'src/lib/axios';
import { PageHeader } from 'src/components/page-header';
import { SelectionCompetition } from 'src/components/selectors';
import { RosterPanel } from 'src/pages/roster/roster-panel';

/**
 * La entrada de siempre al menú: elegí una competencia (o dejá que
 * useLastCompetition elija por vos) y mirá el plantel de sus equipos. El
 * alta/edición en sí vive en RosterPanel, compartido con la pestaña
 * "Nómina" del resumen de una competencia (CompetitionOverviewPage) -- ahí
 * no hace falta este selector porque la competencia ya está fija en la URL.
 */
export default function RosterPage() {
  const { data: comps } = useApi(endpoints.competitions);
  const [compId, setCompId] = useLastCompetition(comps);

  return (
    <Box>
      <PageHeader title="Nomina" />
      <Box sx={{ display: 'flex', gap: 2, mb: 3, maxWidth: 700, flexWrap: 'wrap' }}>
        <Box sx={{ flex: 1, minWidth: 200 }}><SelectionCompetition value={compId} onChange={(e) => setCompId(e.target.value)} required /></Box>
      </Box>
      <RosterPanel competitionId={compId} />
    </Box>
  );
}

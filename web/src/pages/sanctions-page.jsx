import { useLocation } from 'react-router';
import { useApi } from 'src/hooks/use-api';
import { useLastCompetition } from 'src/hooks/use-last-competition';
import Box from '@mui/material/Box';
import Paper from '@mui/material/Paper';
import { endpoints } from 'src/lib/axios';
import { PageHeader } from 'src/components/page-header';
import { SelectionCompetition } from 'src/components/selectors';
import { SanctionsPanel } from 'src/pages/sanctions/sanctions-panel';

/**
 * Sanciones disciplinarias: elegí una competencia (o dejá que
 * useLastCompetition elija por vos) y administrá las suyas.
 *
 * Ya no separa "Sanciones" de "Comprobantes pendientes" en dos pestañas --
 * la tabla de pagos, adentro de cada sanción (ver SanctionsPanel), ya
 * muestra el comprobante de una (miniatura clickeable) y deja
 * Verificar/Rechazar ahí mismo cuando corresponde. Una pestaña aparte para
 * eso, a nivel de toda la competencia, estaba casi siempre vacía: una
 * multa en efectivo nace verificada, y la mayoría de las sanciones no
 * llevan multa en absoluto.
 *
 * El alta/edición en sí vive en SanctionsPanel, compartido con la pestaña
 * "Sanciones" del resumen de una competencia (CompetitionOverviewPage) --
 * mismo reparto que CategoriesPanel ya tiene con CategoriesPage. Ahí no
 * hace falta este selector porque la competencia ya está fija en la URL.
 *
 * `state.prefill` llega desde la sugerencia de "Imponer sanción" al
 * finalizar un partido (ver matches-page.jsx) -- gana sobre la competencia
 * recordada, la misma prioridad que ya tiene un link directo con
 * `urlCompId`, y se le pasa al panel para que abra el diálogo de alta ya
 * completado en vez de uno vacío.
 */
export default function SanctionsPage() {
  const { state } = useLocation();
  const prefill = state?.prefill || null;
  const { data: comps } = useApi(endpoints.competitions);
  const [compId, setCompId] = useLastCompetition(comps, prefill?.competitionId);

  return (
    <Box>
      <PageHeader title="Sanciones" />
      <Box sx={{ display: 'flex', gap: 2, mb: 3, maxWidth: 700, flexWrap: 'wrap' }}>
        <Box sx={{ flex: 1, minWidth: 200 }}><SelectionCompetition value={compId} onChange={(e) => setCompId(e.target.value)} required /></Box>
      </Box>
      <Paper variant="outlined" sx={{ p: 2 }}>
        <SanctionsPanel competitionId={compId} prefill={compId === prefill?.competitionId ? prefill : null} />
      </Paper>
    </Box>
  );
}

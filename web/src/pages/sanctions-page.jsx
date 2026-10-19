import { useState } from 'react';
import { useLocation } from 'react-router';
import { useApi } from 'src/hooks/use-api';
import { useLastCompetition } from 'src/hooks/use-last-competition';
import Box from '@mui/material/Box';
import Paper from '@mui/material/Paper';
import Tabs from '@mui/material/Tabs';
import Tab from '@mui/material/Tab';
import { endpoints } from 'src/lib/axios';
import { PageHeader } from 'src/components/page-header';
import { SelectionCompetition } from 'src/components/selectors';
import { SanctionsPanel } from 'src/pages/sanctions/sanctions-panel';
import { PendingSanctionReceiptsTab } from 'src/pages/sanctions/pending-sanction-receipts-tab';

/**
 * Sanciones disciplinarias: elegí una competencia (o dejá que
 * useLastCompetition elija por vos) y administrá las suyas, en dos
 * pestañas -- mismo patrón que Cobranza separa "En mora" de "Comprobantes
 * pendientes", porque imponer una sanción y revisar el comprobante de una
 * multa ya impuesta son dos tareas distintas que no hace falta mezclar en
 * una sola grilla.
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
  const [tab, setTab] = useState('sanctions');

  return (
    <Box>
      <PageHeader title="Sanciones" />
      <Box sx={{ display: 'flex', gap: 2, mb: 3, maxWidth: 700, flexWrap: 'wrap' }}>
        <Box sx={{ flex: 1, minWidth: 200 }}><SelectionCompetition value={compId} onChange={(e) => setCompId(e.target.value)} required /></Box>
      </Box>
      <Paper variant="outlined" sx={{ p: 2 }}>
        <Tabs value={tab} onChange={(_, v) => setTab(v)} sx={{ mb: 2 }}>
          <Tab value="sanctions" label="Sanciones" />
          <Tab value="receipts" label="Comprobantes pendientes" />
        </Tabs>
        {tab === 'sanctions' && <SanctionsPanel competitionId={compId} prefill={compId === prefill?.competitionId ? prefill : null} />}
        {tab === 'receipts' && <PendingSanctionReceiptsTab competitionId={compId} />}
      </Paper>
    </Box>
  );
}

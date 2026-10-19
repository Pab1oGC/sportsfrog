import { useState } from 'react';
import { useApi } from 'src/hooks/use-api';
import { useLastCompetition } from 'src/hooks/use-last-competition';
import Box from '@mui/material/Box';
import Tabs from '@mui/material/Tabs';
import Tab from '@mui/material/Tab';
import { endpoints } from 'src/lib/axios';
import { PageHeader } from 'src/components/page-header';
import { SelectionCompetition } from 'src/components/selectors';
import { RosterPanel } from 'src/pages/roster/roster-panel';
import { StaffPanel } from 'src/pages/staff/staff-panel';

/**
 * La entrada de siempre al menú: elegí una competencia (o dejá que
 * useLastCompetition elija por vos) y mirá el plantel de sus equipos. El
 * alta/edición en sí vive en RosterPanel, compartido con la pestaña
 * "Nómina" del resumen de una competencia (CompetitionOverviewPage) -- ahí
 * no hace falta este selector porque la competencia ya está fija en la URL.
 *
 * Las dos pestañas son las dos clases de gente que un equipo tiene, y están
 * acá y no dentro de RosterPanel a propósito: ese panel se comparte con el
 * resumen de la competencia, donde la pestaña de jugadores es una entre
 * varias y no corresponde anidarle otro juego de pestañas adentro.
 */
export default function RosterPage() {
  const { data: comps } = useApi(endpoints.competitions);
  const [compId, setCompId] = useLastCompetition(comps);
  const [tab, setTab] = useState(0);

  return (
    <Box>
      <PageHeader title="Nomina" />
      <Box sx={{ display: 'flex', gap: 2, mb: 3, maxWidth: 700, flexWrap: 'wrap' }}>
        <Box sx={{ flex: 1, minWidth: 200 }}><SelectionCompetition value={compId} onChange={(e) => setCompId(e.target.value)} required /></Box>
      </Box>
      <Tabs value={tab} onChange={(e, value) => setTab(value)} sx={{ mb: 3 }}>
        <Tab label="Jugadores" />
        <Tab label="Cuerpo tecnico" />
      </Tabs>
      {tab === 0 ? <RosterPanel competitionId={compId} /> : <StaffPanel competitionId={compId} />}
    </Box>
  );
}

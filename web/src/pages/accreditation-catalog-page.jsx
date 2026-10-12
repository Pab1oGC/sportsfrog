import { useState } from 'react';
import { useSearchParams } from 'react-router';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Tabs from '@mui/material/Tabs';
import Tab from '@mui/material/Tab';
import { useApi } from 'src/hooks/use-api';
import { endpoints } from 'src/lib/axios';
import { useLastCompetition } from 'src/hooks/use-last-competition';
import { SelectionCompetition } from 'src/components/selectors';
import { AccreditationItemsTab } from 'src/pages/accreditation/items-tab';
import { AccreditationCategoriesTab } from 'src/pages/accreditation/categories-tab';

/**
 * El catálogo que una credencial decretada imprime: qué disciplina, qué
 * recintos, qué servicios y qué zonas existen para esta competencia, y qué
 * categorías de acreditación los agrupan.
 *
 * La estructura de la tarjeta está fija por decreto -- no hay nada que
 * diseñar acá, a diferencia de un certificado -- así que esta pantalla no
 * es un editor de plantilla, es un editor de catálogo: nombres, códigos y
 * colores, nada de posiciones.
 */
export default function AccreditationCatalogPage() {
  const [searchParams] = useSearchParams();
  const { data: comps } = useApi(endpoints.competitions);
  const [compId, setCompId] = useLastCompetition(comps, searchParams.get('competition'));
  const [tab, setTab] = useState('items');

  return (
    <Box>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 3, flexWrap: 'wrap', gap: 1 }}>
        <Typography variant="h4" fontWeight={700}>Acreditación</Typography>
      </Box>

      <Box sx={{ maxWidth: 400, mb: 3 }}>
        <SelectionCompetition value={compId} onChange={(e) => setCompId(e.target.value)} required />
      </Box>

      <Tabs value={tab} onChange={(_, v) => setTab(v)} sx={{ mb: 3 }}>
        <Tab value="items" label="Elementos" />
        <Tab value="categories" label="Categorías" />
      </Tabs>

      {tab === 'items' && <AccreditationItemsTab competitionId={compId} />}
      {tab === 'categories' && <AccreditationCategoriesTab competitionId={compId} />}
    </Box>
  );
}

import { useState } from 'react';
import Box from '@mui/material/Box';
import Paper from '@mui/material/Paper';
import Tabs from '@mui/material/Tabs';
import Tab from '@mui/material/Tab';
import { PageHeader } from 'src/components/page-header';
import { CollectionSummary } from 'src/pages/school/collection-summary';
import { ArrearsTab } from 'src/pages/school/arrears-tab';
import { PendingReceiptsTab } from 'src/pages/school/pending-receipts-tab';
import { SearchAccountTab } from 'src/pages/school/search-account-tab';

/**
 * Cobranza: las tres pantallas de cobrar una pensión, juntas en una sola
 * página con pestañas en vez de separadas en el menú (antes "Pensiones" y
 * "Comprobantes" eran dos entradas distintas, y la primera abría vacía
 * hasta elegir un curso -- ver el historial de esta página).
 *
 * "En mora" es la pestaña inicial a propósito: es la única de las tres que
 * tiene algo que mostrar sin pedirle nada a quien entra, así que es lo
 * primero que se ve en vez de un formulario vacío. CollectionSummary suma
 * a eso tres números arriba de las pestañas mismas, para que entrar a
 * Cobranza diga algo de entrada sin tener que abrir ninguna.
 */
export default function CollectionPage() {
  const [tab, setTab] = useState('arrears');

  return (
    <Box>
      <PageHeader title="Cobranza" />

      <CollectionSummary onViewReceipts={() => setTab('receipts')} />

      <Paper variant="outlined" sx={{ p: 2 }}>
        <Tabs value={tab} onChange={(_, v) => setTab(v)} sx={{ mb: 2 }}>
          <Tab value="arrears" label="En mora" />
          <Tab value="receipts" label="Comprobantes pendientes" />
          <Tab value="search" label="Buscar cuenta" />
        </Tabs>
        {tab === 'arrears' && <ArrearsTab />}
        {tab === 'receipts' && <PendingReceiptsTab />}
        {tab === 'search' && <SearchAccountTab />}
      </Paper>
    </Box>
  );
}

import Grid from '@mui/material/Grid';
import { useApi } from 'src/hooks/use-api';
import { endpoints } from 'src/lib/axios';
import { StatCard } from 'src/components/stat-card';

const SLUG = 'morosos';

// Mismo nombre de columna que TuitionArrearsList.Columns del lado del
// servidor -- se busca por header en vez de hardcodear el indice para no
// quedar atado al orden exacto de esa lista.
const BALANCE_HEADER = 'Saldo (Bs)';

/**
 * Tres numeros antes de elegir nada: cuantas pensiones estan en mora,
 * cuanto suman, y cuantos comprobantes estan esperando revision. Es lo que
 * antes faltaba al entrar a Cobranza -- ver el historial de esta pagina --
 * y por eso va arriba de las pestañas, no adentro de ninguna.
 *
 * Ambos pedidos (la lista "morosos" y los comprobantes pendientes) son los
 * mismos que ya hacen ArrearsTab y PendingReceiptsTab: useApi los cachea
 * por URL (swr), asi que mostrar el resumen no duplica ningun pedido a la
 * API, solo lee el mismo dato una vez mas.
 */
export function CollectionSummary({ onViewReceipts }) {
  const { data: table, isLoading: loadingArrears } = useApi(endpoints.listPreview(SLUG));
  const { data: receipts, isLoading: loadingReceipts } = useApi(endpoints.pendingReceipts);

  const rows = (table?.sections || []).flatMap((section) => section.rows);
  const balanceIndex = (table?.columns || []).findIndex((column) => column.header === BALANCE_HEADER);
  const totalOwed = balanceIndex === -1
    ? 0
    : Math.round(rows.reduce((sum, row) => sum + (Number(row[balanceIndex]) || 0), 0) * 100) / 100;

  return (
    <Grid container spacing={2} sx={{ mb: 3 }}>
      <Grid size={{ xs: 12, sm: 4 }}>
        <StatCard
          title="Pensiones en mora"
          value={loadingArrears ? undefined : rows.length}
          icon="mdi:alert-circle-outline"
          color="error"
        />
      </Grid>
      <Grid size={{ xs: 12, sm: 4 }}>
        <StatCard
          title="Adeudado en mora"
          value={loadingArrears ? undefined : `Bs ${totalOwed}`}
          icon="mdi:cash-minus"
          color="warning"
        />
      </Grid>
      <Grid size={{ xs: 12, sm: 4 }}>
        <StatCard
          title="Comprobantes pendientes"
          value={loadingReceipts ? undefined : (receipts || []).length}
          icon="mdi:receipt-text-clock-outline"
          color="info"
          onClick={onViewReceipts}
        />
      </Grid>
    </Grid>
  );
}

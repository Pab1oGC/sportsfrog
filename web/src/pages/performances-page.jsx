import { useState } from 'react';
import { useSearchParams } from 'react-router';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import Chip from '@mui/material/Chip';
import { DataGrid } from '@mui/x-data-grid';
import { Iconify } from 'src/components/iconify';
import { useApi, apiPost } from 'src/hooks/use-api';
import { useCascade } from 'src/hooks/use-cascade';
import { endpoints } from 'src/lib/axios';
import { esJuzgado } from 'src/lib/sport-shape';
import { PageHeader } from 'src/components/page-header';
import { CascadeFilters } from 'src/components/cascade-filters';
import { useConfirm } from 'src/components/confirm-dialog';
import { toast } from 'sonner';
import { ScoreCell } from 'src/pages/performances/score-cell';
import { PromoteClassificationDialog } from 'src/pages/performances/promote-classification-dialog';
import { ScheduleDialog } from 'src/pages/performances/schedule-dialog';
import { BulkOrderDialog } from 'src/pages/performances/bulk-order-dialog';
import { RowActionsMenu } from 'src/components/row-actions-menu';

const STATUS_LABEL = { pending: 'Pendiente', scored: 'Puntuado' };
const STATUS_COLOR = { pending: 'default', scored: 'success' };

/**
 * La etapa de clasificacion de una categoria juzgada (poomsae, hoy el unico
 * deporte con ScoreMode.Judged): rankea por puntaje de jueces en vez de por
 * tabla de posiciones -- el equivalente de StandingsPage para un deporte que
 * no tiene tabla. Ver ClassificationRanking/ClassificationAdvancement en el
 * backend para las reglas que esta pantalla se limita a mostrar, no a
 * reimplementar.
 */
export default function PerformancesPage() {
  const confirm = useConfirm();
  const [searchParams] = useSearchParams();
  // Preseleccionada al llegar desde "Ver clasificación" en Tabla de
  // Posiciones: esa pantalla, para una categoria juzgada, no tiene nada
  // propio que mostrar -- ver StandingsPage. Sin esta señal en la URL,
  // useCascade recuerda la ultima competencia usada en cualquier pantalla.
  const cascade = useCascade(searchParams.get('competition'));
  const { data: sports } = useApi(endpoints.sports);

  const comp = cascade.competiciones.find((c) => c.id === cascade.compId);
  const sport = sports?.find((s) => comp && s.code === comp.sportCode);
  const juzgado = esJuzgado(sport);

  // Nada que leer todavia si la categoria no es juzgada: no tiene una
  // clasificacion que abrir, y pedirsela a la API solo para descartarla
  // seria un pedido de mas.
  const { data, mutate, isLoading } = useApi(
    cascade.catId && juzgado ? endpoints.categoryPerformances(cascade.catId) : null,
  );
  const rows = data?.rows || [];

  const [opening, setOpening] = useState(false);
  const [promoteOpen, setPromoteOpen] = useState(false);
  const [schedRow, setSchedRow] = useState(null);
  const [schedOpen, setSchedOpen] = useState(false);
  const [bulkOpen, setBulkOpen] = useState(false);

  // Una categoria sin clasificacion abierta todavia lee un array vacio, no
  // un 404 -- ver ReadPerformances en el backend. "Abierta" es exactamente
  // eso: hay filas.
  const abierta = rows.length > 0;
  const todosPuntuados = abierta && rows.every((row) => row.status === 'scored');
  // OpenClassificationStage rechaza reabrir en cuanto CUALQUIER competidor
  // ya tiene puntaje cargado (no solo cuando estan todos) -- se deshabilita
  // aca por la misma razon que el sorteo de partidos ya deshabilita
  // "Sortear" cuando sabe de antemano que el pedido va a fallar.
  const algunoPuntuado = rows.some((row) => row.status === 'scored');

  const abrirClasificacion = async () => {
    const ok = await confirm('Abrir la clasificación de esta categoría?', { confirmLabel: 'Abrir' });
    if (!ok) return;
    setOpening(true);
    try {
      const r = await apiPost(endpoints.categoryPerformancesOpen(cascade.catId), {});
      mutate();
      toast.success(r.replaced > 0
        ? `Clasificación abierta: ${r.created} competidor(es), se reemplazaron ${r.replaced} cupo(s) sin puntaje.`
        : `Clasificación abierta: ${r.created} competidor(es).`);
    } catch (err) { toast.error(err.message); }
    finally { setOpening(false); }
  };

  const columns = [
    // Null mientras el competidor no actuo todavia -- ClassificationRanking
    // lo deja sin puesto a proposito, no le inventa un ultimo lugar que no
    // se gano. Se muestra tal cual, no se completa con un numero propio.
    { field: 'position', headerName: '#', width: 60, sortable: false, renderCell: ({ value }) => value ?? '--' },
    { field: 'teamName', headerName: 'Competidor', flex: 1, minWidth: 200, sortable: false },
    { field: 'score', headerName: 'Puntaje', width: 150, sortable: false, renderCell: (params) => (
      <ScoreCell row={params.row} onSaved={mutate} />
    ) },
    { field: 'status', headerName: 'Estado', width: 120, sortable: false, renderCell: ({ value }) => (
      <Chip label={STATUS_LABEL[value] || value} color={STATUS_COLOR[value] || 'default'} size="small" />
    ) },
    { field: 'venueName', headerName: 'Tapete', width: 160, sortable: false, renderCell: ({ row }) =>
      row.venueName ? `${row.venueName}${row.spaceName ? ' — ' + row.spaceName : ''}` : '--' },
    { field: 'scheduledOn', headerName: 'Día / Turno', width: 150, sortable: false, renderCell: ({ row }) =>
      row.scheduledOn ? `${row.scheduledOn}${row.orderNumber ? ` · turno ${row.orderNumber}` : ''}` : '--' },
    { field: 'actions', headerName: 'Acciones', width: 90, align: 'center', headerAlign: 'center', sortable: false, renderCell: ({ row }) => (
      <RowActionsMenu
        primary={{ icon: 'eva:calendar-outline', label: 'Ubicar en el orden', color: 'text.secondary', onClick: () => { setSchedRow(row); setSchedOpen(true); } }}
      />
    ) },
  ];

  return (
    <Box>
      <PageHeader title="Clasificación">
        {cascade.catId && juzgado && (
          <Button variant="outlined" startIcon={<Iconify icon="eva:unlock-outline" />} onClick={abrirClasificacion} disabled={opening || algunoPuntuado}>
            {abierta ? 'Reabrir clasificación' : 'Abrir clasificación'}
          </Button>
        )}
        {cascade.catId && juzgado && (
          <Button
            variant="outlined"
            startIcon={<Iconify icon="eva:trending-up-outline" />}
            onClick={() => setPromoteOpen(true)}
            disabled={!todosPuntuados}
          >
            Sortear eliminatoria
          </Button>
        )}
        {cascade.catId && juzgado && abierta && (
          <Button variant="outlined" startIcon={<Iconify icon="eva:shuffle-2-outline" />} onClick={() => setBulkOpen(true)}>
            Reprogramar en bloque
          </Button>
        )}
      </PageHeader>
      <CascadeFilters cascade={cascade} />

      {!cascade.catId && <Typography color="text.secondary">Selecciona una competición y categoría.</Typography>}

      {cascade.catId && !juzgado && (
        <Typography color="text.secondary">Esta categoría no se decide por puntaje de jueces.</Typography>
      )}

      {cascade.catId && juzgado && !isLoading && !abierta && (
        <Typography color="text.secondary">Esta categoría todavía no tiene una clasificación abierta.</Typography>
      )}

      {cascade.catId && juzgado && abierta && (
        // Se ordena por lo que ya manda ReadPerformances (ClassificationRanking
        // ya resolvio empates y pendientes), no por lo que decida el grid --
        // las cuatro columnas son sortable:false, para que un click en el
        // encabezado no reordene por encima de un ranking que ya vino
        // resuelto.
        <DataGrid
          rows={rows}
          columns={columns}
          loading={isLoading}
          autoHeight
          disableRowSelectionOnClick
          getRowId={(row) => row.performanceId}
        />
      )}

      <PromoteClassificationDialog
        open={promoteOpen}
        onClose={() => setPromoteOpen(false)}
        catId={cascade.catId}
        onPromoted={mutate}
      />

      <ScheduleDialog open={schedOpen} onClose={() => setSchedOpen(false)} row={schedRow} mutate={mutate} />

      <BulkOrderDialog open={bulkOpen} onClose={() => setBulkOpen(false)} rows={rows} mutate={mutate} />
    </Box>
  );
}

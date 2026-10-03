import { useEffect, useState } from 'react';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import TextField from '@mui/material/TextField';
import { DataGrid } from '@mui/x-data-grid';
import { toast } from 'sonner';
import { Iconify } from 'src/components/iconify';
import { useApi } from 'src/hooks/use-api';
import { useCascade } from 'src/hooks/use-cascade';
import { endpoints, default as axios } from 'src/lib/axios';
import { downloadBlob } from 'src/lib/download-blob';
import { withQueryParams } from 'src/lib/query-string';
import { bloquearNoEnteros, soloDigitos } from 'src/lib/entero-sin-signo';
import { PageHeader } from 'src/components/page-header';
import { CascadeFilters } from 'src/components/cascade-filters';
import { SelectionField, SelectionTeam } from 'src/components/selectors';
import { scopeFromCascade, isScopeComplete, neededCascadeLevels } from 'src/pages/lists/list-scope';
import { gridColumns, gridRows } from 'src/pages/lists/list-table';

const GENDER_OPTIONS = [
  { value: 'M', label: 'Masculino' },
  { value: 'F', label: 'Femenino' },
];

/**
 * Centraliza toda lista exportable que la API catalogue, sin que esta
 * pantalla sepa de antemano cuáles son ni qué filtros pide cada una: el
 * catálogo (GET /lists) trae el slug, la etiqueta y los parámetros de cada
 * una, y esta página arma el formulario de filtros y el pedido a partir de
 * eso. Agregar una lista nueva del lado del servidor la hace aparecer acá
 * sola — ver ListParameter / IListProvider.
 *
 * No reemplaza Líderes, Tabla de posiciones ni Reportes: esas pantallas ya
 * eran la lectura correcta de esos datos. Esta es el lugar al que venir
 * cuando lo que se necesita es llevarse esos datos — a Excel o a PDF.
 *
 * Competicion y categoria se eligen primero, no la lista: el catálogo mismo
 * se pide con la competicion elegida (`competitionId`), y el servidor
 * devuelve solo las listas que tienen algo que decir para el deporte de esa
 * competicion — una categoría juzgada (poomsae) no ofrece "Tabla de
 * posiciones", y un vóley no ofrece "Máximos anotadores" ni "Tarjetas" (no
 * tienen una métrica que sume al marcador ni una de tarjetas), ver
 * IListProvider.AppliesToAsync. Sin competicion elegida el catálogo vuelve
 * sin filtrar, así que "Deportistas" (la única lista que no pertenece a
 * ninguna competicion) sigue eligiéndose sin pasar por ahí.
 */
export default function ListsPage() {
  const cascade = useCascade();
  const { data: catalog, isLoading: loadingCatalog } = useApi(
    withQueryParams(endpoints.listCatalog, { competitionId: cascade.compId }),
  );
  const [slug, setSlug] = useState('');
  const [position, setPosition] = useState('');
  const [search, setSearch] = useState('');
  const [gender, setGender] = useState('');
  const [downloading, setDownloading] = useState('');

  const selected = (catalog || []).find((entry) => entry.slug === slug) || null;
  const levels = neededCascadeLevels(selected?.parameters);
  // Ninguno de estos cuatro sale del cascada (Competicion → Categoria →
  // Equipo): son campos propios de esta página, así que scopeFromCascade los
  // recibe pegados al mismo objeto en vez de hacerle conocer un segundo
  // origen por cada uno.
  const scope = selected
    ? scopeFromCascade(selected.parameters, { ...cascade, position, search, gender })
    : {};
  const ready = selected ? isScopeComplete(selected.parameters, scope) : false;

  // Cambiar de competicion puede sacar la lista elegida del catálogo filtrado
  // (una "Tabla de posiciones" elegida para una competicion de equipos deja
  // de aparecer al pasar a una juzgada) -- sin esto, `slug` seguiría
  // apuntando a una lista que ya no está entre las opciones.
  useEffect(() => {
    if (slug && catalog && !catalog.some((entry) => entry.slug === slug)) {
      setSlug('');
    }
  }, [slug, catalog]);

  const { data: table, isLoading: loadingPreview } = useApi(
    ready ? withQueryParams(endpoints.listPreview(slug), scope) : null,
  );

  const listOptions = (catalog || []).map((entry) => ({ value: entry.slug, label: entry.label }));

  const download = async (extension) => {
    setDownloading(extension);
    try {
      const endpoint = extension === 'xlsx' ? endpoints.listXlsx(slug) : endpoints.listPdf(slug);
      const res = await axios.get(withQueryParams(endpoint, scope), { responseType: 'blob' });
      downloadBlob(res, `${slug}.${extension}`);
    } catch (err) {
      toast.error(err.message);
    } finally {
      setDownloading('');
    }
  };

  return (
    <Box>
      <PageHeader title="Listas" />

      <CascadeFilters cascade={cascade} />

      <Box sx={{ display: 'flex', gap: 2, mb: 3, maxWidth: 900, flexWrap: 'wrap' }}>
        <Box sx={{ flex: 1, minWidth: 220 }}>
          <SelectionField
            label="Lista"
            value={slug}
            onChange={(e) => setSlug(e.target.value)}
            options={listOptions}
            isLoading={loadingCatalog}
            required
          />
        </Box>
        {levels.team && (
          <Box sx={{ flex: 1, minWidth: 200 }}>
            <SelectionTeam
              categoryId={cascade.catId}
              value={cascade.teamId}
              onChange={(e) => cascade.setTeamId(e.target.value)}
              required
            />
          </Box>
        )}
        {levels.position && (
          <Box sx={{ flex: 1, minWidth: 140 }}>
            <TextField
              label="Puesto"
              type="number"
              value={position}
              onChange={(e) => setPosition(soloDigitos(e.target.value))}
              onKeyDown={bloquearNoEnteros}
              slotProps={{ htmlInput: { min: 1, step: 1 } }}
              required
              fullWidth
            />
          </Box>
        )}
        {levels.search && (
          <Box sx={{ flex: 1, minWidth: 220 }}>
            <TextField
              label="Buscar"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Nombre, apellido o documento"
              fullWidth
              slotProps={{
                input: {
                  startAdornment: (
                    <Iconify icon="eva:search-outline" width={18} sx={{ mr: 1, color: 'text.disabled' }} />
                  ),
                },
              }}
            />
          </Box>
        )}
        {levels.gender && (
          <Box sx={{ flex: 1, minWidth: 160 }}>
            <SelectionField
              label="Género"
              value={gender}
              onChange={(e) => setGender(e.target.value)}
              options={GENDER_OPTIONS}
              emptyLabel="Todos"
            />
          </Box>
        )}
      </Box>

      {!selected && <Typography color="text.secondary">Elegí una lista para empezar.</Typography>}
      {selected && !ready && <Typography color="text.secondary">Completá los filtros para ver esta lista.</Typography>}

      {selected && ready && (
        <>
          <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: 1, mb: 2 }}>
            <Box>
              {table?.title && <Typography variant="h6" fontWeight={700}>{table.title}</Typography>}
              {table?.subtitle && <Typography color="text.secondary">{table.subtitle}</Typography>}
            </Box>
            <Box sx={{ display: 'flex', gap: 1 }}>
              <Button
                variant="outlined"
                startIcon={<Iconify icon="eva:download-outline" />}
                disabled={!!downloading || loadingPreview}
                onClick={() => download('xlsx')}
              >
                {downloading === 'xlsx' ? 'Generando...' : 'Excel'}
              </Button>
              <Button
                variant="outlined"
                startIcon={<Iconify icon="eva:download-outline" />}
                disabled={!!downloading || loadingPreview}
                onClick={() => download('pdf')}
              >
                {downloading === 'pdf' ? 'Generando...' : 'PDF'}
              </Button>
            </Box>
          </Box>

          {/* El subtítulo ya explica el motivo puntual (la final todavía no
              se jugó, esta categoría no tiene tantos equipos...) cuando el
              backend lo da -- repetir el genérico abajo sería decir lo mismo
              dos veces. */}
          {!loadingPreview && (table?.sections || []).length === 0 && !table?.subtitle && (
            <Typography color="text.secondary">No hay datos para este alcance.</Typography>
          )}

          {(table?.sections || []).map((section, index) => (
            <Box key={section.label || index} sx={{ mb: 3 }}>
              {table.sections.length > 1 && section.label && (
                <Typography variant="subtitle1" fontWeight={600} sx={{ mb: 1 }}>{section.label}</Typography>
              )}
              <DataGrid
                rows={gridRows(section.rows)}
                columns={gridColumns(table.columns)}
                loading={loadingPreview}
                autoHeight
                hideFooter
                disableRowSelectionOnClick
                getRowId={(row) => row.id}
              />
            </Box>
          ))}
        </>
      )}
    </Box>
  );
}

import { useState } from 'react';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import CircularProgress from '@mui/material/CircularProgress';
import { DataGrid } from '@mui/x-data-grid';
import { toast } from 'sonner';
import { Iconify } from 'src/components/iconify';
import { useApi } from 'src/hooks/use-api';
import { endpoints, default as axios } from 'src/lib/axios';
import { downloadBlob } from 'src/lib/download-blob';
import { gridColumns, gridRows } from 'src/pages/lists/list-table';

const SLUG = 'morosos';

/**
 * Pensiones en mora, organización entera -- la misma lista exportable
 * que ya vive en Listas (TuitionArrearsList), mostrada acá directamente
 * porque es lo primero que alguien que entra a Cobranza necesita ver, sin
 * tener que ir a buscarla a otra pantalla ni elegir nada para que aparezca.
 */
export function ArrearsTab() {
  const { data: table, isLoading } = useApi(endpoints.listPreview(SLUG));
  const [downloading, setDownloading] = useState('');

  const sections = table?.sections || [];
  const totalRows = sections.reduce((sum, section) => sum + section.rows.length, 0);

  const download = async (extension) => {
    setDownloading(extension);
    try {
      const endpoint = extension === 'xlsx' ? endpoints.listXlsx(SLUG) : endpoints.listPdf(SLUG);
      const res = await axios.get(endpoint, { responseType: 'blob' });
      downloadBlob(res, `${SLUG}.${extension}`);
    } catch (err) {
      toast.error(err.message);
    } finally {
      setDownloading('');
    }
  };

  return (
    <Box>
      <Box sx={{ display: 'flex', justifyContent: 'flex-end', gap: 1, mb: 2 }}>
        <Button
          variant="outlined" size="small"
          startIcon={<Iconify icon="eva:download-outline" width={16} />}
          disabled={!!downloading || isLoading}
          onClick={() => download('xlsx')}
        >
          {downloading === 'xlsx' ? 'Generando...' : 'Excel'}
        </Button>
        <Button
          variant="outlined" size="small"
          startIcon={<Iconify icon="eva:download-outline" width={16} />}
          disabled={!!downloading || isLoading}
          onClick={() => download('pdf')}
        >
          {downloading === 'pdf' ? 'Generando...' : 'PDF'}
        </Button>
      </Box>

      {isLoading && (
        <Box sx={{ display: 'flex', justifyContent: 'center', py: 6 }}><CircularProgress /></Box>
      )}

      {!isLoading && totalRows === 0 && (
        <Typography color="text.secondary" sx={{ py: 2 }}>No hay pensiones en mora.</Typography>
      )}

      {!isLoading && sections.map((section, index) => (
        <Box key={section.label || index} sx={{ mb: 2 }}>
          {sections.length > 1 && section.label && (
            <Typography variant="subtitle1" fontWeight={600} sx={{ mb: 1 }}>{section.label}</Typography>
          )}
          <DataGrid
            rows={gridRows(section.rows)}
            columns={gridColumns(table.columns)}
            autoHeight
            hideFooter
            disableRowSelectionOnClick
            getRowId={(row) => row.id}
          />
        </Box>
      ))}
    </Box>
  );
}

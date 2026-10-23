import { useState } from 'react';
import Box from '@mui/material/Box';
import Paper from '@mui/material/Paper';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import IconButton from '@mui/material/IconButton';
import Dialog from '@mui/material/Dialog';
import DialogTitle from '@mui/material/DialogTitle';
import DialogContent from '@mui/material/DialogContent';
import { toast } from 'sonner';
import { Iconify } from 'src/components/iconify';
import { readInlineReceipt, INLINE_RECEIPT_REQUIREMENT } from 'src/lib/inline-photo';

/**
 * El comprobante de pago, a tamaño completo -- adentro de la misma página,
 * no en una pestaña/ventana nueva: eso es lo que choca con el bloqueador de
 * ventanas emergentes del navegador, y además una data URL abierta directo
 * con target="_blank" queda titulada "Untitled" (no lleva nombre de
 * archivo). Sirve tanto para el archivo recién elegido en
 * `ReceiptAttachField` (acá abajo) como para el comprobante de un pago ya
 * guardado -- quien la use decide cuál de los dos `url` es.
 */
export function ReceiptViewerDialog({ open, onClose, url, isPdf, title }) {
  return (
    <Dialog open={open} onClose={onClose} maxWidth="md" fullWidth>
      <DialogTitle sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 2 }}>
        <Typography variant="h6" component="span" noWrap>{title || 'Comprobante'}</Typography>
        <IconButton size="small" onClick={onClose} aria-label="Cerrar">
          <Iconify icon="eva:close-fill" width={20} />
        </IconButton>
      </DialogTitle>
      <DialogContent
        sx={{
          display: 'flex', alignItems: 'center', justifyContent: 'center',
          bgcolor: 'action.hover', p: 2, minHeight: '70vh',
        }}
      >
        {isPdf ? (
          <Box
            component="iframe" src={url} title={title || 'Comprobante'}
            sx={{ width: '100%', height: '70vh', border: 0, borderRadius: 1 }}
          />
        ) : (
          <Box
            component="img" src={url} alt="Comprobante"
            sx={{ maxWidth: '100%', maxHeight: '70vh', objectFit: 'contain', borderRadius: 1 }}
          />
        )}
      </DialogContent>
    </Dialog>
  );
}

/**
 * Adjuntar el comprobante de un pago antes de guardarlo: una miniatura de
 * cuerpo entero (object-fit: contain, no cover -- lo que importa es
 * apreciarla, no rellenar un marco parejo), que un click abre a tamaño
 * completo (`ReceiptViewerDialog`), y una X arriba a la derecha para
 * sacarlo por si no era el archivo que correspondía, sin tener que cerrar
 * el diálogo entero para volver a empezar.
 *
 * Nació en SanctionPaymentsDialog (cobrar la multa de una sanción) y se
 * comparte tal cual con TuitionAccountDialog (cobrar una pensión): las dos
 * pantallas cobran algo distinto, pero adjuntan el comprobante exactamente
 * igual, así que un cambio futuro a cómo se hace eso vale para las dos por
 * igual en vez de mantenerse por separado en cada una.
 */
export function ReceiptAttachField({ dataUrl, fileName, onChange, onRemove }) {
  const [viewerOpen, setViewerOpen] = useState(false);

  const isPdf = dataUrl.startsWith('data:application/pdf');

  const onPick = async (e) => {
    const file = e.target.files[0];
    e.target.value = '';
    if (!file) return;
    try {
      const url = await readInlineReceipt(file);
      onChange(url, file.name);
    } catch (err) { toast.error(err.message); }
  };

  return (
    <Paper variant="outlined" sx={{ p: 2, borderRadius: 2 }}>
      <Typography variant="subtitle2" sx={{ mb: 1.5 }}>Comprobante</Typography>

      {!dataUrl && (
        <Button variant="outlined" size="small" component="label" startIcon={<Iconify icon="eva:attach-outline" width={16} />}>
          Adjuntar comprobante
          <input type="file" accept="image/png,image/jpeg,image/webp,application/pdf" hidden onChange={onPick} />
        </Button>
      )}

      {dataUrl && (
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 2, flexWrap: 'wrap' }}>
          <Box sx={{ position: 'relative' }}>
            {isPdf ? (
              <Box
                onClick={() => setViewerOpen(true)} role="button" tabIndex={0}
                onKeyDown={(e) => { if (e.key === 'Enter' || e.key === ' ') setViewerOpen(true); }}
                sx={{
                  width: 260, height: 220, display: 'flex', flexDirection: 'column', alignItems: 'center',
                  justifyContent: 'center', gap: 1, borderRadius: 1.5, border: '1px solid', borderColor: 'divider',
                  bgcolor: 'background.paper', color: 'text.primary', cursor: 'pointer',
                  transition: 'border-color .15s', '&:hover': { borderColor: 'primary.main' },
                }}
              >
                <Iconify icon="mdi:file-pdf-box" width={56} sx={{ color: 'error.main' }} />
                <Typography variant="body2" fontWeight={600}>Ver PDF</Typography>
              </Box>
            ) : (
              <Box
                onClick={() => setViewerOpen(true)} role="button" tabIndex={0}
                onKeyDown={(e) => { if (e.key === 'Enter' || e.key === ' ') setViewerOpen(true); }}
                sx={{ display: 'block', lineHeight: 0, cursor: 'pointer' }}
              >
                <Box
                  component="img" src={dataUrl} alt="Vista previa del comprobante"
                  sx={{
                    width: 260, height: 220, objectFit: 'contain', display: 'block', borderRadius: 1.5,
                    border: '1px solid', borderColor: 'divider', bgcolor: 'action.hover', p: 0.5,
                  }}
                />
              </Box>
            )}
            <IconButton
              size="small" onClick={onRemove} aria-label="Quitar comprobante"
              sx={{
                position: 'absolute', top: -10, right: -10, width: 28, height: 28, bgcolor: 'background.paper',
                border: '1px solid', borderColor: 'divider', boxShadow: 1,
                '&:hover': { bgcolor: 'error.main', borderColor: 'error.main', color: 'error.contrastText' },
              }}
            >
              <Iconify icon="eva:close-fill" width={16} />
            </IconButton>
          </Box>

          <Box sx={{ display: 'flex', flexDirection: 'column', gap: 0.5, minWidth: 0 }}>
            <Typography variant="body2" noWrap sx={{ maxWidth: 200 }}>{fileName}</Typography>
            <Button variant="text" size="small" component="label" startIcon={<Iconify icon="eva:flip-2-outline" width={16} />} sx={{ alignSelf: 'flex-start' }}>
              Cambiar archivo
              <input type="file" accept="image/png,image/jpeg,image/webp,application/pdf" hidden onChange={onPick} />
            </Button>
          </Box>
        </Box>
      )}

      <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 1.5 }}>
        {INLINE_RECEIPT_REQUIREMENT}
      </Typography>

      <ReceiptViewerDialog
        open={viewerOpen} onClose={() => setViewerOpen(false)}
        url={dataUrl} isPdf={isPdf} title={fileName || 'Comprobante'}
      />
    </Paper>
  );
}

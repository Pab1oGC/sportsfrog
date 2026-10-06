import { useState, useEffect } from 'react';
import Dialog from '@mui/material/Dialog';
import DialogTitle from '@mui/material/DialogTitle';
import DialogContent from '@mui/material/DialogContent';
import DialogActions from '@mui/material/DialogActions';
import Button from '@mui/material/Button';
import Box from '@mui/material/Box';
import Alert from '@mui/material/Alert';
import CircularProgress from '@mui/material/CircularProgress';
import axios, { endpoints } from 'src/lib/axios';

/**
 * Cómo se vería la credencial de una persona ahora mismo, sin emitir nada.
 *
 * Decretar la estructura quitó lo único que un diseñador de plantillas daba
 * gratis: ver la tarjeta antes de mandar cuatrocientas a imprimir. Esto es
 * esa mirada, para una tarjeta que no tiene diseñador que la previsualice --
 * ver PreviewCredential del lado del servidor, que la dibuja en vivo, sin
 * número de serie ni QR real: nada de eso existe hasta que un lote lo pide.
 */
export function CredentialPreviewDialog({ competitionId, athleteId, athleteName, open, onClose }) {
  const [imageUrl, setImageUrl] = useState(null);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (!open || !competitionId || !athleteId) return undefined;

    let cancelado = false;
    setLoading(true);
    setError('');
    setImageUrl(null);

    axios.get(endpoints.credentialPreview(competitionId, athleteId), { responseType: 'blob' })
      .then((res) => {
        if (cancelado) return;
        setImageUrl(window.URL.createObjectURL(res.data));
      })
      .catch((err) => {
        if (cancelado) return;
        setError(err.response?.data?.detail || err.message || 'No se pudo generar la vista previa.');
      })
      .finally(() => { if (!cancelado) setLoading(false); });

    return () => { cancelado = true; };
  }, [open, competitionId, athleteId]);

  // La URL del blob solo vive mientras el componente la necesita -- liberarla
  // al desmontar o al pedir una nueva evita acumular objetos sin usar en la
  // memoria del navegador a medida que se previsualiza a varias personas.
  useEffect(() => () => { if (imageUrl) window.URL.revokeObjectURL(imageUrl); }, [imageUrl]);

  return (
    <Dialog open={open} onClose={onClose} maxWidth="md" fullWidth>
      <DialogTitle>Vista previa{athleteName ? ` -- ${athleteName}` : ''}</DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: '16px !important', alignItems: 'center' }}>
        {loading && (
          <Box sx={{ display: 'flex', justifyContent: 'center', py: 6 }}>
            <CircularProgress />
          </Box>
        )}
        {error && <Alert severity="error" sx={{ width: '100%' }}>{error}</Alert>}
        {imageUrl && (
          <Box
            component="img"
            src={imageUrl}
            alt="Vista previa de la credencial"
            sx={{ width: '100%', borderRadius: 1, border: '1px solid', borderColor: 'divider' }}
          />
        )}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cerrar</Button>
      </DialogActions>
    </Dialog>
  );
}

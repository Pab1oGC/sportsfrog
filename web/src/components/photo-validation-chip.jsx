import Box from '@mui/material/Box';
import Chip from '@mui/material/Chip';
import Tooltip from '@mui/material/Tooltip';
import { ESTADO_FOTO, detalleDeFoto, estadoDeFoto } from 'src/lib/photo-validation';

/**
 * El estado de la foto de un deportista. Al pasar el cursor despliega los
 * motivos por los que no pasó y las reglas que no se pudieron verificar.
 *
 * Componente de presentacion: recibe el veredicto ya armado y no pide nada.
 * Vive en su propio modulo para que la grilla y el formulario lo compartan.
 */
export function PhotoValidationChip({ validation }) {
  const estado = estadoDeFoto(validation);
  const { label, color } = ESTADO_FOTO[estado];
  const { motivos, advertencias } = detalleDeFoto(validation);

  const chip = <Chip size="small" label={label} color={color} />;

  if (motivos.length === 0 && advertencias.length === 0) {
    return chip;
  }

  const detalle = (
    <Box component="ul" sx={{ m: 0, pl: 2 }}>
      {motivos.map((texto, i) => <li key={`motivo-${i}`}>{texto}</li>)}
      {advertencias.map((texto, i) => <li key={`advertencia-${i}`}>{texto}</li>)}
    </Box>
  );

  return <Tooltip title={detalle}>{chip}</Tooltip>;
}

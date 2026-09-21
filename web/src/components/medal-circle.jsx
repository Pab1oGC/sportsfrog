import Box from '@mui/material/Box';
import { alpha } from '@mui/material/styles';
import { medalColors } from 'src/theme/palette';

/**
 * Los tres colores de medalla del portal (oro/plata/bronce) -- un solo
 * lugar, en vez de la copia local que hasta ahora repetían por separado
 * `public-competition.jsx` (Tablero y ClassificationView), CardsBoard y
 * EditorialBoard. Los valores viven en theme/palette.js; se re-exportan acá
 * porque el portal público importa MEDAL_COLORS de este archivo, y este
 * componente se importa directo y no a través del tema porque el portal
 * arma un tema propio que no trae esas claves.
 */
export var MEDAL_COLORS = medalColors;

/**
 * El círculo de posición con su medalla -- número, fondo de color si es
 * podio, y el anillo tenue que marca el podio (`0 0 0 2px` del propio
 * color de la medalla). `size` es opcional (26px, el de siempre); las
 * dos posiciones donde antes se armaba a mano con distinto tamaño
 * (Tablero 26px, ClassificationView 30px) le pasan `size` en vez de
 * duplicar el marcado.
 */
export function MedalCircle(props) {
  var position = props.position;
  var size = props.size || 26;
  var color = MEDAL_COLORS[position];

  return (
    <Box
      sx={{
        width: size, height: size, flexShrink: 0, borderRadius: '50%',
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        fontSize: size >= 30 ? 13 : 12, fontWeight: 800,
        bgcolor: color || 'action.selected',
        color: color ? 'white' : 'text.secondary',
        boxShadow: color ? '0 0 0 2px ' + alpha(color, 0.25) : 'none',
      }}
    >
      {position || '-'}
    </Box>
  );
}

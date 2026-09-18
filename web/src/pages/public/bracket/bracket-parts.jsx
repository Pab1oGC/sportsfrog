import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import { EscudoEquipo } from 'src/pages/public/match-card/match-card-parts';

/**
 * Una fila de un cruce de la llave: un equipo, con su escudo y su
 * marcador al costado -- compacta a propósito, para que quepan varios por
 * columna. Compartida por las tres variantes de cruce (standard/compact/
 * detailed); `size` pasa directo a `EscudoEquipo` para las que quieran un
 * escudo más grande, sin el prop se ve igual que siempre. El marcador
 * de quien ganó ese cruce se tiñe en `primary.main`, no solo en negrita.
 */
export function FilaCruce(props) {
  return (
    <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.75 }}>
      <EscudoEquipo url={props.logo} size={props.size} />
      <Typography variant="body2" noWrap title={props.nombre} sx={{ flexGrow: 1, minWidth: 0, fontWeight: props.gano ? 700 : 500 }}>
        {props.nombre}
      </Typography>
      <Typography
        variant="body2"
        fontWeight={800}
        sx={{ minWidth: 16, textAlign: 'right', fontVariantNumeric: 'tabular-nums', color: props.gano ? 'primary.main' : undefined }}
      >
        {props.score != null ? props.score : ''}
      </Typography>
    </Box>
  );
}

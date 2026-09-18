import Box from '@mui/material/Box';
import Divider from '@mui/material/Divider';
import Paper from '@mui/material/Paper';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import { alpha } from '@mui/material/styles';
import { MEDAL_COLORS } from 'src/components/medal-circle';

/**
 * La variante `editorial` -- lista de ranking restringida: posición y
 * nombre grandes, puntos, nada de columnas de detalle. Misma contención
 * que ya pide la variante editorial del hero -- menos ruido, no más
 * badges. La clasificación se marca con una barra de acento angosta a la
 * izquierda de la fila, no con el fondo entero.
 */
export function EditorialBoard(props) {
  var rows = props.rows;
  var qualifies = props.qualifies;

  return (
    <Paper variant="outlined" sx={{ borderRadius: 1, overflow: 'hidden' }}>
      <Stack divider={<Divider flexItem />}>
        {rows.map(function(r) {
          var clasifica = qualifies && r.position <= qualifies;
          return (
            <Box
              key={r.id}
              sx={{
                display: 'flex', alignItems: 'center', gap: 1.5, px: 2, py: 1.25,
                borderLeft: '3px solid', borderLeftColor: clasifica ? 'success.main' : 'transparent',
                bgcolor: clasifica ? (t) => alpha(t.palette.success.main, 0.08) : undefined,
              }}
            >
              <Typography
                variant="h5"
                fontWeight={MEDAL_COLORS[r.position] ? 800 : 700}
                sx={{
                  width: 32, flexShrink: 0, lineHeight: 1, fontVariantNumeric: 'tabular-nums',
                  color: MEDAL_COLORS[r.position] || 'text.secondary',
                }}
              >
                {r.position}
              </Typography>
              <Typography variant="body1" fontWeight={600} noWrap sx={{ flexGrow: 1, minWidth: 0 }} title={r.teamName}>
                {r.teamName}
              </Typography>
              <Typography variant="h6" fontWeight={800} sx={{ flexShrink: 0, lineHeight: 1, fontVariantNumeric: 'tabular-nums' }}>
                {r.points}
              </Typography>
            </Box>
          );
        })}
      </Stack>
    </Paper>
  );
}

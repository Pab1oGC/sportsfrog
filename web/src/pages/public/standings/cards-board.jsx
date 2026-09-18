import Avatar from '@mui/material/Avatar';
import Box from '@mui/material/Box';
import Paper from '@mui/material/Paper';
import Typography from '@mui/material/Typography';
import { alpha } from '@mui/material/styles';
import { Iconify } from 'src/components/iconify';
import { MedalCircle } from 'src/components/medal-circle';
import { columnasMarcador } from 'src/lib/tiebreaker-labels';

/**
 * La variante `cards` -- una tarjeta por equipo en vez de una fila de
 * tabla. Mismo círculo de posición con medalla que ya usan Tablero y
 * ClassificationView (Nivel 1), para que el lenguaje visual del portal sea
 * uno solo. La clasificación a la siguiente ronda se marca con un borde de
 * acento, no con el fondo de toda la fila (acá no hay "fila").
 */
export function CardsBoard(props) {
  var rows = props.rows;
  var cat = props.cat;
  var sportInfo = props.sportInfo;
  var qualifies = props.qualifies;
  var columnasScore = columnasMarcador(sportInfo);

  return (
    <Box sx={{ display: 'grid', gap: 1.5, gridTemplateColumns: 'repeat(auto-fill, minmax(220px, 1fr))' }}>
      {rows.map(function(r) {
        var clasifica = qualifies && r.position <= qualifies;
        return (
          <Paper
            key={r.id}
            variant="outlined"
            sx={{
              p: 1.5, borderRadius: 1,
              borderColor: clasifica ? 'success.main' : undefined,
              bgcolor: clasifica ? (t) => alpha(t.palette.success.main, 0.08) : undefined,
            }}
          >
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
              <MedalCircle position={r.position} />
              <Avatar src={r.clubLogoUrl || undefined} variant="rounded" sx={{ width: 28, height: 28, bgcolor: 'action.hover', '& img': { objectFit: 'contain' } }}>
                {!r.clubLogoUrl && <Iconify icon="mdi:office-building-outline" width={16} sx={{ color: 'text.disabled' }} />}
              </Avatar>
              <Typography variant="body2" fontWeight={600} noWrap sx={{ flexGrow: 1, minWidth: 0 }} title={r.teamName}>
                {r.teamName}
              </Typography>
              <Typography variant="h6" fontWeight={800} sx={{ flexShrink: 0, lineHeight: 1, fontVariantNumeric: 'tabular-nums' }}>
                {r.points}
              </Typography>
            </Box>
            {/* Grilla de columnas fijas, no flex envolvente: así PJ/PG/PP
                empiezan en la misma posición en todas las tarjetas de la
                misma fila, sin importar cuántos dígitos tenga cada valor. */}
            <Box
              sx={{
                display: 'grid',
                gridTemplateColumns: 'repeat(' + (cat.allowsDraw ? 6 : 5) + ', 1fr)',
                gap: 0.5, mt: 1,
              }}
            >
              <Dato label="PJ" value={r.played} />
              <Dato label="PG" value={r.won} />
              {cat.allowsDraw && <Dato label="PE" value={r.drawn} />}
              <Dato label="PP" value={r.lost} />
              <Dato label={columnasScore.favor} value={r.scoreFor} />
              <Dato label={columnasScore.contra} value={r.scoreAgainst} />
            </Box>
          </Paper>
        );
      })}
    </Box>
  );
}

function Dato(props) {
  return (
    <Box sx={{ display: 'flex', alignItems: 'baseline', gap: 0.4 }}>
      <Typography variant="caption" color="text.secondary">{props.label}</Typography>
      <Typography variant="caption" fontWeight={700} sx={{ fontVariantNumeric: 'tabular-nums' }}>{props.value}</Typography>
    </Box>
  );
}

import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import IconButton from '@mui/material/IconButton';
import Typography from '@mui/material/Typography';
import Divider from '@mui/material/Divider';
import { alpha } from '@mui/material/styles';
import { Iconify } from 'src/components/iconify';
import { ESTADO, FORMATO, LiveBadge, CuentaAtras } from './hero-parts';

/**
 * La variante `scoreboard` -- lectura de transmisión deportiva. El bloque
 * de estado deja de ser chips sueltos y se arma como una franja de celdas
 * con separador, como el marcador de un estadio; el nombre va con tracking
 * más cerrado; el campeón se lee como un resultado, no como una frase.
 */
export function ScoreboardHero(props) {
  var data = props.data;
  var comp = data.comp;
  var alFrente = data.alFrente;
  var colorTexto = data.colorTexto;
  var dateLabel = data.dateLabel;
  var social = data.social;
  var campeon = data.campeon;
  var enVivo = data.enVivo;
  var proximoPartido = data.proximoPartido;
  var eyebrow = data.eyebrow;
  var moment = data.moment;

  var celdas = [];
  if (enVivo) celdas.push({ key: 'live', node: <LiveBadge count={moment.liveMatchCount} /> });
  if (!enVivo && proximoPartido) celdas.push({ key: 'countdown', node: <CuentaAtras targetIso={proximoPartido} fg={alFrente} /> });
  if (comp.status) celdas.push({ key: 'status', node: <Celda>{ESTADO[comp.status] || comp.status}</Celda> });
  if (comp.format) celdas.push({ key: 'format', node: <Celda>{FORMATO[comp.format] || comp.format}</Celda> });
  if (dateLabel) celdas.push({ key: 'date', node: <Celda>{dateLabel}</Celda> });

  return (
    <Box>
      {props.onBack && (
        <Button
          size="small"
          sx={{ color: colorTexto, mb: 1, opacity: 0.85 }}
          onClick={props.onBack}
          startIcon={<Iconify icon="eva:arrow-back-outline" />}
        >
          Volver
        </Button>
      )}
      <Typography variant="overline" sx={{ display: 'block', fontWeight: 700, letterSpacing: 1.6, opacity: 0.8, fontSize: 11 }}>
        {eyebrow}
      </Typography>
      <Typography
        variant="h3"
        fontWeight={800}
        sx={{
          lineHeight: 1, letterSpacing: -0.75, mt: 0.25,
          fontSize: props.dense ? { xs: '1.35rem', sm: '1.75rem' } : { xs: '1.7rem', sm: '2.2rem', md: '2.9rem' },
        }}
      >
        {comp.name || 'Nombre de la competencia'}
      </Typography>

      {celdas.length > 0 && (
        <Box
          sx={{
            mt: 2, display: 'inline-flex', flexWrap: 'wrap', alignItems: 'stretch',
            border: '1px solid', borderColor: (t) => alpha(alFrente(t), 0.35), borderRadius: 1.5, overflow: 'hidden',
          }}
        >
          {celdas.map(function(c, i) {
            return (
              <Box key={c.key} sx={{ display: 'flex', alignItems: 'center' }}>
                {i > 0 && <Divider orientation="vertical" flexItem sx={{ borderColor: (t) => alpha(alFrente(t), 0.25) }} />}
                <Box sx={{ px: 1.5, py: 1, display: 'flex', alignItems: 'center' }}>{c.node}</Box>
              </Box>
            );
          })}
        </Box>
      )}

      {campeon && (
        <Box sx={{ mt: 2.5, display: 'flex', alignItems: 'baseline', gap: 1 }}>
          <Typography variant="caption" sx={{ fontWeight: 700, letterSpacing: 1.2, opacity: 0.75 }}>CAMPEÓN</Typography>
          <Typography variant="h6" fontWeight={800} sx={{ fontVariantNumeric: 'tabular-nums' }}>{campeon.teamName}</Typography>
        </Box>
      )}

      {social.length > 0 && (
        <Box sx={{ display: 'flex', gap: 1, mt: 2 }}>
          {social.map(function(s) {
            return (
              <IconButton
                key={s.key}
                size="small"
                component="a"
                href={s.href}
                target="_blank"
                rel="noopener noreferrer"
                sx={{ color: colorTexto, bgcolor: (t) => alpha(alFrente(t), 0.15) }}
              >
                <Iconify icon={s.icon} width={16} />
              </IconButton>
            );
          })}
        </Box>
      )}
    </Box>
  );
}

/** Una celda de la franja de marcador: texto pequeño en mayúsculas, sin badge. */
function Celda(props) {
  return (
    <Typography
      variant="caption"
      sx={{ fontWeight: 800, letterSpacing: 0.6, textTransform: 'uppercase', whiteSpace: 'nowrap', fontVariantNumeric: 'tabular-nums' }}
    >
      {props.children}
    </Typography>
  );
}

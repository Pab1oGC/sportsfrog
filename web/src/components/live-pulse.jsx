import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';

/**
 * El punto que respira al lado de "EN VIVO" -- lo usan el hero del portal
 * público y la tarjeta de cada partido (Partido, CruceLlave). Vive en su
 * propio módulo, no dentro de cada uno, para que el keyframe y el ritmo del
 * pulso se ajusten en un solo lugar y los dos lo hereden (mismo motivo que
 * ya separó EstadoChip de VenuesPage).
 *
 * No trae su propio color: toma `currentColor`, así que quien lo usa decide
 * el color con su propio `color`/`bgcolor` (rojo fijo de "en vivo" en un
 * fondo claro, o el contrastText del tema cuando el fondo ya es rojo) sin
 * que este componente tenga que saber cuál de los dos casos es.
 */
export function LivePulse({ label, size, sx }) {
  var dot = size === 'small' ? 6 : 8;

  return (
    <Box sx={{ display: 'inline-flex', alignItems: 'center', gap: 0.75, color: 'inherit', ...sx }}>
      <Box
        sx={{
          width: dot,
          height: dot,
          borderRadius: '50%',
          bgcolor: 'currentColor',
          flexShrink: 0,
          animation: 'sf-live-pulse 1.6s ease-in-out infinite',
          '@keyframes sf-live-pulse': { '0%, 100%': { opacity: 1 }, '50%': { opacity: 0.3 } },
          '@media (prefers-reduced-motion: reduce)': { animation: 'none' },
        }}
      />
      {label !== false && (
        <Typography component="span" variant="caption" sx={{ fontWeight: 700, letterSpacing: 0.6, lineHeight: 1, color: 'inherit' }}>
          {label || 'EN VIVO'}
        </Typography>
      )}
    </Box>
  );
}

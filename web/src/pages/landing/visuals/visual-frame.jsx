import { forwardRef } from 'react';
import Box from '@mui/material/Box';
import { alpha, useTheme } from '@mui/material/styles';
import { softShadow } from 'src/theme/shadows';
import { useDotPattern } from '../use-dot-pattern';

/**
 * El marco común de los cinco gráficos de sección (ver visuals/*-visual.jsx):
 * borde, sombra suave, esquinas redondeadas y una textura de puntos de
 * fondo -- la misma ventana para los cinco, con contenido distinto adentro.
 * Cada visual-*.jsx solo aporta lo que va dentro del `<svg>`, nunca el marco.
 *
 * `viewBox` por default es el mismo lienzo ancho para los cinco (1442x1026,
 * igual proporción que usa Railway para sus propios mocks): mantenerlo fijo
 * es lo que permite que las coordenadas de un gráfico sean comparables con
 * las de otro sin hacer la cuenta de proporción cada vez.
 */
export const VisualFrame = forwardRef(function VisualFrame({ children, viewBox = '0 0 1442 1026' }, ref) {
  const theme = useTheme();
  const dots = useDotPattern({ color: alpha(theme.palette.text.primary, 0.08) });

  return (
    <Box
      ref={ref}
      sx={{
        aspectRatio: '1442 / 1026',
        borderRadius: 2,
        border: '1px solid',
        borderColor: 'divider',
        bgcolor: 'background.paper',
        boxShadow: softShadow(1)(theme),
        overflow: 'hidden',
      }}
    >
      <svg viewBox={viewBox} width="100%" height="100%" style={{ display: 'block' }}>
        <defs>{dots.pattern}</defs>
        <rect width="100%" height="100%" fill={dots.fill} />
        {children}
      </svg>
    </Box>
  );
});

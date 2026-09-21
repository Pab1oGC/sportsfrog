import { ThemeProvider } from '@mui/material/styles';
import CssBaseline from '@mui/material/CssBaseline';
import { createAppTheme } from './index';

/**
 * Tema base del portal público. Congelado a propósito: el portal NO sigue la
 * paleta de la marca (theme/palette.js). Cada organizador elige los colores de
 * su portal en el estudio, y lo que no elige cae acá -- buildPortalTheme
 * (lib/portal-theme.js) devuelve este mismo tema tal cual si la competencia no
 * personalizó nada, y de él toma el color principal por defecto. Si ese
 * defecto siguiera a la paleta de la marca, cambiarla repintaría en silencio
 * todos los portales que nadie personalizó.
 *
 * Es la paleta que tenía la app antes del cambio a los azules de la portada.
 * Este archivo cambia solo si se decide, aparte, cambiar el look por defecto
 * del portal.
 */
export const portalBasePalette = {
  mode: 'light',
  primary: { main: '#F50057', contrastText: '#111315', lighter: '#FCE4EC' },
  background: { default: '#f4f6f8', paper: '#ffffff' },
  // El valor por defecto de MUI, explícito: theme/components.js lo lee para
  // derivar las sombras y bordes de tarjetas y tablas.
  text: { primary: 'rgba(0,0,0,0.87)' },
  divider: 'rgba(0,0,0,0.12)',
  ui: { tableHeader: 'rgba(0,0,0,0.02)', scrollbarThumb: '#c4c4c4' },
};

export const portalBaseTheme = createAppTheme(portalBasePalette);

/**
 * Envuelve una ruta del portal público con su tema base. Va anidado dentro del
 * AppThemeProvider, así que `useTheme()` adentro devuelve `portalBaseTheme` y
 * las páginas del portal no necesitan saber que la app cambió de paleta.
 * El CssBaseline repinta el fondo del <body> con el gris del portal mientras
 * esté montado.
 */
export function PortalThemeBoundary({ children }) {
  return (
    <ThemeProvider theme={portalBaseTheme}>
      <CssBaseline />
      {children}
    </ThemeProvider>
  );
}

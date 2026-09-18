import { useMemo } from 'react';
import { ThemeProvider, createTheme } from '@mui/material/styles';
import CssBaseline from '@mui/material/CssBaseline';
import { esES as dataGridEsES } from '@mui/x-data-grid/locales';

/**
 * El tema de toda la app -- un solo modo, claro, sin interruptor.
 *
 * `dataGridEsES` como segundo argumento de createTheme: es el mismo mecanismo
 * que usa @mui/material/locale, un objeto de "aumento" del tema que createTheme
 * mezcla en components — acá agrega los textos en español de la grilla ("Filas
 * por página", "de", los menús de columna) para las MuiDataGrid de toda la
 * app sin tener que pasarle localeText a cada una por separado.
 */
function construir() {
  return createTheme(
    {
      palette: {
        mode: 'light',
        primary: {
          main: '#F50057',
          contrastText: '#111315',
          lighter: '#FCE4EC',
        },
        background: {
          default: '#f4f6f8',
          paper: '#ffffff',
        },
        divider: 'rgba(0,0,0,0.12)',
      },
      typography: { fontFamily: 'Inter, sans-serif' },
      shape: { borderRadius: 12 },
      components: {
        MuiButton: {
          styleOverrides: {
            root: {
              textTransform: 'none',
              fontWeight: 600,
              transition: 'transform 0.15s ease, box-shadow 0.15s ease, background-color 0.15s ease',
            },
            contained: {
              '&:hover': { transform: 'translateY(-1px)' },
              '&:active': { transform: 'translateY(0)' },
            },
          },
        },
        // @iconify/react inyecta el contenido del SVG con dangerouslySetInnerHTML:
        // esos <path> quedan fuera del árbol de React. Un click real que aterriza
        // justo ahí a veces no burbujea hasta quien tiene el onClick — ni al
        // IconButton que lo envuelve, ni al propio <svg> cuando el onClick está
        // puesto directamente sobre el ícono (patrón usado en varias páginas del
        // panel: Clubes, Categorías, Reglamentos, Equipos, Deportistas). Global
        // en vez de acotado a MuiIconButton porque cubre los dos patrones con
        // una sola regla: el ícono nunca debe ser el que recibe el click.
        MuiCssBaseline: {
          styleOverrides: {
            '.iconify > *': { pointerEvents: 'none' },
          },
        },
        MuiCard: {
          styleOverrides: {
            root: {
              boxShadow: '0 2px 12px rgba(0,0,0,0.08)',
              transition: 'transform 0.2s ease, box-shadow 0.2s ease, border-color 0.2s ease',
            },
          },
        },
        MuiListItemButton: {
          styleOverrides: {
            root: {
              transition: 'background-color 0.15s ease, color 0.15s ease',
            },
          },
        },
        MuiDataGrid: {
          styleOverrides: {
            root: {
              border: '1px solid rgba(0,0,0,0.08)',
              borderRadius: 12,
              overflow: 'hidden',
              backgroundColor: '#ffffff',
              boxShadow: '0 2px 12px rgba(0,0,0,0.06)',
            },
            columnHeaders: {
              backgroundColor: 'rgba(0,0,0,0.02)',
              borderTopLeftRadius: 0,
              borderTopRightRadius: 0,
            },
            row: {
              transition: 'background-color 0.15s ease',
            },
          },
        },
        MuiPaper: {
          styleOverrides: {
            root: { transition: 'box-shadow 0.2s ease, border-color 0.2s ease' },
          },
        },
        // Un solo lugar para que todo Skeleton de la app respete
        // prefers-reduced-motion, en vez de que cada instancia lo repita
        // por su cuenta -- mismo criterio que ya sigue live-pulse.jsx.
        MuiSkeleton: {
          styleOverrides: {
            root: { '@media (prefers-reduced-motion: reduce)': { animation: 'none' } },
          },
        },
      },
    },
    dataGridEsES,
  );
}

export function AppThemeProvider({ children }) {
  const theme = useMemo(construir, []);

  return (
    <ThemeProvider theme={theme}>
      <CssBaseline />
      {children}
    </ThemeProvider>
  );
}

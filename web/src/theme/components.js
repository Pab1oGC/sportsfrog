import { alpha } from '@mui/material/styles';

/**
 * Overrides de componentes MUI. Ningún color va escrito acá: todo sale de la
 * `palette` que recibe (ver theme/palette.js), así que cambiar la paleta no
 * obliga a tocar este archivo.
 *
 * Objetos planos y no funciones `({ theme }) => ...` a propósito: el portal
 * público arma su tema con createTheme(base, {...}) y sus propios overrides de
 * MuiCard/MuiDataGrid (lib/portal-theme.js). El merge de MUI funde objeto con
 * objeto slot a slot, pero si `base` trae una función el objeto del portal la
 * pisa entera -- y se perdería, por ejemplo, la transición de las tarjetas.
 */
export function createComponentOverrides(palette) {
  const ink = palette.text.primary;

  return {
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
    //
    // La barra de scroll vive acá (y no en global.css) porque su color sale
    // de la paleta.
    MuiCssBaseline: {
      styleOverrides: {
        '.iconify > *': { pointerEvents: 'none' },
        '::-webkit-scrollbar': { width: 6 },
        '::-webkit-scrollbar-thumb': { background: palette.ui.scrollbarThumb, borderRadius: 3 },
      },
    },
    MuiCard: {
      styleOverrides: {
        root: {
          boxShadow: `0 2px 12px ${alpha(ink, 0.08)}`,
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
          border: `1px solid ${alpha(ink, 0.08)}`,
          borderRadius: 12,
          overflow: 'hidden',
          backgroundColor: palette.background.paper,
          boxShadow: `0 2px 12px ${alpha(ink, 0.06)}`,
        },
        columnHeaders: {
          backgroundColor: palette.ui.tableHeader,
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
  };
}

import { useState, useEffect, useMemo, useCallback, createContext, useContext } from 'react';
import { ThemeProvider, createTheme } from '@mui/material/styles';
import CssBaseline from '@mui/material/CssBaseline';

/* ---------------------------------------------------------------------------
   Modo claro y oscuro.

   Por omisión se sigue al sistema operativo, y se sigue en vivo: si alguien
   cambia el tema de su equipo con la página abierta, la página lo acompaña.
   En cuanto la persona toca el interruptor, esa elección manda y se recuerda;
   seguir consultando al sistema después de que alguien eligió sería ignorarlo.
   --------------------------------------------------------------------------- */

const CLAVE = 'color_mode';

const ColorModeContext = createContext({ mode: 'light', toggle: function() {}, sigueAlSistema: true });

export function useColorMode() {
  return useContext(ColorModeContext);
}

function elegidoAntes() {
  try {
    const v = localStorage.getItem(CLAVE);
    return v === 'light' || v === 'dark' ? v : null;
  } catch {
    return null;
  }
}

function prefiereOscuro() {
  try {
    return window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;
  } catch {
    return false;
  }
}

export function ColorModeProvider({ children }) {
  const [elegido, setElegido] = useState(elegidoAntes);
  const [delSistema, setDelSistema] = useState(function() { return prefiereOscuro() ? 'dark' : 'light'; });

  // Mientras nadie haya elegido, la página acompaña al sistema en vivo.
  useEffect(function() {
    if (!window.matchMedia) return undefined;
    const consulta = window.matchMedia('(prefers-color-scheme: dark)');
    const alCambiar = function(e) { setDelSistema(e.matches ? 'dark' : 'light'); };
    consulta.addEventListener('change', alCambiar);
    return function() { consulta.removeEventListener('change', alCambiar); };
  }, []);

  const mode = elegido || delSistema;

  const toggle = useCallback(function() {
    setElegido(function(actual) {
      const siguiente = (actual || (prefiereOscuro() ? 'dark' : 'light')) === 'dark' ? 'light' : 'dark';
      try { localStorage.setItem(CLAVE, siguiente); } catch { /* modo privado */ }
      return siguiente;
    });
  }, []);

  const theme = useMemo(function() { return construir(mode); }, [mode]);
  const valor = useMemo(function() {
    return { mode: mode, toggle: toggle, sigueAlSistema: !elegido };
  }, [mode, toggle, elegido]);

  return (
    <ColorModeContext value={valor}>
      <ThemeProvider theme={theme}>
        <CssBaseline />
        {children}
      </ThemeProvider>
    </ColorModeContext>
  );
}

/**
 * El tema, en el modo pedido.
 *
 * El verde de la marca se aclara en oscuro: el mismo #1B8A2E que se lee bien
 * sobre blanco queda por debajo del contraste mínimo sobre un fondo casi negro.
 */
function construir(mode) {
  const oscuro = mode === 'dark';

  return createTheme({
    palette: {
      mode: mode,
      primary: { main: oscuro ? '#3FBF55' : '#1B8A2E', lighter: oscuro ? 'rgba(63,191,85,0.16)' : '#E8F5EA' },
      background: {
        default: oscuro ? '#111315' : '#f4f6f8',
        paper: oscuro ? '#1A1D20' : '#ffffff',
      },
      divider: oscuro ? 'rgba(255,255,255,0.12)' : 'rgba(0,0,0,0.12)',
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
            // Una sombra negra sobre un fondo casi negro no se ve. En oscuro
            // la separación la da el borde, no la sombra.
            boxShadow: oscuro ? 'none' : '0 2px 12px rgba(0,0,0,0.08)',
            border: oscuro ? '1px solid rgba(255,255,255,0.08)' : undefined,
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
            border: oscuro ? '1px solid rgba(255,255,255,0.1)' : '1px solid rgba(0,0,0,0.08)',
            borderRadius: 12,
            overflow: 'hidden',
            backgroundColor: oscuro ? '#1A1D20' : '#ffffff',
            boxShadow: oscuro ? 'none' : '0 2px 12px rgba(0,0,0,0.06)',
          },
          columnHeaders: {
            backgroundColor: oscuro ? 'rgba(255,255,255,0.03)' : 'rgba(0,0,0,0.02)',
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
    },
  });
}

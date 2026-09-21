import { useMemo } from 'react';
import { ThemeProvider, createTheme } from '@mui/material/styles';
import CssBaseline from '@mui/material/CssBaseline';
import { esES as dataGridEsES } from '@mui/x-data-grid/locales';
import { createAppPalette } from './palette';
import { createComponentOverrides } from './components';

/**
 * El tema de toda la app -- un solo modo, claro, sin interruptor.
 *
 * Solo ensambla: los colores vienen de theme/palette.js y los overrides de
 * componentes de theme/components.js. Por eso `palette` es un parámetro (con
 * la paleta de la marca por defecto): otra paleta entra por acá sin editar
 * este archivo.
 *
 * `dataGridEsES` como segundo argumento de createTheme: es el mismo mecanismo
 * que usa @mui/material/locale, un objeto de "aumento" del tema que createTheme
 * mezcla en components — acá agrega los textos en español de la grilla ("Filas
 * por página", "de", los menús de columna) para las MuiDataGrid de toda la
 * app sin tener que pasarle localeText a cada una por separado.
 */
export function createAppTheme(palette = createAppPalette()) {
  return createTheme(
    {
      palette,
      typography: { fontFamily: 'Inter, sans-serif' },
      shape: { borderRadius: 12 },
      components: createComponentOverrides(palette),
    },
    dataGridEsES,
  );
}

export function AppThemeProvider({ children, palette }) {
  const theme = useMemo(() => createAppTheme(palette), [palette]);

  return (
    <ThemeProvider theme={theme}>
      <CssBaseline />
      {children}
    </ThemeProvider>
  );
}

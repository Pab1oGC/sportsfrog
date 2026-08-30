import IconButton from '@mui/material/IconButton';
import Tooltip from '@mui/material/Tooltip';
import { Iconify } from 'src/components/iconify';
import { useColorMode } from 'src/theme';

/**
 * El interruptor de modo claro / oscuro.
 *
 * El icono muestra a dónde se va, no dónde se está: en claro ofrece la luna.
 * Al revés es la confusión clásica de estos botones.
 */
export function ColorModeToggle({ size, sx }) {
  const { mode, toggle, sigueAlSistema } = useColorMode();
  const aOscuro = mode === 'light';

  const titulo = (aOscuro ? 'Modo oscuro' : 'Modo claro')
    + (sigueAlSistema ? ' (ahora sigue al sistema)' : '');

  return (
    <Tooltip title={titulo}>
      <IconButton onClick={toggle} size={size || 'medium'} sx={sx} aria-label={titulo}>
        <Iconify icon={aOscuro ? 'eva:moon-outline' : 'eva:sun-outline'} width={20} />
      </IconButton>
    </Tooltip>
  );
}

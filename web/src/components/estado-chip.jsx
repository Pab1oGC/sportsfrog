import Tooltip from '@mui/material/Tooltip';
import IconButton from '@mui/material/IconButton';
import Chip from '@mui/material/Chip';
import { Iconify } from 'src/components/iconify';

/**
 * El chip clickeable que alterna un booleano de una fila sin abrir su
 * dialogo de edicion -- "Activo"/"Inactivo" en Sedes, Espacios, Deportistas
 * y Equipos (activo/femenino, con los valores por defecto de abajo); "Si"/
 * "No" para "Pública" en Competencias, pasando labels/iconos/tooltip
 * propios. Vive aca, no en cada pagina, para que un lugar mas que necesite
 * el mismo patron lo reuse en vez de copiar la definicion.
 *
 * Definido en su propio modulo por la misma razon que ya obligaba a sacarlo
 * del cuerpo de VenuesPage: si viviera dentro del componente de una pagina,
 * React lo trataria como un componente distinto en cada renderizado (uno
 * nuevo por tecla escrita en cualquier campo del formulario), remontando
 * por completo el Tooltip/IconButton/Chip de cada fila — el parpadeo que se
 * veia al escribir.
 *
 * `onLabel`/`offLabel`/`onIcon`/`offIcon`/`onColor`/`offColor`/`onTooltip`/
 * `offTooltip` son opcionales: sin ellos, el chip queda igual que siempre
 * ("Activo"/"Activa" según `femenino`, verde u gris, con ✓ o ⊘, tooltip
 * "Desactivar"/"Activar"). Un llamador con un booleano de otro significado
 * -- publicar en vez de activar, retirar en vez de desactivar -- los
 * reemplaza sin tocar este archivo. Los dos tooltips son el verbo que hace
 * el click, no el estado actual: `onTooltip` se ve con el chip en su estado
 * "on" y describe la acción que apaga, y viceversa -- igual que ya hacía el
 * "Desactivar"/"Activar" por defecto.
 */
export function EstadoChip({
  activo,
  femenino,
  onClick,
  onLabel,
  offLabel,
  onIcon = 'eva:checkmark-circle-2-fill',
  offIcon = 'eva:slash-outline',
  onColor = 'success',
  offColor = 'default',
  onTooltip = 'Desactivar',
  offTooltip = 'Activar',
}) {
  const label = activo
    ? (onLabel ?? (femenino ? 'Activa' : 'Activo'))
    : (offLabel ?? (femenino ? 'Inactiva' : 'Inactivo'));

  return (
    <Tooltip title={activo ? onTooltip : offTooltip}>
      <IconButton size="small" onClick={onClick} sx={{ p: 0.25 }}>
        <Chip
          label={label}
          color={activo ? onColor : offColor}
          size="small"
          variant="outlined"
          icon={<Iconify icon={activo ? onIcon : offIcon} width={14} />}
        />
      </IconButton>
    </Tooltip>
  );
}

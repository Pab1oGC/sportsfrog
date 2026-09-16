import Tooltip from '@mui/material/Tooltip';
import IconButton from '@mui/material/IconButton';
import Chip from '@mui/material/Chip';
import { Iconify } from 'src/components/iconify';

/**
 * El chip clickeable "Activo"/"Inactivo" que ya usan Sedes y Espacios
 * (VenuesPage) para alternar el estado de una fila sin abrir su dialogo de
 * edicion. Vive aca, no en cada pagina, para que un tercer lugar que
 * necesite el mismo patron (como Deportistas) lo reuse en vez de copiar la
 * definicion.
 *
 * Definido en su propio modulo por la misma razon que ya obligaba a sacarlo
 * del cuerpo de VenuesPage: si viviera dentro del componente de una pagina,
 * React lo trataria como un componente distinto en cada renderizado (uno
 * nuevo por tecla escrita en cualquier campo del formulario), remontando
 * por completo el Tooltip/IconButton/Chip de cada fila — el parpadeo que se
 * veia al escribir.
 */
export function EstadoChip({ activo, femenino, onClick }) {
  return (
    <Tooltip title={activo ? 'Desactivar' : 'Activar'}>
      <IconButton size="small" onClick={onClick} sx={{ p: 0.25 }}>
        <Chip
          label={activo ? (femenino ? 'Activa' : 'Activo') : (femenino ? 'Inactiva' : 'Inactivo')}
          color={activo ? 'success' : 'default'}
          size="small"
          variant="outlined"
          icon={<Iconify icon={activo ? 'eva:checkmark-circle-2-fill' : 'eva:slash-outline'} width={14} />}
        />
      </IconButton>
    </Tooltip>
  );
}

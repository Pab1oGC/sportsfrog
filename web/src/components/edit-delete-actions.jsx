import Box from '@mui/material/Box';
import IconButton from '@mui/material/IconButton';
import Tooltip from '@mui/material/Tooltip';
import { Iconify } from 'src/components/iconify';

/**
 * El par Editar/Eliminar, sin menu.
 *
 * A diferencia de RowActionsMenu, este par no se esconde detras de un "...":
 * el lapiz y el tacho son de los pocos iconos que casi cualquiera reconoce
 * sin leer el tooltip, así que ocultarlos no gana nada y cuesta un toque
 * extra. El "..." se reserva para filas con acciones que de verdad no son
 * obvias a simple vista.
 */
export function EditDeleteActions({ onEdit, onDelete, editLabel = 'Editar', deleteLabel = 'Eliminar' }) {
  return (
    <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
      <Tooltip title={editLabel}>
        <IconButton size="small" onClick={onEdit}>
          <Iconify icon="eva:edit-fill" width={20} sx={{ color: 'text.secondary' }} />
        </IconButton>
      </Tooltip>
      <Tooltip title={deleteLabel}>
        <IconButton size="small" onClick={onDelete}>
          <Iconify icon="eva:trash-2-outline" width={20} sx={{ color: 'error.main' }} />
        </IconButton>
      </Tooltip>
    </Box>
  );
}

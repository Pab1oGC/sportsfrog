import { useState } from 'react';
import Box from '@mui/material/Box';
import IconButton from '@mui/material/IconButton';
import Menu from '@mui/material/Menu';
import MenuItem from '@mui/material/MenuItem';
import ListItemIcon from '@mui/material/ListItemIcon';
import ListItemText from '@mui/material/ListItemText';
import Tooltip from '@mui/material/Tooltip';
import { Iconify } from 'src/components/iconify';

/**
 * Las acciones de una fila de tabla: la mas usada como icono suelto, el
 * resto detras de un menu "...".
 *
 * Antes cada fila era una hilera de iconos con un Tooltip cada uno, lo que
 * en el celular no explica nada -no hay hover que mantener apretado- y de
 * paso empuja el ancho de toda la tabla. Un menu con texto no depende de
 * pasar el mouse para decir que hace cada boton, y reduce la columna entera
 * a uno o dos iconos.
 *
 *   <RowActionsMenu
 *     primary={{ icon: 'eva:edit-fill', label: 'Editar', onClick: () => openDialog(row) }}
 *     actions={[
 *       { icon: 'eva:trash-2-outline', label: 'Eliminar', onClick: () => remove(row.id), color: 'error.main' },
 *     ]}
 *   />
 *
 * `primary` es opcional -se omite si no hay ninguna accion lo bastante
 * estandar como para mostrarla suelta- y acepta tanto una accion sola como
 * una lista: Editar y Eliminar, en particular, se pasan siempre sueltos
 * porque el lapiz y el tacho son de los pocos iconos que casi cualquiera
 * reconoce sin leer una etiqueta. Lo que de verdad gana por esconderse
 * detras del "..." es lo demas -Walkover, Programar, Espacios- que nadie
 * adivina solo con el icono.
 *
 * Cada entrada de `actions` (y de `primary`) acepta `hidden` para dejarla
 * afuera segun el estado de la fila, sin tener que armar el arreglo a mano
 * en cada caso de uso, y `disabled` (con un `disabledLabel` opcional que
 * reemplaza el tooltip mientras lo esta).
 */
export function RowActionsMenu({ primary, actions = [] }) {
  const [anchorEl, setAnchorEl] = useState(null);
  const visible = actions.filter((a) => a && !a.hidden);
  const primaries = (Array.isArray(primary) ? primary : [primary]).filter((a) => a && !a.hidden);

  const run = (onClick) => {
    setAnchorEl(null);
    onClick();
  };

  return (
    // width/height 100%: sin esto el Box mide justo lo que ocupan sus
    // botones y "centrarlo" no hace nada — lo que tiene que centrarse es el
    // contenido dentro de toda la celda de la grilla, no el Box consigo
    // mismo (ver el mismo fix en EditDeleteActions).
    <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'center', width: '100%', height: '100%' }}>
      {primaries.map((p, i) => (
        // El span es obligatorio cuando el boton puede estar disabled: un
        // elemento deshabilitado no dispara los eventos de mouse que el
        // Tooltip necesita para saber cuando mostrarse.
        <Tooltip key={i} title={p.disabled && p.disabledLabel ? p.disabledLabel : p.label}>
          <span>
            <IconButton size="small" disabled={p.disabled} onClick={p.onClick}>
              <Iconify icon={p.icon} width={20} sx={{ color: p.color }} />
            </IconButton>
          </span>
        </Tooltip>
      ))}
      {visible.length > 0 && (
        <>
          <IconButton size="small" onClick={(e) => setAnchorEl(e.currentTarget)}>
            <Iconify icon="eva:more-vertical-fill" width={20} />
          </IconButton>
          <Menu anchorEl={anchorEl} open={Boolean(anchorEl)} onClose={() => setAnchorEl(null)}>
            {visible.map((a, i) => (
              <MenuItem key={i} disabled={a.disabled} onClick={() => run(a.onClick)}>
                <ListItemIcon>
                  <Iconify icon={a.icon} width={20} sx={{ color: a.color }} />
                </ListItemIcon>
                <ListItemText>{a.label}</ListItemText>
              </MenuItem>
            ))}
          </Menu>
        </>
      )}
    </Box>
  );
}

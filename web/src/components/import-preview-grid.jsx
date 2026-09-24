import Box from '@mui/material/Box';
import Chip from '@mui/material/Chip';
import { DataGrid } from '@mui/x-data-grid';

const COLORES = { register: 'success', create_and_register: 'info', already_registered: 'default', rejected: 'error' };
const ETIQUETAS = { register: 'Registrar', create_and_register: 'Crear+Registrar', already_registered: 'Ya registrado', rejected: 'Rechazado' };

// Cada problema en su propia linea y sin cortar: con una fila de altura fija
// el texto se recortaba con "..." justo donde estaba el motivo del rechazo, y
// por eso hay que dejar que la fila crezca (getRowHeight 'auto').
const celdaDeTexto = { whiteSpace: 'normal', overflowWrap: 'anywhere', lineHeight: 1.4, py: 1 };

/**
 * Lo que haria una importacion de Excel, fila por fila -- la vista previa de
 * la nomina de un equipo y la de una delegacion. `conCategoria` agrega la
 * columna de categoria, que solo tiene sentido en la de delegacion (cada fila
 * elige la suya; ver DelegationRosterSheet.Category en el backend).
 *
 * El contenedor necesita ser ancho para que esto se lea sin scroll: ver
 * `ANCHO_CON_VISTA_PREVIA`.
 */
export function ImportPreviewGrid({ rows, conCategoria = false }) {
  const columns = [
    { field: 'number', headerName: '#', width: 60 },
    { field: 'document', headerName: 'Documento', width: 150 },
    {
      field: 'name', headerName: 'Nombre', flex: 1, minWidth: 220,
      renderCell: ({ value }) => <Box sx={celdaDeTexto}>{value}</Box>,
    },
    conCategoria && {
      field: 'category', headerName: 'Categoria', width: 190,
      renderCell: ({ value }) => <Box sx={celdaDeTexto}>{value || '--'}</Box>,
    },
    {
      field: 'outcome', headerName: 'Accion', width: 170,
      renderCell: ({ value }) => (
        <Box sx={{ display: 'flex', alignItems: 'center', height: '100%' }}>
          <Chip label={ETIQUETAS[value] || value} color={COLORES[value] || 'default'} size="small" />
        </Box>
      ),
    },
    {
      field: 'problems', headerName: 'Observaciones', flex: 2, minWidth: 380,
      renderCell: ({ value }) => {
        const problemas = value || [];
        if (problemas.length === 0) return <Box sx={celdaDeTexto}>--</Box>;
        return (
          <Box sx={celdaDeTexto}>
            {problemas.map((problema, i) => <div key={i}>{problema}</div>)}
          </Box>
        );
      },
    },
  ].filter(Boolean);

  return (
    <DataGrid
      rows={(rows || []).map((row, i) => ({ ...row, id: i }))}
      columns={columns}
      getRowHeight={() => 'auto'}
      autoHeight
      hideFooter
      disableRowSelectionOnClick
      sx={{ mb: 2 }}
    />
  );
}

/**
 * Medidas del dialogo que las contiene: angosto mientras se elige el archivo,
 * ancho al mostrar la tabla. La misma idea en los dos dialogos de importacion.
 */
export const dialogoDeImportacion = (verTabla) => ({
  width: verTabla ? { xs: '100%', md: 1200 } : { xs: '100%', sm: 500 },
  maxWidth: verTabla ? '95vw' : 900,
});

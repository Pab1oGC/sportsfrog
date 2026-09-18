import Avatar from '@mui/material/Avatar';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import { alpha } from '@mui/material/styles';
import { DataGrid } from '@mui/x-data-grid';
import { Iconify } from 'src/components/iconify';
import { columnasMarcador } from 'src/lib/tiebreaker-labels';

/**
 * La variante `standard` -- el `DataGrid` de siempre, sin cambios de
 * comportamiento. `sortable: false` en cada columna es deliberado: una
 * tabla de posiciones pública no es una planilla, el orden lo decide el
 * reglamento (puntos, luego los criterios de desempate ya aplicados del
 * lado del servidor).
 */
export function StandardBoard(props) {
  var rows = props.rows;
  var cat = props.cat;
  var sportInfo = props.sportInfo;
  var qualifies = props.qualifies;

  var columnasScore = columnasMarcador(sportInfo);

  var cols = [
    { field: 'position', headerName: '', width: 50, sortable: false },
    { field: 'teamName', headerName: 'Equipo', flex: 1, minWidth: 180, sortable: false, renderCell: function(p) {
      return (
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, height: '100%' }}>
          <Avatar src={p.row.clubLogoUrl || undefined} variant="rounded" sx={{ width: 24, height: 24, bgcolor: 'action.hover', '& img': { objectFit: 'contain' } }}>
            {!p.row.clubLogoUrl && <Iconify icon="mdi:office-building-outline" width={14} sx={{ color: 'text.disabled' }} />}
          </Avatar>
          <Typography variant="body2" noWrap>{p.value}</Typography>
        </Box>
      );
    } },
    { field: 'played', headerName: 'PJ', width: 50, sortable: false },
    { field: 'won', headerName: 'PG', width: 50, sortable: false },
    // No es que la columna siempre de cero: es que este reglamento no
    // tiene un empate que precie — bajo sets porque el modo no lo tiene,
    // en un deporte de suma porque estos organizadores no le pusieron
    // puntaje. La pregunta directamente no aplica.
    cat.allowsDraw && { field: 'drawn', headerName: 'PE', width: 50, sortable: false },
    { field: 'lost', headerName: 'PP', width: 50, sortable: false },
    { field: 'scoreFor', headerName: columnasScore.favor, width: 50, sortable: false },
    { field: 'scoreAgainst', headerName: columnasScore.contra, width: 50, sortable: false },
    { field: 'scoreDifference', headerName: 'DF', width: 50, sortable: false },
    {
      // `align: 'center'` solo mueve el encabezado -- un `renderCell` propio
      // no hereda esa alineación sola, hay que centrar el contenido a mano
      // (mismo motivo por el que la columna "Equipo", arriba, ya arma su
      // propio Box en vez de confiar en la del DataGrid).
      field: 'points', headerName: 'Pts', width: 60, sortable: false, align: 'center', headerAlign: 'center',
      renderCell: function(p) {
        return (
          <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'center', width: '100%', height: '100%' }}>
            <Typography variant="body2" fontWeight={800} sx={{ fontVariantNumeric: 'tabular-nums' }}>{p.value}</Typography>
          </Box>
        );
      },
    }
  ].filter(Boolean);

  return (
    <DataGrid
      rows={rows}
      columns={cols}
      autoHeight
      hideFooter
      disableRowSelectionOnClick
      disableColumnMenu
      disableColumnResize
      getRowId={function(r) { return r.id; }}
      getRowClassName={function(p) { return qualifies && p.row.position <= qualifies ? 'row-clasifica' : ''; }}
      sx={{
        // Sin el menu de columna (que ademas de ordenar dejaba
        // esconder columnas) el header ya no tiene nada para hacer
        // clic — se le saca tambien el cursor y el resaltado de hover
        // que sugerian lo contrario, para que se lea como una tabla
        // fija y no como una planilla editable.
        '& .MuiDataGrid-columnHeader': { cursor: 'default' },
        '& .MuiDataGrid-columnHeader:focus, & .MuiDataGrid-columnHeader:focus-within': { outline: 'none' },
        '& .MuiDataGrid-columnHeaderTitle': { fontWeight: 700, letterSpacing: 0.3, textTransform: 'uppercase', fontSize: 11.5 },
        '& .MuiDataGrid-row:hover': { bgcolor: 'transparent' },
        '& .MuiDataGrid-row.row-clasifica, & .MuiDataGrid-row.row-clasifica:hover': function(theme) {
          return { bgcolor: alpha(theme.palette.success.main, 0.14), borderLeft: '3px solid', borderLeftColor: theme.palette.success.main };
        },
      }}
    />
  );
}

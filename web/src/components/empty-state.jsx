import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import { alpha } from '@mui/material/styles';
import { Iconify } from 'src/components/iconify';

/**
 * Un estado vacío del portal público: el mismo `Alert severity="info"` de
 * siempre (misma semántica de accesibilidad, mismo texto factual), con un
 * ícono en un círculo tonal arriba en vez de aparecer como una alerta
 * genérica de formulario. Cuatro secciones del portal (líderes,
 * clasificación, galería, calendario/llave) repetían el mismo `Alert`
 * desnudo -- esto no cambia el mensaje, solo le da una pieza visual.
 */
export function EmptyState(props) {
  return (
    <Box>
      <Box sx={{ display: 'flex', justifyContent: 'center', mb: 1.5 }}>
        <Box
          sx={{
            width: 48, height: 48, borderRadius: 1, display: 'flex',
            alignItems: 'center', justifyContent: 'center',
            bgcolor: (t) => alpha(t.palette.primary.main, 0.08),
          }}
        >
          <Iconify icon={props.icon} width={24} sx={{ color: 'primary.main' }} />
        </Box>
      </Box>
      <Alert severity="info">{props.children}</Alert>
    </Box>
  );
}

import Box from '@mui/material/Box';
import Skeleton from '@mui/material/Skeleton';
import { alpha } from '@mui/material/styles';

/**
 * El esqueleto de carga del portal público -- reemplaza al spinner
 * genérico (`CircularProgress`) que antes se repetía, ya duplicado, en
 * `Cargando` (public-competition.jsx), `standings-view.jsx` y
 * `bracket-view.jsx`. Cada `kind` imita la forma real de la sección que
 * va a aparecer, en vez de un giro sin relación con el contenido.
 */
var TINTE = { bgcolor: (t) => alpha(t.palette.primary.main, 0.11) };

function Fila(props) {
  return (
    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.25, py: 0.9 }}>
      <Skeleton variant="circular" width={26} height={26} sx={TINTE} />
      <Skeleton variant="text" sx={{ ...TINTE, flexGrow: 1 }} height={20} />
      <Skeleton variant="text" width={28} sx={TINTE} height={28} />
    </Box>
  );
}

function TarjetaPartido() {
  return (
    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5, p: 2, mb: 1.5, border: '1px solid', borderColor: 'divider', borderRadius: 2 }}>
      <Skeleton variant="text" width={64} sx={TINTE} />
      <Skeleton variant="text" sx={{ ...TINTE, flex: 1 }} />
      <Skeleton variant="rounded" width={56} height={36} sx={TINTE} />
      <Skeleton variant="text" sx={{ ...TINTE, flex: 1 }} />
    </Box>
  );
}

function ColumnaLlave() {
  return (
    <Box sx={{ minWidth: 210, width: 210, flexShrink: 0, display: 'flex', flexDirection: 'column', gap: 2 }}>
      {[0, 1].map(function(i) {
        return (
          <Box key={i} sx={{ border: '1px solid', borderColor: 'divider', borderRadius: 1.5, p: 1 }}>
            <Skeleton variant="text" sx={TINTE} />
            <Skeleton variant="text" sx={TINTE} />
          </Box>
        );
      })}
    </Box>
  );
}

export function SectionSkeleton(props) {
  var kind = props.kind;

  if (kind === 'rows') {
    return <Box>{[0, 1, 2, 3, 4].map(function(i) { return <Fila key={i} />; })}</Box>;
  }
  if (kind === 'calendar') {
    return <Box>{[0, 1, 2].map(function(i) { return <TarjetaPartido key={i} />; })}</Box>;
  }
  if (kind === 'bracket') {
    return (
      <Box sx={{ display: 'flex', gap: { xs: 2, sm: 3 } }}>
        {[0, 1].map(function(i) { return <ColumnaLlave key={i} />; })}
      </Box>
    );
  }

  return <Box sx={{ display: 'flex', justifyContent: 'center', py: 4 }}><Skeleton variant="circular" width={32} height={32} sx={TINTE} /></Box>;
}

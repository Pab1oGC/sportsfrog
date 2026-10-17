import Box from '@mui/material/Box';
import LinearProgress from '@mui/material/LinearProgress';
import Paper from '@mui/material/Paper';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import { Iconify } from 'src/components/iconify';

/**
 * Pinta los requisitos bloqueantes de una competencia como una lista con
 * barra de progreso -- nada más. No decide qué es "requisito" ni cómo se
 * cumple: eso lo resuelve quien la usa (ver overview-page.jsx, donde `pasos`
 * refleja las mismas condiciones que ChangeCompetitionStatus ya hace cumplir
 * del lado del servidor). Cada item llega resuelto como
 * { label, hecho, pendiente, onClick }, así que agregar o quitar un
 * requisito no toca este archivo.
 */
export function SetupChecklist({ items }) {
  const completados = items.filter((item) => item.hecho).length;
  const progresoPct = Math.round((completados / items.length) * 100);
  const completo = completados === items.length;

  return (
    <Box sx={{ mb: 3 }}>
      <Stack direction="row" alignItems="center" justifyContent="space-between" sx={{ mb: 1, width: '100%', gap: 2 }}>
        <Typography variant="subtitle2" color="text.secondary">Requerido</Typography>
        <Stack direction="row" alignItems="center" spacing={0.5} sx={{ flexShrink: 0 }}>
          {completo && <Iconify icon="eva:checkmark-circle-2-fill" width={16} sx={{ color: 'success.main' }} />}
          <Typography variant="caption" color="text.secondary">
            {completados}/{items.length} completados
          </Typography>
        </Stack>
      </Stack>
      <Paper variant="outlined" sx={{ p: 2, borderRadius: 2 }}>
        <LinearProgress
          variant="determinate"
          value={progresoPct}
          sx={{
            height: 8,
            borderRadius: 5,
            mb: 2,
            bgcolor: 'action.hover',
            '& .MuiLinearProgress-bar': {
              borderRadius: 5,
              bgcolor: completo ? 'success.main' : 'primary.main',
            },
          }}
        />
        <Stack spacing={0.5}>
          {items.map((item) => (
            <Box
              key={item.label}
              onClick={item.onClick}
              role="button"
              tabIndex={0}
              onKeyDown={(e) => { if (e.key === 'Enter' || e.key === ' ') item.onClick(); }}
              sx={{
                display: 'flex',
                alignItems: 'flex-start',
                gap: 1,
                p: 1,
                borderRadius: 1.5,
                cursor: 'pointer',
                '&:hover': { bgcolor: 'action.hover' },
              }}
            >
              <Iconify
                icon={item.hecho ? 'eva:checkmark-circle-2-fill' : 'eva:radio-button-off-outline'}
                width={20}
                sx={{ color: item.hecho ? 'success.main' : 'text.disabled', flexShrink: 0, mt: 0.1 }}
              />
              <Box sx={{ flexGrow: 1, minWidth: 0 }}>
                <Typography
                  variant="body2"
                  sx={{
                    textDecoration: item.hecho ? 'line-through' : 'none',
                    color: item.hecho ? 'text.secondary' : 'text.primary',
                    fontWeight: item.hecho ? 400 : 600,
                  }}
                >
                  {item.label}
                </Typography>
                {!item.hecho && (
                  <Typography variant="caption" color="text.secondary" sx={{ display: 'block' }}>
                    {item.pendiente}
                  </Typography>
                )}
              </Box>
              <Iconify icon="eva:chevron-right-fill" width={16} sx={{ color: 'text.disabled', flexShrink: 0, mt: 0.3 }} />
            </Box>
          ))}
        </Stack>
      </Paper>
    </Box>
  );
}

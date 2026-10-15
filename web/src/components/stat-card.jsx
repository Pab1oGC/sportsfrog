import Box from '@mui/material/Box';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import Typography from '@mui/material/Typography';
import { alpha } from '@mui/material/styles';
import { Iconify } from 'src/components/iconify';

/**
 * Una tarjeta de indicador: icono + numero + etiqueta. Vivia solo adentro
 * de DashboardPage; se separa para que cualquier pantalla con algo que
 * resumir en numeros (ver CollectionSummary, en Cobranza) la use igual en
 * vez de reinventar la misma tarjeta con otro nombre.
 *
 * `onClick` es opcional -- sin el, la tarjeta es puramente informativa,
 * igual que en el Dashboard. Con el (ver CollectionSummary), se vuelve un
 * acceso directo: cursor de mano y una flechita que lo deja ver.
 */
export function StatCard({ title, value, icon, color, onClick }) {
  return (
    <Card
      onClick={onClick}
      sx={{
        cursor: onClick ? 'pointer' : 'default',
        '&:hover': { transform: 'translateY(-3px)', boxShadow: (t) => `0 10px 28px ${alpha(t.palette[color].main, 0.16)}` },
      }}
    >
      <CardContent sx={{ display: 'flex', alignItems: 'center', gap: 2 }}>
        <Box
          sx={{
            width: 48, height: 48, borderRadius: 2, flexShrink: 0,
            display: 'flex', alignItems: 'center', justifyContent: 'center',
            bgcolor: (t) => alpha(t.palette[color].main, 0.12),
            transition: 'transform 0.2s ease',
            '.MuiCard-root:hover &': { transform: 'scale(1.08)' },
          }}
        >
          <Iconify icon={icon} width={24} sx={{ color: color + '.main' }} />
        </Box>
        <Box sx={{ flex: 1, minWidth: 0 }}>
          <Typography variant='h4' fontWeight={700}>{value ?? '--'}</Typography>
          <Typography variant='body2' color='text.secondary'>{title}</Typography>
        </Box>
        {onClick && <Iconify icon='eva:arrow-ios-forward-fill' width={20} sx={{ color: 'text.disabled', flexShrink: 0 }} />}
      </CardContent>
    </Card>
  );
}

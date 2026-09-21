import { useState } from 'react';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import Chip from '@mui/material/Chip';
import TextField from '@mui/material/TextField';
import MenuItem from '@mui/material/MenuItem';
import AppBar from '@mui/material/AppBar';
import Toolbar from '@mui/material/Toolbar';
import Container from '@mui/material/Container';
import CircularProgress from '@mui/material/CircularProgress';
import Alert from '@mui/material/Alert';
import { alpha } from '@mui/material/styles';
import { useNavigate, Link as RouterLink } from 'react-router';
import useSWR from 'swr';
import publicAxios from 'src/lib/public-axios';
import { Iconify } from 'src/components/iconify';
import { FREDOKA_HREF, FREDOKA_STACK } from 'src/pages/landing/playful-hero';
import { stickerShadow } from 'src/theme/shadows';

const SL = { draft: 'Borrador', scheduled: 'Programada', in_progress: 'En curso', finished: 'Finalizada', cancelled: 'Cancelada' };
const SC = { draft: 'default', scheduled: 'info', in_progress: 'warning', finished: 'success', cancelled: 'error' };
const fetcher = (url) => publicAxios.get(url).then((r) => r.data);

export default function PublicPortalPage() {
  const navigate = useNavigate();
  const [sport, setSport] = useState('');
  const [season, setSeason] = useState('');
  const [search, setSearch] = useState('');

  const qs = `?skip=0&take=24${sport ? `&sport=${sport}` : ''}${season ? `&season=${season}` : ''}${search ? `&search=${encodeURIComponent(search)}` : ''}`;
  const { data, isLoading, error } = useSWR(`/api/public/competitions${qs}`, fetcher);
  const competitions = data?.competitions || [];
  const total = data?.total || 0;

  return (
    <Box sx={{ minHeight: '100vh', bgcolor: 'background.default' }}>
      {/* React 19 iza este <link> al <head> y lo deduplica -- si el hero ya
          lo pidio (o quien entra directo a /public sin pasar por el), esto
          asegura que el titulo en Fredoka de esta pagina no se quede en la
          alternativa Inter del stack. */}
      <link rel="stylesheet" href={FREDOKA_HREF} precedence="hero-font" />
      <PublicNavbar />
      <Container maxWidth="lg" sx={{ py: 4 }}>
        <Typography variant="h4" fontWeight={700} sx={{ mb: 1, fontFamily: FREDOKA_STACK }}>Competiciones publicas</Typography>
        <Typography variant="body1" color="text.secondary" sx={{ mb: 3 }}>{total} competicion(es) publicada{total !== 1 ? 's' : ''}</Typography>

        <Box sx={{ display: 'flex', gap: 2, mb: 3, flexWrap: 'wrap' }}>
          <TextField label="Buscar" value={search} onChange={(e) => setSearch(e.target.value)} size="small" sx={{ minWidth: 200 }} placeholder="Nombre..." />
          <TextField select label="Deporte" value={sport} onChange={(e) => setSport(e.target.value)} size="small" sx={{ minWidth: 150 }}>
            <MenuItem value="">Todos</MenuItem>
            <MenuItem value="football">Futbol</MenuItem>
            <MenuItem value="futsal">Futsal</MenuItem>
            <MenuItem value="basketball">Basquetbol</MenuItem>
            <MenuItem value="volleyball">Voleibol</MenuItem>
          </TextField>
          <TextField label="Temporada" value={season} onChange={(e) => setSeason(e.target.value)} size="small" sx={{ minWidth: 120 }} placeholder="2026" />
        </Box>

        {isLoading && <Box sx={{ display: 'flex', justifyContent: 'center', py: 6 }}><CircularProgress /></Box>}
        {error && <Alert severity="info" sx={{ mb: 3 }}>Las competiciones publicadas apareceran aqui.</Alert>}
        {!isLoading && competitions.length === 0 && !error && <Alert severity="info">No hay competiciones que coincidan.</Alert>}

        <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr', md: '1fr 1fr 1fr' }, gap: 3 }}>
          {competitions.map((c) => (
            <Card key={`${c.organizationSlug}/${c.competitionSlug}`}
              sx={{
                cursor: 'pointer', border: 2, borderColor: 'brand.edge', boxShadow: 'none',
                transition: 'transform 0.15s, box-shadow 0.15s, border-color 0.15s',
                '&:hover': { transform: 'translateY(-4px)', boxShadow: stickerShadow(8, 0.9), borderColor: 'primary.main' },
              }}
              onClick={() => navigate(`/public/${c.organizationSlug}/${c.competitionSlug}`)}>
              <CardContent>
                <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 1 }}>
                  <Typography variant="h6" fontWeight={600} sx={{ flex: 1, fontSize: '1rem' }}>{c.competitionName}</Typography>
                  <Chip label={SL[c.status] || c.status} color={SC[c.status] || 'default'} size="small" />
                </Box>
                <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>{c.organizationName}</Typography>
                <Box sx={{ display: 'flex', gap: 0.5, flexWrap: 'wrap', mb: 1 }}>
                  <Chip label={c.sportName} size="small" variant="outlined" />
                  <Chip label={c.season} size="small" variant="outlined" />
                  <Chip label={`${c.categories} cat.`} size="small" variant="outlined" />
                  <Chip label={`${c.teams} equip.`} size="small" variant="outlined" />
                </Box>
                {c.startsOn && c.endsOn && <Typography variant="caption" color="text.secondary">{new Date(c.startsOn).toLocaleDateString()} - {new Date(c.endsOn).toLocaleDateString()}</Typography>}
                <Button
                  size="small" endIcon={<Iconify icon="eva:arrow-forward-outline" />}
                  sx={{ mt: 1.5, border: 2, borderColor: 'primary.main', color: 'primary.main', borderRadius: 999, px: 2, fontWeight: 700, '&:hover': { bgcolor: 'action.hover' } }}
                >
                  Ver detalles
                </Button>
              </CardContent>
            </Card>
          ))}
        </Box>
      </Container>
    </Box>
  );
}

function PublicNavbar() {
  const navigate = useNavigate();
  return (
    <AppBar
      position="static"
      elevation={0}
      sx={{
        bgcolor: (theme) => alpha(theme.palette.brand.tint, theme.palette.mode === 'dark' ? 0.92 : 0.78),
        backdropFilter: 'blur(16px) saturate(1.4)',
        color: 'primary.main',
        borderBottom: 2.5,
        borderColor: 'primary.main',
        boxShadow: 'none',
      }}
    >
      <Container maxWidth="lg">
        <Toolbar disableGutters>
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.1, cursor: 'pointer', transition: 'opacity 0.15s ease', '&:hover': { opacity: 0.8 } }} onClick={() => navigate('/')}>
            <Box sx={{
              width: 36, height: 36, borderRadius: 1.5,
              background: (theme) => `linear-gradient(135deg, ${theme.palette.brand.bright} 0%, ${theme.palette.primary.main} 100%)`,
              border: 2, borderColor: 'brand.edge', display: 'flex', alignItems: 'center', justifyContent: 'center',
              boxShadow: stickerShadow(3, 0.9),
            }}>
              <Typography sx={{ color: 'primary.contrastText', fontWeight: 800, fontSize: 16 }}>SF</Typography>
            </Box>
            <Typography variant="h6" fontWeight={700} letterSpacing={-0.2} sx={{ fontFamily: FREDOKA_STACK, color: 'primary.main' }}>SportFrog</Typography>
          </Box>
          <Box sx={{ flexGrow: 1 }} />
          <Button component={RouterLink} to="/" sx={{ mr: 1, color: 'primary.main', fontWeight: 700, borderRadius: 999, px: 2, '&:hover': { bgcolor: 'action.hover' } }}>
            Inicio
          </Button>
          <Button
            component={RouterLink} to="/auth/jwt/sign-in"
            sx={{ bgcolor: 'primary.main', color: 'primary.contrastText', px: 3, py: 0.9, borderRadius: 999, fontWeight: 700, boxShadow: stickerShadow(4, 0.9), '&:hover': { bgcolor: 'brand.bright' } }}
          >
            Iniciar sesion
          </Button>
        </Toolbar>
      </Container>
    </AppBar>
  );
}
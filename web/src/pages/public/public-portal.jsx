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
import { useNavigate, Link as RouterLink } from 'react-router';
import useSWR from 'swr';
import publicAxios from 'src/lib/public-axios';
import { Iconify } from 'src/components/iconify';
import { ColorModeToggle } from 'src/components/color-mode-toggle';
import { getThemeForCompetition } from 'src/context/tournament-theme-context';

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
      <PublicNavbar />
      <Container maxWidth="lg" sx={{ py: 4 }}>
        <Typography variant="h4" fontWeight={700} sx={{ mb: 1 }}>Competiciones públicas</Typography>
        <Typography variant="body1" color="text.secondary" sx={{ mb: 3 }}>
          {total} competición(es) publicada{total !== 1 ? 's' : ''}
        </Typography>

        <Box sx={{ display: 'flex', gap: 2, mb: 3, flexWrap: 'wrap' }}>
          <TextField label="Buscar" value={search} onChange={(e) => setSearch(e.target.value)} size="small" sx={{ minWidth: 200 }} placeholder="Nombre..." />
          <TextField select label="Deporte" value={sport} onChange={(e) => setSport(e.target.value)} size="small" sx={{ minWidth: 150 }}>
            <MenuItem value="">Todos</MenuItem>
            <MenuItem value="football">Fútbol</MenuItem>
            <MenuItem value="taekwondo_kyorugi">Taekwondo</MenuItem>
            <MenuItem value="futsal">Futsal</MenuItem>
            <MenuItem value="basketball">Básquetbol</MenuItem>
            <MenuItem value="volleyball">Voleibol</MenuItem>
          </TextField>
          <TextField label="Temporada" value={season} onChange={(e) => setSeason(e.target.value)} size="small" sx={{ minWidth: 120 }} placeholder="2026" />
        </Box>

        {isLoading && <Box sx={{ display: 'flex', justifyContent: 'center', py: 6 }}><CircularProgress /></Box>}
        {error && <Alert severity="info" sx={{ mb: 3 }}>Las competiciones publicadas aparecerán aquí.</Alert>}
        {!isLoading && competitions.length === 0 && !error && <Alert severity="info">No hay competiciones que coincidan.</Alert>}

        <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr', md: '1fr 1fr 1fr' }, gap: 3 }}>
          {competitions.map((c) => {
            const compTheme = getThemeForCompetition(c);
            return (
              <Card
                key={`${c.organizationSlug}/${c.competitionSlug}`}
                sx={{
                  cursor: 'pointer',
                  transition: 'transform 0.2s, box-shadow 0.2s',
                  '&:hover': { transform: 'translateY(-4px)', boxShadow: 4 },
                  borderRadius: 2,
                  overflow: 'hidden',
                  display: 'flex',
                  flexDirection: 'column',
                }}
                onClick={() => navigate(`/public/${c.organizationSlug}/${c.competitionSlug}`)}
              >
                <Box
                  sx={{
                    height: 120,
                    position: 'relative',
                    background: compTheme.bannerUrl
                      ? `linear-gradient(180deg, rgba(0,0,0,0.3) 0%, rgba(0,0,0,0.75) 100%), url("${compTheme.bannerUrl}") center/cover no-repeat`
                      : `linear-gradient(135deg, ${compTheme.primaryColor} 0%, ${compTheme.secondaryColor} 100%)`,
                    p: 2,
                    display: 'flex',
                    flexDirection: 'column',
                    justifyContent: 'space-between',
                    color: '#fff',
                  }}
                >
                  <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
                    <Chip
                      label={c.sportName}
                      size="small"
                      sx={{
                        bgcolor: 'rgba(255,255,255,0.25)',
                        backdropFilter: 'blur(4px)',
                        color: '#fff',
                        fontWeight: 600,
                        fontSize: '0.7rem',
                      }}
                    />
                    <Chip label={SL[c.status] || c.status} color={SC[c.status] || 'default'} size="small" />
                  </Box>
                  <Typography variant="h6" fontWeight={700} noWrap sx={{ fontSize: '1.05rem', textShadow: '0 2px 4px rgba(0,0,0,0.6)' }}>
                    {c.competitionName}
                  </Typography>
                </Box>
                <CardContent sx={{ flexGrow: 1, pt: 1.5 }}>
                  <Typography variant="body2" color="text.secondary" fontWeight={500} sx={{ mb: 1 }}>
                    {c.organizationName} &middot; {c.season}
                  </Typography>
                  <Box sx={{ display: 'flex', gap: 0.5, flexWrap: 'wrap', mb: 1.5 }}>
                    <Chip label={`${c.categories} cat.`} size="small" variant="outlined" />
                    <Chip label={`${c.teams} equip.`} size="small" variant="outlined" />
                  </Box>
                  {c.startsOn && c.endsOn && (
                    <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 1 }}>
                      {new Date(c.startsOn).toLocaleDateString()} - {new Date(c.endsOn).toLocaleDateString()}
                    </Typography>
                  )}
                  <Button
                    size="small"
                    sx={{ mt: 0.5, color: compTheme.primaryColor, fontWeight: 700 }}
                    endIcon={<Iconify icon="eva:arrow-forward-outline" />}
                  >
                    Ver detalles
                  </Button>
                </CardContent>
              </Card>
            );
          })}
        </Box>
      </Container>
    </Box>
  );
}

function PublicNavbar() {
  const navigate = useNavigate();
  return (
    <AppBar position="static" elevation={0} sx={{ bgcolor: 'background.paper', color: 'text.primary', borderBottom: '1px solid', borderColor: 'divider' }}>
      <Toolbar>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, cursor: 'pointer' }} onClick={() => navigate('/')}>
          <Box sx={{ width: 36, height: 36, borderRadius: 1.5, bgcolor: 'primary.main', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
            <Typography sx={{ color: 'white', fontWeight: 800, fontSize: 16 }}>SF</Typography>
          </Box>
          <Typography variant="h6" fontWeight={700}>SportFrog</Typography>
        </Box>
        <Box sx={{ flexGrow: 1 }} />
        <ColorModeToggle sx={{ mr: 0.5 }} />
        <Button component={RouterLink} to="/" sx={{ mr: 1 }}>Inicio</Button>
        <Button variant="outlined" component={RouterLink} to="/auth/jwt/sign-in">Iniciar sesión</Button>
      </Toolbar>
    </AppBar>
  );
}
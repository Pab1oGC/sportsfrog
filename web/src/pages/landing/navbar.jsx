import AppBar from '@mui/material/AppBar';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Container from '@mui/material/Container';
import Toolbar from '@mui/material/Toolbar';
import Typography from '@mui/material/Typography';
import { alpha } from '@mui/material/styles';
import { Link as RouterLink, useNavigate } from 'react-router';
import { INTER_TIGHT_STACK } from 'src/theme/fonts';

/**
 * El navbar de la landing, en el registro formal (ver plan de rediseño):
 * fondo esmerilado neutro y borde de 1px en vez del tinte de marca y el
 * borde grueso de antes, wordmark en Inter Tight en vez de Fredoka. Solo dos
 * destinos reales existen fuera de la propia landing, así que es todo lo
 * que ofrece: el directorio público y el inicio de sesión.
 */
export function LandingNavbar() {
  const navigate = useNavigate();

  return (
    <AppBar
      position="fixed"
      elevation={0}
      sx={{
        bgcolor: (theme) => alpha(theme.palette.background.paper, 0.82),
        backdropFilter: 'blur(16px)',
        color: 'text.primary',
        borderBottom: 1,
        borderColor: 'divider',
        boxShadow: 'none',
        zIndex: 1200,
      }}
    >
      <Container maxWidth="lg">
        <Toolbar disableGutters>
          <Box
            sx={{
              display: 'flex',
              alignItems: 'center',
              gap: 1.1,
              mr: 2,
              cursor: 'pointer',
              transition: 'opacity 0.15s ease',
              '&:hover': { opacity: 0.75 },
            }}
            onClick={() => navigate('/')}
          >
            <Box component="img" src="/logo_sportfrog.svg" alt="SportFrog" sx={{ width: 32, height: 32 }} />
            <Typography
              variant="h6"
              fontWeight={700}
              letterSpacing={-0.3}
              sx={{ fontFamily: INTER_TIGHT_STACK, color: 'text.primary' }}
            >
              SportFrog
            </Typography>
          </Box>
          <Box sx={{ flexGrow: 1 }} />
          <Button
            component={RouterLink}
            to="/public"
            sx={{
              mr: 1,
              display: { xs: 'none', sm: 'flex' },
              color: 'text.secondary',
              fontWeight: 600,
              '&:hover': { bgcolor: 'action.hover', color: 'text.primary' },
            }}
          >
            Competiciones
          </Button>
          <Button component={RouterLink} to="/auth/jwt/sign-in" variant="contained" disableElevation sx={{ px: 3 }}>
            Iniciar sesión
          </Button>
        </Toolbar>
      </Container>
    </AppBar>
  );
}

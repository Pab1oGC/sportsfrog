import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Container from '@mui/material/Container';
import Typography from '@mui/material/Typography';
import { alpha, useTheme } from '@mui/material/styles';
import { Link as RouterLink } from 'react-router';
import { Iconify } from 'src/components/iconify';
import { PLEX_SERIF_STACK } from 'src/theme/fonts';
import { softShadow } from 'src/theme/shadows';
import { useDotPattern } from './use-dot-pattern';
import { useScrollReveal } from './use-scroll-reveal';

/**
 * El cierre de la landing: un panel sólido en vez del degradé de marca que
 * tenía antes (ver plan de rediseño) -- el mismo tratamiento sobrio que las
 * cinco SectionPanel, no un remate aparte con su propio lenguaje visual.
 * La textura de puntos es la misma que usan hero.jsx y los cinco gráficos
 * (ver use-dot-pattern.jsx), solo que acá se dibuja sobre `primary.main` en
 * vez de sobre un fondo claro.
 *
 * Trae su propia animación de entrada (useScrollReveal) en vez de que
 * landing-page.jsx se la arme por fuera: el componente queda completo por sí
 * solo, como el resto de las piezas de la landing.
 */
export function ClosingCta() {
  const theme = useTheme();
  const dots = useDotPattern({ color: alpha(theme.palette.primary.contrastText, 0.12) });
  const scopeRef = useScrollReveal({ selector: '.cta-card', from: { duration: 0.7 } });

  return (
    <Container maxWidth="md" sx={{ py: { xs: 7, md: 10 } }} ref={scopeRef}>
      <Box
        className="cta-card"
        sx={{
          textAlign: 'center',
          borderRadius: 3,
          bgcolor: 'primary.main',
          color: 'primary.contrastText',
          p: { xs: 4, md: 6 },
          boxShadow: softShadow(1.5)(theme),
          position: 'relative',
          overflow: 'hidden',
        }}
      >
        <Box component="svg" aria-hidden sx={{ position: 'absolute', inset: 0, width: '100%', height: '100%' }}>
          <defs>{dots.pattern}</defs>
          <rect width="100%" height="100%" fill={dots.fill} />
        </Box>

        <Typography
          variant="h3"
          sx={{
            mb: 1.5,
            fontFamily: PLEX_SERIF_STACK,
            fontWeight: 600,
            letterSpacing: '-0.02em',
            fontSize: { xs: '1.4rem', md: '1.9rem' },
            position: 'relative',
          }}
        >
          Todo listo para tu próxima temporada
        </Typography>
        <Typography variant="body1" sx={{ opacity: 0.9, mb: 3.5, maxWidth: 520, mx: 'auto', position: 'relative' }}>
          Inicia sesión y arma tu competencia en una tarde: categorías, calendario y credenciales incluidos.
        </Typography>
        <Box sx={{ display: 'flex', gap: 2, justifyContent: 'center', flexWrap: 'wrap', position: 'relative' }}>
          <Button
            variant="contained"
            size="large"
            component={RouterLink}
            to="/auth/jwt/sign-in"
            disableElevation
            startIcon={<Iconify icon="eva:log-in-outline" />}
            sx={{ bgcolor: 'background.paper', color: 'primary.main', '&:hover': { bgcolor: 'brand.tint' } }}
          >
            Iniciar sesión
          </Button>
          <Button
            variant="outlined"
            size="large"
            component={RouterLink}
            to="/public"
            endIcon={<Iconify icon="eva:arrow-forward-outline" width={18} />}
            sx={{
              color: 'primary.contrastText',
              borderColor: alpha(theme.palette.primary.contrastText, 0.5),
              '&:hover': { bgcolor: alpha(theme.palette.primary.contrastText, 0.1), borderColor: 'primary.contrastText' },
            }}
          >
            Ver competencias en vivo
          </Button>
        </Box>
      </Box>
    </Container>
  );
}

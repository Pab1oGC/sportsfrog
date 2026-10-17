import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Container from '@mui/material/Container';
import Typography from '@mui/material/Typography';
import { alpha, useTheme } from '@mui/material/styles';
import { Link as RouterLink } from 'react-router';
import { Iconify } from 'src/components/iconify';
import { PLEX_SERIF_STACK } from 'src/theme/fonts';
import { useDotPattern } from './use-dot-pattern';

// Discreta a propósito, mismo criterio que layouts/dashboard/layout.jsx: el
// hero ya está en pantalla al cargar, así que esto no es una revelación por
// scroll (eso lo hace ScrollTrigger en otras secciones) sino la diferencia
// entre "la página ya estaba ahí" y "algo entró". Se apaga entera con
// prefers-reduced-motion.
const ENTRADA = {
  '@keyframes landingHeroIn': {
    from: { opacity: 0, transform: 'translateY(14px)' },
    to: { opacity: 1, transform: 'translateY(0)' },
  },
  animation: 'landingHeroIn 0.5s ease-out',
  '@media (prefers-reduced-motion: reduce)': { animation: 'none' },
};

/**
 * El hero formal: fondo geométrico (líneas de cancha en perspectiva, sin
 * mascotas ni ilustración) + titular editorial + dos llamadas a la acción.
 * Reemplaza a PlayfulHero (ver plan de rediseño, fase del hero).
 *
 * El fondo se dibuja en SVG por código -- HeroBackground, acá abajo -- no es
 * un asset ilustrado: no depende de un diseñador, no agrega peso al bundle,
 * y sus colores salen del tema como todo lo demás en la landing.
 */
export function Hero() {
  return (
    <Box component="section" sx={{ position: 'relative', overflow: 'hidden' }}>
      <HeroBackground />
      <Container maxWidth="md" sx={{ position: 'relative', textAlign: 'center', pt: { xs: 16, md: 22 }, pb: { xs: 10, md: 14 } }}>
        <Box sx={ENTRADA}>
          <Typography
            variant="h1"
            sx={{
              fontFamily: PLEX_SERIF_STACK,
              fontWeight: 600,
              letterSpacing: '-0.02em',
              lineHeight: 1.15,
              fontSize: { xs: '2.1rem', sm: '2.6rem', md: '3.2rem' },
            }}
          >
            Organiza tu competencia sin perder la calma
          </Typography>
          <Typography
            variant="h6"
            color="text.secondary"
            sx={{ mt: 3, fontWeight: 400, lineHeight: 1.6, maxWidth: 560, mx: 'auto' }}
          >
            Reglamento, calendario, resultados, credenciales y portal público en un solo
            lugar — sin planillas sueltas ni grupos de WhatsApp para avisar la fecha.
          </Typography>
          <Box sx={{ mt: 5, display: 'flex', gap: 2, justifyContent: 'center', flexWrap: 'wrap' }}>
            <Button
              component={RouterLink}
              to="/auth/jwt/sign-in"
              variant="contained"
              size="large"
              disableElevation
              startIcon={<Iconify icon="eva:log-in-outline" />}
            >
              Iniciar sesión
            </Button>
            <Button
              component={RouterLink}
              to="/public"
              variant="outlined"
              size="large"
              endIcon={<Iconify icon="eva:arrow-forward-outline" width={18} />}
            >
              Ver competencias publicadas
            </Button>
          </Box>
        </Box>
      </Container>
    </Box>
  );
}

/**
 * Una cancha vista en perspectiva, no una ilustración: las líneas laterales
 * convergen hacia un punto de fuga arriba, el círculo central queda
 * recortado por el borde inferior, y las líneas de profundidad se van
 * desvaneciendo a medida que se alejan. Todo el color sale de
 * `theme.palette` -- ver la regla de theme/palette.js de no escribir un hex
 * a mano -- así que esto sigue la paleta si la marca cambia de color.
 */
function HeroBackground() {
  const theme = useTheme();
  const line = theme.palette.primary.main;
  const dots = useDotPattern({ color: alpha(theme.palette.text.primary, 0.08), size: 28, radius: 1.5 });
  const vanishX = 720;
  const vanishY = 110;
  const baseY = 560;
  const spread = 920; // ancho (desde el centro) que alcanzan las líneas laterales al llegar abajo

  // Líneas de profundidad: más cerca del punto de fuga (t chico) = más lejos
  // y más tenues: las cuatro comparten la misma fórmula de perspectiva, solo
  // cambia cuánto bajaron desde el punto de fuga.
  const depthLines = [0.35, 0.55, 0.75, 0.95].map((t) => {
    const y = vanishY + (baseY - vanishY) * t;
    const halfWidth = spread * t;
    return { y, x1: vanishX - halfWidth, x2: vanishX + halfWidth, opacity: 0.08 + t * 0.22 };
  });

  return (
    <Box
      aria-hidden
      sx={{
        position: 'absolute',
        inset: 0,
        bgcolor: 'brand.page',
      }}
    >
      <svg viewBox="0 0 1440 560" preserveAspectRatio="xMidYMax slice" style={{ position: 'absolute', inset: 0, width: '100%', height: '100%' }}>
        <defs>
          <linearGradient id="landing-hero-sky" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" stopColor={theme.palette.brand.tint} />
            <stop offset="100%" stopColor={theme.palette.background.paper} />
          </linearGradient>
          <radialGradient id="landing-hero-glow" cx="50%" cy="0%" r="65%">
            <stop offset="0%" stopColor={alpha(theme.palette.brand.bright, 0.22)} />
            <stop offset="100%" stopColor={alpha(theme.palette.brand.bright, 0)} />
          </radialGradient>
          {dots.pattern}
        </defs>

        <rect width="1440" height="560" fill="url(#landing-hero-sky)" />
        <rect width="1440" height="560" fill="url(#landing-hero-glow)" />
        <rect y="200" width="1440" height="360" fill={dots.fill} />

        {/* línea lateral izquierda y derecha, convergiendo al punto de fuga */}
        <path d={`M ${vanishX} ${vanishY} L ${vanishX - spread} ${baseY}`} stroke={line} strokeWidth={2} opacity={0.18} fill="none" />
        <path d={`M ${vanishX} ${vanishY} L ${vanishX + spread} ${baseY}`} stroke={line} strokeWidth={2} opacity={0.18} fill="none" />

        {depthLines.map((depthLine) => (
          <line
            key={depthLine.y}
            x1={depthLine.x1}
            y1={depthLine.y}
            x2={depthLine.x2}
            y2={depthLine.y}
            stroke={line}
            strokeWidth={2}
            opacity={depthLine.opacity}
          />
        ))}

        {/* círculo central, recortado por el borde inferior -- como si la cámara estuviera parada sobre la línea de medio campo */}
        <ellipse cx={vanishX} cy={520} rx={260} ry={72} stroke={line} strokeWidth={2} opacity={0.22} fill="none" />
      </svg>
    </Box>
  );
}

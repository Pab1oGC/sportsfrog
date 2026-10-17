import Box from '@mui/material/Box';
import Container from '@mui/material/Container';
import Grid from '@mui/material/Grid';
import Typography from '@mui/material/Typography';
import { Link as RouterLink } from 'react-router';
import { INTER_TIGHT_STACK } from 'src/theme/fonts';
import { LANDING_SECTIONS } from './content';
import { scrollToSection } from './scroll-to-section';

const ACCOUNT_LINKS = [
  { label: 'Iniciar sesión', to: '/auth/jwt/sign-in' },
  { label: 'Competiciones públicas', to: '/public' },
];

/**
 * El footer de la landing, multicolumna (ver plan de rediseño) en vez de los
 * dos botones sueltos de antes. Dos columnas reales, no las seis de un
 * footer de SaaS grande: SportFrog no tiene blog, docs ni página de precios
 * todavía, así que no hay nada honesto que poner ahí -- "Producto" son las
 * cinco secciones de content.js (mismas anclas que section-nav.jsx) y
 * "Cuenta" son las dos rutas públicas reales.
 */
export function SiteFooter() {
  var year = new Date().getFullYear();

  return (
    <Box component="footer" sx={{ bgcolor: 'background.paper', borderTop: 1, borderColor: 'divider', py: { xs: 6, md: 8 } }}>
      <Container maxWidth="lg">
        <Grid container spacing={{ xs: 5, md: 6 }}>
          <Grid size={{ xs: 12, md: 5 }}>
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
              <Box component="img" src="/logo_sportfrog.svg" alt="SportFrog" sx={{ width: 28, height: 28 }} />
              <Typography variant="h6" fontWeight={700} letterSpacing={-0.3} sx={{ fontFamily: INTER_TIGHT_STACK }}>
                SportFrog
              </Typography>
            </Box>
            <Typography variant="body2" color="text.secondary" sx={{ mt: 2, maxWidth: 320, lineHeight: 1.7 }}>
              Reglamento, calendario, resultados, credenciales y portal público para organizar una competencia deportiva.
            </Typography>
            <Typography variant="caption" color="text.disabled" sx={{ mt: 2, display: 'block' }}>
              Hecho en Bolivia.
            </Typography>
          </Grid>

          <Grid size={{ xs: 6, md: 3.5 }}>
            <FooterColumn heading="Producto">
              {LANDING_SECTIONS.map((section) => (
                <FooterAnchorLink key={section.id} id={section.id} label={section.navLabel} />
              ))}
            </FooterColumn>
          </Grid>

          <Grid size={{ xs: 6, md: 3.5 }}>
            <FooterColumn heading="Cuenta">
              {ACCOUNT_LINKS.map((link) => (
                <FooterRouteLink key={link.to} to={link.to} label={link.label} />
              ))}
            </FooterColumn>
          </Grid>
        </Grid>

        <Box sx={{ mt: { xs: 5, md: 7 }, pt: 3, borderTop: 1, borderColor: 'divider' }}>
          <Typography variant="caption" color="text.secondary">
            © {year} SportFrog. Plataforma de gestión deportiva.
          </Typography>
        </Box>
      </Container>
    </Box>
  );
}

// `:visited` explícito y no solo `color`: el estilo por default del
// navegador para un link visitado (violeta/rojo) gana la pelea de
// especificidad contra un simple `color` en algunos casos -- FooterRouteLink
// apunta a rutas reales (/auth/jwt/sign-in, /public) que alguien puede haber
// visitado antes.
const LINK_SX = {
  display: 'block',
  color: 'text.secondary',
  textDecoration: 'none',
  fontSize: '0.875rem',
  lineHeight: 2.4,
  '&:hover': { color: 'primary.main' },
  '&:visited': { color: 'text.secondary' },
};

function FooterColumn({ heading, children }) {
  return (
    <>
      <Typography variant="overline" sx={{ color: 'text.disabled', fontWeight: 700, letterSpacing: 0.6 }}>
        {heading}
      </Typography>
      <Box sx={{ mt: 1 }}>{children}</Box>
    </>
  );
}

/** Ancla a una sección de la propia landing -- mismo destino que section-nav.jsx, ver scroll-to-section.js. */
function FooterAnchorLink({ id, label }) {
  return (
    <Box
      component="a"
      href={`#${id}`}
      onClick={(event) => {
        event.preventDefault();
        scrollToSection(id);
      }}
      sx={LINK_SX}
    >
      {label}
    </Box>
  );
}

/** Enlace a una ruta real de la app. */
function FooterRouteLink({ to, label }) {
  return (
    <Box component={RouterLink} to={to} sx={LINK_SX}>
      {label}
    </Box>
  );
}

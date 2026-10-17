import Box from '@mui/material/Box';
import Chip from '@mui/material/Chip';
import Container from '@mui/material/Container';
import Grid from '@mui/material/Grid';
import Typography from '@mui/material/Typography';
import { Iconify } from 'src/components/iconify';
import { PLEX_SERIF_STACK } from 'src/theme/fonts';

/**
 * Un bloque de la landing formal: insignia, titular editorial, párrafo, un
 * espacio para el gráfico de la sección (ver visuals/, pendientes) y una
 * fila de tres sub-características. No conoce qué sección es "Reglamento" o
 * "Portal" -- solo lee `section` (la forma de content.js) e `index`, que
 * decide cuál de los fondos de `theme.palette.panel` le toca.
 *
 * El gráfico va como `children` en vez de un campo de `section`, porque es
 * la única parte de cada sección que no es texto plano serializable: hasta
 * que exista (ver plan de rediseño, fase de los mocks SVG), se deja un
 * espacio reservado en vez de simular una captura de pantalla.
 */
export function SectionPanel({ section, index, children }) {
  return (
    <Box
      component="section"
      id={section.id}
      sx={{
        bgcolor: (theme) => theme.palette.panel[index % theme.palette.panel.length],
        py: { xs: 7, md: 11 },
        // Altura del navbar fijo + el section-nav sticky: sin esto, saltar a
        // un ancla deja su titular tapado detrás de los dos.
        scrollMarginTop: { xs: 112, md: 128 },
      }}
    >
      <Container maxWidth="lg">
        <Box sx={{ maxWidth: 560 }}>
          <Chip
            label={section.eyebrow}
            size="small"
            sx={{
              bgcolor: 'action.hover',
              color: 'primary.main',
              fontWeight: 600,
              letterSpacing: 0.4,
              textTransform: 'uppercase',
              fontSize: '0.7rem',
            }}
          />
          <Typography
            variant="h3"
            component="h2"
            sx={{
              mt: 2.5,
              fontFamily: PLEX_SERIF_STACK,
              fontWeight: 600,
              letterSpacing: '-0.02em',
              lineHeight: 1.25,
              fontSize: { xs: '1.6rem', md: '2.1rem' },
            }}
          >
            {section.title.map((line, lineIndex) => (
              <Box key={lineIndex} component="span" sx={{ display: 'block' }}>
                {line}
              </Box>
            ))}
          </Typography>
          <Typography variant="body1" color="text.secondary" sx={{ mt: 2, lineHeight: 1.7 }}>
            {section.description}
          </Typography>
        </Box>

        <Box sx={{ mt: { xs: 5, md: 7 } }}>{children || <SectionVisualPlaceholder />}</Box>

        <Grid container spacing={4} sx={{ mt: { xs: 5, md: 7 } }}>
          {section.subFeatures.map((feature) => (
            <Grid key={feature.title} size={{ xs: 12, sm: 6, md: 4 }}>
              <Iconify icon={feature.icon} width={22} sx={{ color: 'primary.main' }} />
              <Typography variant="subtitle1" fontWeight={600} sx={{ mt: 1.5 }}>
                {feature.title}
              </Typography>
              <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5, lineHeight: 1.65 }}>
                {feature.desc}
              </Typography>
            </Grid>
          ))}
        </Grid>

        {section.insteadOf && (
          <Typography
            variant="caption"
            sx={{ mt: { xs: 4, md: 6 }, display: 'block', color: 'text.secondary', fontStyle: 'italic' }}
          >
            {section.insteadOf}
          </Typography>
        )}
      </Container>
    </Box>
  );
}

/**
 * Marcador honesto mientras el gráfico real de la sección no existe. Misma
 * proporción que VisualFrame (visuals/visual-frame.jsx), para que una
 * sección nueva sin gráfico todavía no salte de tamaño el día que lo tenga.
 */
function SectionVisualPlaceholder() {
  return (
    <Box
      sx={{
        aspectRatio: '1442 / 1026',
        borderRadius: 2,
        border: (theme) => `1px dashed ${theme.palette.divider}`,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        color: 'text.disabled',
      }}
    >
      <Typography variant="body2">Vista previa en construcción</Typography>
    </Box>
  );
}

import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import Chip from '@mui/material/Chip';
import Container from '@mui/material/Container';
import Grid from '@mui/material/Grid';
import Typography from '@mui/material/Typography';
import { useTheme } from '@mui/material/styles';
import { Link as RouterLink, useNavigate } from 'react-router';
import useSWR from 'swr';
import { Iconify } from 'src/components/iconify';
import { publicFetcher } from 'src/lib/public-axios';
import { softShadow } from 'src/theme/shadows';
import { PLEX_SERIF_STACK } from 'src/theme/fonts';
import { useScrollReveal } from 'src/pages/landing/use-scroll-reveal';

const STATUS_LABEL = {
  draft: 'Borrador',
  scheduled: 'Programada',
  in_progress: 'En curso',
  finished: 'Finalizada',
  cancelled: 'Cancelada',
};
const STATUS_COLOR = {
  draft: 'default',
  scheduled: 'info',
  in_progress: 'warning',
  finished: 'success',
  cancelled: 'error',
};

/**
 * La única sección de la landing con datos reales: lo que está corriendo
 * ahora mismo en SportFrog, leído del directorio público (anónimo, cruza
 * organizaciones -- ver api/Features/Public/ReadPublicCompetitions.cs). Es
 * la única fuente honesta de números que hay: inventar una cifra de
 * marketing para esta sección era peor que no tener sección.
 *
 * Extraída de landing-page.jsx -- misma consulta, mismo click-a-detalle,
 * misma animación -- para que ese archivo quede como un compositor y esta
 * sección se pueda seguir el rastro sola, sin que su prueba tenga que montar
 * también el hero y el resto de la página. El estilo de las tarjetas sí
 * cambió desde la extracción: borde de 1px y sombra suave, igual que el
 * resto del registro formal (ver plan de rediseño).
 */
export function LiveCompetitions() {
  const theme = useTheme();
  const navigate = useNavigate();
  const { data: directorio } = useSWR('/api/public/competitions?take=100', publicFetcher, {
    revalidateOnFocus: false,
  });

  const competencias = directorio?.competitions || [];
  const enVivo = competencias.slice(0, 6);
  // deps: [enVivo.length] -- en el primer render, sin competencias, no se
  // monta nada (ver el `return null` de abajo) y el hook no tiene nada que
  // animar; cuando llega la respuesta y enVivo deja de estar vacío, este
  // efecto tiene que volver a correr contra el Container que recién se montó.
  const containerRef = useScrollReveal({ selector: '.vivo-card', from: { stagger: 0.08, duration: 0.5 }, deps: [enVivo.length] });

  if (enVivo.length === 0) return null;

  return (
    <Container maxWidth="lg" sx={{ py: { xs: 6, md: 9 } }} ref={containerRef}>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-end', gap: 2, mb: 4, flexWrap: 'wrap' }}>
        <Box>
          <Chip
            label="AHORA MISMO"
            size="small"
            sx={{
              bgcolor: 'action.hover',
              color: 'primary.main',
              fontWeight: 600,
              letterSpacing: 0.4,
              fontSize: '0.7rem',
            }}
          />
          <Typography
            variant="h3"
            component="h2"
            sx={{
              mt: 1.5,
              fontFamily: PLEX_SERIF_STACK,
              fontWeight: 600,
              letterSpacing: '-0.02em',
              fontSize: { xs: '1.6rem', md: '2.1rem' },
            }}
          >
            Competencias corriendo en SportFrog
          </Typography>
        </Box>
        <Button component={RouterLink} to="/public" variant="outlined" endIcon={<Iconify icon="eva:arrow-forward-outline" width={18} />}>
          Ver todas
        </Button>
      </Box>

      <Grid container spacing={3}>
        {enVivo.map((competencia) => (
          <Grid key={`${competencia.organizationSlug}/${competencia.competitionSlug}`} size={{ xs: 12, sm: 6, md: 4 }}>
            <Card
              className="vivo-card"
              onClick={() => navigate(`/public/${competencia.organizationSlug}/${competencia.competitionSlug}`)}
              sx={{
                height: '100%',
                display: 'flex',
                flexDirection: 'column',
                borderRadius: 3,
                cursor: 'pointer',
                border: '1px solid',
                borderColor: 'divider',
                boxShadow: 'none',
                transition: 'transform .2s, box-shadow .2s, border-color .2s',
                '&:hover': { transform: 'translateY(-4px)', boxShadow: softShadow(1)(theme), borderColor: 'primary.main' },
              }}
            >
              <CardContent sx={{ p: 2.5, flexGrow: 1, display: 'flex', flexDirection: 'column' }}>
                <Box sx={{ display: 'flex', gap: 1, mb: 1.5, flexWrap: 'wrap' }}>
                  <Chip label={STATUS_LABEL[competencia.status] || competencia.status} color={STATUS_COLOR[competencia.status] || 'default'} size="small" />
                  <Chip label={competencia.sportName} size="small" variant="outlined" />
                </Box>
                {/* Alto reservado para dos líneas siempre, tenga el nombre una
                    o dos: si no, una tarjeta con título corto queda más baja
                    que la de al lado y la fila de "equipos / categorías /
                    temporada" no coincide entre tarjetas de la misma fila. */}
                <Typography
                  variant="h6"
                  fontWeight={700}
                  sx={{
                    lineHeight: 1.3,
                    mb: 0.5,
                    minHeight: '2.6em',
                    display: '-webkit-box',
                    WebkitLineClamp: 2,
                    WebkitBoxOrient: 'vertical',
                    overflow: 'hidden',
                  }}
                >
                  {competencia.competitionName}
                </Typography>
                <Typography variant="body2" color="text.secondary" noWrap>
                  {competencia.organizationName}
                </Typography>
                {/* mt: 'auto' ancla esta fila al pie de la tarjeta pase lo que
                    pase arriba, en vez de quedar a la altura que deje el
                    contenido de arriba. */}
                <Box sx={{ display: 'flex', gap: 2, mt: 'auto', pt: 2, color: 'text.secondary' }}>
                  <Typography variant="caption">{competencia.teams} equipos</Typography>
                  <Typography variant="caption">
                    {competencia.categories} {competencia.categories === 1 ? 'categoria' : 'categorias'}
                  </Typography>
                  <Typography variant="caption">{competencia.season}</Typography>
                </Box>
              </CardContent>
            </Card>
          </Grid>
        ))}
      </Grid>
    </Container>
  );
}

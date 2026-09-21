import { useEffect, useRef } from 'react';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import Grid from '@mui/material/Grid';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import Chip from '@mui/material/Chip';
import AppBar from '@mui/material/AppBar';
import Toolbar from '@mui/material/Toolbar';
import Container from '@mui/material/Container';
import { alpha } from '@mui/material/styles';
import { Link as RouterLink, useNavigate } from 'react-router';
import useSWR from 'swr';
import { publicFetcher } from 'src/lib/public-axios';
import { Iconify } from 'src/components/iconify';
import { PlayfulHero, FREDOKA_STACK } from 'src/pages/landing/playful-hero';
import { sinMovimiento } from 'src/lib/motion';
import { stickerShadow, glowShadow } from 'src/theme/shadows';
import gsap from 'gsap';
import { ScrollTrigger } from 'gsap/ScrollTrigger';

gsap.registerPlugin(ScrollTrigger);

var SL = { draft: 'Borrador', scheduled: 'Programada', in_progress: 'En curso', finished: 'Finalizada', cancelled: 'Cancelada' };
var SC = { draft: 'default', scheduled: 'info', in_progress: 'warning', finished: 'success', cancelled: 'error' };

// Los colores de esta portada salen del tema (theme/palette.js): acá no hay
// ningun hex.

var features = [
  { icon: 'mdi:trophy-outline', title: 'Competiciones', desc: 'Ligas, eliminatorias y fases de grupos, cada una con su reglamento.' },
  { icon: 'mdi:account-group-outline', title: 'Equipos y nomina', desc: 'Clubes, deportistas e inscripciones por categoria, o una planilla de Excel.' },
  { icon: 'mdi:calendar-clock-outline', title: 'Calendario', desc: 'El fixture se sortea solo y los partidos se reparten entre tus canchas.' },
  { icon: 'mdi:format-list-numbered', title: 'Tabla de posiciones', desc: 'Se recalcula con cada resultado, con los desempates que definiste.' },
  { icon: 'mdi:certificate-outline', title: 'Credenciales', desc: 'Acomoda los datos sobre tu arte y emitilas en lote, con QR verificable.' },
  { icon: 'mdi:earth', title: 'Portal publico', desc: 'Tus resultados en una direccion que cualquiera puede abrir y compartir.' }
];

var steps = [
  { num: '01', title: 'Configura la competencia', desc: 'Define categorias, reglamento y equipos participantes.' },
  { num: '02', title: 'Sortea el calendario', desc: 'El sistema arma los cruces y los reparte entre sedes y horarios.' },
  { num: '03', title: 'Carga resultados', desc: 'La tabla de posiciones y los lideres se recalculan solos.' },
  { num: '04', title: 'Publica y emite', desc: 'El portal publico se actualiza solo, y las credenciales salen en lote.' }
];

export default function LandingPage() {
  var navigate = useNavigate();
  var raizRef = useRef(null);
  var vivoRef = useRef(null);

  // El directorio publico es anonimo, asi que la portada puede contarlo sin
  // que nadie haya iniciado sesion. Es la unica fuente honesta de numeros que
  // hay: los que estaban antes ("500+ competiciones", "99.9% uptime") no
  // salian de ningun lado.
  var { data: directorio } = useSWR('/api/public/competitions?take=100', publicFetcher, {
    revalidateOnFocus: false
  });

  var competencias = (directorio && directorio.competitions) || [];
  var totalCompetencias = (directorio && directorio.total) || 0;

  var cifras = totalCompetencias > 0 ? [
    { valor: totalCompetencias, etiqueta: 'Competencias publicadas' },
    { valor: new Set(competencias.map(function(c) { return c.organizationSlug; })).size, etiqueta: 'Organizaciones' },
    { valor: competencias.reduce(function(a, c) { return a + c.teams; }, 0), etiqueta: 'Equipos en juego' },
    { valor: new Set(competencias.map(function(c) { return c.sportCode; })).size, etiqueta: 'Deportes' }
  ] : [];

  var enVivo = competencias.slice(0, 6);

  // Las entradas de las secciones que siguen al hero (el hero tiene su
  // propia entrada, ver PlayfulHero).
  //
  // Van dentro de un gsap.context que se revierte al desmontar, y eso no es
  // prolijidad: StrictMode monta, desmonta y vuelve a montar. Un tl.kill() a
  // secas deja los elementos en opacity 0, y el segundo montaje hace
  // from(opacity:0) sobre algo que ya vale 0 — o sea, de 0 a 0. Por eso las
  // tarjetas aparecian en blanco. revert() devuelve los estilos originales
  // antes de volver a empezar.
  useEffect(function() {
    if (sinMovimiento()) return undefined;

    var ctx = gsap.context(function() {
      gsap.from('.feature-card', {
        y: 60, opacity: 0, duration: 0.6, stagger: 0.1,
        scrollTrigger: { trigger: '.features-grid', start: 'top 80%' }
      });

      gsap.from('.step-item', {
        y: 40, opacity: 0, duration: 0.5, stagger: 0.12,
        scrollTrigger: { trigger: '.steps-grid', start: 'top 80%' }
      });

      gsap.from('.cta-card', {
        y: 40, opacity: 0, duration: 0.7,
        scrollTrigger: { trigger: '.cta-card', start: 'top 85%' }
      });
    }, raizRef);

    return function() { ctx.revert(); };
  }, []);

  // La seccion de competencias llega despues que el resto, cuando responde la
  // API, asi que se anima aparte: animarla en el efecto de arriba seria
  // animar elementos que todavia no existen.
  useEffect(function() {
    if (sinMovimiento() || enVivo.length === 0) return undefined;

    var ctx = gsap.context(function() {
      gsap.from('.vivo-card', {
        y: 40, opacity: 0, duration: 0.5, stagger: 0.08,
        scrollTrigger: { trigger: vivoRef.current, start: 'top 85%' }
      });
    }, vivoRef);

    return function() { ctx.revert(); };
  }, [enVivo.length]);

  return (
    <Box ref={raizRef} sx={{ minHeight: '100vh', bgcolor: 'background.default' }}>
      {/* Misma linea grafica que PlayfulHero: tinte celeste del hero en vez
          del blanco/paper generico, borde negro grueso en vez de un
          "divider" de 1px, y botones tipo sticker (pildora, sombra solida)
          en vez de los defaults de MUI. El contenido va en un Container
          maxWidth="lg" -- igual que el resto de las secciones -- para que
          no quede pegado a los bordes de la pantalla como antes. */}
      <AppBar
        position="fixed"
        elevation={0}
        sx={{
          bgcolor: (theme) => alpha(theme.palette.brand.tint, theme.palette.mode === 'dark' ? 0.92 : 0.78),
          backdropFilter: 'blur(16px) saturate(1.4)',
          color: 'primary.main',
          borderBottom: 2.5,
          borderColor: 'primary.main',
          boxShadow: 'none',
          zIndex: 1200,
        }}
      >
        <Container maxWidth="lg">
          <Toolbar disableGutters>
            <Box
              sx={{
                display: 'flex', alignItems: 'center', gap: 1.1, mr: 2, cursor: 'pointer',
                transition: 'opacity 0.15s ease', '&:hover': { opacity: 0.8 },
              }}
              onClick={function() { navigate('/'); }}
            >
              <Box component="img" src="/logo_sportfrog.svg" alt="SportFrog" sx={{ width: 40, height: 40 }} />
              <Typography variant="h6" fontWeight={700} letterSpacing={-0.2} sx={{ fontFamily: FREDOKA_STACK, color: 'primary.main' }}>SportFrog</Typography>
            </Box>
            <Box sx={{ flexGrow: 1 }} />
            <Button
              component={RouterLink} to="/public"
              sx={{
                mr: 1, display: { xs: 'none', sm: 'flex' }, color: 'primary.main', fontWeight: 700,
                borderRadius: 999, px: 2, '&:hover': { bgcolor: 'action.hover' },
              }}
            >
              Competiciones
            </Button>
            <Button
              component={RouterLink} to="/auth/jwt/sign-in"
              sx={{
                bgcolor: 'primary.main', color: 'primary.contrastText', px: 3, py: 0.9, borderRadius: 999, fontWeight: 700,
                boxShadow: stickerShadow(4, 0.9),
                '&:hover': { bgcolor: 'brand.bright' },
              }}
            >
              Iniciar sesion
            </Button>
          </Toolbar>
        </Container>
      </AppBar>

      {/* -------------------------------------------------------------- hero */}
      <PlayfulHero />

      {/* ------------------------------------------------------------ cifras */}
      {cifras.length > 0 && (
        // Antes se superponia al hero con margen negativo, para flotar
        // sobre el piso oscuro con que terminaba el degrade viejo. El hero
        // nuevo (PlayfulHero) termina en un color plano sin ese piso, asi
        // que ahora es solo un respiro despues de el.
        <Container maxWidth="lg" sx={{ position: 'relative', mt: { xs: 5, md: 7 }, mb: { xs: 2, md: 4 } }}>
          <Grid container spacing={{ xs: 2, md: 3 }}>
            {cifras.map(function(c) {
              return (
                <Grid key={c.etiqueta} size={{ xs: 6, md: 3 }}>
                  <Card sx={{ textAlign: 'center', py: { xs: 2, md: 2.5 }, borderRadius: 3, bgcolor: 'background.paper', border: 2, borderColor: 'brand.edge', boxShadow: stickerShadow(6, 0.55), height: '100%' }}>
                    <Contador valor={c.valor} etiqueta={c.etiqueta} />
                  </Card>
                </Grid>
              );
            })}
          </Grid>
        </Container>
      )}

      {/* ------------------------------------------------ competencias reales */}
      {enVivo.length > 0 && (
        <Container maxWidth="lg" sx={{ py: { xs: 6, md: 9 } }} ref={vivoRef}>
          <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-end', gap: 2, mb: 3, flexWrap: 'wrap' }}>
            <Box>
              <Typography variant="overline" fontWeight={700} sx={{ letterSpacing: 2, color: 'primary.main' }}>AHORA MISMO</Typography>
              <Typography variant="h3" fontWeight={800} sx={{ mt: 0.5, fontFamily: FREDOKA_STACK, fontSize: { xs: '1.5rem', md: '2rem' } }}>
                Competencias corriendo en SportFrog
              </Typography>
            </Box>
            <Button
              component={RouterLink} to="/public" endIcon={<Iconify icon="eva:arrow-forward-outline" width={18} />}
              sx={{ border: 2, borderColor: 'primary.main', color: 'primary.main', borderRadius: 999, px: 2.5, fontWeight: 700, '&:hover': { bgcolor: 'action.hover' } }}
            >
              Ver todas
            </Button>
          </Box>

          <Grid container spacing={3}>
            {enVivo.map(function(c) {
              return (
                <Grid key={c.organizationSlug + '/' + c.competitionSlug} size={{ xs: 12, sm: 6, md: 4 }}>
                  <Card
                    className="vivo-card"
                    onClick={function() { navigate('/public/' + c.organizationSlug + '/' + c.competitionSlug); }}
                    sx={{
                      height: '100%', display: 'flex', flexDirection: 'column',
                      borderRadius: 3, cursor: 'pointer', border: 2, borderColor: 'brand.edge', boxShadow: 'none',
                      transition: 'transform .2s, box-shadow .2s, border-color .2s',
                      '&:hover': { transform: 'translateY(-4px)', boxShadow: stickerShadow(8, 0.9), borderColor: 'primary.main' }
                    }}
                  >
                    <CardContent sx={{ p: 2.5, flexGrow: 1, display: 'flex', flexDirection: 'column' }}>
                      <Box sx={{ display: 'flex', gap: 1, mb: 1.5, flexWrap: 'wrap' }}>
                        <Chip label={SL[c.status] || c.status} color={SC[c.status] || 'default'} size="small" />
                        <Chip label={c.sportName} size="small" variant="outlined" />
                      </Box>
                      {/* Alto reservado para dos lineas siempre, tenga el
                          nombre una o dos: si no, una tarjeta con titulo
                          corto queda mas baja que la de al lado y la fila de
                          "equipos / categorias / temporada" no coincide
                          entre tarjetas de la misma fila. */}
                      <Typography
                        variant="h6"
                        fontWeight={700}
                        sx={{
                          lineHeight: 1.3, mb: 0.5, minHeight: '2.6em',
                          display: '-webkit-box', WebkitLineClamp: 2, WebkitBoxOrient: 'vertical', overflow: 'hidden',
                        }}
                      >
                        {c.competitionName}
                      </Typography>
                      <Typography variant="body2" color="text.secondary" noWrap>{c.organizationName}</Typography>
                      {/* mt: 'auto' en vez de un mt fijo: ahora que CardContent
                          es una columna flex, esto ancla la fila de datos al
                          pie de la tarjeta pase lo que pase arriba, en vez de
                          quedar a la altura que el contenido de arriba deje. */}
                      <Box sx={{ display: 'flex', gap: 2, mt: 'auto', pt: 2, color: 'text.secondary' }}>
                        <Typography variant="caption">{c.teams} equipos</Typography>
                        <Typography variant="caption">{c.categories} {c.categories === 1 ? 'categoria' : 'categorias'}</Typography>
                        <Typography variant="caption">{c.season}</Typography>
                      </Box>
                    </CardContent>
                  </Card>
                </Grid>
              );
            })}
          </Grid>
        </Container>
      )}

      {/* --------------------------------------------------- funcionalidades */}
      <Container maxWidth="lg" sx={{ py: { xs: 6, md: 9 } }}>
        <Box sx={{ textAlign: 'center', mb: 5 }}>
          <Typography variant="overline" fontWeight={700} sx={{ letterSpacing: 2, color: 'primary.main' }}>FUNCIONALIDADES</Typography>
          <Typography variant="h3" fontWeight={800} sx={{ mt: 0.5, fontFamily: FREDOKA_STACK, fontSize: { xs: '1.5rem', md: '2rem' } }}>Todo lo que necesitas para organizar deporte</Typography>
          <Typography variant="body1" color="text.secondary" sx={{ mt: 1.5, maxWidth: 620, mx: 'auto' }}>
            Sin planillas sueltas, sin grupos de WhatsApp para avisar la fecha.
          </Typography>
        </Box>
        <Grid container spacing={3} className="features-grid">
          {features.map(function(f) {
            return (
              <Grid key={f.title} size={{ xs: 12, sm: 6, md: 4 }}>
                <Card className="feature-card" sx={{ height: '100%', borderRadius: 3, border: 2, borderColor: 'brand.edge', boxShadow: 'none', transition: 'transform .25s, box-shadow .25s, border-color .25s', '&:hover': { transform: 'translateY(-5px)', boxShadow: stickerShadow(8, 0.9), borderColor: 'primary.main' } }}>
                  <CardContent sx={{ p: 3 }}>
                    <Box sx={{
                      width: 48, height: 48, borderRadius: 2.5, border: 2, borderColor: 'primary.main',
                      background: function(theme) {
                        return 'linear-gradient(135deg, ' + alpha(theme.palette.primary.main, 0.16) + ', ' + alpha(theme.palette.primary.main, 0.05) + ')';
                      },
                      display: 'flex', alignItems: 'center', justifyContent: 'center', mb: 2
                    }}>
                      <Iconify icon={f.icon} width={24} sx={{ color: 'primary.main' }} />
                    </Box>
                    <Typography variant="h6" fontWeight={700} sx={{ mb: 0.75 }}>{f.title}</Typography>
                    <Typography variant="body2" color="text.secondary" sx={{ lineHeight: 1.7 }}>{f.desc}</Typography>
                  </CardContent>
                </Card>
              </Grid>
            );
          })}
        </Grid>
      </Container>

      {/* ------------------------------------------------------ como funciona */}
      {/* Mismo cherub del fondo del hero en vez del panel oscuro que tenia
          antes -- una franja de color en vez de un quiebre a modo oscuro,
          bordeada igual que el hero (borde grueso arriba y abajo). */}
      <Box sx={{ bgcolor: 'brand.tint', color: 'text.primary', py: { xs: 7, md: 10 }, borderTop: 2.5, borderBottom: 2.5, borderColor: 'primary.main' }}>
        <Container maxWidth="lg">
          <Box sx={{ textAlign: 'center', mb: 5 }}>
            <Typography variant="overline" sx={{ color: 'primary.main', letterSpacing: 2, fontWeight: 700 }}>COMO FUNCIONA</Typography>
            <Typography variant="h3" fontWeight={800} sx={{ mt: 0.5, fontFamily: FREDOKA_STACK, fontSize: { xs: '1.5rem', md: '2rem' } }}>En cuatro pasos</Typography>
          </Box>
          <Grid container spacing={4} className="steps-grid">
            {steps.map(function(s) {
              return (
                <Grid key={s.num} size={{ xs: 12, sm: 6, md: 3 }}>
                  <Box className="step-item">
                    <Typography variant="h1" fontWeight={900} sx={{ color: function(theme) { return alpha(theme.palette.primary.main, 0.18); }, fontFamily: FREDOKA_STACK, fontSize: '3.6rem', lineHeight: 1 }}>{s.num}</Typography>
                    <Box sx={{ width: 32, height: 3, borderRadius: 2, bgcolor: 'brand.accent', mt: -1.5, mb: 1.5 }} />
                    <Typography variant="h6" fontWeight={700} sx={{ position: 'relative' }}>{s.title}</Typography>
                    <Typography variant="body2" sx={{ color: 'text.secondary', mt: 1, lineHeight: 1.7 }}>{s.desc}</Typography>
                  </Box>
                </Grid>
              );
            })}
          </Grid>
        </Container>
      </Box>

      {/* --------------------------------------------------------------- cta */}
      <Container maxWidth="md" sx={{ py: { xs: 7, md: 10 } }}>
        <Card className="cta-card" sx={{
          textAlign: 'center', borderRadius: 4,
          background: function(theme) {
            var p = theme.palette;
            return 'linear-gradient(135deg, ' + p.brand.bright + ' 0%, ' + p.primary.main + ' 60%, ' + p.brand.edge + ' 100%)';
          },
          color: 'primary.contrastText', p: { xs: 4, md: 6 }, boxShadow: glowShadow(20, 60, 0.32), position: 'relative', overflow: 'hidden'
        }}>
          <Box sx={{
            position: 'absolute', top: -80, right: -80, width: 240, height: 240, borderRadius: '50%',
            background: function(theme) {
              var accent = theme.palette.brand.accent;
              return 'radial-gradient(circle, ' + alpha(accent, 0.18) + ' 0%, ' + alpha(accent, 0) + ' 70%)';
            }
          }} />
          <Typography variant="h3" fontWeight={800} sx={{ mb: 1.5, fontFamily: FREDOKA_STACK, fontSize: { xs: '1.4rem', md: '1.9rem' }, position: 'relative' }}>Todo listo para tu proxima temporada</Typography>
          <Typography variant="body1" sx={{ opacity: 0.9, mb: 3.5, maxWidth: 520, mx: 'auto', position: 'relative' }}>
            Inicia sesion y arma tu competencia en una tarde: categorias, calendario y credenciales incluidos.
          </Typography>
          <Box sx={{ display: 'flex', gap: 2, justifyContent: 'center', flexWrap: 'wrap', position: 'relative' }}>
            <Button variant="contained" size="large" component={RouterLink} to="/auth/jwt/sign-in"
              startIcon={<Iconify icon="eva:log-in-outline" />}
              sx={{ bgcolor: 'background.paper', color: 'primary.main', px: 4.5, py: 1.4, borderRadius: 2.5, fontWeight: 700, boxShadow: function(theme) { return '0 4px 20px ' + alpha(theme.palette.text.primary, 0.18); }, '&:hover': { bgcolor: 'brand.tint' } }}>
              Iniciar sesion
            </Button>
            <Button size="large" component={RouterLink} to="/public"
              endIcon={<Iconify icon="eva:arrow-forward-outline" width={18} />}
              sx={{
                color: 'primary.contrastText', border: 2, borderColor: function(theme) { return alpha(theme.palette.primary.contrastText, 0.7); },
                borderRadius: 999, px: 3, py: 1.35, fontWeight: 700,
                '&:hover': { bgcolor: function(theme) { return alpha(theme.palette.primary.contrastText, 0.12); }, borderColor: 'primary.contrastText' }
              }}>
              Ver competencias en vivo
            </Button>
          </Box>
        </Card>
      </Container>

      {/* ------------------------------------------------------------ footer */}
      {/* Banda clara con borde grueso arriba, como el borde inferior del
          navbar del hero -- en vez del panel oscuro que tenia antes. */}
      <Box sx={{ bgcolor: 'background.paper', color: 'text.secondary', py: 4, px: 3, borderTop: 2.5, borderColor: 'primary.main' }}>
        <Container maxWidth="lg" sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 2 }}>
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
            <Box sx={{ width: 28, height: 28, borderRadius: 1, bgcolor: 'primary.main', border: 2, borderColor: 'brand.edge', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
              <Typography sx={{ color: 'primary.contrastText', fontWeight: 800, fontSize: 11 }}>SF</Typography>
            </Box>
            <Typography variant="body2" sx={{ color: 'text.secondary' }}>2026 SportFrog. Plataforma de gestion deportiva.</Typography>
          </Box>
          <Box sx={{ display: 'flex', gap: 1 }}>
            <Button component={RouterLink} to="/public" sx={{ color: 'text.secondary', fontSize: '0.8rem', '&:hover': { color: 'primary.main' } }}>Competiciones</Button>
            <Button component={RouterLink} to="/auth/jwt/sign-in" sx={{ color: 'text.secondary', fontSize: '0.8rem', '&:hover': { color: 'primary.main' } }}>Iniciar sesion</Button>
          </Box>
        </Container>
      </Box>
    </Box>
  );
}

/** Cuenta hasta el numero al entrar en pantalla. */
function Contador(props) {
  var cajaRef = useRef(null);
  var textoRef = useRef(null);
  var valor = props.valor;

  useEffect(function() {
    if (!cajaRef.current || !textoRef.current) return undefined;

    // Sin animacion el numero igual tiene que estar: se escribe y se sale.
    if (sinMovimiento()) {
      textoRef.current.textContent = String(valor);
      return undefined;
    }

    var ctx = gsap.context(function() {
      var estado = { v: 0 };
      gsap.to(estado, {
        v: valor,
        duration: 1.4,
        ease: 'power2.out',
        scrollTrigger: { trigger: cajaRef.current, start: 'top 92%' },
        onUpdate: function() {
          if (textoRef.current) textoRef.current.textContent = String(Math.round(estado.v));
        },
        onComplete: function() {
          if (textoRef.current) textoRef.current.textContent = String(valor);
        }
      });
    }, cajaRef);

    return function() {
      ctx.revert();
      if (textoRef.current) textoRef.current.textContent = String(valor);
    };
  }, [valor]);

  return (
    <Box ref={cajaRef} sx={{ px: 1 }}>
      <Typography ref={textoRef} variant="h2" fontWeight={800} sx={{ fontFamily: FREDOKA_STACK, fontSize: { xs: '1.9rem', md: '2.6rem' }, lineHeight: 1.1, color: 'primary.main' }}>
        {valor}
      </Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5, fontSize: { xs: '0.78rem', md: '0.875rem' } }}>
        {props.etiqueta}
      </Typography>
    </Box>
  );
}

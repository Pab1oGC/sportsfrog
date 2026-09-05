import { useEffect, useRef, useState } from 'react';
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
import publicAxios from 'src/lib/public-axios';
import { Iconify } from 'src/components/iconify';
import gsap from 'gsap';
import { ScrollTrigger } from 'gsap/ScrollTrigger';
import { ColorModeToggle } from 'src/components/color-mode-toggle';

gsap.registerPlugin(ScrollTrigger);

var publicFetcher = function(url) { return publicAxios.get(url).then(function(r) { return r.data; }); };

var SL = { draft: 'Borrador', scheduled: 'Programada', in_progress: 'En curso', finished: 'Finalizada', cancelled: 'Cancelada' };
var SC = { draft: 'default', scheduled: 'info', in_progress: 'warning', finished: 'success', cancelled: 'error' };

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

/** Alguien pidio que no se mueva nada, y eso vale mas que la animacion. */
function sinMovimiento() {
  return typeof window !== 'undefined'
    && typeof window.matchMedia === 'function'
    && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
}

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

  // Las entradas de las secciones fijas.
  //
  // Van dentro de un gsap.context que se revierte al desmontar, y eso no es
  // prolijidad: StrictMode monta, desmonta y vuelve a montar. Un tl.kill() a
  // secas deja los elementos en opacity 0, y el segundo montaje hace
  // from(opacity:0) sobre algo que ya vale 0 — o sea, de 0 a 0. Por eso el
  // hero y las tarjetas aparecian en blanco. revert() devuelve los estilos
  // originales antes de volver a empezar.
  useEffect(function() {
    if (sinMovimiento()) return undefined;

    var ctx = gsap.context(function() {
      gsap.timeline({ defaults: { ease: 'power3.out' } })
        .from('.hero-titulo', { y: 50, opacity: 0, duration: 0.9 })
        .from('.hero-sub', { y: 30, opacity: 0, duration: 0.7 }, '-=0.5')
        .from('.hero-botones', { y: 24, opacity: 0, duration: 0.6 }, '-=0.4')
        .from('.hero-fondo', { scale: 1.08, opacity: 0, duration: 1.2, ease: 'power2.out' }, 0);

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
      {/* bgcolor sacado del tema (background.paper con alfa) en vez de un
          blanco fijo — antes se quedaba blanco aunque se cambiara a modo
          oscuro, con texto claro encima de un fondo claro. */}
      <AppBar
        position="fixed"
        elevation={0}
        sx={{
          bgcolor: (theme) => alpha(theme.palette.background.paper, 0.72),
          backdropFilter: 'blur(16px) saturate(1.4)',
          color: 'text.primary',
          borderBottom: '1px solid',
          borderColor: 'divider',
          boxShadow: (theme) => theme.palette.mode === 'dark'
            ? '0 1px 0 rgba(255,255,255,0.06)'
            : '0 1px 12px rgba(15,40,20,0.06)',
          zIndex: 1200,
        }}
      >
        <Toolbar>
          <Box
            sx={{
              display: 'flex', alignItems: 'center', gap: 1.1, mr: 2, cursor: 'pointer',
              transition: 'opacity 0.15s ease', '&:hover': { opacity: 0.8 },
            }}
            onClick={function() { navigate('/'); }}
          >
            <Box sx={{ width: 36, height: 36, borderRadius: 1.5, background: 'linear-gradient(135deg, #1B8A2E 0%, #0d6b1e 100%)', display: 'flex', alignItems: 'center', justifyContent: 'center', boxShadow: '0 2px 8px rgba(27,138,46,0.35)' }}>
              <Typography sx={{ color: 'white', fontWeight: 800, fontSize: 16 }}>SF</Typography>
            </Box>
            <Typography variant="h6" fontWeight={700} letterSpacing={-0.2}>SportFrog</Typography>
          </Box>
          <Box sx={{ flexGrow: 1 }} />
          <ColorModeToggle sx={{ mr: 0.5 }} />
          <Button component={RouterLink} to="/public" sx={{ mr: 1, display: { xs: 'none', sm: 'flex' } }}>Competiciones</Button>
          <Button variant="contained" component={RouterLink} to="/auth/jwt/sign-in">Iniciar sesion</Button>
        </Toolbar>
      </AppBar>

      {/* -------------------------------------------------------------- hero */}
      <Box sx={{
        background: 'linear-gradient(135deg, #1B8A2E 0%, #0d6b1e 45%, #084a14 100%)',
        color: 'white', pt: { xs: 13, md: 17 }, pb: { xs: 12, md: 20 }, px: 3, position: 'relative', overflow: 'hidden',
      }}>
        {/* El degrade de arriba es el fondo de respaldo: se ve mientras no
            haya fotos en web/public/hero/, y sigue ahi por si alguna
            llegara a fallar. Las fotos, cuando existen, se pintan encima. */}
        <HeroCarrusel className="hero-fondo" />

        {/* Oscurecido en verde, mas fuerte del lado del texto y casi
            transparente hacia la derecha — las fotos tienen que verse, no
            desaparecer bajo el color: solo el rincon donde esta el texto
            necesita suficiente contraste para leerse. */}
        <Box sx={{
          position: 'absolute', inset: 0,
          background: 'linear-gradient(100deg, rgba(6,54,16,0.9) 0%, rgba(8,74,20,0.68) 35%, rgba(13,107,30,0.32) 62%, rgba(13,107,30,0.12) 100%)',
        }} />
        {/* Y un poco de piso oscuro abajo de todo — ahi es donde se apoyan
            las tarjetas de cifras, superpuestas con margen negativo. */}
        <Box sx={{
          position: 'absolute', insetInline: 0, bottom: 0, height: '35%',
          background: 'linear-gradient(to top, rgba(8,30,14,0.55), transparent)',
        }} />

        <Container maxWidth="lg" sx={{ position: 'relative' }}>
          <Box sx={{ maxWidth: 640 }}>
            <Box className="hero-titulo">
              <Box sx={{ display: 'inline-flex', alignItems: 'center', gap: 1, bgcolor: 'rgba(255,255,255,0.15)', borderRadius: 10, px: 2, py: 0.6, mb: 3 }}>
                <Box sx={{ width: 8, height: 8, borderRadius: '50%', bgcolor: '#7CFC00' }} />
                <Typography variant="caption" fontWeight={600}>Plataforma deportiva hecha en Bolivia</Typography>
              </Box>
              <Typography variant="h1" fontWeight={900} sx={{ fontSize: { xs: '2.1rem', sm: '2.8rem', md: '3.4rem' }, lineHeight: 1.12, mb: 2.5 }}>
                Tu liga entera, de la inscripcion{' '}
                <Box component="span" sx={{ background: 'linear-gradient(90deg, #7CFC00, #baff5c)', WebkitBackgroundClip: 'text', backgroundClip: 'text', color: 'transparent' }}>
                  al campeon
                </Box>
              </Typography>
            </Box>

            <Typography className="hero-sub" variant="h5" sx={{ opacity: 0.92, fontWeight: 300, mb: 4, fontSize: { xs: '1rem', md: '1.15rem' }, lineHeight: 1.65 }}>
              Inscribi jugadores, sortea el fixture, carga resultados y emiti credenciales.
              Todo se publica solo en una pagina que cualquiera puede abrir.
            </Typography>

            <Box className="hero-botones" sx={{ display: 'flex', gap: 2, flexWrap: 'wrap' }}>
              <Button variant="contained" size="large" component={RouterLink} to="/auth/jwt/sign-in"
                startIcon={<Iconify icon="eva:log-in-outline" />}
                sx={{ bgcolor: 'white', color: 'primary.main', px: 3.5, py: 1.4, borderRadius: 2.5, fontWeight: 700, boxShadow: '0 4px 20px rgba(0,0,0,0.18)', '&:hover': { bgcolor: 'grey.100' } }}>
                Iniciar sesion
              </Button>
              <Button variant="outlined" size="large" component={RouterLink} to="/public"
                startIcon={<Iconify icon="eva:eye-outline" />}
                sx={{ borderColor: 'rgba(255,255,255,0.5)', color: 'white', px: 3.5, py: 1.4, borderRadius: 2.5, fontWeight: 600, '&:hover': { borderColor: 'white', bgcolor: 'rgba(255,255,255,0.1)' } }}>
                Ver competencias
              </Button>
            </Box>
          </Box>
        </Container>
      </Box>

      {/* ------------------------------------------------------------ cifras */}
      {cifras.length > 0 && (
        <Container maxWidth="lg" sx={{ position: 'relative', mt: { xs: -7, md: -9 }, mb: { xs: 2, md: 4 } }}>
          <Grid container spacing={{ xs: 2, md: 3 }}>
            {cifras.map(function(c) {
              return (
                <Grid key={c.etiqueta} size={{ xs: 6, md: 3 }}>
                  <Card sx={{ textAlign: 'center', py: { xs: 2, md: 2.5 }, borderRadius: 3, boxShadow: '0 6px 28px rgba(0,0,0,0.10)', border: '1px solid', borderColor: 'divider', height: '100%' }}>
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
              <Typography variant="overline" color="primary" fontWeight={700} sx={{ letterSpacing: 2 }}>AHORA MISMO</Typography>
              <Typography variant="h3" fontWeight={800} sx={{ mt: 0.5, fontSize: { xs: '1.5rem', md: '2rem' } }}>
                Competencias corriendo en SportFrog
              </Typography>
            </Box>
            <Button component={RouterLink} to="/public" endIcon={<Iconify icon="eva:arrow-forward-outline" width={18} />}>
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
                      borderRadius: 3, cursor: 'pointer', border: '1px solid', borderColor: 'divider',
                      transition: 'transform .2s, box-shadow .2s, border-color .2s',
                      '&:hover': { transform: 'translateY(-4px)', boxShadow: '0 12px 32px rgba(27,138,46,0.14)', borderColor: 'primary.main' }
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
          <Typography variant="overline" color="primary" fontWeight={700} sx={{ letterSpacing: 2 }}>FUNCIONALIDADES</Typography>
          <Typography variant="h3" fontWeight={800} sx={{ mt: 0.5, fontSize: { xs: '1.5rem', md: '2rem' } }}>Todo lo que necesitas para organizar deporte</Typography>
          <Typography variant="body1" color="text.secondary" sx={{ mt: 1.5, maxWidth: 620, mx: 'auto' }}>
            Sin planillas sueltas, sin grupos de WhatsApp para avisar la fecha.
          </Typography>
        </Box>
        <Grid container spacing={3} className="features-grid">
          {features.map(function(f) {
            return (
              <Grid key={f.title} size={{ xs: 12, sm: 6, md: 4 }}>
                <Card className="feature-card" sx={{ height: '100%', borderRadius: 3, border: '1px solid', borderColor: 'divider', transition: 'transform .25s, box-shadow .25s, border-color .25s', '&:hover': { transform: 'translateY(-5px)', boxShadow: '0 12px 36px rgba(27,138,46,0.12)', borderColor: 'primary.main' } }}>
                  <CardContent sx={{ p: 3 }}>
                    <Box sx={{ width: 48, height: 48, borderRadius: 2.5, background: 'linear-gradient(135deg, rgba(27,138,46,0.16), rgba(27,138,46,0.05))', display: 'flex', alignItems: 'center', justifyContent: 'center', mb: 2 }}>
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
      <Box sx={{ bgcolor: 'grey.900', color: 'white', py: { xs: 7, md: 10 } }}>
        <Container maxWidth="lg">
          <Box sx={{ textAlign: 'center', mb: 5 }}>
            <Typography variant="overline" sx={{ color: '#7CFC00', letterSpacing: 2, fontWeight: 700 }}>COMO FUNCIONA</Typography>
            <Typography variant="h3" fontWeight={800} sx={{ mt: 0.5, fontSize: { xs: '1.5rem', md: '2rem' } }}>En cuatro pasos</Typography>
          </Box>
          <Grid container spacing={4} className="steps-grid">
            {steps.map(function(s) {
              return (
                <Grid key={s.num} size={{ xs: 12, sm: 6, md: 3 }}>
                  <Box className="step-item">
                    <Typography variant="h1" fontWeight={900} sx={{ color: 'rgba(124,252,0,0.1)', fontSize: '3.6rem', lineHeight: 1 }}>{s.num}</Typography>
                    <Box sx={{ width: 32, height: 3, borderRadius: 2, bgcolor: '#7CFC00', mt: -1.5, mb: 1.5 }} />
                    <Typography variant="h6" fontWeight={700} sx={{ position: 'relative' }}>{s.title}</Typography>
                    <Typography variant="body2" sx={{ color: 'grey.400', mt: 1, lineHeight: 1.7 }}>{s.desc}</Typography>
                  </Box>
                </Grid>
              );
            })}
          </Grid>
        </Container>
      </Box>

      {/* --------------------------------------------------------------- cta */}
      <Container maxWidth="md" sx={{ py: { xs: 7, md: 10 } }}>
        <Card className="cta-card" sx={{ textAlign: 'center', borderRadius: 4, background: 'linear-gradient(135deg, #1B8A2E 0%, #0d6b1e 100%)', color: 'white', p: { xs: 4, md: 6 }, boxShadow: '0 20px 60px rgba(27,138,46,0.28)', position: 'relative', overflow: 'hidden' }}>
          <Box sx={{ position: 'absolute', top: -80, right: -80, width: 240, height: 240, borderRadius: '50%', background: 'radial-gradient(circle, rgba(124,252,0,0.18) 0%, rgba(124,252,0,0) 70%)' }} />
          <Typography variant="h3" fontWeight={800} sx={{ mb: 1.5, fontSize: { xs: '1.4rem', md: '1.9rem' }, position: 'relative' }}>Todo listo para tu proxima temporada</Typography>
          <Typography variant="body1" sx={{ opacity: 0.9, mb: 3.5, maxWidth: 520, mx: 'auto', position: 'relative' }}>
            Inicia sesion y arma tu competencia en una tarde: categorias, calendario y credenciales incluidos.
          </Typography>
          <Box sx={{ display: 'flex', gap: 2, justifyContent: 'center', flexWrap: 'wrap', position: 'relative' }}>
            <Button variant="contained" size="large" component={RouterLink} to="/auth/jwt/sign-in"
              startIcon={<Iconify icon="eva:log-in-outline" />}
              sx={{ bgcolor: 'white', color: 'primary.main', px: 4.5, py: 1.4, borderRadius: 2.5, fontWeight: 700, boxShadow: '0 4px 20px rgba(0,0,0,0.18)', '&:hover': { bgcolor: 'grey.100' } }}>
              Iniciar sesion
            </Button>
            <Button variant="text" size="large" component={RouterLink} to="/public"
              endIcon={<Iconify icon="eva:arrow-forward-outline" width={18} />}
              sx={{ color: 'white', px: 2, fontWeight: 600, '&:hover': { bgcolor: 'rgba(255,255,255,0.1)' } }}>
              Ver competencias en vivo
            </Button>
          </Box>
        </Card>
      </Container>

      {/* ------------------------------------------------------------ footer */}
      <Box sx={{ bgcolor: 'grey.900', color: 'grey.500', py: 4, px: 3 }}>
        <Container maxWidth="lg" sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 2 }}>
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
            <Box sx={{ width: 28, height: 28, borderRadius: 1, bgcolor: 'primary.main', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
              <Typography sx={{ color: 'white', fontWeight: 800, fontSize: 11 }}>SF</Typography>
            </Box>
            <Typography variant="body2" color="grey.400">2026 SportFrog. Plataforma de gestion deportiva.</Typography>
          </Box>
          <Box sx={{ display: 'flex', gap: 1 }}>
            <Button component={RouterLink} to="/public" sx={{ color: 'grey.500', fontSize: '0.8rem' }}>Competiciones</Button>
            <Button component={RouterLink} to="/auth/jwt/sign-in" sx={{ color: 'grey.500', fontSize: '0.8rem' }}>Iniciar sesion</Button>
          </Box>
        </Container>
      </Box>
    </Box>
  );
}

/**
 * Las fotos del hero: web/public/hero/1, 2 y 3 — sin importar la extension.
 * Lo que sea que pise esas rutas se sirve tal cual, porque viven en public/ y
 * Vite no las procesa, así que alcanza con dejar los archivos ahí, con
 * cualquiera de estos nombres, sin tocar código:
 *   web/public/hero/1.jpg  (o .jpeg, .png, .webp)
 *   web/public/hero/2.jpg
 *   web/public/hero/3.jpg
 */
var HERO_NUMEROS = [1, 2, 3];
var HERO_EXTENSIONES = ['jpg', 'jpeg', 'png', 'webp'];
var HERO_INTERVALO_MS = 5000;

/**
 * El fondo del hero: las tres fotos, una detras de otra con fundido cruzado,
 * llenando toda la seccion. El texto y el oscurecido en verde van encima,
 * fuera de este componente — este solo pinta las fotos.
 */
function HeroCarrusel(props) {
  var [indice, setIndice] = useState(0);

  useEffect(function() {
    if (sinMovimiento()) return undefined;

    var id = setInterval(function() {
      setIndice(function(i) { return (i + 1) % HERO_NUMEROS.length; });
    }, HERO_INTERVALO_MS);

    return function() { clearInterval(id); };
  }, []);

  return (
    <Box className={props.className} sx={{ position: 'absolute', inset: 0, overflow: 'hidden' }}>
      {HERO_NUMEROS.map(function(numero, i) {
        return <HeroFoto key={numero} numero={numero} activa={i === indice} />;
      })}
    </Box>
  );
}

/**
 * Una foto del hero, probando cada extension de HERO_EXTENSIONES en orden
 * hasta que una cargue. No queda ninguna: no se dibuja nada, y el degrade
 * de respaldo del hero sigue ahi debajo — un formato que nadie subio nunca
 * no debe verse como una foto rota.
 */
function HeroFoto(props) {
  var [intento, setIntento] = useState(0);
  var agotado = intento >= HERO_EXTENSIONES.length;

  if (agotado) return null;

  return (
    <Box
      component="img"
      src={'/hero/' + props.numero + '.' + HERO_EXTENSIONES[intento]}
      alt=""
      onError={function() { setIntento(function(n) { return n + 1; }); }}
      sx={{
        position: 'absolute', inset: 0, width: '100%', height: '100%', objectFit: 'cover',
        opacity: props.activa ? 1 : 0,
        transition: sinMovimiento() ? 'none' : 'opacity 1.2s ease',
      }}
    />
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
      <Typography ref={textoRef} variant="h2" fontWeight={800} color="primary" sx={{ fontSize: { xs: '1.9rem', md: '2.6rem' }, lineHeight: 1.1 }}>
        {valor}
      </Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5, fontSize: { xs: '0.78rem', md: '0.875rem' } }}>
        {props.etiqueta}
      </Typography>
    </Box>
  );
}

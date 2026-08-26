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
import { Link as RouterLink, useNavigate } from 'react-router';
import useSWR from 'swr';
import publicAxios from 'src/lib/public-axios';
import { Iconify } from 'src/components/iconify';
import gsap from 'gsap';
import { ScrollTrigger } from 'gsap/ScrollTrigger';

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
  { icon: 'mdi:globe-outline', title: 'Portal publico', desc: 'Tus resultados en una direccion que cualquiera puede abrir y compartir.' }
];

var steps = [
  { num: '01', title: 'Crea tu organizacion', desc: 'Registra tu liga o federacion en segundos.' },
  { num: '02', title: 'Configura la competencia', desc: 'Define categorias, reglamento y equipos participantes.' },
  { num: '03', title: 'Sortea el calendario', desc: 'El sistema arma los cruces y los reparte entre sedes y horarios.' },
  { num: '04', title: 'Publica resultados', desc: 'El portal publico los muestra apenas los cargas.' }
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
  var destacada = competencias.find(function(c) { return c.status === 'in_progress'; }) || competencias[0];

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
        .from('.hero-visual', { scale: 0.9, opacity: 0, duration: 0.9, ease: 'back.out(1.4)' }, '-=0.5');

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
    <Box ref={raizRef} sx={{ minHeight: '100vh', bgcolor: 'grey.50' }}>
      <AppBar position="fixed" elevation={0} sx={{ bgcolor: 'rgba(255,255,255,0.85)', backdropFilter: 'blur(12px)', color: 'text.primary', borderBottom: '1px solid', borderColor: 'divider', zIndex: 1200 }}>
        <Toolbar>
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mr: 2, cursor: 'pointer' }} onClick={function() { navigate('/'); }}>
            <Box sx={{ width: 36, height: 36, borderRadius: 1.5, bgcolor: 'primary.main', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
              <Typography sx={{ color: 'white', fontWeight: 800, fontSize: 16 }}>SF</Typography>
            </Box>
            <Typography variant="h6" fontWeight={700}>SportFrog</Typography>
          </Box>
          <Box sx={{ flexGrow: 1 }} />
          <Button component={RouterLink} to="/public" sx={{ mr: 1, display: { xs: 'none', sm: 'flex' } }}>Competiciones</Button>
          <Button variant="outlined" component={RouterLink} to="/auth/jwt/sign-in" sx={{ mr: 1 }}>Iniciar sesion</Button>
          <Button variant="contained" component={RouterLink} to="/auth/jwt/sign-up" sx={{ display: { xs: 'none', sm: 'flex' } }}>Crear organizacion</Button>
        </Toolbar>
      </AppBar>

      {/* -------------------------------------------------------------- hero */}
      <Box sx={{ background: 'linear-gradient(135deg, #1B8A2E 0%, #0d6b1e 45%, #084a14 100%)', color: 'white', pt: { xs: 13, md: 17 }, pb: { xs: 12, md: 20 }, px: 3, position: 'relative', overflow: 'hidden' }}>
        <Box sx={{ position: 'absolute', top: -140, right: -100, width: 420, height: 420, borderRadius: '50%', background: 'rgba(255,255,255,0.05)' }} />
        <Box sx={{ position: 'absolute', bottom: -100, left: -100, width: 320, height: 320, borderRadius: '50%', background: 'rgba(255,255,255,0.035)' }} />

        <Container maxWidth="lg" sx={{ position: 'relative' }}>
          <Grid container spacing={{ xs: 5, md: 6 }} alignItems="center">
            <Grid size={{ xs: 12, md: 6 }}>
              <Box className="hero-titulo">
                <Box sx={{ display: 'inline-flex', alignItems: 'center', gap: 1, bgcolor: 'rgba(255,255,255,0.15)', borderRadius: 10, px: 2, py: 0.6, mb: 3 }}>
                  <Box sx={{ width: 8, height: 8, borderRadius: '50%', bgcolor: '#7CFC00' }} />
                  <Typography variant="caption" fontWeight={600}>Plataforma deportiva hecha en Bolivia</Typography>
                </Box>
                <Typography variant="h1" fontWeight={900} sx={{ fontSize: { xs: '2.1rem', sm: '2.8rem', md: '3.4rem' }, lineHeight: 1.12, mb: 2.5 }}>
                  Tu liga entera, de la inscripcion al campeon
                </Typography>
              </Box>

              <Typography className="hero-sub" variant="h5" sx={{ opacity: 0.92, fontWeight: 300, mb: 4, fontSize: { xs: '1rem', md: '1.15rem' }, lineHeight: 1.65, maxWidth: 520 }}>
                Inscribi jugadores, sortea el fixture, carga resultados y emiti credenciales.
                Todo se publica solo en una pagina que cualquiera puede abrir.
              </Typography>

              <Box className="hero-botones" sx={{ display: 'flex', gap: 2, flexWrap: 'wrap' }}>
                <Button variant="contained" size="large" component={RouterLink} to="/auth/jwt/sign-up"
                  startIcon={<Iconify icon="eva:rocket-outline" />}
                  sx={{ bgcolor: 'white', color: 'primary.main', px: 3.5, py: 1.4, borderRadius: 2.5, fontWeight: 700, boxShadow: '0 4px 20px rgba(0,0,0,0.18)', '&:hover': { bgcolor: 'grey.100' } }}>
                  Crear mi organizacion
                </Button>
                <Button variant="outlined" size="large" component={RouterLink} to="/public"
                  startIcon={<Iconify icon="eva:eye-outline" />}
                  sx={{ borderColor: 'rgba(255,255,255,0.5)', color: 'white', px: 3.5, py: 1.4, borderRadius: 2.5, fontWeight: 600, '&:hover': { borderColor: 'white', bgcolor: 'rgba(255,255,255,0.1)' } }}>
                  Ver competencias
                </Button>
              </Box>
            </Grid>

            <Grid size={{ xs: 12, md: 6 }} sx={{ display: { xs: 'none', md: 'block' } }}>
              <Box className="hero-visual" sx={{ display: 'flex', justifyContent: 'center' }}>
                <VistaPrevia competencia={destacada} />
              </Box>
            </Grid>
          </Grid>
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
                      height: '100%', borderRadius: 3, cursor: 'pointer', border: '1px solid', borderColor: 'divider',
                      transition: 'transform .2s, box-shadow .2s, border-color .2s',
                      '&:hover': { transform: 'translateY(-4px)', boxShadow: '0 12px 32px rgba(27,138,46,0.14)', borderColor: 'primary.main' }
                    }}
                  >
                    <CardContent sx={{ p: 2.5 }}>
                      <Box sx={{ display: 'flex', gap: 1, mb: 1.5, flexWrap: 'wrap' }}>
                        <Chip label={SL[c.status] || c.status} color={SC[c.status] || 'default'} size="small" />
                        <Chip label={c.sportName} size="small" variant="outlined" />
                      </Box>
                      <Typography variant="h6" fontWeight={700} sx={{ lineHeight: 1.3, mb: 0.5 }}>{c.competitionName}</Typography>
                      <Typography variant="body2" color="text.secondary" noWrap>{c.organizationName}</Typography>
                      <Box sx={{ display: 'flex', gap: 2, mt: 2, color: 'text.secondary' }}>
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
                    <Box sx={{ width: 48, height: 48, borderRadius: 2.5, bgcolor: 'primary.lighter', display: 'flex', alignItems: 'center', justifyContent: 'center', mb: 2 }}>
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
                    <Typography variant="h1" fontWeight={900} sx={{ color: 'rgba(255,255,255,0.07)', fontSize: '3.6rem', lineHeight: 1 }}>{s.num}</Typography>
                    <Typography variant="h6" fontWeight={700} sx={{ mt: -1.5, position: 'relative' }}>{s.title}</Typography>
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
        <Card className="cta-card" sx={{ textAlign: 'center', borderRadius: 4, background: 'linear-gradient(135deg, #1B8A2E 0%, #0d6b1e 100%)', color: 'white', p: { xs: 4, md: 6 }, boxShadow: '0 20px 60px rgba(27,138,46,0.28)' }}>
          <Typography variant="h3" fontWeight={800} sx={{ mb: 1.5, fontSize: { xs: '1.4rem', md: '1.9rem' } }}>Empeza a organizar tu liga hoy</Typography>
          <Typography variant="body1" sx={{ opacity: 0.9, mb: 3.5, maxWidth: 520, mx: 'auto' }}>
            Crea tu organizacion y carga tu primera competencia en una tarde.
          </Typography>
          <Button variant="contained" size="large" component={RouterLink} to="/auth/jwt/sign-up"
            startIcon={<Iconify icon="eva:rocket-outline" />}
            sx={{ bgcolor: 'white', color: 'primary.main', px: 4.5, py: 1.4, borderRadius: 2.5, fontWeight: 700, boxShadow: '0 4px 20px rgba(0,0,0,0.18)', '&:hover': { bgcolor: 'grey.100' } }}>
            Crear mi organizacion
          </Button>
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
 * La maqueta del hero, armada con una competencia de verdad cuando la hay.
 *
 * Antes eran datos inventados escritos a mano. Mostrar una que existe es mas
 * honesto y ademas se ve mejor: dice que la plataforma esta en uso.
 */
function VistaPrevia(props) {
  var c = props.competencia;

  return (
    <Box sx={{ width: 340, borderRadius: 4, bgcolor: 'rgba(255,255,255,0.12)', backdropFilter: 'blur(10px)', border: '1px solid rgba(255,255,255,0.2)', p: 3, boxShadow: '0 24px 60px rgba(0,0,0,0.25)' }}>
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 2 }}>
        <Box sx={{ width: 8, height: 8, borderRadius: '50%', bgcolor: '#7CFC00' }} />
        <Typography variant="caption" sx={{ opacity: 0.85, letterSpacing: 1 }}>PORTAL PUBLICO</Typography>
      </Box>

      <Typography variant="h6" fontWeight={800} sx={{ lineHeight: 1.25 }}>
        {c ? c.competitionName : 'Copa Apertura 2026'}
      </Typography>
      <Typography variant="caption" sx={{ opacity: 0.75 }}>
        {c ? c.organizationName + ' · ' + c.sportName : 'Tu liga · Futbol'}
      </Typography>

      <Box sx={{ mt: 2.5, pt: 2, borderTop: '1px solid rgba(255,255,255,0.15)' }}>
        <Typography variant="caption" sx={{ opacity: 0.7 }}>Tabla de posiciones</Typography>
        {[
          { n: 'Illimani', p: 15 },
          { n: 'Miraflores', p: 12 },
          { n: 'Sopocachi', p: 9 }
        ].map(function(t, i) {
          return (
            <Box key={t.n} sx={{ display: 'flex', justifyContent: 'space-between', py: 0.9, borderBottom: i < 2 ? '1px solid rgba(255,255,255,0.08)' : 'none' }}>
              <Typography variant="body2" fontWeight={600}>{i + 1}. {t.n}</Typography>
              <Typography variant="body2" fontWeight={700}>{t.p} pts</Typography>
            </Box>
          );
        })}
      </Box>

      <Box sx={{ display: 'flex', gap: 2, mt: 2.5 }}>
        <Box sx={{ flex: 1, bgcolor: 'rgba(255,255,255,0.1)', borderRadius: 2, p: 1.5, textAlign: 'center' }}>
          <Typography variant="h6" fontWeight={800}>{c ? c.teams : 16}</Typography>
          <Typography variant="caption" sx={{ opacity: 0.7 }}>equipos</Typography>
        </Box>
        <Box sx={{ flex: 1, bgcolor: 'rgba(255,255,255,0.1)', borderRadius: 2, p: 1.5, textAlign: 'center' }}>
          <Typography variant="h6" fontWeight={800}>{c ? c.categories : 3}</Typography>
          <Typography variant="caption" sx={{ opacity: 0.7 }}>categorias</Typography>
        </Box>
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
      <Typography ref={textoRef} variant="h2" fontWeight={800} color="primary" sx={{ fontSize: { xs: '1.9rem', md: '2.6rem' }, lineHeight: 1.1 }}>
        {valor}
      </Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5, fontSize: { xs: '0.78rem', md: '0.875rem' } }}>
        {props.etiqueta}
      </Typography>
    </Box>
  );
}

import { useEffect, useRef, useState } from 'react';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import { Link as RouterLink } from 'react-router';
import useSWR from 'swr';
import gsap from 'gsap';
import { alpha, useTheme } from '@mui/material/styles';
import { Iconify } from 'src/components/iconify';
import { LivePulse } from 'src/components/live-pulse';
import { publicFetcher } from 'src/lib/public-axios';
import { sinMovimiento } from 'src/lib/motion';
import { Marquesina } from './floating-results';

/**
 * El hero de la portada, a pantalla completa: mascota general + una por
 * deporte (web/public/hero/*.svg), wordmark en letras burbuja, resultados
 * reales flotando y un poco de parallax por mouse. Aparte de
 * landing-page.jsx a proposito -- es la pieza mas grande y con mas estado
 * propio (animacion de entrada, loop de flote, parallax, hover de mascotas),
 * mismo criterio que ya separa matches-page.jsx de matches/events-dialog.jsx.
 *
 * La fuente del wordmark (Fredoka) se pide como <link> a Google Fonts en vez
 * de instalar @fontsource -- mismo patron que portalFontHref ya usa para las
 * tipografias del portal: React 19 iza el <link> al <head> y lo deduplica
 * solo, sin build step nuevo.
 */
// Exportados: el AppBar de landing-page.jsx los reusa para que el wordmark
// de la navbar quede en la misma familia tipografica que el del hero, en vez
// de pedir la fuente dos veces con dos constantes iguales.
export var FREDOKA_HREF = 'https://fonts.googleapis.com/css2?family=Fredoka:wght@600;700&display=swap';
export var FREDOKA_STACK = '"Fredoka", "Inter", sans-serif';

var PALABRA = 'SPORTFROG';

// Las dos O de SPORTFROG (indices 2 y 7) se reemplazan por una pelota que
// gira -- sustitucion tipografica de toda la vida en marca deportiva, y
// barata: ya se arma letra por letra.
var INDICES_PELOTA = {
  2: { tipo: 'futbol', icon: 'noto:soccer-ball' },
  7: { tipo: 'basquet', icon: 'noto:basketball' },
};

// Cada mascota de deporte, con su posicion (en % del hero), su "profundidad"
// -- que tan lejos se mueve con el parallax del mouse -- y la frase de su
// globo al pasar el mouse. Las mas grandes/cerca del centro se mueven mas;
// queda mas creible que si todas se movieran igual.
var MASCOTAS = [
  { id: 'football', src: '/hero/football.svg', top: '5%', left: '9%', size: { xs: 190, md: 340 }, rotate: -19, profundidad: 26, duracion: 3.4, retraso: 0.05, frase: '¡Gol!' },
  { id: 'basquetball', src: '/hero/basquetball.svg', top: '10%', left: '70%', size: { xs: 160, md: 285 }, rotate: 24, profundidad: 34, duracion: 3.1, retraso: 0.18, frase: '¡Canasta!' },
  { id: 'volleyball', src: '/hero/volleyball.svg', top: '58%', left: '14%', size: { xs: 175, md: 310 }, rotate: 14, profundidad: 20, duracion: 3.7, retraso: 0.3, frase: '¡Punto!' },
  { id: 'taekwondo', src: '/hero/taekwondo.svg', top: '52%', left: '66%', size: { xs: 180, md: 320 }, rotate: -26, profundidad: 30, duracion: 3.3, retraso: 0.42, frase: '¡Kyorugi!' },
];

/**
 * El "iman" de un boton: se corre un poco hacia el cursor mientras esta
 * cerca, y vuelve elastico al soltarlo. No es un hook -- son handlers de
 * puntero comunes, armados una vez por boton con el ref que ya existe, sin
 * el problema de llamar hooks condicionalmente adentro de un mapeo.
 */
function imantar(ref) {
  return {
    onMouseMove: function(e) {
      if (!ref.current || sinMovimiento()) return;
      var rect = ref.current.getBoundingClientRect();
      var relX = e.clientX - (rect.left + rect.width / 2);
      var relY = e.clientY - (rect.top + rect.height / 2);
      gsap.to(ref.current, { x: relX * 0.25, y: relY * 0.35, duration: 0.3, ease: 'power2.out' });
    },
    onMouseLeave: function() {
      if (!ref.current) return;
      gsap.to(ref.current, { x: 0, y: 0, duration: 0.5, ease: 'elastic.out(1, 0.4)' });
    },
  };
}

export function PlayfulHero() {
  var raizRef = useRef(null);
  var mascotaRefs = useRef({});
  var boton1Ref = useRef(null);
  var boton2Ref = useRef(null);
  var [hover, setHover] = useState(null);

  // El directorio publico es anonimo: la portada puede mostrar resultados
  // reales sin que nadie haya iniciado sesion. PRODUCT.md es explicito acá
  // ("Honest numbers only") -- si todavia no hay nada jugado, resultados
  // llega vacio y Marquesina simplemente no dibuja nada inventado.
  var { data: datosResultados } = useSWR('/api/public/recent-results?take=8', publicFetcher, {
    revalidateOnFocus: false,
  });
  var resultados = (datosResultados && datosResultados.results) || [];
  var enVivo = resultados.find(function(r) { return r.status === 'in_progress'; });

  useEffect(function() {
    if (sinMovimiento()) return undefined;

    // Fuera del gsap.context: un listener de window no es algo que
    // ctx.revert() sepa deshacer -- revert() vuelve estilos, no desregistra
    // eventos. Se junta con ctx.revert() en el cleanup de abajo.
    var quitarParallax;

    var ctx = gsap.context(function() {
      // ---- Entrada ------------------------------------------------------
      // El flote continuo y el parallax se registran recien en onComplete:
      // ambos tocan y/rotate de las mismas mascotas que esta linea de tiempo
      // anima, y dos tweens de GSAP peleando por la misma propiedad al mismo
      // tiempo es la entrada saliendo a tirones, no una entrada mas rica.
      var entrada = gsap.timeline({ defaults: { ease: 'back.out(1.6)' }, onComplete: iniciarLoops });

      entrada
        .from('.hero-insignia', { scale: 0, opacity: 0, duration: 0.6, ease: 'back.out(2)' })
        .from('.hero-eyebrow', { y: 20, opacity: 0, duration: 0.5, ease: 'power2.out' }, '-=0.25')
        .from('.hero-letra', {
          y: 70, opacity: 0, scale: 0.4, rotate: function() { return gsap.utils.random(-18, 18); },
          duration: 0.7, stagger: 0.045,
        }, '-=0.1')
        .from('.hero-mascota-central', { y: 90, opacity: 0, scale: 0.7, duration: 0.8, ease: 'back.out(1.4)' }, '-=0.5')
        .from('.hero-mascota-lateral', {
          scale: 0, opacity: 0, duration: 0.7, stagger: 0.12,
          rotate: function(i, target) { return Number(target.dataset.rotate) * 2; },
          ease: 'back.out(1.8)',
        }, '-=0.6')
        .from('.hero-cta > *', { y: 20, opacity: 0, duration: 0.5, stagger: 0.08, ease: 'power2.out' }, '-=0.35')
        .from('.hero-scroll', { opacity: 0, duration: 0.5 }, '-=0.2')
        .from('.hero-volador', { opacity: 0, duration: 1 }, '-=1.4');

      // ---- Insignia: giro continuo ---------------------------------------
      // No toca y/rotate de ninguna mascota, asi que puede arrancar ya
      // mismo sin pelearse con la entrada.
      gsap.to('.hero-insignia', { rotate: 360, duration: 14, repeat: -1, ease: 'none' });

      // ---- Pelotas del wordmark: giro continuo ---------------------------
      // Viven en un span propio adentro de .hero-letra (que solo anima
      // scale/opacity/rotate de ENTRADA sobre el span contenedor): el giro
      // infinito de la pelota es un elemento distinto, nunca pelea por la
      // misma propiedad.
      gsap.to('.hero-pelota', { rotate: 360, duration: 9, repeat: -1, ease: 'none' });

      // ---- Voladores: cruzan el hero en zigzag y girando ------------------
      // Ya no son nubes derivando parejo: cada uno sigue las 3 paradas de
      // top/left que se sortearon al montar (ver Voladores), con
      // "keyframes" en vez de un solo left/top -- GSAP interpola de parada
      // en parada con el mismo ease entre todas (easeEach), asi que el
      // camino zigzaguea en vez de ser la linea recta que tenia la nube.
      // Sin yoyo: cada vuelta arranca de nuevo desde el punto de partida
      // (fuera o al borde de la pantalla) y el salto de vuelta pasa siempre
      // con el icono fuera del área visible (el hero tiene overflow:hidden),
      // asi que se sigue viendo como un flujo infinito entrando y saliendo.
      // El giro continuo (independiente, en su propia duracion y sentido) es
      // lo que mas se nota como "ya no es una nube": una pelota o un icono
      // de deporte da vueltas mientras vuela, una nube no.
      gsap.utils.toArray('.hero-volador').forEach(function(volador) {
        gsap.to(volador, {
          keyframes: {
            top: volador.dataset.tops.split(','),
            left: volador.dataset.lefts.split(','),
            easeEach: 'sine.inOut',
          },
          duration: Number(volador.dataset.duracion) || 40,
          delay: Number(volador.dataset.retraso) || 0,
          repeat: -1,
        });
        gsap.to(volador, {
          rotate: volador.dataset.espin,
          duration: Number(volador.dataset.duracionEspin) || 8,
          repeat: -1,
          ease: 'none',
        });
      });

      function iniciarLoops() {
        // ---- Flote continuo: cada mascota sube y baja a su propio ritmo -
        MASCOTAS.forEach(function(m) {
          gsap.to('.hero-mascota-' + m.id, {
            y: '+=16', rotate: '+=3', duration: m.duracion, repeat: -1, yoyo: true,
            ease: 'sine.inOut', delay: m.retraso,
          });
        });
        gsap.to('.hero-mascota-central', {
          y: '+=10', duration: 3.8, repeat: -1, yoyo: true, ease: 'sine.inOut',
        });

        // ---- Parallax por mouse: solo en pantallas con hover real, y solo
        // en x -- el flote continuo ya es dueño de y, y sumarle otro tween
        // peleando por esa misma propiedad es lo que se evito arriba.
        if (window.matchMedia('(hover: hover)').matches) {
          var mueveX = {};

          MASCOTAS.forEach(function(m) {
            var el = mascotaRefs.current[m.id];
            if (!el) return;
            mueveX[m.id] = gsap.quickTo(el, 'x', { duration: 0.6, ease: 'power3.out' });
          });

          var alMover = function(e) {
            var offsetX = (e.clientX / window.innerWidth - 0.5) * 2;

            MASCOTAS.forEach(function(m) {
              if (mueveX[m.id]) mueveX[m.id](offsetX * m.profundidad);
            });
          };

          window.addEventListener('pointermove', alMover);
          quitarParallax = function() { window.removeEventListener('pointermove', alMover); };
        }
      }
    }, raizRef);

    return function() {
      ctx.revert();
      if (quitarParallax) quitarParallax();
    };
  }, []);

  /**
   * El festejo al pasar el mouse: un squash-and-stretch rapido (se achata,
   * rebota, se asienta elastico) y el globo de dialogo de la mascota. No
   * pelea con el flote continuo ni el parallax -- esos tocan x/y/rotate,
   * esto solo toca scaleX/scaleY, asi que GSAP los compone sin que ninguno
   * se pise.
   */
  function festejar(id) {
    if (sinMovimiento()) { setHover(id); return; }

    setHover(id);
    var el = mascotaRefs.current[id];
    if (!el) return;

    gsap.timeline()
      .to(el, { scaleX: 1.25, scaleY: 0.78, duration: 0.14, ease: 'power2.out' })
      .to(el, { scaleX: 0.88, scaleY: 1.16, duration: 0.16, ease: 'power2.out' })
      .to(el, { scaleX: 1, scaleY: 1, duration: 0.35, ease: 'elastic.out(1, 0.5)' });
  }

  return (
    <Box
      component="section"
      ref={raizRef}
      sx={{
        position: 'relative', minHeight: '100svh', overflow: 'hidden',
        display: 'flex', flexDirection: 'column',
        bgcolor: 'brand.tint',
        backgroundImage: function(theme) {
          return 'radial-gradient(' + alpha(theme.palette.primary.main, 0.06) + ' 1.4px, transparent 1.4px)';
        },
        backgroundSize: '18px 18px',
      }}
    >
      {/* React 19 iza este <link> al <head> y lo deduplica -- si esta pagina
          nunca se monta, nunca se pide. */}
      <link rel="stylesheet" href={FREDOKA_HREF} precedence="hero-font" />

      <Voladores />
      <Insignia />

      {MASCOTAS.map(function(m) {
        return (
          <Box
            key={m.id}
            sx={{
              position: 'absolute', top: m.top, left: m.left,
              width: m.size, height: m.size,
              display: { xs: 'none', sm: 'block' }, zIndex: 2,
            }}
            onMouseEnter={function() { festejar(m.id); }}
            onMouseLeave={function() { setHover(null); }}
          >
            {hover === m.id && (
              <Box
                sx={{
                  position: 'absolute', top: -34, left: '50%', transform: 'translateX(-50%)',
                  bgcolor: 'background.paper', border: 2, borderColor: 'primary.main', borderRadius: 2,
                  px: 1, py: 0.3, whiteSpace: 'nowrap', fontWeight: 800, fontSize: '0.75rem',
                  zIndex: 5, pointerEvents: 'none',
                }}
              >
                {m.frase}
              </Box>
            )}
            <Box
              ref={function(el) { mascotaRefs.current[m.id] = el; }}
              data-rotate={m.rotate}
              className={'hero-mascota-lateral hero-mascota-' + m.id}
              component="img"
              src={m.src}
              alt=""
              sx={{
                width: '100%', height: '100%',
                transform: 'rotate(' + m.rotate + 'deg)',
                filter: function(theme) { return 'drop-shadow(0 14px 20px ' + alpha(theme.palette.primary.main, 0.16) + ')'; },
                userSelect: 'none', cursor: 'pointer',
              }}
            />
          </Box>
        );
      })}

      <Box
        sx={{
          position: 'relative', zIndex: 3, flexGrow: 1,
          display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center',
          textAlign: 'center', px: 3, pt: { xs: 13, md: 15 }, pb: { xs: 6, md: 8 },
        }}
      >
        {enVivo ? (
          <Box
            className="hero-eyebrow"
            component={RouterLink}
            to={'/public/' + enVivo.organizationSlug + '/' + enVivo.competitionSlug}
            sx={{
              display: 'inline-flex', alignItems: 'center', gap: 1, bgcolor: 'error.main', color: 'error.contrastText',
              borderRadius: 10, px: 2, py: 0.7, mb: { xs: 2, md: 3 }, textDecoration: 'none',
            }}
          >
            <LivePulse label={'EN VIVO · ' + enVivo.homeTeamName + ' vs ' + enVivo.awayTeamName} />
          </Box>
        ) : (
          <Box
            className="hero-eyebrow"
            sx={{
              display: 'inline-flex', alignItems: 'center', gap: 1,
              bgcolor: function(theme) { return alpha(theme.palette.brand.bright, 0.16); },
              borderRadius: 10, px: 2, py: 0.7, mb: { xs: 2, md: 3 },
            }}
          >
            <Box sx={{ width: 8, height: 8, borderRadius: '50%', bgcolor: 'primary.main' }} />
            <Typography variant="caption" fontWeight={700} sx={{ color: 'primary.main' }}>
              Plataforma deportiva hecha en Bolivia
            </Typography>
          </Box>
        )}

        <Box sx={{ display: 'flex', justifyContent: 'center', flexWrap: 'wrap', lineHeight: 0.85 }}>
          {PALABRA.split('').map(function(letra, i) {
            var pelota = INDICES_PELOTA[i];

            if (pelota) {
              return <LetraPelota key={i} tipo={pelota.tipo} icon={pelota.icon} />;
            }

            return (
              <Typography
                key={i}
                component="span"
                className="hero-letra"
                sx={{
                  fontFamily: FREDOKA_STACK, fontWeight: 700, color: 'text.primary',
                  fontSize: { xs: '3.1rem', sm: '4.6rem', md: '6.4rem', lg: '7.6rem' },
                  lineHeight: 0.9, display: 'inline-block',
                }}
              >
                {letra}
              </Typography>
            );
          })}
        </Box>

        <Box sx={{ position: 'relative', mt: { xs: -2, md: -4 }, mb: { xs: 1, md: 2 } }}>
          <Box
            className="hero-mascota-central"
            component="img"
            src="/hero/sportfrog.svg"
            alt="SportFrog"
            sx={{
              width: { xs: 260, sm: 350, md: 480 }, height: { xs: 260, sm: 350, md: 480 },
              filter: function(theme) { return 'drop-shadow(0 18px 22px ' + alpha(theme.palette.primary.main, 0.22) + ')'; },
              pointerEvents: 'none', userSelect: 'none',
            }}
          />
          <Box
            sx={{
              width: { xs: 90, md: 130 }, height: { xs: 14, md: 20 }, mx: 'auto', mt: -1,
              borderRadius: '50%', bgcolor: function(theme) { return alpha(theme.palette.brand.bright, 0.20); }, filter: 'blur(4px)',
            }}
          />
        </Box>

        <Box className="hero-cta" sx={{ display: 'flex', gap: 2, flexWrap: 'wrap', justifyContent: 'center', mt: { xs: 1, md: 2 } }}>
          <Button
            ref={boton1Ref}
            {...imantar(boton1Ref)}
            variant="contained" size="large" component={RouterLink} to="/auth/jwt/sign-in"
            startIcon={<Iconify icon="eva:log-in-outline" />}
            sx={{
              bgcolor: 'primary.main', color: 'primary.contrastText', px: 4, py: 1.5, borderRadius: 999, fontWeight: 700,
              boxShadow: function(theme) { return '0 8px 24px ' + alpha(theme.palette.primary.main, 0.28); },
              '&:hover': { bgcolor: 'brand.bright' },
            }}
          >
            Iniciar sesion
          </Button>
          <Button
            ref={boton2Ref}
            {...imantar(boton2Ref)}
            variant="outlined" size="large" component={RouterLink} to="/public"
            startIcon={<Iconify icon="eva:eye-outline" />}
            sx={{
              borderWidth: 2, borderColor: 'primary.main', color: 'primary.main', px: 4, py: 1.5,
              borderRadius: 999, fontWeight: 700,
              '&:hover': { borderWidth: 2, borderColor: 'primary.main', bgcolor: 'action.hover' },
            }}
          >
            Ver competencias
          </Button>
        </Box>
      </Box>

      <Box
        className="hero-scroll"
        sx={{
          position: 'relative', zIndex: 3, display: 'flex', flexDirection: 'column',
          alignItems: 'center', gap: 0.5, pb: { xs: 1.5, md: 2 },
          color: function(theme) { return alpha(theme.palette.primary.main, 0.55); },
        }}
      >
        <Iconify icon="eva:chevron-down-outline" width={20} className="hero-scroll-flecha" />
      </Box>

      {/* Grano sutil encima de todo, sin bloquear clicks -- lo que le da el
          aire de impresion en vez de gradiente digital liso. Un tile de 120px
          con feTurbulence, repetido; mix-blend-mode multiply para que tiña en
          vez de tapar. */}
      <Box
        sx={{
          position: 'absolute', inset: 0, zIndex: 6, pointerEvents: 'none',
          opacity: 0.05, mixBlendMode: 'multiply',
          backgroundImage: 'url("data:image/svg+xml,%3Csvg xmlns=\'http://www.w3.org/2000/svg\' width=\'120\' height=\'120\'%3E%3Cfilter id=\'n\'%3E%3CfeTurbulence type=\'fractalNoise\' baseFrequency=\'0.85\' numOctaves=\'2\' stitchTiles=\'stitch\'/%3E%3C/filter%3E%3Crect width=\'100%25\' height=\'100%25\' filter=\'url(%23n)\'/%3E%3C/svg%3E")',
        }}
      />

      <Marquesina resultados={resultados} />
    </Box>
  );
}

/**
 * Una O del wordmark reemplazada por una pelota -- mismo tamaño de caja que
 * ocuparia la letra, para no correr el resto de la palabra. El giro vive en
 * un span propio adentro (.hero-pelota), asi que el span de afuera
 * (.hero-letra) sigue participando de la entrada en bloque con el resto de
 * las letras sin pelearse por la propiedad rotate.
 */
function LetraPelota(props) {
  return (
    <Box
      component="span"
      className="hero-letra"
      sx={{
        display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
        width: { xs: '2.6rem', sm: '3.9rem', md: '5.5rem', lg: '6.6rem' },
        height: { xs: '2.6rem', sm: '3.9rem', md: '5.5rem', lg: '6.6rem' },
        verticalAlign: 'middle',
      }}
    >
      <Box className={'hero-pelota hero-pelota-' + props.tipo} sx={{ width: '78%', height: '78%' }}>
        <Iconify icon={props.icon} width="100%" height="100%" />
      </Box>
    </Box>
  );
}

// Los 5 svg de web/public/hero/fly, ya recoloreados a #99e6ff en el propio
// archivo (no via CSS: son <img>, no svg inline, asi que "color"/currentColor
// no llega adentro). Cada posicion de VOLADORES sortea uno de estos al
// montar -- ver el useState de mas abajo.
var SVGS_VOLADOR = [
  '/hero/fly/sport-football.svg',
  '/hero/fly/sport-basketball.svg',
  '/hero/fly/sport-taekwondo.svg',
  '/hero/fly/sport-volleyball.svg',
  '/hero/fly/sport-winner.svg',
];

function numeroAleatorio(min, max) {
  return min + Math.random() * (max - min);
}

/**
 * Lo que antes eran nubes, ahora son estos mismos 5 iconos volando -- pero
 * una nube deriva pareja y estos son pelotas/iconos de deporte, asi que el
 * movimiento tiene que sentirse mas caotico: cada uno arma su propio zigzag
 * (tres puntos de paso con un "top" al azar, no una linea recta de punta a
 * punta) y gira sobre si mismo mientras cruza, en vez del flote parejo de
 * arriba/abajo que tenia la nube. Todo el azar (que icono, que tan arriba o
 * abajo se desvia, cuanto tarda, cuando arranca, para que lado gira) se
 * sortea una sola vez al montar -- ver el useState de mas abajo -- no en
 * cada render, o el hover de una mascota (que hace re-renderizar
 * PlayfulHero entero) reordenaria el vuelo a mitad de camino.
 */
function Voladores() {
  var base = [
    { top: 6, left: -18, width: 84, rotate: -4, direccion: 'derecha' },
    { top: 68, left: 112, width: 66, rotate: 3, direccion: 'izquierda' },
    { top: 24, left: -22, width: 60, rotate: -2, direccion: 'derecha' },
    { top: 44, left: 118, width: 76, rotate: 5, direccion: 'izquierda' },
    { top: 12, left: 108, width: 54, rotate: -6, direccion: 'izquierda' },
    { top: 84, left: -20, width: 70, rotate: 4, direccion: 'derecha' },
    { top: 36, left: -20, width: 58, rotate: 8, direccion: 'derecha' },
    { top: 78, left: 104, width: 72, rotate: -8, direccion: 'izquierda' },
    { top: 92, left: -16, width: 62, rotate: -10, direccion: 'derecha' },
    { top: 2, left: -14, width: 68, rotate: 6, direccion: 'derecha' },
    { top: 52, left: 118, width: 64, rotate: -5, direccion: 'izquierda' },
    { top: 60, left: -18, width: 58, rotate: 9, direccion: 'derecha' },
    { top: 16, left: 112, width: 72, rotate: -9, direccion: 'izquierda' },
    { top: 96, left: -22, width: 60, rotate: 7, direccion: 'derecha' },
    { top: 30, left: 106, width: 66, rotate: -3, direccion: 'izquierda' },
    { top: 8, left: 120, width: 60, rotate: 4, direccion: 'izquierda' },
    { top: 48, left: -26, width: 80, rotate: -7, direccion: 'derecha' },
    { top: 72, left: -14, width: 56, rotate: 11, direccion: 'derecha' },
    { top: 20, left: -24, width: 70, rotate: -8, direccion: 'derecha' },
    { top: 88, left: 116, width: 64, rotate: 6, direccion: 'izquierda' },
    { top: 58, left: 110, width: 74, rotate: -4, direccion: 'izquierda' },
    { top: 4, left: 104, width: 52, rotate: 9, direccion: 'izquierda' },
    { top: 64, left: -22, width: 68, rotate: -6, direccion: 'derecha' },
  ];

  var [voladores] = useState(function() {
    return base.map(function(b) {
      var destino = b.direccion === 'derecha' ? 135 : -35;

      // Tres paradas a lo largo del cruce -- las dos primeras se apartan
      // del "top" de arranque en cualquier direccion, la tercera se acerca
      // de nuevo, asi la trayectoria zigzaguea en vez de ir derecho.
      var tops = [
        b.top + numeroAleatorio(-18, 18),
        b.top + numeroAleatorio(-18, 18),
        b.top + numeroAleatorio(-10, 10),
      ].map(function(v) { return Math.max(2, Math.min(92, v)) + '%'; });

      var lefts = [
        b.left + (destino - b.left) * numeroAleatorio(0.3, 0.45),
        b.left + (destino - b.left) * numeroAleatorio(0.65, 0.8),
        destino,
      ].map(function(v) { return v + '%'; });

      return {
        svg: SVGS_VOLADOR[Math.floor(Math.random() * SVGS_VOLADOR.length)],
        top: b.top + '%',
        left: b.left + '%',
        width: b.width,
        rotate: b.rotate,
        tops: tops.join(','),
        lefts: lefts.join(','),
        duracion: numeroAleatorio(26, 50),
        retraso: numeroAleatorio(0, 20),
        espin: Math.random() < 0.5 ? '+=360' : '-=360',
        duracionEspin: numeroAleatorio(5, 13),
      };
    });
  });

  return (
    <>
      {voladores.map(function(v, i) {
        return (
          <Box
            key={i}
            component="img"
            src={v.svg}
            alt=""
            className="hero-volador"
            data-tops={v.tops}
            data-lefts={v.lefts}
            data-duracion={v.duracion}
            data-retraso={v.retraso}
            data-espin={v.espin}
            data-duracion-espin={v.duracionEspin}
            sx={{
              position: 'absolute', top: v.top, left: v.left, width: v.width, height: v.width,
              transform: 'rotate(' + v.rotate + 'deg)',
              filter: function(theme) { return 'drop-shadow(0 6px 10px ' + alpha(theme.palette.primary.main, 0.25) + ')'; },
              zIndex: 1, pointerEvents: 'none', userSelect: 'none',
            }}
          />
        );
      })}
    </>
  );
}

/** El sello circular que gira en una esquina -- el "logo vivo" del hero. */
function Insignia() {
  // Los atributos de un <svg> no leen el tema como `sx`, así que se toman
  // los colores del tema y se pasan a mano.
  var palette = useTheme().palette;

  return (
    <Box
      className="hero-insignia"
      sx={{
        position: 'absolute', top: { xs: 78, md: 96 }, left: { xs: 16, md: 40 },
        width: { xs: 64, md: 88 }, height: { xs: 64, md: 88 }, zIndex: 3, pointerEvents: 'none',
      }}
    >
      <svg viewBox="0 0 100 100" width="100%" height="100%">
        <circle cx="50" cy="50" r="47" fill={palette.brand.bright} />
        <circle cx="50" cy="50" r="41" fill={palette.primary.main} />
        <path id="insignia-curva" d="M 50,50 m -30,0 a 30,30 0 1,1 60,0 a 30,30 0 1,1 -60,0" fill="none" />
        <text fontSize="8.6" fontWeight="700" fill={palette.primary.contrastText} letterSpacing="1.6">
          <textPath href="#insignia-curva" startOffset="0%">SPORTFROG • SPORTFROG • </textPath>
        </text>
        <circle cx="50" cy="50" r="13" fill={palette.brand.accent} />
      </svg>
    </Box>
  );
}

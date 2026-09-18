import Avatar from '@mui/material/Avatar';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import IconButton from '@mui/material/IconButton';
import Typography from '@mui/material/Typography';
import { alpha } from '@mui/material/styles';
import { Iconify } from 'src/components/iconify';
import { MEDAL_COLORS } from 'src/components/medal-circle';
import { ESTADO, FORMATO, HeroChip, LiveBadge, CuentaAtras } from './hero-parts';

/**
 * La variante `standard` -- el hero de Nivel 1+2, sin cambios de
 * comportamiento respecto de antes de que existieran las otras tres. Recibe
 * `data`, los hechos ya derivados por el dispatcher (portal-hero.jsx), y
 * solo decide cómo ordenarlos: dos columnas en el portal real, o un stack
 * centrado si el tema eligió `heroLayout: centered`.
 *
 * También es el fallback de LiveHero cuando no hay un partido en vivo que
 * mostrar -- por eso vive en su propio módulo en vez de adentro de
 * portal-hero.jsx: LiveHero la importa directo.
 */
export function StandardHero(props) {
  var data = props.data;
  var comp = data.comp;
  var portal = data.portal;
  var theme = data.theme;
  var moment = data.moment;
  var alFrente = data.alFrente;
  var colorTexto = data.colorTexto;
  var dateLabel = data.dateLabel;
  var social = data.social;
  var campeon = data.campeon;
  var enVivo = data.enVivo;
  var proximoPartido = data.proximoPartido;
  var eyebrow = data.eyebrow;
  var centered = data.centered;
  // Prendido salvo que se haya apagado a propósito -- mismo criterio que
  // showStandings/showGallery del lado del backend.
  var fondoLogo = !theme || theme.showLogoBackground !== false;

  // El bloque de nombre, campeón y descripción -- lo mismo en modo denso
  // (vista previa del estudio) y en el portal real, solo que el segundo lo
  // deja respirar en una columna aparte del estado (ver más abajo).
  var textBlock = (
    <Box sx={{ minWidth: 0, flex: '1 1 auto', textAlign: centered ? 'center' : 'left' }}>
      {props.onBack && (
        <Button
          size="small"
          sx={{ color: colorTexto, mb: 1, opacity: 0.85 }}
          onClick={props.onBack}
          startIcon={<Iconify icon="eva:arrow-back-outline" />}
        >
          Volver
        </Button>
      )}
      {campeon && (
        <Box
          sx={{
            display: 'inline-flex', alignItems: 'center', gap: 1, mb: 1.5,
            pl: campeon.logoUrl ? 0.5 : 1.25, pr: 1.5, py: 0.5, borderRadius: 5,
            bgcolor: (t) => alpha(alFrente(t), 0.15),
            border: '1px solid', borderColor: alpha(MEDAL_COLORS[1], 0.5),
            alignSelf: centered ? 'center' : 'auto',
          }}
        >
          {campeon.logoUrl ? (
            <Avatar
              src={campeon.logoUrl}
              variant="rounded"
              sx={{ width: 32, height: 32, bgcolor: (t) => alpha(alFrente(t), 0.15), '& img': { objectFit: 'contain' } }}
            />
          ) : (
            <Iconify icon="mdi:trophy" width={18} />
          )}
          <Typography variant="subtitle2" sx={{ fontWeight: 700, letterSpacing: 0.3, lineHeight: 1.2 }}>
            {campeon.teamName} es el campeón
          </Typography>
        </Box>
      )}
      <Typography
        variant="overline"
        sx={{ display: 'block', fontWeight: 700, letterSpacing: 1.4, opacity: 0.8, fontSize: 11 }}
      >
        {eyebrow}
      </Typography>
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: centered ? 'center' : 'flex-start', gap: 1.5, mt: 0.25 }}>
        {portal.logoUrl && (
          <Avatar
            src={portal.logoUrl}
            variant="rounded"
            sx={{
              // Más grande que antes (era 40/44/56) y, con fondo, un disco
              // casi opaco con sombra propia en vez del tinte al 15% de
              // antes -- eso apenas se notaba sobre una portada de color o
              // foto. Sin fondo, el logo flota solo: para una marca que ya
              // trae su propio color o encuadre, un segundo fondo detrás
              // era enmarcar un marco.
              width: { xs: 48, sm: props.dense ? 52 : 64 },
              height: { xs: 48, sm: props.dense ? 52 : 64 },
              bgcolor: fondoLogo ? 'rgba(255,255,255,0.94)' : 'transparent',
              boxShadow: fondoLogo ? '0 2px 10px rgba(0,0,0,0.28)' : 'none',
              '& img': { objectFit: 'contain' },
            }}
          />
        )}
        <Typography
          variant="h3"
          fontWeight={800}
          sx={{
            lineHeight: 1.05,
            letterSpacing: -0.5,
            fontSize: props.dense ? { xs: '1.35rem', sm: '1.75rem' } : { xs: '1.6rem', sm: '2.1rem', md: '2.75rem' },
          }}
        >
          {comp.name || 'Nombre de la competencia'}
        </Typography>
      </Box>
      {portal.description && (
        <Typography variant="body2" sx={{ opacity: 0.95, mt: 1, maxWidth: 640, mx: centered ? 'auto' : 0 }}>{portal.description}</Typography>
      )}
    </Box>
  );

  // El bloque de estado -- vivo, cuenta regresiva, formato, fecha, redes.
  // En el portal real se alinea a la derecha del nombre en pantallas
  // anchas; en modo denso (el panel angosto del estudio) siempre queda
  // debajo, sin depender del ancho de la ventana del navegador.
  var statusJustify = centered ? 'center' : (props.dense ? 'flex-start' : { xs: 'flex-start', md: 'flex-end' });
  var statusCluster = (
    <Box
      sx={{
        display: 'flex', gap: 1, flexWrap: 'wrap', alignItems: 'center',
        justifyContent: statusJustify,
      }}
    >
      {enVivo && <LiveBadge count={moment.liveMatchCount} />}
      {!enVivo && proximoPartido && <CuentaAtras targetIso={proximoPartido} fg={alFrente} />}
      {comp.status && <HeroChip label={ESTADO[comp.status] || comp.status} fg={alFrente} />}
      {comp.format && <HeroChip label={FORMATO[comp.format] || comp.format} fg={alFrente} />}
      {dateLabel && <HeroChip label={dateLabel} fg={alFrente} />}
      {social.map(function(s) {
        return (
          <IconButton
            key={s.key}
            size="small"
            component="a"
            href={s.href}
            target="_blank"
            rel="noopener noreferrer"
            sx={{ color: colorTexto, bgcolor: (t) => alpha(alFrente(t), 0.15) }}
          >
            <Iconify icon={s.icon} width={16} />
          </IconButton>
        );
      })}
    </Box>
  );

  // "centered" reemplaza el layout de dos columnas por un stack único
  // centrado incluso en escritorio -- es una composición distinta, no una
  // variante del ancho disponible como sí lo es `dense`.
  var twoColumn = !props.dense && !centered;

  if (twoColumn) {
    return (
      <Box sx={{ display: 'flex', flexDirection: { xs: 'column', md: 'row' }, gap: { xs: 1.5, md: 3 }, alignItems: { md: 'flex-end' } }}>
        {textBlock}
        <Box sx={{ flexShrink: 0 }}>{statusCluster}</Box>
      </Box>
    );
  }

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', alignItems: centered ? 'center' : 'stretch' }}>
      {textBlock}
      <Box sx={{ mt: 1.5, display: 'flex' }}>{statusCluster}</Box>
    </Box>
  );
}

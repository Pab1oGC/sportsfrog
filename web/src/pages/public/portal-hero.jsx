import { useState, useEffect } from 'react';
import Avatar from '@mui/material/Avatar';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Chip from '@mui/material/Chip';
import IconButton from '@mui/material/IconButton';
import Typography from '@mui/material/Typography';
import Container from '@mui/material/Container';
import { alpha } from '@mui/material/styles';
import { Iconify } from 'src/components/iconify';
import { heroBackground, isHex } from 'src/lib/portal-theme';

/* ---------------------------------------------------------------------------
   La portada del portal de una competencia.

   Sacada de public-competition.jsx tal cual para que el estudio
   (portal-studio.jsx) muestre exactamente la misma portada que verá el
   visitante — si fueran dos dibujos distintos, la vista previa mentiría.

   Sin personalización (ni portal.theme ni portal.accentColor), el fondo cae
   en `primary.main` y el texto en `primary.contrastText`, que en el tema
   base son el verde y el blanco de siempre: la portada queda idéntica a
   antes de que esto existiera.
   --------------------------------------------------------------------------- */

var ESTADO = { draft: 'Borrador', scheduled: 'Programada', in_progress: 'En curso', finished: 'Finalizada', cancelled: 'Cancelada' };
var FORMATO = { league: 'Todos vs todos', knockout: 'Eliminacion', groups: 'Grupos' };

/**
 * @param {object} props
 * @param {object} props.comp - { name, organizationName, season, sportName, status, format, startsOn, endsOn }
 * @param {object} props.portal - comp.portal de la API (o el equivalente que arma el estudio)
 * @param {object} [props.moment] - comp.moment de la API: { nextMatchAt, liveMatchCount, champion }.
 *   Nunca lo pasa el estudio -- no es algo que se configure, es un hecho de la
 *   competencia real, y la vista previa no simula una.
 * @param {function} [props.onBack] - si está, dibuja "Volver"; el estudio no lo pasa
 * @param {boolean} [props.dense] - vista previa: menos aire, sin ancho máximo de Container
 */
export function PortalHero(props) {
  var comp = props.comp || {};
  var portal = props.portal || {};
  var theme = portal.theme || null;
  var moment = props.moment || null;
  // Sin estilo elegido: "imagen" si hay portada (una competencia sin tema
  // que subió banner lo sigue mostrando), "color plano" si no. Mismo criterio
  // que PortalTheme.Resolve en el backend.
  var heroStyle = (theme && theme.heroStyle) || (portal.bannerUrl ? 'image' : 'solid');

  // El mismo cálculo de fondo que usa la vista previa. Sin color propio cae
  // en `primary.main` (el tema del portal ya lo tiene puesto si hay tema).
  var bg = heroBackground({
    heroStyle: heroStyle,
    primary: (theme && theme.primary) || portal.accentColor,
    bannerUrl: portal.bannerUrl,
    focusX: theme && theme.focusX,
    focusY: theme && theme.focusY,
  });

  // Con imagen de portada siempre hay una capa oscura encima, así que el
  // texto va blanco pase lo que pase con el color principal. En los demás
  // estilos el fondo es el color principal y el texto es su contrastText.
  var conImagen = heroStyle === 'image' && !!portal.bannerUrl;
  var alFrente = function(t) { return conImagen ? '#fff' : t.palette.primary.contrastText; };
  var colorTexto = conImagen ? '#fff' : 'primary.contrastText';

  var dateLabel = comp.startsOn ? comp.startsOn + ' - ' + (comp.endsOn || '?') : null;

  var social = [
    { key: 'instagram', href: portal.instagram, icon: 'mdi:instagram' },
    { key: 'facebook', href: portal.facebook, icon: 'mdi:facebook' },
    { key: 'whatsApp', href: portal.whatsApp, icon: 'mdi:whatsapp' },
    { key: 'website', href: portal.website, icon: 'mdi:web' },
  ].filter(function(s) { return s.href; });

  var campeon = moment && moment.champion;
  var enVivo = moment && moment.liveMatchCount > 0;
  var proximoPartido = moment && moment.nextMatchAt;

  var inner = (
    <>
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
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 1 }}>
          {campeon.logoUrl && (
            <Avatar
              src={campeon.logoUrl}
              variant="rounded"
              sx={{ width: 26, height: 26, bgcolor: (t) => alpha(alFrente(t), 0.15), '& img': { objectFit: 'contain' } }}
            />
          )}
          <Typography variant="subtitle2" sx={{ fontWeight: 700, letterSpacing: 0.3 }}>
            🏆 {campeon.teamName} es el campeón
          </Typography>
        </Box>
      )}
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5 }}>
        {portal.logoUrl && (
          <Avatar
            src={portal.logoUrl}
            variant="rounded"
            sx={{
              width: { xs: 40, sm: props.dense ? 44 : 56 },
              height: { xs: 40, sm: props.dense ? 44 : 56 },
              bgcolor: (t) => alpha(alFrente(t), 0.15),
              '& img': { objectFit: 'contain' },
            }}
          />
        )}
        <Typography
          variant="h3"
          fontWeight={700}
          sx={{ fontSize: props.dense ? { xs: '1.35rem', sm: '1.75rem' } : { xs: '1.5rem', sm: '2rem', md: '2.5rem' } }}
        >
          {comp.name || 'Nombre de la competencia'}
        </Typography>
      </Box>
      <Typography variant="subtitle1" sx={{ opacity: 0.9, mt: 0.5 }}>
        {[comp.organizationName, comp.season, comp.sportName].filter(Boolean).join(' · ')}
      </Typography>
      {portal.description && (
        <Typography variant="body2" sx={{ opacity: 0.95, mt: 1, maxWidth: 640 }}>{portal.description}</Typography>
      )}
      <Box sx={{ display: 'flex', gap: 1, mt: 1.5, flexWrap: 'wrap', alignItems: 'center' }}>
        {enVivo && (
          <Chip
            label={'EN VIVO' + (moment.liveMatchCount > 1 ? ' · ' + moment.liveMatchCount + ' partidos' : '')}
            size="small"
            color="error"
            sx={{ fontWeight: 700, letterSpacing: 0.5 }}
          />
        )}
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
    </>
  );

  return (
    <Box
      sx={{
        position: 'relative',
        color: colorTexto,
        py: props.dense ? 2.5 : { xs: 3, sm: 4 },
        px: 3,
        // Sin color propio: el verde del tema. isHex evita pasar undefined a
        // backgroundColor y perder el fallback.
        bgcolor: isHex(bg.backgroundColor) ? bg.backgroundColor : 'primary.main',
        backgroundImage: bg.backgroundImage,
        backgroundSize: 'cover',
        backgroundPosition: bg.backgroundPosition || 'center',
      }}
    >
      {props.dense ? <Box>{inner}</Box> : <Container maxWidth="lg">{inner}</Container>}
    </Box>
  );
}

function HeroChip(props) {
  var fg = props.fg;
  return (
    <Chip
      label={props.label}
      size="small"
      icon={props.icon ? <Iconify icon={props.icon} width={14} /> : undefined}
      sx={{ bgcolor: (t) => alpha(fg(t), 0.2), color: (t) => fg(t), '& .MuiChip-icon': { color: 'inherit' } }}
    />
  );
}

/**
 * Cuánto falta para el próximo partido, en minutos redondeados hacia el
 * cuarto de hora que sea legible ("3 días", "2h 30m") — no segundo a
 * segundo, que en un cover no aporta y solo redibuja de más. Se actualiza
 * cada minuto por si alguien deja la pestaña abierta.
 */
function CuentaAtras(props) {
  var [ahora, setAhora] = useState(function() { return Date.now(); });

  useEffect(function() {
    var id = setInterval(function() { setAhora(Date.now()); }, 60000);
    return function() { clearInterval(id); };
  }, []);

  var restanteMin = Math.floor((new Date(props.targetIso).getTime() - ahora) / 60000);
  // Ya deberia haber arrancado (el visitante llego con la pestaña abierta
  // desde antes, o el reloj del servidor y el del navegador no coinciden
  // por unos segundos): no se muestra una cuenta en negativo.
  if (restanteMin <= 0) return null;

  var dias = Math.floor(restanteMin / 1440);
  var horas = Math.floor((restanteMin % 1440) / 60);
  var minutos = restanteMin % 60;

  var texto;
  if (dias > 0) texto = 'Empieza en ' + dias + (dias === 1 ? ' día' : ' días');
  else if (horas > 0) texto = 'Empieza en ' + horas + 'h' + (minutos > 0 ? ' ' + minutos + 'm' : '');
  else texto = 'Empieza en ' + minutos + ' min';

  return <HeroChip label={texto} fg={props.fg} icon="mdi:timer-outline" />;
}

import Box from '@mui/material/Box';
import Container from '@mui/material/Container';
import { heroBackground, isHex } from 'src/lib/portal-theme';
import { HeroDecoration } from './hero/hero-parts';
import { StandardHero } from './hero/standard-hero';
import { ScoreboardHero } from './hero/scoreboard-hero';
import { EditorialHero } from './hero/editorial-hero';
import { LiveHero } from './hero/live-hero';

/* ---------------------------------------------------------------------------
   La portada del portal de una competencia.

   Sacada de public-competition.jsx tal cual para que el estudio
   (portal-studio.jsx) muestre exactamente la misma portada que verá el
   visitante — si fueran dos dibujos distintos, la vista previa mentiría.

   Sin personalización (ni portal.theme ni portal.accentColor), el fondo cae
   en `primary.main` y el texto en `primary.contrastText`, que en el tema
   base son el verde y el blanco de siempre: la portada queda idéntica a
   antes de que esto existiera.

   Este módulo es solo el despachador: deriva una vez los hechos que
   cualquier variante podría necesitar (fondo, contraste, campeón, en vivo,
   redes...) y se los pasa a la variante elegida (theme.heroVariant). Cada
   variante vive en su propio archivo bajo ./hero/ y solo decide cómo
   ordenar esos hechos, nunca de dónde salen -- agregar una variante nueva
   más adelante es un componente más en HERO_VARIANTS, sin tocar las demás.
   --------------------------------------------------------------------------- */

var HERO_VARIANTS = {
  standard: StandardHero,
  scoreboard: ScoreboardHero,
  editorial: EditorialHero,
  live: LiveHero,
};

/**
 * @param {object} props
 * @param {object} props.comp - { name, organizationName, season, sportName, status, format, startsOn, endsOn }
 * @param {object} props.portal - comp.portal de la API (o el equivalente que arma el estudio)
 * @param {object} [props.moment] - comp.moment de la API: { nextMatchAt, liveMatchCount, champion, liveMatch }.
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
    gradientTo: theme && theme.heroGradientTo,
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
  var liveMatch = moment && moment.liveMatch;

  var eyebrow = [comp.organizationName, comp.season, comp.sportName].filter(Boolean).join(' · ');

  // Independiente de `dense`: `dense` es "poco aire, panel angosto" (el
  // estudio); `centered` es una composición distinta, y las dos preguntas no
  // dependen una de la otra -- una competencia que eligió "centered" la ve
  // centrada tanto en el estudio como en el portal real. Solo la variante
  // `standard` la usa -- las otras tres fijan su propia composición.
  var centered = theme && theme.heroLayout === 'centered';

  var data = {
    comp: comp,
    portal: portal,
    theme: theme,
    moment: moment,
    alFrente: alFrente,
    colorTexto: colorTexto,
    dateLabel: dateLabel,
    social: social,
    campeon: campeon,
    enVivo: enVivo,
    proximoPartido: proximoPartido,
    liveMatch: liveMatch,
    eyebrow: eyebrow,
    centered: centered,
  };

  var Variant = HERO_VARIANTS[theme && theme.heroVariant] || StandardHero;
  var inner = <Variant data={data} dense={props.dense} onBack={props.onBack} />;

  return (
    <Box
      sx={{
        position: 'relative',
        color: colorTexto,
        py: props.dense ? 2.5 : { xs: 3, sm: 4 },
        px: 3,
        overflow: 'hidden',
        // Sin color propio: el verde del tema. isHex evita pasar undefined a
        // backgroundColor y perder el fallback.
        bgcolor: isHex(bg.backgroundColor) ? bg.backgroundColor : 'primary.main',
        backgroundImage: bg.backgroundImage,
        backgroundSize: 'cover',
        backgroundPosition: bg.backgroundPosition || 'center',
      }}
    >
      <HeroDecoration level={theme && theme.decoration} />
      <Box sx={{ position: 'relative', zIndex: 1 }}>
        {props.dense ? <Box>{inner}</Box> : <Container maxWidth="lg">{inner}</Container>}
      </Box>
    </Box>
  );
}

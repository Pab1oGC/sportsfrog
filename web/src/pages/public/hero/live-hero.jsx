import Avatar from '@mui/material/Avatar';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Typography from '@mui/material/Typography';
import { alpha } from '@mui/material/styles';
import { Iconify } from 'src/components/iconify';
import { LivePulse } from 'src/components/live-pulse';
import { StandardHero } from './standard-hero';

/**
 * La variante `live` -- invierte la jerarquía de las otras tres: si hay un
 * partido en vivo sin ambigüedad (`data.liveMatch`, resuelto por el backend
 * solo cuando hay exactamente uno -- ver ResolveLiveMatchAsync), el marcador
 * es lo primero que se ve y el nombre de la competencia baja a una
 * etiqueta chica.
 *
 * Sin partido en vivo (0 o 2+ a la vez, o directamente ninguno -- la
 * inmensa mayoría del tiempo) delega en StandardHero explícitamente: nunca
 * un render a medias. El estudio nunca simula `moment` (ver portal-hero.jsx),
 * así que la vista previa de "Live" siempre cae acá -- es honesto, no una
 * vista previa que miente: en el estudio nunca hay, ni va a haber, un
 * partido en curso que mostrar.
 */
export function LiveHero(props) {
  var data = props.data;
  var liveMatch = data.liveMatch;

  if (!liveMatch) {
    return <StandardHero data={data} dense={props.dense} onBack={props.onBack} />;
  }

  var colorTexto = data.colorTexto;
  var alFrente = data.alFrente;
  var hayMarcador = liveMatch.homeScore != null && liveMatch.awayScore != null;

  return (
    <Box sx={{ textAlign: 'center' }}>
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
      <Box sx={{ display: 'inline-flex', px: 1.25, py: 0.5, borderRadius: 5, bgcolor: 'error.main', color: 'error.contrastText', mb: 1.5 }}>
        <LivePulse label={'EN VIVO · ' + liveMatch.categoryName} />
      </Box>
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'center', gap: { xs: 1.5, sm: 3 } }}>
        <Equipo nombre={liveMatch.homeTeamName} logo={liveMatch.homeClubLogoUrl} alFrente={alFrente} dense={props.dense} />
        <Typography
          variant="h2"
          fontWeight={800}
          sx={{
            fontVariantNumeric: 'tabular-nums', lineHeight: 1,
            fontSize: props.dense ? '1.75rem' : { xs: '2.2rem', sm: '3rem' },
          }}
        >
          {hayMarcador ? liveMatch.homeScore + ' - ' + liveMatch.awayScore : 'VS'}
        </Typography>
        <Equipo nombre={liveMatch.awayTeamName} logo={liveMatch.awayClubLogoUrl} alFrente={alFrente} dense={props.dense} />
      </Box>
      <Typography variant="overline" sx={{ display: 'block', mt: 1.5, opacity: 0.8, letterSpacing: 1.2 }}>
        {data.comp.name}
      </Typography>
    </Box>
  );
}

/** Un equipo del partido en vivo: escudo sobre el color del hero, nombre debajo. */
function Equipo(props) {
  var tamano = props.dense ? 44 : 64;
  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 0.75, minWidth: 0, maxWidth: { xs: 96, sm: 140 } }}>
      <Avatar
        src={props.logo || undefined}
        variant="rounded"
        sx={{ width: tamano, height: tamano, bgcolor: (t) => alpha(props.alFrente(t), 0.15), '& img': { objectFit: 'contain' } }}
      >
        {!props.logo && <Iconify icon="mdi:shield-outline" width={tamano * 0.5} />}
      </Avatar>
      <Typography variant="body2" fontWeight={700} noWrap sx={{ maxWidth: '100%' }} title={props.nombre}>
        {props.nombre}
      </Typography>
    </Box>
  );
}

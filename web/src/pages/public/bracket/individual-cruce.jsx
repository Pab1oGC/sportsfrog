import Avatar from '@mui/material/Avatar';
import Badge from '@mui/material/Badge';
import Box from '@mui/material/Box';
import Divider from '@mui/material/Divider';
import Paper from '@mui/material/Paper';
import Typography from '@mui/material/Typography';
import { Iconify } from 'src/components/iconify';
import { LivePulse } from 'src/components/live-pulse';
import { AMBIENT_CARD, fechaCorta, hora } from 'src/pages/public/match-card/match-card-parts';

/**
 * El cruce para un deporte individual (hoy, taekwondo) -- reemplaza a
 * standard/compact/detailed por completo cuando `Llave` recibe
 * `esIndividual`, no una variante más entre ellas (ver llave.jsx). La
 * foto del competidor solo llega si la organización prendió el
 * interruptor correspondiente y el cruce es de un solo atleta por lado
 * -- ver `ReadPublicCalendar.AthletePhotoKeysAsync` del lado del
 * servidor, que es quien decide eso, no este componente: acá alcanza
 * con mostrar el respaldo genérico cuando `fotoUrl` viene null, sea
 * cual sea la razón.
 */
export function IndividualCruce(props) {
  var m = props.m;
  var outcome = props.outcome;

  return (
    <Paper variant="outlined" sx={{ p: 1, borderRadius: 1.5, ...AMBIENT_CARD }}>
      <FilaIndividual
        nombre={m.homeTeamName || m.homePlaceholder || 'Por definir'}
        club={m.homeClubName}
        fotoUrl={m.homePhotoUrl}
        escudoUrl={m.homeClubLogoUrl}
        score={outcome.homeMostrado}
        gano={outcome.ganoLocal}
      />
      <Divider sx={{ my: 0.75 }} />
      <FilaIndividual
        nombre={m.awayTeamName || m.awayPlaceholder || 'Por definir'}
        club={m.awayClubName}
        fotoUrl={m.awayPhotoUrl}
        escudoUrl={m.awayClubLogoUrl}
        score={outcome.awayMostrado}
        gano={outcome.ganoVisita}
      />
      {outcome.huboPenales && (
        <Typography variant="caption" color="text.secondary" sx={{ display: 'block', textAlign: 'center', mt: 0.5 }}>
          ({m.penaltyHomeScore}-{m.penaltyAwayScore} pen)
        </Typography>
      )}
      {outcome.enVivo && (
        <Box sx={{ display: 'flex', justifyContent: 'center', mt: 0.5, py: 0.35, borderRadius: 1, bgcolor: 'error.main', color: 'error.contrastText' }}>
          <LivePulse label="EN VIVO" size="small" />
        </Box>
      )}
      {!outcome.jugado && !outcome.enVivo && (
        <Typography variant="caption" color="text.secondary" sx={{ display: 'block', textAlign: 'center', mt: 0.5 }}>
          {m.scheduledAt ? fechaCorta(m.scheduledAt) + ' · ' + hora(m.scheduledAt) : 'Por programar'}
        </Typography>
      )}
    </Paper>
  );
}

/**
 * Una fila de competidor: foto (o el respaldo genérico) con el escudo
 * del club superpuesto como insignia -- entran las dos imágenes que se
 * pidieron sin duplicar la altura de la fila -- nombre y club, y el
 * marcador a la derecha, mismo criterio de "quién ganó" que ya usa
 * FilaCruce (bracket-parts.jsx) para las demás variantes.
 */
function FilaIndividual(props) {
  return (
    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
      <Badge
        overlap="circular"
        anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
        sx={{ '& .MuiBadge-badge': { p: 0, minWidth: 'auto', height: 'auto', bgcolor: 'transparent' } }}
        badgeContent={
          <Avatar
            src={props.escudoUrl || undefined}
            variant="circular"
            sx={{ width: 16, height: 16, border: '1.5px solid', borderColor: 'background.paper', bgcolor: 'action.hover' }}
          >
            {!props.escudoUrl && <Iconify icon="mdi:shield-outline" width={9} sx={{ color: 'text.disabled' }} />}
          </Avatar>
        }
      >
        <Avatar
          src={props.fotoUrl || undefined}
          variant="circular"
          sx={{ width: 40, height: 40, flexShrink: 0, bgcolor: 'action.hover' }}
        >
          {!props.fotoUrl && <Iconify icon="mdi:account" width={22} sx={{ color: 'text.disabled' }} />}
        </Avatar>
      </Badge>
      <Box sx={{ flexGrow: 1, minWidth: 0 }}>
        <Typography variant="body2" noWrap title={props.nombre} sx={{ fontWeight: props.gano ? 700 : 500, lineHeight: 1.2 }}>
          {props.nombre}
        </Typography>
        {props.club && (
          <Typography variant="caption" color="text.secondary" noWrap title={props.club} sx={{ display: 'block', lineHeight: 1.2 }}>
            {props.club}
          </Typography>
        )}
      </Box>
      <Typography
        variant="body2"
        fontWeight={800}
        sx={{ minWidth: 20, textAlign: 'right', flexShrink: 0, fontVariantNumeric: 'tabular-nums', color: props.gano ? 'primary.main' : undefined }}
      >
        {props.score != null ? props.score : ''}
      </Typography>
    </Box>
  );
}

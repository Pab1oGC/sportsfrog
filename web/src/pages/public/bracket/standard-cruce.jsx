import Box from '@mui/material/Box';
import Divider from '@mui/material/Divider';
import Paper from '@mui/material/Paper';
import Typography from '@mui/material/Typography';
import { LivePulse } from 'src/components/live-pulse';
import { AMBIENT_CARD, fechaCorta, hora } from 'src/pages/public/match-card/match-card-parts';
import { FilaCruce } from './bracket-parts';

/**
 * La variante `standard` -- el cruce de siempre, sin cambios de
 * comportamiento.
 */
export function StandardCruce(props) {
  var m = props.m;
  var outcome = props.outcome;

  return (
    <Paper variant="outlined" sx={{ p: 1, borderRadius: 1.5, ...AMBIENT_CARD }}>
      <FilaCruce nombre={m.homeTeamName || m.homePlaceholder || 'Por definir'} logo={m.homeClubLogoUrl} score={outcome.homeMostrado} gano={outcome.ganoLocal} />
      <Divider sx={{ my: 0.5 }} />
      <FilaCruce nombre={m.awayTeamName || m.awayPlaceholder || 'Por definir'} logo={m.awayClubLogoUrl} score={outcome.awayMostrado} gano={outcome.ganoVisita} />
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

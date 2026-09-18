import Box from '@mui/material/Box';
import Divider from '@mui/material/Divider';
import Paper from '@mui/material/Paper';
import Typography from '@mui/material/Typography';
import { LivePulse } from 'src/components/live-pulse';
import { AMBIENT_CARD, fechaCorta, hora } from 'src/pages/public/match-card/match-card-parts';
import { FilaCruce } from './bracket-parts';

/**
 * La variante `detailed` -- escudos más grandes (32px en vez de 22),
 * fecha/hora y cancha siempre visibles debajo de los dos equipos, no solo
 * cuando el partido no se jugó -- mismo criterio de armado de `cancha`
 * que ya usa la tarjeta de partido.
 */
export function DetailedCruce(props) {
  var m = props.m;
  var outcome = props.outcome;
  var cancha = [m.venueName, m.spaceName].filter(Boolean).join(' · ');
  var fechaLabel = m.scheduledAt ? fechaCorta(m.scheduledAt) + ' · ' + hora(m.scheduledAt) : 'Por programar';

  return (
    <Paper variant="outlined" sx={{ p: 1.5, borderRadius: 2, ...AMBIENT_CARD }}>
      <FilaCruce nombre={m.homeTeamName || m.homePlaceholder || 'Por definir'} logo={m.homeClubLogoUrl} score={outcome.homeMostrado} gano={outcome.ganoLocal} size={32} />
      <Divider sx={{ my: 0.75 }} />
      <FilaCruce nombre={m.awayTeamName || m.awayPlaceholder || 'Por definir'} logo={m.awayClubLogoUrl} score={outcome.awayMostrado} gano={outcome.ganoVisita} size={32} />
      {outcome.huboPenales && (
        <Typography variant="caption" color="text.secondary" sx={{ display: 'block', textAlign: 'center', mt: 0.5 }}>
          ({m.penaltyHomeScore}-{m.penaltyAwayScore} pen)
        </Typography>
      )}
      {outcome.enVivo && (
        <Box sx={{ display: 'flex', justifyContent: 'center', mt: 0.75, py: 0.35, borderRadius: 1, bgcolor: 'error.main', color: 'error.contrastText' }}>
          <LivePulse label="EN VIVO" size="small" />
        </Box>
      )}
      <Typography variant="caption" color="text.secondary" sx={{ display: 'block', textAlign: 'center', mt: 0.75 }}>
        {fechaLabel}
        {cancha ? ' · ' + cancha : ''}
      </Typography>
    </Paper>
  );
}

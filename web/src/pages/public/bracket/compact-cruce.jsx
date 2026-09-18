import Box from '@mui/material/Box';
import Divider from '@mui/material/Divider';
import Paper from '@mui/material/Paper';
import { LivePulse } from 'src/components/live-pulse';
import { AMBIENT_CARD } from 'src/pages/public/match-card/match-card-parts';
import { FilaCruce } from './bracket-parts';

/**
 * La variante `compact` -- menos padding, sin la línea de fecha para los
 * partidos aún no jugados, pensada para llaves de muchas rondas: que
 * entren más columnas sin scroll horizontal.
 */
export function CompactCruce(props) {
  var m = props.m;
  var outcome = props.outcome;

  return (
    <Paper variant="outlined" sx={{ p: 0.5, borderRadius: 1, ...AMBIENT_CARD }}>
      <FilaCruce nombre={m.homeTeamName || m.homePlaceholder || 'Por definir'} logo={m.homeClubLogoUrl} score={outcome.homeMostrado} gano={outcome.ganoLocal} />
      <Divider sx={{ my: 0.25 }} />
      <FilaCruce nombre={m.awayTeamName || m.awayPlaceholder || 'Por definir'} logo={m.awayClubLogoUrl} score={outcome.awayMostrado} gano={outcome.ganoVisita} />
      {outcome.enVivo && (
        <Box sx={{ display: 'flex', justifyContent: 'center', mt: 0.25, py: 0.2, borderRadius: 1, bgcolor: 'error.main', color: 'error.contrastText' }}>
          <LivePulse label={false} size="small" />
        </Box>
      )}
    </Paper>
  );
}

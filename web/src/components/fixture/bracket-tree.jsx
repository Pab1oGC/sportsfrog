/**
 * T-17 — Árbol de llaves (bracket) para fase eliminatoria.
 * Renderiza el cuadro de partidos en estructura de árbol CSS puro.
 */

import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Chip from '@mui/material/Chip';
import Avatar from '@mui/material/Avatar';
import { nombreFase } from 'src/lib/phase-labels';
import { fechaHora } from 'src/lib/format-date';

const PHASE_ORDER = ['round_of_64', 'round_of_32', 'round_of_16', 'quarter_final', 'semi_final', 'final'];

function sortPhase(phase) {
  const idx = PHASE_ORDER.indexOf(phase);
  return idx >= 0 ? idx : 99;
}

function BracketMatch({ match }) {
  const homeScore = match.homeTotal;
  const awayScore = match.awayTotal;
  const hasScore = homeScore != null && awayScore != null;
  const homeWin = hasScore && homeScore > awayScore;
  const awayWin = hasScore && awayScore > homeScore;

  const TeamRow = function ({ name, score, winner, logoUrl }) {
    return (
      <Box
        sx={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          p: '6px 8px',
          bgcolor: winner ? 'primary.lighter' : 'transparent',
          borderRadius: 1,
          gap: 1,
        }}
      >
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.75, minWidth: 0, flex: 1 }}>
          <Avatar
            src={logoUrl || undefined}
            sx={{ width: 20, height: 20, fontSize: '0.55rem', bgcolor: 'primary.lighter', color: 'primary.main', flexShrink: 0 }}
          >
            {(name || '?')[0]}
          </Avatar>
          <Typography
            variant="caption"
            fontWeight={winner ? 700 : 400}
            color={winner ? 'primary.main' : 'text.primary'}
            noWrap
            sx={{ fontSize: '0.72rem' }}
          >
            {name || 'Por definir'}
          </Typography>
        </Box>
        {score != null && (
          <Typography variant="caption" fontWeight={700} sx={{ flexShrink: 0, minWidth: 16, textAlign: 'right', fontSize: '0.75rem' }}>
            {score}
          </Typography>
        )}
      </Box>
    );
  };

  return (
    <Box
      sx={{
        width: 200,
        border: '1px solid',
        borderColor: 'divider',
        borderRadius: 1.5,
        overflow: 'hidden',
        bgcolor: 'background.paper',
        boxShadow: '0 1px 4px rgba(0,0,0,0.06)',
        flexShrink: 0,
      }}
    >
      {match.scheduledAt && (
        <Typography
          variant="caption"
          color="text.disabled"
          sx={{ display: 'block', px: 1, pt: 0.5, fontSize: '0.6rem' }}
        >
          {fechaHora(match.scheduledAt)}
        </Typography>
      )}
      <TeamRow name={match.homeTeamName} score={homeScore} winner={homeWin} logoUrl={match.homeTeamLogoUrl} />
      <Box sx={{ height: '1px', bgcolor: 'divider' }} />
      <TeamRow name={match.awayTeamName} score={awayScore} winner={awayWin} logoUrl={match.awayTeamLogoUrl} />
    </Box>
  );
}

export function BracketTree({ matches }) {
  if (!matches || matches.length === 0) {
    return (
      <Typography color="text.secondary" sx={{ py: 4, textAlign: 'center' }}>
        No hay partidos de fase eliminatoria para mostrar.
      </Typography>
    );
  }

  // Agrupa por fase
  const byPhase = {};
  matches.forEach(function (m) {
    if (!m.phase) return;
    if (!byPhase[m.phase]) byPhase[m.phase] = [];
    byPhase[m.phase].push(m);
  });

  const phases = Object.keys(byPhase).sort(function (a, b) {
    return sortPhase(a) - sortPhase(b);
  });

  if (phases.length === 0) {
    return (
      <Typography color="text.secondary" sx={{ py: 4, textAlign: 'center' }}>
        La fase eliminatoria aún no ha sido sorteada.
      </Typography>
    );
  }

  return (
    <Box
      sx={{
        overflowX: 'auto',
        pb: 2,
      }}
    >
      <Box
        sx={{
          display: 'inline-flex',
          gap: 4,
          alignItems: 'center',
          minWidth: 'max-content',
        }}
      >
        {phases.map(function (phase, phaseIdx) {
          const phaseMatches = byPhase[phase];
          const isLast = phaseIdx === phases.length - 1;

          return (
            <Box key={phase} sx={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 0 }}>
              {/* Etiqueta de fase */}
              <Box
                sx={{
                  mb: 2,
                  px: 2,
                  py: 0.5,
                  bgcolor: isLast ? 'var(--sf-primary, #1B8A2E)' : 'background.paper',
                  border: '1px solid',
                  borderColor: isLast ? 'var(--sf-primary, #1B8A2E)' : 'divider',
                  borderRadius: 4,
                }}
              >
                <Typography
                  variant="overline"
                  sx={{
                    fontSize: '0.65rem',
                    fontWeight: 700,
                    color: isLast ? '#fff' : 'text.secondary',
                    letterSpacing: 1.5,
                  }}
                >
                  {nombreFase(phase)}
                </Typography>
              </Box>

              {/* Partidos de la fase */}
              <Box
                sx={{
                  display: 'flex',
                  flexDirection: 'column',
                  gap: isLast ? 0 : `${Math.pow(2, phaseIdx) * 20}px`,
                  alignItems: 'center',
                }}
              >
                {phaseMatches.map(function (m) {
                  return <BracketMatch key={m.id} match={m} />;
                })}
              </Box>
            </Box>
          );
        })}
      </Box>
    </Box>
  );
}

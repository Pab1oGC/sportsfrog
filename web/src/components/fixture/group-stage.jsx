/**
 * T-17.2 — Cuadrícula de partidos: Fase de Grupos.
 * Muestra tarjetas de partido agrupadas por ronda / grupo.
 */

import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Chip from '@mui/material/Chip';
import Avatar from '@mui/material/Avatar';
import Divider from '@mui/material/Divider';
import { fechaHora } from 'src/lib/format-date';

const SC = {
  scheduled: { color: '#64B5F6', label: 'Programado' },
  in_progress: { color: '#FFB74D', label: 'En curso' },
  finished: { color: '#81C784', label: 'Finalizado' },
  cancelled: { color: '#E57373', label: 'Cancelado' },
  walkover: { color: '#FFB74D', label: 'Walkover' },
  postponed: { color: '#B0BEC5', label: 'Aplazado' },
};

function MatchCard({ match }) {
  const st = SC[match.status] || { color: '#B0BEC5', label: match.status };
  const homeScore = match.homeTotal != null ? match.homeTotal : match.liveHomeTotal;
  const awayScore = match.awayTotal != null ? match.awayTotal : match.liveAwayTotal;
  const hasScore = homeScore != null && awayScore != null;
  const isLive = match.status === 'in_progress' && match.homeTotal == null;

  return (
    <Box
      sx={{
        bgcolor: 'background.paper',
        border: '1px solid',
        borderColor: 'divider',
        borderRadius: 2,
        p: 2,
        display: 'flex',
        flexDirection: 'column',
        gap: 1,
        position: 'relative',
        overflow: 'hidden',
        transition: 'box-shadow 0.2s',
        '&:hover': { boxShadow: '0 4px 20px rgba(0,0,0,0.1)' },
        '&::before': {
          content: '""',
          position: 'absolute',
          top: 0, left: 0, right: 0,
          height: 3,
          bgcolor: st.color,
        },
      }}
    >
      {/* Fecha / hora / cancha */}
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <Typography variant="caption" color="text.secondary">
          {match.scheduledAt ? fechaHora(match.scheduledAt) : 'Sin fecha'}
        </Typography>
        {match.venueName && (
          <Typography variant="caption" color="text.secondary" noWrap sx={{ maxWidth: 120 }}>
            {match.venueName}{match.spaceName ? ` · ${match.spaceName}` : ''}
          </Typography>
        )}
      </Box>

      {/* Equipos y marcador */}
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 1 }}>
        {/* Local */}
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, flex: 1, minWidth: 0 }}>
          <Avatar
            src={match.homeTeamLogoUrl || undefined}
            sx={{ width: 30, height: 30, fontSize: '0.65rem', bgcolor: 'primary.lighter', color: 'primary.main', flexShrink: 0 }}
          >
            {(match.homeTeamName || 'L')[0]}
          </Avatar>
          <Typography variant="body2" fontWeight={600} noWrap>
            {match.homeTeamName || 'Local'}
          </Typography>
        </Box>

        {/* Marcador */}
        <Box
          sx={{
            display: 'flex',
            alignItems: 'center',
            gap: 0.5,
            flexShrink: 0,
            px: 1.5,
            py: 0.5,
            bgcolor: hasScore ? 'background.default' : 'transparent',
            borderRadius: 1,
            border: hasScore ? '1px solid' : 'none',
            borderColor: 'divider',
          }}
        >
          {hasScore ? (
            <>
              <Typography variant="h6" fontWeight={700} color={isLive ? 'warning.main' : 'text.primary'}>
                {homeScore}
              </Typography>
              <Typography variant="body2" color="text.disabled" sx={{ mx: 0.25 }}>-</Typography>
              <Typography variant="h6" fontWeight={700} color={isLive ? 'warning.main' : 'text.primary'}>
                {awayScore}
              </Typography>
            </>
          ) : (
            <Typography variant="caption" color="text.disabled">VS</Typography>
          )}
        </Box>

        {/* Visitante */}
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, flex: 1, minWidth: 0, justifyContent: 'flex-end' }}>
          <Typography variant="body2" fontWeight={600} noWrap>
            {match.awayTeamName || 'Visitante'}
          </Typography>
          <Avatar
            src={match.awayTeamLogoUrl || undefined}
            sx={{ width: 30, height: 30, fontSize: '0.65rem', bgcolor: 'primary.lighter', color: 'primary.main', flexShrink: 0 }}
          >
            {(match.awayTeamName || 'V')[0]}
          </Avatar>
        </Box>
      </Box>

      {/* Estado + penales */}
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
        <Chip
          label={st.label}
          size="small"
          sx={{ bgcolor: `${st.color}22`, color: st.color, border: `1px solid ${st.color}44`, height: 20, fontSize: '0.65rem' }}
        />
        {match.penaltyHomeScore != null && (
          <Typography variant="caption" color="text.secondary">
            (pen {match.penaltyHomeScore}-{match.penaltyAwayScore})
          </Typography>
        )}
        {isLive && (
          <Chip label="EN VIVO" size="small" color="warning" sx={{ height: 18, fontSize: '0.6rem', ml: 0.5 }} />
        )}
      </Box>
    </Box>
  );
}

export function GroupStage({ matches }) {
  if (!matches || matches.length === 0) {
    return (
      <Typography color="text.secondary" sx={{ py: 4, textAlign: 'center' }}>
        No hay partidos de fase de grupos para mostrar.
      </Typography>
    );
  }

  // Agrupa por jornada
  const byRound = {};
  matches.forEach(function (m) {
    const round = m.roundNumber || 0;
    const key = m.groupLabel ? `${round}_${m.groupLabel}` : String(round);
    if (!byRound[key]) byRound[key] = { round, group: m.groupLabel, items: [] };
    byRound[key].items.push(m);
  });

  const rounds = Object.values(byRound).sort(function (a, b) {
    if (a.round !== b.round) return a.round - b.round;
    return (a.group || '').localeCompare(b.group || '');
  });

  return (
    <Box>
      {rounds.map(function (rg, i) {
        return (
          <Box key={i} sx={{ mb: 4 }}>
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5, mb: 2 }}>
              <Box sx={{ height: 2, flex: 1, bgcolor: 'divider' }} />
              <Typography variant="overline" color="text.secondary" sx={{ fontWeight: 700, whiteSpace: 'nowrap' }}>
                {rg.group ? `Grupo ${rg.group} — Jornada ${rg.round}` : `Jornada ${rg.round || 1}`}
              </Typography>
              <Box sx={{ height: 2, flex: 1, bgcolor: 'divider' }} />
            </Box>
            <Box
              sx={{
                display: 'grid',
                gridTemplateColumns: { xs: '1fr', sm: 'repeat(2, 1fr)', md: 'repeat(3, 1fr)' },
                gap: 2,
              }}
            >
              {rg.items.map(function (m) {
                return <MatchCard key={m.id} match={m} />;
              })}
            </Box>
          </Box>
        );
      })}
    </Box>
  );
}

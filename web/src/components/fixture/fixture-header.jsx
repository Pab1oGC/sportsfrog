/**
 * T-17 — Encabezado corporativo del Fixture Oficial.
 * Muestra el banner de la competencia, su nombre y los logos de sponsors.
 */

import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import { useTournamentTheme } from 'src/context/tournament-theme-context';

export function FixtureHeader({ competition }) {
  const { theme } = useTournamentTheme();

  const bannerUrl = theme.bannerUrl || competition?.publicPreview?.bannerUrl || null;
  const compName = competition?.name || theme.competitionName || 'Campeonato';
  const season = competition?.season || '';
  const sponsors = theme.sponsors?.length
    ? theme.sponsors
    : (competition?.settings?.public?.sponsors || []);

  return (
    <Box
      className="fixture-header no-print-nav"
      sx={{
        position: 'relative',
        width: '100%',
        minHeight: 140,
        borderRadius: 2,
        overflow: 'hidden',
        mb: 3,
        background: bannerUrl
          ? `linear-gradient(to right, rgba(0,0,0,0.55) 0%, rgba(0,0,0,0.0) 100%), url("${bannerUrl}") center/cover no-repeat`
          : `linear-gradient(135deg, var(--sf-primary, #1B8A2E) 0%, var(--sf-secondary, #0D4E1A) 100%)`,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        p: { xs: 2, md: 4 },
        gap: 2,
      }}
    >
      {/* Título */}
      <Box>
        <Typography
          variant="overline"
          sx={{ color: 'rgba(255,255,255,0.75)', letterSpacing: 3, fontSize: '0.65rem' }}
        >
          Fixture Oficial
        </Typography>
        <Typography
          variant="h4"
          fontWeight={800}
          sx={{
            color: '#fff',
            textShadow: '0 2px 8px rgba(0,0,0,0.4)',
            lineHeight: 1.1,
            fontSize: { xs: '1.4rem', md: '2rem' },
          }}
        >
          {compName}
        </Typography>
        {season && (
          <Typography
            variant="subtitle1"
            sx={{ color: 'rgba(255,255,255,0.8)', fontWeight: 500, mt: 0.5 }}
          >
            Temporada {season}
          </Typography>
        )}
      </Box>

      {/* Sponsors */}
      {sponsors.length > 0 && (
        <Box
          sx={{
            display: 'flex',
            alignItems: 'center',
            gap: 1.5,
            flexWrap: 'wrap',
            justifyContent: 'flex-end',
          }}
        >
          {sponsors.slice(0, 5).map(function (s, i) {
            return s.logoUrl ? (
              <Box
                key={i}
                component="img"
                src={s.logoUrl}
                alt={s.name || 'Sponsor'}
                sx={{
                  height: { xs: 32, md: 44 },
                  maxWidth: 100,
                  objectFit: 'contain',
                  filter: 'brightness(0) invert(1)',
                  opacity: 0.9,
                }}
              />
            ) : s.name ? (
              <Typography
                key={i}
                variant="caption"
                sx={{
                  color: 'rgba(255,255,255,0.8)',
                  border: '1px solid rgba(255,255,255,0.4)',
                  borderRadius: 1,
                  px: 1,
                  py: 0.25,
                  fontWeight: 600,
                  fontSize: '0.65rem',
                  letterSpacing: 1,
                }}
              >
                {s.name}
              </Typography>
            ) : null;
          })}
        </Box>
      )}
    </Box>
  );
}

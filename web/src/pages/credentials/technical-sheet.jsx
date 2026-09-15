/**
 * T-21.2 — Ficha Técnica Deportiva (individual y por equipo).
 * Tabla imprimible de nómina con datos de atleta, rol, grupo sanguíneo y validación médica.
 */

import { forwardRef } from 'react';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Table from '@mui/material/Table';
import TableBody from '@mui/material/TableBody';
import TableCell from '@mui/material/TableCell';
import TableHead from '@mui/material/TableHead';
import TableRow from '@mui/material/TableRow';
import Chip from '@mui/material/Chip';
import Avatar from '@mui/material/Avatar';
import { useTournamentTheme } from 'src/context/tournament-theme-context';

const ROLE_LABELS = {
  player: 'Jugador',
  captain: 'Capitán',
  coach: 'Entrenador',
  assistant_coach: 'Asistente Técnico',
  goalkeeper: 'Portero',
  manager: 'Delegado',
};

const ROLE_COLORS = {
  captain: '#FFC107',
  coach: '#7E57C2',
  assistant_coach: '#AB47BC',
  manager: '#29B6F6',
};

function RoleChip({ role }) {
  const label = ROLE_LABELS[role] || role;
  const color = ROLE_COLORS[role];
  return (
    <Chip
      label={label}
      size="small"
      sx={{
        height: 18,
        fontSize: '0.6rem',
        fontWeight: 700,
        bgcolor: color ? `${color}22` : 'default',
        color: color || 'text.primary',
        border: color ? `1px solid ${color}44` : 'none',
      }}
    />
  );
}

function MedicalBadge({ cleared }) {
  return (
    <Box
      sx={{
        width: 20,
        height: 20,
        borderRadius: '50%',
        bgcolor: cleared ? '#81C784' : '#E57373',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        fontSize: '0.6rem',
        fontWeight: 700,
        color: '#fff',
        flexShrink: 0,
      }}
    >
      {cleared ? '✓' : '✗'}
    </Box>
  );
}

export const TechnicalSheet = forwardRef(function TechnicalSheet(
  { team, roster, competition, category },
  ref
) {
  const { theme } = useTournamentTheme();
  const primary = theme?.primaryColor || '#1B8A2E';
  const secondary = theme?.secondaryColor || '#0D4E1A';
  const banner = theme?.bannerUrl;

  const active = (roster || []).filter(function (e) { return !e.withdrawnAt; });
  const now = new Date().toLocaleDateString('es-BO');

  return (
    <Box
      ref={ref}
      sx={{
        width: '100%',
        maxWidth: 800,
        bgcolor: '#fff',
        borderRadius: 2,
        overflow: 'hidden',
        fontFamily: 'Inter, sans-serif',
        border: '1px solid #e0e0e0',
      }}
    >
      {/* Encabezado corporativo */}
      <Box
        sx={{
          minHeight: 80,
          background: banner
            ? `linear-gradient(to right, ${primary}cc, ${secondary}cc), url("${banner}") center/cover no-repeat`
            : `linear-gradient(135deg, ${primary} 0%, ${secondary} 100%)`,
          p: 2.5,
          display: 'flex',
          alignItems: 'center',
          gap: 2,
        }}
      >
        {theme?.logoUrl && (
          <Box component="img" src={theme.logoUrl} sx={{ height: 52, width: 52, objectFit: 'contain', flexShrink: 0 }} />
        )}
        <Box sx={{ flex: 1 }}>
          <Typography sx={{ color: 'rgba(255,255,255,0.75)', fontSize: '0.6rem', letterSpacing: 2, textTransform: 'uppercase' }}>
            Ficha Técnica Oficial
          </Typography>
          <Typography sx={{ color: '#fff', fontWeight: 800, fontSize: '1.1rem', lineHeight: 1.1 }}>
            {team?.name || 'Equipo'}
          </Typography>
          <Typography sx={{ color: 'rgba(255,255,255,0.8)', fontSize: '0.7rem', mt: 0.25 }}>
            {competition?.name} {competition?.season ? `· ${competition.season}` : ''} {category?.name ? `· ${category.name}` : ''}
          </Typography>
        </Box>
        <Box sx={{ textAlign: 'right' }}>
          <Typography sx={{ color: 'rgba(255,255,255,0.7)', fontSize: '0.6rem' }}>
            Emitido: {now}
          </Typography>
          <Typography sx={{ color: 'rgba(255,255,255,0.5)', fontSize: '0.55rem' }}>
            Total: {active.length} activos
          </Typography>
        </Box>
      </Box>

      {/* Info del equipo */}
      {team && (
        <Box sx={{ display: 'flex', gap: 3, px: 3, py: 1.5, bgcolor: `${primary}08`, borderBottom: '1px solid', borderColor: '#e0e0e0' }}>
          {team.clubName && (
            <Box>
              <Typography sx={{ fontSize: '0.6rem', color: '#888', textTransform: 'uppercase', letterSpacing: 1 }}>Club</Typography>
              <Typography sx={{ fontSize: '0.75rem', fontWeight: 600 }}>{team.clubName}</Typography>
            </Box>
          )}
          {team.groupLabel && (
            <Box>
              <Typography sx={{ fontSize: '0.6rem', color: '#888', textTransform: 'uppercase', letterSpacing: 1 }}>Grupo</Typography>
              <Typography sx={{ fontSize: '0.75rem', fontWeight: 600 }}>{team.groupLabel}</Typography>
            </Box>
          )}
        </Box>
      )}

      {/* Tabla de nómina */}
      <Table size="small">
        <TableHead>
          <TableRow sx={{ '& th': { bgcolor: `${primary}0f`, fontWeight: 700, fontSize: '0.65rem', color: primary, borderBottom: `2px solid ${primary}33` } }}>
            <TableCell sx={{ width: 40 }}>#</TableCell>
            <TableCell sx={{ width: 44 }}>Foto</TableCell>
            <TableCell>Apellido, Nombre</TableCell>
            <TableCell>Documento</TableCell>
            <TableCell>Rol</TableCell>
            <TableCell>Dorsal</TableCell>
            <TableCell>Sangre</TableCell>
            <TableCell sx={{ width: 60, textAlign: 'center' }}>Médico</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {active.map(function (entry, i) {
            const name = `${entry.lastName || ''}, ${entry.firstName || ''}`.trim();
            return (
              <TableRow
                key={entry.id}
                sx={{
                  '&:nth-of-type(even)': { bgcolor: '#fafafa' },
                  '& td': { fontSize: '0.72rem', py: 0.75, borderBottom: '1px solid #f0f0f0' },
                }}
              >
                <TableCell sx={{ color: '#888', fontWeight: 600 }}>{i + 1}</TableCell>
                <TableCell>
                  <Avatar src={entry.photoUrl || undefined} sx={{ width: 28, height: 28, fontSize: '0.6rem', bgcolor: `${primary}22`, color: primary }}>
                    {name[0]}
                  </Avatar>
                </TableCell>
                <TableCell sx={{ fontWeight: 600 }}>{name || '—'}</TableCell>
                <TableCell sx={{ fontFamily: 'monospace', color: '#555' }}>{entry.documentId || '—'}</TableCell>
                <TableCell><RoleChip role={entry.position || 'player'} /></TableCell>
                <TableCell sx={{ fontWeight: 700, color: primary }}>
                  {entry.jerseyNumber ? `#${entry.jerseyNumber}` : '—'}
                </TableCell>
                <TableCell sx={{ fontFamily: 'monospace', fontWeight: 600 }}>{entry.bloodType || '?'}</TableCell>
                <TableCell sx={{ textAlign: 'center' }}>
                  <MedicalBadge cleared={entry.medicalCleared} />
                </TableCell>
              </TableRow>
            );
          })}
          {active.length === 0 && (
            <TableRow>
              <TableCell colSpan={8} sx={{ textAlign: 'center', color: '#888', py: 3, fontSize: '0.75rem' }}>
                No hay integrantes activos en esta nómina.
              </TableCell>
            </TableRow>
          )}
        </TableBody>
      </Table>

      {/* Pie de página con firmas */}
      <Box sx={{ px: 3, py: 2, borderTop: '1px solid #e0e0e0' }}>
        <Box sx={{ display: 'flex', justifyContent: 'space-between', mt: 3 }}>
          {['Delegado del equipo', 'Director Técnico', 'Oficial de competencia'].map(function (label) {
            return (
              <Box key={label} sx={{ textAlign: 'center' }}>
                <Box sx={{ width: 120, height: 1, bgcolor: '#333', mx: 'auto', mb: 0.5 }} />
                <Typography sx={{ fontSize: '0.55rem', color: '#888' }}>{label}</Typography>
              </Box>
            );
          })}
        </Box>
      </Box>
    </Box>
  );
});

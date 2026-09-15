/**
 * T-21.1 — Credencial Deportiva (doble cara).
 * Anverso: foto, nombre, categoría, disciplina, equipo, código QR.
 * Reverso: normas del evento y línea de firma de acreditación.
 */

import { forwardRef } from 'react';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Avatar from '@mui/material/Avatar';
import Divider from '@mui/material/Divider';
import { QRCodeSVG } from 'qrcode.react';
import { useTournamentTheme } from 'src/context/tournament-theme-context';

const CARD_W = 340;
const CARD_H = 210;

// ─── Anverso ─────────────────────────────────────────────────────────────────

function CredentialFront({ athlete, competition, theme }) {
  const primary = theme?.primaryColor || '#1B8A2E';
  const secondary = theme?.secondaryColor || '#0D4E1A';
  const banner = theme?.bannerUrl;

  const verifyUrl = athlete.serialNumber
    ? `${window.location.origin}/public/verify/sportfrog/${athlete.serialNumber}`
    : window.location.origin;

  return (
    <Box
      sx={{
        width: CARD_W,
        height: CARD_H,
        borderRadius: 2,
        overflow: 'hidden',
        position: 'relative',
        bgcolor: '#fff',
        fontFamily: 'Inter, sans-serif',
        boxShadow: '0 4px 20px rgba(0,0,0,0.15)',
        display: 'flex',
        flexDirection: 'column',
        flexShrink: 0,
      }}
    >
      {/* Cabecera */}
      <Box
        sx={{
          height: 68,
          background: banner
            ? `linear-gradient(to right, ${primary}cc, ${secondary}cc), url("${banner}") center/cover no-repeat`
            : `linear-gradient(135deg, ${primary} 0%, ${secondary} 100%)`,
          display: 'flex',
          alignItems: 'center',
          px: 2,
          gap: 1.5,
        }}
      >
        {theme?.logoUrl && (
          <Box
            component="img"
            src={theme.logoUrl}
            sx={{ height: 40, maxWidth: 50, objectFit: 'contain', filter: 'brightness(0) invert(1)', opacity: 0.9 }}
          />
        )}
        <Box sx={{ flex: 1, minWidth: 0 }}>
          <Typography
            sx={{ color: '#fff', fontWeight: 800, fontSize: '0.7rem', letterSpacing: 1.5, lineHeight: 1, textTransform: 'uppercase' }}
          >
            {competition?.name || 'Campeonato'}
          </Typography>
          <Typography sx={{ color: 'rgba(255,255,255,0.8)', fontSize: '0.58rem', letterSpacing: 0.5 }}>
            {competition?.season || ''}
          </Typography>
        </Box>
        <Typography
          sx={{
            color: '#fff',
            fontWeight: 900,
            fontSize: '0.55rem',
            letterSpacing: 2,
            textTransform: 'uppercase',
            bgcolor: 'rgba(255,255,255,0.2)',
            px: 0.75,
            py: 0.25,
            borderRadius: 0.5,
          }}
        >
          CREDENTIAL
        </Typography>
      </Box>

      {/* Cuerpo */}
      <Box sx={{ display: 'flex', flex: 1, p: 1.5, gap: 1.5 }}>
        {/* Foto */}
        <Avatar
          src={athlete.photoUrl || undefined}
          sx={{
            width: 72,
            height: 90,
            borderRadius: 1.5,
            bgcolor: `${primary}22`,
            color: primary,
            fontSize: '1.6rem',
            fontWeight: 700,
            flexShrink: 0,
            border: `2px solid ${primary}`,
          }}
        >
          {athlete.firstName?.[0]}{athlete.lastName?.[0]}
        </Avatar>

        {/* Datos */}
        <Box sx={{ flex: 1, minWidth: 0, display: 'flex', flexDirection: 'column', justifyContent: 'center', gap: 0.3 }}>
          <Typography sx={{ fontWeight: 800, fontSize: '0.9rem', lineHeight: 1.1, color: '#111' }}>
            {athlete.lastName?.toUpperCase()}, {athlete.firstName}
          </Typography>
          <Typography sx={{ fontSize: '0.62rem', color: primary, fontWeight: 600, letterSpacing: 0.5 }}>
            {athlete.teamName || '—'}
          </Typography>
          <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 0.5, mt: 0.5 }}>
            {athlete.categoryName && (
              <Box sx={{ px: 0.75, py: 0.2, bgcolor: `${primary}18`, borderRadius: 0.5, fontSize: '0.55rem', fontWeight: 700, color: primary }}>
                {athlete.categoryName}
              </Box>
            )}
            {athlete.position && (
              <Box sx={{ px: 0.75, py: 0.2, bgcolor: '#f0f0f0', borderRadius: 0.5, fontSize: '0.55rem', fontWeight: 600, color: '#555' }}>
                {athlete.position}
              </Box>
            )}
            {athlete.jerseyNumber && (
              <Box sx={{ px: 0.75, py: 0.2, bgcolor: '#f0f0f0', borderRadius: 0.5, fontSize: '0.55rem', fontWeight: 600, color: '#555' }}>
                #{athlete.jerseyNumber}
              </Box>
            )}
          </Box>
          <Typography sx={{ fontSize: '0.58rem', color: '#888', mt: 0.5 }}>
            Doc: {athlete.documentId || '—'}
          </Typography>
        </Box>

        {/* QR */}
        <Box
          sx={{
            display: 'flex',
            flexDirection: 'column',
            alignItems: 'center',
            justifyContent: 'center',
            gap: 0.5,
            flexShrink: 0,
          }}
        >
          <QRCodeSVG value={verifyUrl} size={60} level="M" />
          <Typography sx={{ fontSize: '0.48rem', color: '#aaa', letterSpacing: 0.3 }}>
            Verificar
          </Typography>
        </Box>
      </Box>

      {/* Pie */}
      <Box
        sx={{
          bgcolor: `${primary}12`,
          borderTop: `2px solid ${primary}`,
          px: 2,
          py: 0.5,
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
        }}
      >
        <Typography sx={{ fontSize: '0.52rem', color: '#888', fontFamily: 'monospace' }}>
          {athlete.serialNumber || 'SF-0000-0000'}
        </Typography>
        <Box sx={{ display: 'flex', gap: 0.75 }}>
          {(athlete.validFrom || athlete.validTo) && (
            <Typography sx={{ fontSize: '0.52rem', color: '#888' }}>
              {athlete.validFrom} — {athlete.validTo}
            </Typography>
          )}
        </Box>
      </Box>
    </Box>
  );
}

// ─── Reverso ─────────────────────────────────────────────────────────────────

function CredentialBack({ competition, theme }) {
  const primary = theme?.primaryColor || '#1B8A2E';
  const rules = competition?.settings?.public?.credentialRules || [
    'Portar la credencial visible en todo momento.',
    'No es transferible ni canjeable.',
    'En caso de pérdida, reportar inmediatamente.',
    'Válida únicamente para el evento indicado.',
  ];

  return (
    <Box
      sx={{
        width: CARD_W,
        height: CARD_H,
        borderRadius: 2,
        overflow: 'hidden',
        bgcolor: '#fff',
        fontFamily: 'Inter, sans-serif',
        boxShadow: '0 4px 20px rgba(0,0,0,0.15)',
        display: 'flex',
        flexDirection: 'column',
        p: 2,
        flexShrink: 0,
      }}
    >
      <Typography sx={{ fontWeight: 800, fontSize: '0.7rem', color: primary, letterSpacing: 1, textTransform: 'uppercase', mb: 1 }}>
        Normas del Evento
      </Typography>
      <Box sx={{ flex: 1 }}>
        {rules.map(function (rule, i) {
          return (
            <Box key={i} sx={{ display: 'flex', gap: 0.75, mb: 0.75 }}>
              <Typography sx={{ fontSize: '0.6rem', color: primary, fontWeight: 800, flexShrink: 0 }}>
                {i + 1}.
              </Typography>
              <Typography sx={{ fontSize: '0.6rem', color: '#333', lineHeight: 1.4 }}>
                {rule}
              </Typography>
            </Box>
          );
        })}
      </Box>
      <Divider sx={{ my: 1 }} />
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-end' }}>
        <Box>
          <Box sx={{ width: 120, height: 1, bgcolor: '#333', mb: 0.25 }} />
          <Typography sx={{ fontSize: '0.55rem', color: '#888' }}>
            Firma autorización
          </Typography>
        </Box>
        <Box sx={{ textAlign: 'right' }}>
          <Typography sx={{ fontSize: '0.55rem', color: '#aaa' }}>
            {competition?.name || 'SportFrog'}
          </Typography>
          <Typography sx={{ fontSize: '0.5rem', color: '#ccc' }}>
            Acreditación oficial
          </Typography>
        </Box>
      </Box>
    </Box>
  );
}

// ─── Componente público ───────────────────────────────────────────────────────

export const CredentialCard = forwardRef(function CredentialCard(
  { athlete, competition, showBack = false },
  ref
) {
  const { theme } = useTournamentTheme();

  return (
    <Box ref={ref} sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
      <CredentialFront athlete={athlete} competition={competition} theme={theme} />
      {showBack && <CredentialBack competition={competition} theme={theme} />}
    </Box>
  );
});

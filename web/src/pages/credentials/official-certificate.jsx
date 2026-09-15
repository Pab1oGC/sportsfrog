/**
 * T-21.3 — Generador de Certificados Oficiales.
 * Plantilla configurable para: participación, 1er/2do/3er lugar y mención de honor.
 */

import { forwardRef } from 'react';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import { useTournamentTheme } from 'src/context/tournament-theme-context';

const CERT_TYPES = {
  participation: {
    headline: 'CERTIFICADO DE PARTICIPACIÓN',
    verb: 'ha participado en',
    badge: '⭐',
    gradient: ['#1B8A2E', '#0D4E1A'],
  },
  first_place: {
    headline: 'CERTIFICADO DE PRIMER LUGAR',
    verb: 'obtiene el primer lugar en',
    badge: '🥇',
    gradient: ['#F5A623', '#C87F0A'],
  },
  second_place: {
    headline: 'CERTIFICADO DE SEGUNDO LUGAR',
    verb: 'obtiene el segundo lugar en',
    badge: '🥈',
    gradient: ['#607D8B', '#37474F'],
  },
  third_place: {
    headline: 'CERTIFICADO DE TERCER LUGAR',
    verb: 'obtiene el tercer lugar en',
    badge: '🥉',
    gradient: ['#8D6E63', '#5D4037'],
  },
  honorable_mention: {
    headline: 'MENCIÓN DE HONOR',
    verb: 'recibe una mención de honor en',
    badge: '🎖️',
    gradient: ['#7E57C2', '#4527A0'],
  },
};

// Sello institucional vectorial
function InstitutionalSeal({ color, size = 70 }) {
  return (
    <Box
      sx={{
        width: size,
        height: size,
        borderRadius: '50%',
        border: `3px solid ${color}`,
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center',
        position: 'relative',
        flexShrink: 0,
      }}
    >
      <Box
        sx={{
          width: size - 12,
          height: size - 12,
          borderRadius: '50%',
          border: `1px solid ${color}55`,
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
        }}
      >
        <Typography sx={{ fontSize: `${size * 0.22}px`, fontWeight: 900, color, letterSpacing: 0.5, textAlign: 'center', lineHeight: 1 }}>
          S.F.
        </Typography>
      </Box>
      {/* Texto circular decorativo */}
      <Box
        component="svg"
        viewBox="0 0 100 100"
        width={size}
        height={size}
        sx={{ position: 'absolute', top: 0, left: 0 }}
      >
        <path id="circle-text" d="M50,10 a40,40 0 1,1 -0.1,0" fill="none" />
        <text fontSize="9" fill={color} opacity="0.8">
          <textPath href="#circle-text" startOffset="5%">
            SPORTFROG • ACREDITACIÓN OFICIAL •
          </textPath>
        </text>
      </Box>
    </Box>
  );
}

// Ornamento decorativo
function OrnamentLine({ color }) {
  return (
    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5, my: 0.5 }}>
      <Box sx={{ flex: 1, height: 1, bgcolor: `${color}40` }} />
      <Box sx={{ width: 6, height: 6, transform: 'rotate(45deg)', bgcolor: color, opacity: 0.6 }} />
      <Box sx={{ flex: 1, height: 1, bgcolor: `${color}40` }} />
    </Box>
  );
}

export const OfficialCertificate = forwardRef(function OfficialCertificate(
  { recipientName, recipientCategory, certType = 'participation', competition, category, customText, signatories = [] },
  ref
) {
  const { theme } = useTournamentTheme();
  const config = CERT_TYPES[certType] || CERT_TYPES.participation;
  const [grad1, grad2] = config.gradient;
  const primary = theme?.primaryColor || grad1;
  const banner = theme?.bannerUrl;
  const now = new Date().toLocaleDateString('es-BO', { year: 'numeric', month: 'long', day: 'numeric' });

  const defaultSignatories = [
    { title: 'Presidente', name: '' },
    { title: 'Director Técnico', name: '' },
  ];
  const signs = signatories.length > 0 ? signatories : defaultSignatories;

  return (
    <Box
      ref={ref}
      sx={{
        width: 794, // A4 landscape ≈ 794px @ 96dpi
        minHeight: 560,
        bgcolor: '#fff',
        position: 'relative',
        overflow: 'hidden',
        fontFamily: '"Georgia", "Times New Roman", serif',
        border: '12px solid', // borde exterior
        borderColor: primary,
        outline: `4px solid ${primary}33`,
        outlineOffset: '-16px',
        display: 'flex',
        flexDirection: 'column',
      }}
    >
      {/* Fondo decorativo sutil */}
      <Box
        sx={{
          position: 'absolute',
          inset: 0,
          background: `radial-gradient(ellipse at 50% 50%, ${primary}08 0%, transparent 70%)`,
          pointerEvents: 'none',
        }}
      />

      {/* Header con banner y logos */}
      <Box
        sx={{
          minHeight: 90,
          background: banner
            ? `linear-gradient(to right, ${primary}ee, ${grad2}ee), url("${banner}") center/cover no-repeat`
            : `linear-gradient(135deg, ${primary} 0%, ${grad2} 100%)`,
          px: 4,
          py: 2,
          display: 'flex',
          alignItems: 'center',
          gap: 3,
        }}
      >
        {theme?.logoUrl && (
          <Box component="img" src={theme.logoUrl}
            sx={{ height: 60, maxWidth: 70, objectFit: 'contain', filter: 'brightness(0) invert(1)', opacity: 0.9, flexShrink: 0 }}
          />
        )}
        <Box sx={{ flex: 1, textAlign: 'center' }}>
          <Typography sx={{ color: 'rgba(255,255,255,0.8)', fontSize: '0.65rem', letterSpacing: 3, textTransform: 'uppercase', fontFamily: 'Inter, sans-serif' }}>
            {competition?.name || 'SportFrog'}
          </Typography>
          <Typography sx={{ color: '#fff', fontWeight: 900, fontSize: '1.3rem', letterSpacing: 2, textTransform: 'uppercase', fontFamily: 'Inter, sans-serif' }}>
            {config.headline}
          </Typography>
        </Box>
        <InstitutionalSeal color="rgba(255,255,255,0.85)" size={72} />
      </Box>

      {/* Cuerpo principal */}
      <Box sx={{ flex: 1, display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', px: 6, py: 3, textAlign: 'center' }}>

        <Typography sx={{ fontSize: '3.5rem', lineHeight: 1, mb: 1 }}>
          {config.badge}
        </Typography>

        <Typography sx={{ fontSize: '0.75rem', color: '#888', letterSpacing: 3, textTransform: 'uppercase', fontFamily: 'Inter, sans-serif', mb: 0.5 }}>
          La organización certifica que
        </Typography>

        <OrnamentLine color={primary} />

        <Typography
          sx={{
            fontSize: '2rem',
            fontWeight: 900,
            color: primary,
            letterSpacing: 1.5,
            lineHeight: 1.1,
            mt: 0.5,
            mb: 0.5,
            textTransform: 'uppercase',
            fontFamily: '"Georgia", serif',
          }}
        >
          {recipientName || 'Nombre del Deportista'}
        </Typography>

        {recipientCategory && (
          <Typography sx={{ fontSize: '0.8rem', color: '#888', fontFamily: 'Inter, sans-serif', mb: 0.5 }}>
            {recipientCategory}
          </Typography>
        )}

        <OrnamentLine color={primary} />

        <Typography sx={{ fontSize: '0.9rem', color: '#444', mt: 1, lineHeight: 1.8, maxWidth: 480, fontFamily: '"Georgia", serif' }}>
          {customText || (
            <>
              {config.verb} <strong>{competition?.name || 'el campeonato'}</strong>
              {category?.name ? `, categoría ${category.name}` : ''},
              celebrado en {competition?.season || new Date().getFullYear()}.
            </>
          )}
        </Typography>

        <Typography sx={{ fontSize: '0.7rem', color: '#aaa', mt: 2, fontFamily: 'Inter, sans-serif' }}>
          Emitido el {now}
        </Typography>
      </Box>

      {/* Líneas de firma */}
      <Box
        sx={{
          px: 4,
          pb: 3,
          pt: 1,
          borderTop: `2px solid ${primary}20`,
          display: 'flex',
          justifyContent: 'space-around',
          gap: 2,
        }}
      >
        {signs.map(function (s, i) {
          return (
            <Box key={i} sx={{ textAlign: 'center', flex: 1 }}>
              {s.name && (
                <Typography sx={{ fontFamily: '"Georgia", serif', fontStyle: 'italic', fontSize: '0.85rem', color: '#333', mb: 0.25 }}>
                  {s.name}
                </Typography>
              )}
              <Box sx={{ width: 140, height: 1, bgcolor: '#333', mx: 'auto', mb: 0.5 }} />
              <Typography sx={{ fontSize: '0.6rem', color: '#888', letterSpacing: 0.5, fontFamily: 'Inter, sans-serif', textTransform: 'uppercase' }}>
                {s.title}
              </Typography>
            </Box>
          );
        })}
        <Box sx={{ display: 'flex', alignItems: 'flex-end', pb: 0.5, flexShrink: 0 }}>
          <InstitutionalSeal color={`${primary}55`} size={56} />
        </Box>
      </Box>
    </Box>
  );
});

export { CERT_TYPES };

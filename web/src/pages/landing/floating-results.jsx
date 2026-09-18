import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';

/**
 * La cinta inferior tipo tablero de estadio: nombres reales de competencias
 * en scroll infinito. El truco de siempre -- el mismo texto repetido dos
 * veces adentro de una caja que se traslada exactamente -50% de su propio
 * ancho, asi que cuando la primera copia termina de salir la segunda ya esta
 * exactamente donde empezo la primera. Sin datos reales cae a una frase de
 * marca generica en vez de dejar la cinta vacia.
 */
export function Marquesina({ resultados }) {
  var frases = Array.from(new Set((resultados || [])
    .map(function(r) { return r.competitionName + ' — ' + r.sportName; })))
    .slice(0, 8);

  if (frases.length === 0) {
    frases = ['SPORTFROG', 'MULTIDEPORTE', 'EN VIVO', 'BOLIVIA'];
  }

  var texto = frases.join('   ★   ') + '   ★   ';

  return (
    <Box
      className="hero-marquesina"
      sx={{
        position: 'relative', zIndex: 3, bgcolor: '#1D709F', color: '#ffffff',
        py: 1.1, overflow: 'hidden', whiteSpace: 'nowrap', borderTop: '2px solid #00A4D1',
      }}
    >
      <Box
        sx={{
          display: 'inline-block',
          animation: 'sf-marquesina 26s linear infinite',
          '@keyframes sf-marquesina': { from: { transform: 'translateX(0)' }, to: { transform: 'translateX(-50%)' } },
          '@media (prefers-reduced-motion: reduce)': { animation: 'none' },
        }}
      >
        <Typography component="span" sx={{ fontWeight: 700, letterSpacing: 1, fontSize: '0.8rem', px: 1.5 }}>
          {texto}{texto}
        </Typography>
      </Box>
    </Box>
  );
}

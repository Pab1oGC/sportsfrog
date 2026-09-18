import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import IconButton from '@mui/material/IconButton';
import Typography from '@mui/material/Typography';
import { alpha } from '@mui/material/styles';
import { Iconify } from 'src/components/iconify';
import { ESTADO, HeroChip, LiveBadge, CuentaAtras } from './hero-parts';

/**
 * La variante `editorial` -- composición asimétrica tipo revista: una regla
 * corta antes del eyebrow, título grande con mucho aire, la descripción
 * como bajada. El cluster de estado se reduce a una sola línea discreta --
 * la estética editorial pide contención, no más badges que la de
 * `standard`.
 */
export function EditorialHero(props) {
  var data = props.data;
  var comp = data.comp;
  var portal = data.portal;
  var moment = data.moment;
  var alFrente = data.alFrente;
  var colorTexto = data.colorTexto;
  var social = data.social;
  var campeon = data.campeon;
  var enVivo = data.enVivo;
  var proximoPartido = data.proximoPartido;
  var eyebrow = data.eyebrow;

  return (
    <Box sx={{ maxWidth: 720 }}>
      {props.onBack && (
        <Button
          size="small"
          sx={{ color: colorTexto, mb: 1.5, opacity: 0.85 }}
          onClick={props.onBack}
          startIcon={<Iconify icon="eva:arrow-back-outline" />}
        >
          Volver
        </Button>
      )}
      <Box sx={{ width: 48, height: 3, bgcolor: 'currentColor', opacity: 0.55, mb: 1.5 }} />
      <Typography variant="overline" sx={{ display: 'block', fontWeight: 700, letterSpacing: 1.6, opacity: 0.8, fontSize: 11 }}>
        {eyebrow}
      </Typography>
      <Typography
        variant="h2"
        fontWeight={800}
        sx={{
          lineHeight: 0.98, letterSpacing: -1, my: 1,
          fontSize: props.dense ? { xs: '1.5rem', sm: '2rem' } : { xs: '1.9rem', sm: '2.6rem', md: '3.4rem' },
        }}
      >
        {comp.name || 'Nombre de la competencia'}
      </Typography>
      {portal.description && (
        <Typography variant="body1" sx={{ fontStyle: 'italic', opacity: 0.92, maxWidth: 520, mb: 2 }}>
          {portal.description}
        </Typography>
      )}
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5, flexWrap: 'wrap' }}>
        {enVivo && <LiveBadge count={moment.liveMatchCount} />}
        {!enVivo && proximoPartido && <CuentaAtras targetIso={proximoPartido} fg={alFrente} />}
        {comp.status && <HeroChip label={ESTADO[comp.status] || comp.status} fg={alFrente} />}
        {campeon && (
          <Typography variant="body2" sx={{ fontWeight: 700, letterSpacing: 0.3 }}>
            🏆 {campeon.teamName}
          </Typography>
        )}
      </Box>
      {social.length > 0 && (
        <Box sx={{ display: 'flex', gap: 1, mt: 2 }}>
          {social.map(function(s) {
            return (
              <IconButton
                key={s.key}
                size="small"
                component="a"
                href={s.href}
                target="_blank"
                rel="noopener noreferrer"
                sx={{ color: colorTexto, bgcolor: (t) => alpha(alFrente(t), 0.15) }}
              >
                <Iconify icon={s.icon} width={16} />
              </IconButton>
            );
          })}
        </Box>
      )}
    </Box>
  );
}

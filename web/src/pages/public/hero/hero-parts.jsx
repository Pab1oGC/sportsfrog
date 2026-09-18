import { useState, useEffect, useId } from 'react';
import Box from '@mui/material/Box';
import Chip from '@mui/material/Chip';
import { alpha } from '@mui/material/styles';
import { Iconify } from 'src/components/iconify';
import { LivePulse } from 'src/components/live-pulse';

/* ---------------------------------------------------------------------------
   Piezas que comparten las cuatro variantes del hero (standard, scoreboard,
   editorial, live) -- vive acá y no dentro de cada variante para que cambiar
   el pulso de "EN VIVO" o la cuenta regresiva se haga una sola vez, no
   cuatro. Cada variante decide cómo ordenarlas, nunca cómo se ven.
   --------------------------------------------------------------------------- */

export var ESTADO = { draft: 'Borrador', scheduled: 'Programada', in_progress: 'En curso', finished: 'Finalizada', cancelled: 'Cancelada' };
export var FORMATO = { league: 'Todos vs todos', knockout: 'Eliminacion', groups: 'Grupos' };

export function HeroChip(props) {
  var fg = props.fg;
  return (
    <Chip
      label={props.label}
      size="small"
      icon={props.icon ? <Iconify icon={props.icon} width={14} /> : undefined}
      sx={{
        bgcolor: (t) => alpha(fg(t), 0.28),
        color: (t) => fg(t),
        fontWeight: 700,
        border: '1px solid',
        borderColor: (t) => alpha(fg(t), 0.4),
        '& .MuiChip-icon': { color: 'inherit' },
      }}
    />
  );
}

/**
 * El badge "EN VIVO". A propósito no deriva su color de `fg` como HeroChip
 * -- "en vivo" es rojo siempre, sobre cualquier estilo o variante de
 * portada, igual que un marcador de transmisión deportiva. error.contrastText
 * ya viene calculado por el tema del portal para tener contraste garantizado
 * contra error.main en cualquier modo.
 */
export function LiveBadge(props) {
  var label = 'EN VIVO' + (props.count > 1 ? ' · ' + props.count + ' partidos' : '');
  return (
    <Box sx={{ display: 'inline-flex', px: 1.25, py: 0.5, borderRadius: 5, bgcolor: 'error.main', color: 'error.contrastText' }}>
      <LivePulse label={label} />
    </Box>
  );
}

/**
 * Cuánto falta para el próximo partido, en minutos redondeados hacia el
 * cuarto de hora que sea legible ("3 días", "2h 30m") — no segundo a
 * segundo, que en un cover no aporta y solo redibuja de más. Se actualiza
 * cada minuto por si alguien deja la pestaña abierta.
 */
export function CuentaAtras(props) {
  var [ahora, setAhora] = useState(function() { return Date.now(); });

  useEffect(function() {
    var id = setInterval(function() { setAhora(Date.now()); }, 60000);
    return function() { clearInterval(id); };
  }, []);

  var restanteMin = Math.floor((new Date(props.targetIso).getTime() - ahora) / 60000);
  // Ya deberia haber arrancado (el visitante llego con la pestaña abierta
  // desde antes, o el reloj del servidor y el del navegador no coinciden
  // por unos segundos): no se muestra una cuenta en negativo.
  if (restanteMin <= 0) return null;

  var dias = Math.floor(restanteMin / 1440);
  var horas = Math.floor((restanteMin % 1440) / 60);
  var minutos = restanteMin % 60;

  var texto;
  if (dias > 0) texto = 'Empieza en ' + dias + (dias === 1 ? ' día' : ' días');
  else if (horas > 0) texto = 'Empieza en ' + horas + 'h' + (minutos > 0 ? ' ' + minutos + 'm' : '');
  else texto = 'Empieza en ' + minutos + ' min';

  return <HeroChip label={texto} fg={props.fg} icon="mdi:timer-outline" />;
}

/**
 * La textura detrás del texto de la portada -- un solo motivo (líneas
 * diagonales), dos intensidades. `currentColor` toma el `color` del `Box`
 * padre (`colorTexto`), así que siempre queda legible con el mismo criterio
 * de contraste que ya usan HeroChip y el badge de campeón, sin que este
 * componente tenga que conocerlo. El id del patrón se genera por instancia
 * (useId) para no colisionar si el hero llegara a montarse más de una vez
 * en la misma página. Compartida por las cuatro variantes -- el dispatcher
 * (portal-hero.jsx) la dibuja una sola vez, detrás de cualquiera de ellas.
 */
export function HeroDecoration(props) {
  var uid = useId();
  if (!props.level || props.level === 'none') return null;

  var opacity = props.level === 'bold' ? 0.16 : 0.07;
  var patternId = 'sf-hero-lines-' + uid;

  return (
    <Box
      aria-hidden="true"
      sx={{ position: 'absolute', inset: 0, overflow: 'hidden', pointerEvents: 'none', color: 'inherit', opacity: opacity }}
    >
      <svg width="100%" height="100%" preserveAspectRatio="none">
        <defs>
          <pattern id={patternId} width="26" height="26" patternUnits="userSpaceOnUse" patternTransform="rotate(35)">
            <line x1="0" y1="0" x2="0" y2="26" stroke="currentColor" strokeWidth="2" />
          </pattern>
        </defs>
        <rect width="100%" height="100%" fill={`url(#${patternId})`} />
      </svg>
    </Box>
  );
}

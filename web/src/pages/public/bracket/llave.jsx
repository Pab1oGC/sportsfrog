import { useRef } from 'react';
import Box from '@mui/material/Box';
import Chip from '@mui/material/Chip';
import Typography from '@mui/material/Typography';
import { alpha } from '@mui/material/styles';
import { MEDAL_COLORS } from 'src/components/medal-circle';
import { AMBIENT_CARD } from 'src/pages/public/match-card/match-card-parts';
import { resolveMatchOutcome } from 'src/lib/match-outcome';
import { BracketConnectors } from './bracket-connectors';
import { StandardCruce } from './standard-cruce';
import { CompactCruce } from './compact-cruce';
import { DetailedCruce } from './detailed-cruce';
import { IndividualCruce } from './individual-cruce';

var CRUCES = { standard: StandardCruce, compact: CompactCruce, detailed: DetailedCruce };

/**
 * La llave de eliminatoria: el banner de campeón + el scroll horizontal
 * de columnas -- no varía por variante, es el mismo scaffold para las
 * tres (mismo criterio que el loop categoría→grupo de StandingsView).
 * Lo único que cambia es la tarjeta de cada cruce, elegida por
 * `props.variant` (theme.bracketVariant) e independiente de
 * `matchCardVariant`, que nunca llega acá.
 *
 * `props.esIndividual` reemplaza esa elección por completo, no se suma a
 * ella: un deporte individual (hoy, taekwondo) siempre usa
 * `IndividualCruce`, sin mirar `props.variant` -- la tarjeta con foto y
 * club no es una variante más, es la única forma en que esta llave se ve
 * para ese tipo de deporte.
 *
 * Las líneas que conectan un cruce con el siguiente (`BracketConnectors`)
 * se miden sobre las tarjetas ya renderizadas -- por eso cada `Cruce` se
 * envuelve en un `Box` con `ref` propio, registrado en `refsMap` por
 * `m.id`, y el contenedor de columnas (`containerRef`) es un `position:
 * relative` *adentro* del `Box` que scrollea, no el que scrollea él
 * mismo -- así el overlay se mueve en conjunto con las columnas.
 *
 * Ese `ref` de cada tarjeta sale de `refCallback(m.id)`, no de una función
 * nueva escrita ahí mismo en el `.map()`: una función literal ahí sería
 * una identidad distinta en cada render de Llave (aunque la tarjeta sea
 * la misma de siempre), y React reacciona a eso desconectando y volviendo
 * a conectar el ref -- un vaivén que deja `refsMap` con huecos momentáneos
 * en medio del cambio. Si `BracketConnectors` mide justo en ese instante
 * (su ResizeObserver puede disparar en cualquier momento, no está atado
 * al render de Llave), encuentra menos tarjetas de las que hay y calcula
 * de menos -- sin que nada dispare un recálculo después, porque nada
 * cambió de tamaño. Cacheado por id, la MISMA función sirve mientras la
 * tarjeta siga siendo la misma, y React no toca el ref para nada.
 */
export function Llave(props) {
  var grupos = props.grupos;
  var Cruce = props.esIndividual ? IndividualCruce : (CRUCES[props.variant] || StandardCruce);
  var containerRef = useRef(null);
  var refsMap = useRef(new Map());
  var refCallbacks = useRef(new Map());

  function refCallback(id) {
    if (!refCallbacks.current.has(id)) {
      refCallbacks.current.set(id, function(el) {
        if (el) refsMap.current.set(id, el); else refsMap.current.delete(id);
      });
    }
    return refCallbacks.current.get(id);
  }

  // El campeón: la última ronda, cuando quedó en un solo cruce ya jugado.
  var ultima = grupos[grupos.length - 1];
  var final = ultima && ultima.matches.length === 1 ? ultima.matches[0] : null;
  var finalJugada = final && final.homeTotal != null && final.awayTotal != null;
  var campeon = null;

  if (finalJugada) {
    var penales = final.homeTotal === final.awayTotal && final.penaltyHomeScore != null;
    var ganoLocal = penales ? final.penaltyHomeScore > final.penaltyAwayScore : final.homeTotal > final.awayTotal;
    campeon = ganoLocal ? final.homeTeamName : final.awayTeamName;
  }

  return (
    <Box>
      {campeon && (
        <Box
          sx={{
            display: 'flex', alignItems: 'center', gap: 1.5, mb: 2.5, p: 1.5, borderRadius: 2,
            ...AMBIENT_CARD,
            bgcolor: alpha(MEDAL_COLORS[1], 0.12),
            border: '1px solid', borderColor: alpha(MEDAL_COLORS[1], 0.4),
          }}
        >
          <Typography sx={{ fontSize: 24, lineHeight: 1 }}>🏆</Typography>
          <Box>
            <Typography variant="subtitle1" fontWeight={700} sx={{ lineHeight: 1.2 }}>{campeon}</Typography>
            <Typography variant="caption" color="text.secondary">Campeón</Typography>
          </Box>
        </Box>
      )}
      <Box sx={{ overflowX: 'auto', pb: 1 }}>
        <Box ref={containerRef} sx={{ position: 'relative', display: 'flex', gap: { xs: 2, sm: 3 } }}>
          {grupos.map(function(g, gi) {
            var esFinal = gi === grupos.length - 1;
            return (
              <Box key={g.clave} sx={{ minWidth: 210, width: 210, flexShrink: 0, display: 'flex', flexDirection: 'column' }}>
                <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 0.75, mb: 1 }}>
                  <Typography
                    variant="overline"
                    sx={{ fontWeight: 700, textAlign: 'center', color: esFinal ? MEDAL_COLORS[1] : 'text.secondary' }}
                  >
                    {g.titulo}
                  </Typography>
                  {g.enCurso && <Chip label="En juego" color="warning" size="small" sx={{ height: 18, fontSize: 10 }} />}
                </Box>
                <Box sx={{ flexGrow: 1, display: 'flex', flexDirection: 'column', justifyContent: 'space-around', gap: 2 }}>
                  {g.matches.map(function(m) {
                    return (
                      <Box key={m.id} ref={refCallback(m.id)}>
                        <Cruce m={m} outcome={resolveMatchOutcome(m)} />
                      </Box>
                    );
                  })}
                </Box>
              </Box>
            );
          })}
          <BracketConnectors containerRef={containerRef} refsMap={refsMap} grupos={grupos} variant={props.variant} />
        </Box>
      </Box>
    </Box>
  );
}

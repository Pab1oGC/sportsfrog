import { useRef } from 'react';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import { alpha } from '@mui/material/styles';
import { MEDAL_COLORS } from 'src/components/medal-circle';
import { resolveMatchOutcome } from 'src/lib/match-outcome';
import { nombreFase } from 'src/lib/phase-labels';
import { BracketConnectors } from './bracket-connectors';
import { StandardCruce } from './standard-cruce';
import { CompactCruce } from './compact-cruce';
import { DetailedCruce } from './detailed-cruce';
import { IndividualCruce } from './individual-cruce';

var CRUCES = { standard: StandardCruce, compact: CompactCruce, detailed: DetailedCruce };

/**
 * El repechaje de kyorugi: un gráfico aparte de la llave principal, nunca
 * mezclado con ella -- `BracketView` filtra los partidos `isRepechage` antes
 * de armar `Llave`, y son exactamente esos los que llegan acá. Ver
 * `SportFrog.Domain.Scheduling.Repechage` del lado del servidor para la
 * regla real: quien perdió contra uno de los dos finalistas, en cualquier
 * ronda, tiene derecho a jugarlo de nuevo por uno de los dos bronces.
 *
 * No es un árbol de eliminación directa como la llave principal -- es una
 * escalera por cada mitad de la llave, y las dos mitades no siempre tienen
 * el mismo largo. En vez de reproducir el cálculo de columnas por ronda que
 * hace `Llave` (pensado para un solo árbol simétrico), esto agrupa nada más
 * que por fase: "Repechaje" (todavía no define nada) y "Repechaje - Bronce"
 * (sí lo define, hasta dos partidos, uno por mitad). Con divisiones chicas
 * -la gran mayoría- cada mitad tiene como mucho un partido antes del bronce,
 * así que estas dos columnas ya son la escalera completa; en una división
 * grande, una escalera de tres o más pasos en una misma mitad cae entera en
 * la columna "Repechaje" sin una línea propia entre sus propios pasos --
 * simplificación deliberada antes que inventar una alineación de columnas
 * que ninguna de las dos mitades comparte.
 *
 * `BracketConnectors` sigue siendo el mismo componente de la llave
 * principal: empareja por nombre de competidor entre columnas adyacentes,
 * así que conecta un paso de la escalera con el bronce que efectivamente
 * alimenta sin necesitar saber a qué mitad pertenece cada uno.
 *
 * El `ref` de cada tarjeta sale de `refCallback(m.id)`, cacheado por id, no
 * de una función nueva escrita en el propio `.map()` -- mismo motivo que ya
 * documenta `Llave`: una función literal ahí es una identidad distinta en
 * cada render, y `BracketConnectors` puede medir justo en el instante en
 * que React todavía está desconectando y reconectando ese ref por eso,
 * encontrando menos tarjetas de las que hay.
 */
export function RepechageBracket(props) {
  var matches = props.matches;
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

  var bronce = matches.filter(function(m) { return m.phase === 'repechaje bronce'; });
  var escalera = matches
    .filter(function(m) { return m.phase !== 'repechaje bronce'; })
    .sort(function(a, b) { return (a.roundNumber || 0) - (b.roundNumber || 0); });

  if (bronce.length === 0 && escalera.length === 0) {
    return null;
  }

  var grupos = [
    escalera.length > 0 && { clave: 'repechaje', titulo: nombreFase('repechaje'), matches: escalera },
    { clave: 'bronce', titulo: nombreFase('repechaje bronce'), matches: bronce },
  ].filter(Boolean);

  return (
    <Box sx={{ mt: 4 }}>
      <Typography variant="overline" sx={{ fontWeight: 700, color: MEDAL_COLORS[3], display: 'block', mb: 1.5 }}>
        Repechaje — definición del tercer puesto
      </Typography>
      <Box sx={{ overflowX: 'auto', pb: 1 }}>
        <Box ref={containerRef} sx={{ position: 'relative', display: 'flex', gap: { xs: 2, sm: 3 } }}>
          {grupos.map(function(g) {
            var esBronce = g.clave === 'bronce';
            return (
              <Box key={g.clave} sx={{ minWidth: 210, width: 210, flexShrink: 0, display: 'flex', flexDirection: 'column' }}>
                <Typography
                  variant="overline"
                  sx={{ fontWeight: 700, textAlign: 'center', display: 'block', mb: 1, color: esBronce ? MEDAL_COLORS[3] : 'text.secondary' }}
                >
                  {g.titulo}
                </Typography>
                <Box sx={{ flexGrow: 1, display: 'flex', flexDirection: 'column', justifyContent: 'space-around', gap: 2 }}>
                  {g.matches.map(function(m) {
                    return (
                      <Box
                        key={m.id}
                        ref={refCallback(m.id)}
                        sx={esBronce ? { borderRadius: 1.5, boxShadow: '0 0 0 2px ' + alpha(MEDAL_COLORS[3], 0.35) } : undefined}
                      >
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

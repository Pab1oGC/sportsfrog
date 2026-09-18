import { useLayoutEffect, useState } from 'react';
import Box from '@mui/material/Box';
import { alpha } from '@mui/material/styles';

/**
 * Las líneas que conectan cada cruce con el de la ronda siguiente que
 * alimenta -- medidas por JS a partir de las tarjetas ya renderizadas,
 * no asumidas: las tres variantes de cruce (standard/compact/detailed)
 * tienen alturas distintas, y una llave con muchas rondas no siempre
 * tiene el mismo número de cruces por columna.
 *
 * El emparejamiento padre→hijo se resuelve por **nombre de equipo**, no
 * por posición en la lista: el dato público de partidos no expone un id
 * de equipo, y el sorteo de cada ronda es independiente del anterior
 * (`AdvanceBracket` del lado del servidor arma un sorteo nuevo sobre los
 * ganadores, sin guardar un link "próximo partido"). Así que el único
 * hecho confiable es que el mismo nombre de equipo jugó, como local o
 * visitante, en el cruce anterior y en este. Cuando no se encuentra un
 * padre -típicamente un bye de la primera ronda- esa línea puntual
 * simplemente no se dibuja: nunca se adivina una conexión.
 *
 * `containerRef` debe apuntar al wrapper `position: relative` que
 * contiene las columnas *dentro* del `Box` que scrollea horizontalmente
 * (no al propio `overflowX: auto`) -- así este overlay se desplaza en
 * conjunto con las columnas en vez de quedar fijo al viewport visible.
 *
 * El trazo va en `primary.main` (a media opacidad), no en un gris
 * neutro: por cómo se resuelve el emparejamiento de arriba, un padre
 * solo se encuentra cuando ese equipo ganó ese cruce -un eliminado
 * nunca aparece en la ronda siguiente-, así que toda línea que esto
 * dibuja ya es, por construcción, el camino de un ganador. No hace
 * falta lógica extra para distinguir "ganador" de "perdedor": ya lo es.
 */
export function BracketConnectors(props) {
  var containerRef = props.containerRef;
  var refsMap = props.refsMap;
  var grupos = props.grupos;
  var variant = props.variant;
  var [paths, setPaths] = useState([]);

  useLayoutEffect(function() {
    var container = containerRef.current;
    if (!container) return undefined;

    function recompute() {
      var containerRect = container.getBoundingClientRect();
      var nuevas = [];

      for (var i = 0; i < grupos.length - 1; i++) {
        var actual = grupos[i];
        var siguiente = grupos[i + 1];

        siguiente.matches.forEach(function(hijo) {
          var hijoEl = refsMap.current.get(hijo.id);
          if (!hijoEl) return;
          var hijoRect = hijoEl.getBoundingClientRect();
          var hijoY = hijoRect.top + hijoRect.height / 2 - containerRect.top;
          var hijoX = hijoRect.left - containerRect.left;

          var padres = actual.matches.filter(function(m) {
            return m.homeTeamName === hijo.homeTeamName || m.awayTeamName === hijo.homeTeamName ||
                   m.homeTeamName === hijo.awayTeamName || m.awayTeamName === hijo.awayTeamName;
          });

          padres.forEach(function(padre) {
            var padreEl = refsMap.current.get(padre.id);
            if (!padreEl) return;
            var padreRect = padreEl.getBoundingClientRect();
            var padreY = padreRect.top + padreRect.height / 2 - containerRect.top;
            var padreX = padreRect.right - containerRect.left;
            var midX = padreX + (hijoX - padreX) / 2;

            nuevas.push('M ' + padreX + ' ' + padreY + ' H ' + midX + ' V ' + hijoY + ' H ' + hijoX);
          });
        });
      }

      setPaths(nuevas);
    }

    recompute();

    var observer = new ResizeObserver(recompute);
    observer.observe(container);
    return function() { observer.disconnect(); };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [grupos, variant, containerRef, refsMap]);

  if (paths.length === 0) return null;

  return (
    <Box
      component="svg"
      aria-hidden="true"
      sx={{ position: 'absolute', inset: 0, width: '100%', height: '100%', pointerEvents: 'none', color: (t) => alpha(t.palette.primary.main, 0.6) }}
    >
      {paths.map(function(d, i) {
        return <path key={i} d={d} fill="none" stroke="currentColor" strokeWidth={1.5} />;
      })}
    </Box>
  );
}

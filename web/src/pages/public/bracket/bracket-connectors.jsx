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
    var cleanupActivo = null;

    function iniciar(container) {
      function recompute() {
        // Todo el calculo va adentro del try: una sola tarjeta con un rect
        // raro o un grupo sin `matches` no deberia tirar abajo TODAS las
        // lineas en silencio -- sin este catch, una excepcion a mitad de
        // camino corta la funcion antes de llegar a setPaths(), y como nada
        // mas dispara un recalculo despues (no cambio ningun tamaño), las
        // lineas quedan en blanco para siempre hasta el proximo motivo real
        // de recomputo. console.error en vez de tragarselo: silencioso de
        // cara a quien mira la llave, pero visible para quien mira la consola.
        // DEBUG TEMPORAL -- sacar una vez que se entienda por que a veces da
        // cero lineas. Sin poder ver la consola del navegador desde aca, este
        // es el unico dato real posible: cuantos grupos hay, si el ref de
        // cada tarjeta existe, y si el emparejamiento por nombre encuentra
        // algo. console.groupCollapsed para no ensuciar la consola de quien
        // no esta mirando esto.
        var debug = { grupos: grupos.length, porGrupo: [] };

        try {
          var containerRect = container.getBoundingClientRect();
          var nuevas = [];

          for (var i = 0; i < grupos.length - 1; i++) {
            var actual = grupos[i];
            var siguiente = grupos[i + 1];
            var entradaDebug = {
              de: actual && actual.titulo, a: siguiente && siguiente.titulo,
              actualTieneMatches: !!(actual && actual.matches), siguienteTieneMatches: !!(siguiente && siguiente.matches),
              hijos: [],
            };
            debug.porGrupo.push(entradaDebug);
            if (!actual || !siguiente || !actual.matches || !siguiente.matches) continue;

            siguiente.matches.forEach(function(hijo) {
              var hijoEl = refsMap.current.get(hijo.id);
              var entradaHijo = {
                hijo: hijo.homeTeamName + ' vs ' + hijo.awayTeamName, hijoId: hijo.id, hijoElEncontrado: !!hijoEl,
              };
              entradaDebug.hijos.push(entradaHijo);
              if (!hijoEl) return;
              var hijoRect = hijoEl.getBoundingClientRect();
              var hijoY = hijoRect.top + hijoRect.height / 2 - containerRect.top;
              var hijoX = hijoRect.left - containerRect.left;

              var padres = actual.matches.filter(function(m) {
                return m.homeTeamName === hijo.homeTeamName || m.awayTeamName === hijo.homeTeamName ||
                       m.homeTeamName === hijo.awayTeamName || m.awayTeamName === hijo.awayTeamName;
              });
              entradaHijo.padresEncontradosPorNombre = padres.map(function(p) { return p.homeTeamName + ' vs ' + p.awayTeamName; });

              padres.forEach(function(padre) {
                var padreEl = refsMap.current.get(padre.id);
                entradaHijo.padreElEncontrado = !!padreEl;
                if (!padreEl) return;
                var padreRect = padreEl.getBoundingClientRect();
                var padreY = padreRect.top + padreRect.height / 2 - containerRect.top;
                var padreX = padreRect.right - containerRect.left;
                var midX = padreX + (hijoX - padreX) / 2;

                nuevas.push('M ' + padreX + ' ' + padreY + ' H ' + midX + ' V ' + hijoY + ' H ' + hijoX);
              });
            });
          }

          debug.lineasCalculadas = nuevas.length;
          console.groupCollapsed('[BracketConnectors] recompute -> ' + nuevas.length + ' línea(s)');
          console.log(JSON.parse(JSON.stringify(debug)));
          console.groupEnd();

          setPaths(nuevas);
        } catch (err) {
          console.error('BracketConnectors: no se pudieron calcular las líneas de la llave.', err, debug);
        }
      }

      recompute();

      // Dos recomputos mas en los frames que siguen, no solo el sincronico de
      // arriba: layout en cadena entre columnas (el contenedor es flex-row con
      // el stretch de siempre, cada columna flex-column con sus cruces
      // repartidos por justifyContent:'space-around') a veces todavia no
      // termino de asentarse en el mismo frame en que este efecto corre --
      // sobre todo la primera vez que la pestaña "Llave" se monta, que es
      // justo cuando se notaba que a veces las líneas salian mal. Dos rAF
      // encadenados (no uno) porque un solo frame de margen no siempre alcanza
      // cuando el propio montaje de Llave viene de un swap (el esqueleto de
      // carga cambiando por el cuadro real) en vez de un montaje derecho.
      var raf2;
      var raf1 = requestAnimationFrame(function() {
        recompute();
        raf2 = requestAnimationFrame(recompute);
      });

      var observer = new ResizeObserver(recompute);
      observer.observe(container);
      // No solo el contenedor: si una tarjeta cambia de alto o de posicion por
      // su cuenta sin que el contenedor cambie de tamaño total (columnas mas
      // cortas que la mas alta, por ejemplo), el observer del contenedor solo
      // nunca se entera y las líneas quedan con la medicion vieja.
      refsMap.current.forEach(function(el) { observer.observe(el); });

      cleanupActivo = function() {
        cancelAnimationFrame(raf1);
        if (raf2) cancelAnimationFrame(raf2);
        observer.disconnect();
      };
    }

    var container = containerRef.current;

    if (container) {
      iniciar(container);
    } else {
      // DEBUG TEMPORAL -- el unico camino de este efecto que no deja rastro
      // en la consola (ni el grupo de arriba, ni el catch) es este: salir
      // porque `containerRef.current` todavia es null en el instante en que
      // este efecto corre. Si esto es lo que pasa en el caso roto, tiene que
      // aparecer este warn cuando antes no aparecia nada. En vez de
      // resignarse (lo que hacia antes: `return` sin mas), reintenta en el
      // siguiente frame -- y si aparece, arranca todo el calculo recien ahi,
      // no solo lo deja constar en un log.
      console.warn('[BracketConnectors] containerRef.current es null al montar -- reintentando.');
      var intentos = 0;
      var reintentoId;
      var cancelado = false;
      var intentarDeNuevo = function() {
        intentos += 1;
        var actual = containerRef.current;
        if (actual) {
          console.warn('[BracketConnectors] containerRef.current apareció después de ' + intentos + ' intento(s).');
          if (!cancelado) iniciar(actual);
          return;
        }
        if (intentos < 10) {
          reintentoId = requestAnimationFrame(intentarDeNuevo);
        } else {
          console.error('[BracketConnectors] containerRef.current nunca apareció después de ' + intentos + ' intentos.');
        }
      };
      reintentoId = requestAnimationFrame(intentarDeNuevo);
      cleanupActivo = function() {
        cancelado = true;
        cancelAnimationFrame(reintentoId);
      };
    }

    return function() {
      if (cleanupActivo) cleanupActivo();
    };
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

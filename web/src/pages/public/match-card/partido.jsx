import { resolveMatchOutcome } from 'src/lib/match-outcome';
import { StandardCard } from './standard-card';
import { CompactCard } from './compact-card';
import { MatchupCard } from './matchup-card';

var CARDS = { standard: StandardCard, compact: CompactCard, matchup: MatchupCard };

/**
 * La tarjeta de un partido -- despachador. Deriva una vez lo que cualquier
 * variante podría necesitar (`resolveMatchOutcome`, si el partido destaca
 * por el filtro de equipo, la cancha armada, si corresponde ofrecer
 * cronología) y se lo pasa a la variante elegida (theme.matchCardVariant).
 * Cada variante decide su propia composición y maneja su propio estado de
 * "ver cronología"/"ver mapa" -- qué tan disponibles están esas acciones es
 * parte de esa composición, no un hecho compartido.
 *
 * Exporta `Partido`, el mismo nombre que ya usaba CalendarView, para no
 * tocar ese contrato. Nunca llega a la llave de eliminatoria (Llave/
 * CruceLlave en public-competition.jsx), que mantiene su propia tarjeta
 * compacta fija.
 */
export function Partido(props) {
  var m = props.m;
  var outcome = resolveMatchOutcome(m);

  // Cuando se filtra por un equipo, marcarlo dentro de la fila: de un vistazo
  // se ve si jugó de local o de visitante sin leer los dos nombres.
  var esLocal = props.destacado && m.homeTeamName === props.destacado;
  var esVisita = props.destacado && m.awayTeamName === props.destacado;

  var cancha = [m.venueName, m.spaceName].filter(Boolean).join(' · ');

  // La cronología nombra jugadores, así que sigue la misma regla que un
  // plantel: solo se ofrece cuando la competencia publicó planteles, y solo
  // tiene sentido pedirla una vez que el partido empezó a jugarse.
  var puedeVerCronologia = props.mostrarEventos && (m.status === 'in_progress' || m.status === 'finished');

  var data = {
    m: m,
    outcome: outcome,
    esLocal: esLocal,
    esVisita: esVisita,
    cancha: cancha,
    puedeVerCronologia: puedeVerCronologia,
    mostrarCategoria: props.mostrarCategoria,
    orgSlug: props.orgSlug,
    compSlug: props.compSlug,
  };

  var Card = CARDS[props.variant] || StandardCard;
  return <Card data={data} />;
}

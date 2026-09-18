import Avatar from '@mui/material/Avatar';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import CircularProgress from '@mui/material/CircularProgress';
import Dialog from '@mui/material/Dialog';
import IconButton from '@mui/material/IconButton';
import Typography from '@mui/material/Typography';
import { alpha } from '@mui/material/styles';
import useSWR from 'swr';
import { LivePulse } from 'src/components/live-pulse';
import { Iconify } from 'src/components/iconify';
import publicAxios from 'src/lib/public-axios';
import { embedSrc } from 'src/lib/google-maps-url';

/* ---------------------------------------------------------------------------
   Piezas que comparten las variantes de la tarjeta de partido
   (standard-card.jsx, compact-card.jsx, matchup-card.jsx) y, para
   EscudoEquipo/fechaCorta/hora, también FilaCruce en public-competition.jsx
   (la llave de eliminatoria, que queda fuera de este cambio pero usa las
   mismas piezas). Vive acá y no en cada archivo para que cambiar un ícono o
   un formato de fecha se haga una sola vez.
   --------------------------------------------------------------------------- */

var publicFetcher = function(url) { return publicAxios.get(url).then(function(r) { return r.data; }); };

export var SL = { scheduled: 'Programado', in_progress: 'En curso', finished: 'Finalizado', cancelled: 'Cancelado', walkover: 'Walkover', postponed: 'Aplazado' };
export var SC = { scheduled: 'info', in_progress: 'warning', finished: 'success', cancelled: 'error', walkover: 'warning', postponed: 'default' };

/**
 * Si ya no hay nada mas que este partido pueda decir de si mismo --
 * jugado o entregado por walkover, en cualquiera de los dos casos ya
 * tiene un resultado final. Un walkover nunca tiene `scheduledAt` (no
 * se llego a programar cancha ni horario, se otorgo directo) pero eso
 * no es lo mismo que "todavia sin programar": mostrar "Por programar"
 * al lado de un marcador ya cargado y un chip que dice "Walkover" es
 * contradictorio, no honesto. Se usa tanto para el chip de estado
 * (relleno cuando ya se decidio, contorneado mientras se espera algo)
 * como para decidir que mostrar en el lugar de la fecha.
 */
export function esResultadoFinal(status) {
  return status === 'finished' || status === 'walkover';
}

/**
 * La elevación "ambient-card" que documenta DESIGN.md para las tarjetas
 * de partido y de llave: sombra suave sobre `background.paper`. Se
 * desparrama con un spread (`...AMBIENT_CARD`) en el `sx` de cada tarjeta
 * en vez de duplicar el valor por archivo.
 */
export var AMBIENT_CARD = {
  boxShadow: '0 2px 12px rgba(0,0,0,0.08)',
  bgcolor: 'background.paper',
};

export function fechaCorta(iso) {
  var d = new Date(iso);
  return d.toLocaleDateString('es-BO', { weekday: 'short', day: '2-digit', month: 'short' });
}

export function hora(iso) {
  var d = new Date(iso);
  return d.toLocaleTimeString('es-BO', { hour: '2-digit', minute: '2-digit' });
}

/**
 * El escudo del club, o un icono generico cuando el club no subio uno.
 * `size` es opcional -- 22px (el de siempre) cuando no se pasa, así que
 * cada lugar que ya lo usa sin el prop se sigue viendo exactamente igual.
 */
export function EscudoEquipo(props) {
  var tamano = props.size || 22;
  return (
    <Avatar src={props.url || undefined} variant="rounded" sx={{ width: tamano, height: tamano, flexShrink: 0, bgcolor: 'action.hover', '& img': { objectFit: 'contain' } }}>
      {!props.url && <Iconify icon="mdi:office-building-outline" width={Math.round(tamano * 0.6)} sx={{ color: 'text.disabled' }} />}
    </Avatar>
  );
}

var TAMANOS = {
  compact: { minWidth: 52, fontSize: 14, px: 1, py: 0.5 },
  standard: { minWidth: 72, fontSize: { xs: 17, sm: 20 }, px: 1.5, py: 0.75 },
  large: { minWidth: 96, fontSize: { xs: 24, sm: 30 }, px: 2, py: 1 },
};

/**
 * El resultado de un partido, como caja de marcador: números grandes,
 * tabulares (no saltan de ancho al cambiar), y "EN VIVO" con el mismo pulso
 * que el hero. `size` ajusta el tamaño de la caja para las variantes
 * `compact`/`matchup` sin duplicar el componente -- sin el prop se ve
 * exactamente igual que siempre (`standard`).
 */
export function Marcador(props) {
  var m = props.m;
  var outcome = props.outcome;
  var tamano = TAMANOS[props.size] || TAMANOS.standard;
  var mostrarResultado = outcome.jugado || outcome.enVivo;

  return (
    <Box
      sx={{
        flexShrink: 0, textAlign: 'center', borderRadius: 1.5, fontWeight: 800,
        letterSpacing: 0.5, lineHeight: 1, fontVariantNumeric: 'tabular-nums',
        minWidth: tamano.minWidth, fontSize: tamano.fontSize, px: tamano.px, py: tamano.py,
        bgcolor: outcome.jugado ? 'primary.main' : outcome.enVivo ? 'error.main' : 'action.selected',
        color: outcome.jugado ? 'primary.contrastText' : outcome.enVivo ? 'error.contrastText' : 'text.secondary',
        // El "VS" neutro necesita distinguirse de la tarjeta que lo rodea --
        // un borde propio se lo garantiza sin depender de qué tono tenga
        // encima.
        border: !outcome.jugado && !outcome.enVivo ? '1px solid' : 'none',
        borderColor: 'divider',
        // El resultado ya jugado o en curso se incrusta en la tarjeta con
        // una sombra teñida del mismo color que lo pinta -- el "VS" neutro
        // se queda sin sombra, no compite por atención con un resultado real.
        boxShadow: (t) => {
          if (outcome.jugado) return '0 2px 8px ' + alpha(t.palette.primary.main, 0.35);
          if (outcome.enVivo) return '0 2px 8px ' + alpha(t.palette.error.main, 0.35);
          return 'none';
        },
      }}
    >
      {mostrarResultado ? outcome.homeMostrado + ' - ' + outcome.awayMostrado : 'VS'}
      {outcome.huboPenales && (
        <Typography variant="caption" component="div" sx={{ fontSize: 9, fontWeight: 600, letterSpacing: 0.3, lineHeight: 1.3, opacity: 0.85, mt: 0.25 }}>
          ({m.penaltyHomeScore}-{m.penaltyAwayScore} pen)
        </Typography>
      )}
      {outcome.enVivo && (
        <Box sx={{ display: 'flex', justifyContent: 'center', mt: 0.4 }}>
          <LivePulse size="small" />
        </Box>
      )}
    </Box>
  );
}

/**
 * El mapa de una sede, incrustado en un dialogo en vez de abrirse en otra
 * pestaña -- eso era todo lo que habia antes de que se pidiera verlo "en el
 * portal" en si. El enlace que carga el organizador puede ser cualquier
 * forma en que Google Maps entrega un lugar (un "compartir", un lugar, un
 * enlace corto): la mayoria de esas paginas rechazan mostrarse dentro de un
 * iframe ajeno, asi que en vez de usarlo tal cual se arma la URL de consulta
 * que Google si permite incrustar (?q=...&output=embed) -- ver `embedSrc`,
 * que devuelve null cuando no hay coordenadas confiables de donde partir
 * (un enlace corto sin resolver, por ejemplo) en vez de adivinar con la URL
 * entera como texto de busqueda, algo que probadamente termina en el error
 * de Google "contenido personalizado" mas un mapa del mundo sin zoom. Ese
 * caso se avisa acá mismo, antes de intentar el iframe. El caso que sí sigue
 * sin poder detectarse en JavaScript es el iframe bloqueado silenciosamente
 * por X-Frame-Options -ahí no dispara ningún evento-, así que el enlace para
 * abrirlo en una pestaña aparte queda siempre visible debajo de todos modos.
 */
export function MapaSedeDialog(props) {
  var src = embedSrc(props.mapsUrl);

  return (
    <Dialog open={props.open} onClose={props.onClose} maxWidth="sm" fullWidth>
      <Box sx={{ p: 2 }}>
        <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', mb: 1.5 }}>
          <Typography variant="subtitle1" fontWeight={600} noWrap title={props.titulo} sx={{ minWidth: 0 }}>
            {props.titulo || 'Ubicación'}
          </Typography>
          <IconButton size="small" onClick={props.onClose} aria-label="Cerrar">
            <Iconify icon="eva:close-outline" width={20} />
          </IconButton>
        </Box>
        {src ? (
          <Box
            component="iframe"
            src={src}
            title={props.titulo || 'Mapa'}
            loading="lazy"
            referrerPolicy="no-referrer-when-downgrade"
            sx={{ width: '100%', height: { xs: 260, sm: 340 }, border: 0, borderRadius: 1, display: 'block' }}
          />
        ) : (
          <Box
            sx={{
              width: '100%', height: { xs: 260, sm: 340 }, borderRadius: 1, bgcolor: 'action.hover',
              display: 'flex', alignItems: 'center', justifyContent: 'center', textAlign: 'center', p: 2,
            }}
          >
            <Typography variant="body2" color="text.secondary">
              No se pudo cargar el mapa de esta sede. Podés abrirlo en Google Maps.
            </Typography>
          </Box>
        )}
        <Button
          component="a"
          href={props.mapsUrl}
          target="_blank"
          rel="noopener noreferrer"
          size="small"
          startIcon={<Iconify icon="mdi:map-marker" width={16} />}
          sx={{ mt: 1 }}
        >
          Abrir en Google Maps
        </Button>
      </Box>
    </Dialog>
  );
}

// Un icono por codigo de metrica, no por "afecta el marcador" -- ese booleano
// lo comparten goles, dobles/triples de basquetbol y los puntos de kyorugi
// (ver la migracion AddTaekwondoKyorugiScoringEvents), asi que decidir el
// dibujo con el solo `affectsScore` le ponia el mismo balon de futbol a las
// tres cosas. Un codigo sin entrada cae al puntito generico de mas abajo en
// vez de heredar el balon por default.
var ICONOS_EVENTO = {
  goal: '⚽',
  own_goal: '⚽',
  free_throw: '🏀',
  field_goal: '🏀',
  three_point: '🏀',
  point: '🥋',
  penalty: '⚠️',
  yellow_card: '🟨',
  red_card: '🟥',
};

/** Un símbolo reconocible para el evento, segun su código de métrica. Null
    para uno sin icono propio -- ese caso dibuja un puntito, no un carácter
    de puntuación suelto (ver el render de la fila, más abajo). */
function marcaDeEvento(ev) {
  return ICONOS_EVENTO[ev.metricCode] || null;
}

/** El minuto a minuto de un partido: goles y tarjetas, en el orden en que pasaron. */
export function Cronologia(props) {
  var { data, isLoading } = useSWR(
    '/api/public/' + props.orgSlug + '/' + props.compSlug + '/matches/' + props.matchId + '/events',
    publicFetcher
  );

  if (isLoading) return <Box sx={{ display: 'flex', justifyContent: 'center', py: 1.5 }}><CircularProgress size={20} /></Box>;

  var eventos = (data && data.events) || [];

  if (eventos.length === 0) {
    return (
      <Typography variant="caption" color="text.secondary" sx={{ display: 'block', pt: 1, borderTop: '1px solid', borderColor: 'divider' }}>
        Todavía no se registraron eventos en este partido.
      </Typography>
    );
  }

  return (
    <Box sx={{ display: 'flex', pt: 1.5, borderTop: '1px solid', borderColor: 'divider' }}>
      {/* Los escudos, fijos a la izquierda -- arriba el visitante, abajo el
          local, la misma orientación que el resto de la franja usa para
          decidir qué evento va arriba y cuál abajo. No scrollean con los
          eventos: son el ancla que dice de quién es cada fila. */}
      <Box sx={{ display: 'flex', flexDirection: 'column', justifyContent: 'space-between', alignItems: 'center', flexShrink: 0, mr: 1.5 }}>
        <Box title={props.awayTeamName || ''}><EscudoEquipo url={props.awayClubLogoUrl} size={28} /></Box>
        <Box sx={{ flexGrow: 1, minHeight: 12 }} />
        <Box title={props.homeTeamName || ''}><EscudoEquipo url={props.homeClubLogoUrl} size={28} /></Box>
      </Box>
      <Box sx={{ overflowX: 'auto', flexGrow: 1, minWidth: 0 }}>
        <Box
          sx={{
            display: 'grid',
            gridTemplateColumns: 'repeat(' + eventos.length + ', minmax(76px, 1fr))',
            minWidth: eventos.length * 76,
          }}
        >
          {/* Grid de 3 filas × N columnas (una por evento), en el orden en
              que el propio grid las acomoda solas: las N celdas de arriba
              (visitante), después las N del minuto/línea, después las N de
              abajo (local) -- sin gridColumn/gridRow explícito en cada una,
              CSS ya sabe volver a la primera columna al llegar a la N+1. */}
          {eventos.map(function(ev, i) { return <CeldaEvento key={'v' + i} ev={ev} esLocal={false} />; })}
          {eventos.map(function(ev, i) { return <CeldaMinuto key={'m' + i} ev={ev} />; })}
          {eventos.map(function(ev, i) { return <CeldaEvento key={'l' + i} ev={ev} esLocal={true} />; })}
        </Box>
      </Box>
    </Box>
  );
}

function CeldaMinuto(props) {
  var ev = props.ev;
  var minuto = ev.minute != null ? ev.minute + "'" : (ev.periodNumber ? 'P' + ev.periodNumber : '--');
  return (
    <Box sx={{ position: 'relative', display: 'flex', alignItems: 'center', justifyContent: 'center', height: 26 }}>
      <Box sx={{ position: 'absolute', left: 0, right: 0, top: '50%', height: 2, bgcolor: 'divider' }} />
      {/* Solo el fondo de AMBIENT_CARD (no la sombra ni el borde, que ahí
          son para la tarjeta entera, no para este numerito) "tapa" la línea
          detrás del número -- el mismo fondo exacto que ya usa la tarjeta
          que envuelve esto, no un "background.paper" fijo que desentonaría
          en oscuro. */}
      <Typography variant="caption" sx={{ position: 'relative', fontWeight: 800, px: 0.5, bgcolor: AMBIENT_CARD.bgcolor }}>
        {minuto}
      </Typography>
    </Box>
  );
}

/**
 * Un evento en su columna, arriba o abajo según de quién sea -- null
 * (una `Box` vacía) cuando este evento no es del equipo de esta fila,
 * así el grid de 3×N no se desarma. Un autogol lo mete un jugador del
 * equipo contrario al que suma: se ordena del lado de a quien le
 * sirvió, no del plantel al que pertenece, que es como se lee un
 * resultado en cualquier resumen de partido -- el XOR da vuelta el
 * lado solo en ese caso.
 */
function CeldaEvento(props) {
  var ev = props.ev;
  var esLocal = ev.isHome !== ev.countsForOpponent;
  if (esLocal !== props.esLocal) return <Box />;

  var marca = marcaDeEvento(ev);
  var nombre = (ev.firstName ? ev.firstName.charAt(0) + '. ' : '') + ev.lastName
    + (ev.jerseyNumber != null ? ' #' + ev.jerseyNumber : '')
    + (ev.quantity > 1 ? ' x' + ev.quantity : '');

  return (
    <Box
      sx={{
        display: 'flex', flexDirection: props.esLocal ? 'column-reverse' : 'column',
        alignItems: 'center', gap: 0.4, px: 0.5, py: 0.5, textAlign: 'center',
      }}
    >
      {marca ? (
        <Typography sx={{ fontSize: 15, lineHeight: 1 }}>{marca}</Typography>
      ) : (
        <Box sx={{ width: 7, height: 7, borderRadius: '50%', bgcolor: (t) => alpha(t.palette.primary.main, 0.35) }} />
      )}
      <Typography variant="caption" noWrap title={nombre} sx={{ fontWeight: 700, fontSize: 10.5, lineHeight: 1.25, maxWidth: '100%' }}>
        {nombre}
        {ev.countsForOpponent && <Box component="span" sx={{ color: 'error.main' }}> (AG)</Box>}
      </Typography>
    </Box>
  );
}

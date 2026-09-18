import Box from '@mui/material/Box';
import Chip from '@mui/material/Chip';
import Paper from '@mui/material/Paper';
import Typography from '@mui/material/Typography';
import { AMBIENT_CARD, SL, SC, esResultadoFinal, EscudoEquipo, hora, Marcador } from './match-card-parts';

/**
 * La variante `compact` -- una sola fila, sin salto a columna en móvil, para
 * listas largas de partidos. Sin cancha, sin mapa, sin cronología a
 * propósito: cada partido ocupa una línea, no un bloque. La hora alcanza
 * (la fecha completa ya la dice el título de la jornada que agrupa estas
 * filas en CalendarView).
 */
export function CompactCard(props) {
  var data = props.data;
  var m = data.m;
  var outcome = data.outcome;
  var esLocal = data.esLocal;
  var esVisita = data.esVisita;
  // Una llave de eliminacion directa sorteada completa puede reservar
  // fecha y cancha para una ronda futura sin saber todavia quien la juega
  // -- homeTeamName/awayTeamName llegan null en ese caso.
  var homeNombre = m.homeTeamName || m.homePlaceholder || 'Por definir';
  var awayNombre = m.awayTeamName || m.awayPlaceholder || 'Por definir';

  return (
    <Paper variant="outlined" sx={{ px: 1.25, py: 0.75, borderRadius: 1.5, display: 'flex', alignItems: 'center', gap: 1, ...AMBIENT_CARD }}>
      <Typography variant="caption" color="text.secondary" sx={{ width: 42, flexShrink: 0 }}>
        {m.scheduledAt ? hora(m.scheduledAt) : (esResultadoFinal(m.status) ? '' : '-')}
      </Typography>
      <Box sx={{ flex: 1, minWidth: 0, display: 'flex', alignItems: 'center', justifyContent: 'flex-end', gap: 0.5 }}>
        <Typography
          variant="body2"
          noWrap
          title={homeNombre}
          sx={{ minWidth: 0, textAlign: 'right', fontSize: 13, fontWeight: esLocal || outcome.ganoLocal ? 700 : 500, color: esLocal ? 'primary.main' : undefined }}
        >
          {homeNombre}
        </Typography>
        <EscudoEquipo url={m.homeClubLogoUrl} />
      </Box>
      <Marcador m={m} outcome={outcome} size="compact" />
      <Box sx={{ flex: 1, minWidth: 0, display: 'flex', alignItems: 'center', gap: 0.5 }}>
        <EscudoEquipo url={m.awayClubLogoUrl} />
        <Typography
          variant="body2"
          noWrap
          title={awayNombre}
          sx={{ minWidth: 0, fontSize: 13, fontWeight: esVisita || outcome.ganoVisita ? 700 : 500, color: esVisita ? 'primary.main' : undefined }}
        >
          {awayNombre}
        </Typography>
      </Box>
      {/* Ancho fijo: "Walkover" y "Finalizado"/"Programado" no miden lo
          mismo, y este chip queda fuera del par simétrico de nombres (los
          dos `flex:1` de arriba) -- sin un ancho fijo, una etiqueta más
          larga o más corta corre el marcador de su lugar de una fila a
          otra, en vez de dejarlo siempre en el mismo punto. */}
      <Chip
        size="small"
        label={SL[m.status] || m.status}
        color={SC[m.status] || 'default'}
        variant={esResultadoFinal(m.status) ? 'filled' : 'outlined'}
        sx={{ flexShrink: 0, height: 22, fontSize: 11, width: 92, justifyContent: 'center' }}
      />
    </Paper>
  );
}

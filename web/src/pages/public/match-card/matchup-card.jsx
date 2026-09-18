import { useState } from 'react';
import Avatar from '@mui/material/Avatar';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Chip from '@mui/material/Chip';
import Collapse from '@mui/material/Collapse';
import Paper from '@mui/material/Paper';
import Typography from '@mui/material/Typography';
import { Iconify } from 'src/components/iconify';
import { LiveBadge } from 'src/pages/public/hero/hero-parts';
import { AMBIENT_CARD, SL, SC, esResultadoFinal, fechaCorta, hora, Marcador, MapaSedeDialog, Cronologia } from './match-card-parts';

/**
 * La variante `matchup` -- tarjeta grande tipo "partido destacado": cabecera
 * con fecha/cancha/estado, después un bloque centrado con escudos grandes a
 * los costados de un marcador grande. Mismo lenguaje visual que el bloque
 * de equipo del hero `live` (escudo grande + nombre debajo), pensado para
 * cuando un solo partido merece protagonismo en vez de compartir línea con
 * los demás. El filo superior en `primary.main` es lo que de verdad la
 * distingue de `standard`/`compact` -- antes solo cambiaba de tamaño.
 * En vivo, el chip de estado se reemplaza por el mismo `LiveBadge` del
 * hero -- la tarjeta "destacada" no puede tener menos presencia que un
 * chip genérico cuando es justo el partido en curso.
 */
export function MatchupCard(props) {
  var data = props.data;
  var m = data.m;
  var outcome = data.outcome;
  var cancha = data.cancha;
  var puedeVerCronologia = data.puedeVerCronologia;
  // Una llave de eliminacion directa sorteada completa puede reservar
  // fecha y cancha para una ronda futura sin saber todavia quien la juega
  // -- homeTeamName/awayTeamName llegan null en ese caso.
  var homeNombre = m.homeTeamName || m.homePlaceholder || 'Por definir';
  var awayNombre = m.awayTeamName || m.awayPlaceholder || 'Por definir';
  var [expandido, setExpandido] = useState(false);
  var [mapaAbierto, setMapaAbierto] = useState(false);

  return (
    <Paper variant="outlined" sx={{ p: { xs: 2, sm: 3 }, borderRadius: 2, ...AMBIENT_CARD, borderTop: '3px solid', borderTopColor: 'primary.main' }}>
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 1, flexWrap: 'wrap', mb: 2 }}>
        <Box>
          {/* Un walkover nunca tuvo fecha -- no es que falte programarlo,
              ya tiene resultado. "Por programar" ahí sería contradictorio. */}
          {(m.scheduledAt || !esResultadoFinal(m.status)) && (
            <Typography variant="body2" fontWeight={600} sx={{ textTransform: 'capitalize' }}>
              {m.scheduledAt ? fechaCorta(m.scheduledAt) + ' · ' + hora(m.scheduledAt) : 'Por programar'}
            </Typography>
          )}
          {cancha && (
            <Box sx={{ display: 'flex', alignItems: 'center', flexWrap: 'wrap', gap: 1, mt: 0.5 }}>
              <Typography variant="caption" color="text.secondary">{cancha}</Typography>
              {m.venueMapsUrl && (
                // Un botón de verdad, con texto -- un ícono suelto de 14px
                // se pasaba por alto, mismo motivo que ya tiene "Cronologia".
                <Button
                  size="small"
                  color="primary"
                  variant="outlined"
                  onClick={() => setMapaAbierto(true)}
                  startIcon={<Iconify icon="mdi:map-marker" width={16} />}
                  sx={{ flexShrink: 0, minWidth: 0, px: 1 }}
                >
                  Ver mapa
                </Button>
              )}
            </Box>
          )}
        </Box>
        {m.status === 'in_progress' ? (
          <LiveBadge />
        ) : (
          <Chip
            size="small"
            label={SL[m.status] || m.status}
            color={SC[m.status] || 'default'}
            variant={esResultadoFinal(m.status) ? 'filled' : 'outlined'}
          />
        )}
      </Box>

      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'center', gap: { xs: 2, sm: 4 } }}>
        <Equipo nombre={homeNombre} logo={m.homeClubLogoUrl} destacado={outcome.ganoLocal} />
        <Marcador m={m} outcome={outcome} size="large" />
        <Equipo nombre={awayNombre} logo={m.awayClubLogoUrl} destacado={outcome.ganoVisita} />
      </Box>

      {puedeVerCronologia && (
        <Box sx={{ textAlign: 'center', mt: 2 }}>
          <Button
            size="small"
            color="primary"
            onClick={function() { setExpandido(!expandido); }}
            startIcon={<Iconify icon={expandido ? 'eva:chevron-up-fill' : 'eva:chevron-down-fill'} width={16} />}
          >
            {expandido ? 'Ocultar cronología' : 'Ver cronología'}
          </Button>
          <Collapse in={expandido} unmountOnExit>
            <Cronologia
              orgSlug={data.orgSlug}
              compSlug={data.compSlug}
              matchId={m.id}
              homeTeamName={homeNombre}
              awayTeamName={awayNombre}
              homeClubLogoUrl={m.homeClubLogoUrl}
              awayClubLogoUrl={m.awayClubLogoUrl}
            />
          </Collapse>
        </Box>
      )}
      {m.venueMapsUrl && (
        <MapaSedeDialog open={mapaAbierto} onClose={function() { setMapaAbierto(false); }} titulo={cancha} mapsUrl={m.venueMapsUrl} />
      )}
    </Paper>
  );
}

function Equipo(props) {
  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 0.75, minWidth: 0, maxWidth: { xs: 88, sm: 130 } }}>
      <Avatar
        src={props.logo || undefined}
        variant="rounded"
        sx={{ width: { xs: 40, sm: 48 }, height: { xs: 40, sm: 48 }, bgcolor: 'action.hover', '& img': { objectFit: 'contain' } }}
      >
        {!props.logo && <Iconify icon="mdi:office-building-outline" width={24} sx={{ color: 'text.disabled' }} />}
      </Avatar>
      <Typography
        variant="body2"
        fontWeight={props.destacado ? 700 : 600}
        noWrap
        title={props.nombre}
        sx={{ maxWidth: '100%', color: props.destacado ? 'primary.main' : undefined }}
      >
        {props.nombre}
      </Typography>
    </Box>
  );
}

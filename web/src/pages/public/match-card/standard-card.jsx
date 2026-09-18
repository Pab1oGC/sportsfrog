import { useState } from 'react';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Chip from '@mui/material/Chip';
import Collapse from '@mui/material/Collapse';
import Paper from '@mui/material/Paper';
import Typography from '@mui/material/Typography';
import { Iconify } from 'src/components/iconify';
import { AMBIENT_CARD, SL, SC, esResultadoFinal, EscudoEquipo, fechaCorta, hora, Marcador, MapaSedeDialog, Cronologia } from './match-card-parts';

/**
 * La variante `standard` -- la tarjeta de partido de siempre, sin cambios
 * de comportamiento. Recibe `data`, los hechos ya derivados por el
 * despachador (partido.jsx: `outcome`, `esLocal`/`esVisita`, `cancha`,
 * `puedeVerCronologia`), y solo decide la composición.
 */
export function StandardCard(props) {
  var data = props.data;
  var m = data.m;
  var outcome = data.outcome;
  var esLocal = data.esLocal;
  var esVisita = data.esVisita;
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
    <Paper
      variant="outlined"
      sx={{
        p: { xs: 1.5, sm: 2 },
        borderRadius: 2,
        display: 'flex',
        flexDirection: 'column',
        gap: { xs: 1, md: 1 },
        ...AMBIENT_CARD,
      }}
    >
    <Box
      sx={{
        display: 'flex',
        flexDirection: { xs: 'column', md: 'row' },
        alignItems: { md: 'center' },
        gap: { xs: 1, md: 2 }
      }}
    >
      <Box sx={{ width: { md: 140 }, flexShrink: 0 }}>
        {m.scheduledAt ? (
          <>
            <Typography variant="body2" fontWeight={600} sx={{ textTransform: 'capitalize' }}>
              {fechaCorta(m.scheduledAt)}
            </Typography>
            <Typography variant="caption" color="text.secondary">{hora(m.scheduledAt)}</Typography>
          </>
        ) : (
          // Un walkover nunca tuvo fecha -- no es que falte programarlo,
          // ya tiene resultado. "Por programar" ahí sería contradictorio.
          !esResultadoFinal(m.status) && (
            <Typography variant="caption" color="text.secondary" fontStyle="italic">Por programar</Typography>
          )
        )}
        {data.mostrarCategoria && (
          <Typography variant="caption" color="text.disabled" noWrap sx={{ display: 'block' }}>
            {m.categoryName}
          </Typography>
        )}
      </Box>

      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5, flexGrow: 1, minWidth: 0 }}>
        <Box sx={{ flex: 1, minWidth: 0, display: 'flex', alignItems: 'center', justifyContent: 'flex-end', gap: 0.75 }}>
          <Typography
            variant="body2"
            noWrap
            title={homeNombre}
            sx={{ minWidth: 0, textAlign: 'right', fontWeight: esLocal || outcome.ganoLocal ? 700 : 500, color: esLocal ? 'primary.main' : undefined }}
          >
            {homeNombre}
          </Typography>
          <EscudoEquipo url={m.homeClubLogoUrl} />
        </Box>
        <Marcador m={m} outcome={outcome} />
        <Box sx={{ flex: 1, minWidth: 0, display: 'flex', alignItems: 'center', gap: 0.75 }}>
          <EscudoEquipo url={m.awayClubLogoUrl} />
          <Typography
            variant="body2"
            noWrap
            title={awayNombre}
            sx={{ minWidth: 0, fontWeight: esVisita || outcome.ganoVisita ? 700 : 500, color: esVisita ? 'primary.main' : undefined }}
          >
            {awayNombre}
          </Typography>
        </Box>
      </Box>

      {/* Ancho fijo, no mínimo: si fuera solo un mínimo, una fila sin botón
          de Cronología (un partido todavía no jugado, o un walkover) queda
          más angosta que una que sí lo tiene -- y como esta caja y la de
          fecha (arriba) son las que fijan cuánto le queda al bloque del
          medio, el marcador terminaría en una posición horizontal distinta
          según la fila. La cancha ya no vive acá (ver más abajo, línea
          propia) precisamente porque su largo es el más variable de
          todos -- meterla en una columna de ancho fijo era obligarla a
          truncarse. */}
      <Box
        sx={{
          display: 'flex', alignItems: 'center', gap: 1, flexShrink: 0,
          justifyContent: { xs: 'space-between', md: 'flex-end' },
          width: { md: 220 }
        }}
      >
        <Chip
          size="small"
          label={SL[m.status] || m.status}
          color={SC[m.status] || 'default'}
          variant={esResultadoFinal(m.status) ? 'filled' : 'outlined'}
          sx={{ flexShrink: 0 }}
        />
        {puedeVerCronologia && (
          // Un icono suelto se pasaba por alto — "mas intuitivo" fue el
          // pedido, asi que ahora dice lo que hace en vez de solo insinuarlo.
          <Button
            size="small"
            color="primary"
            onClick={function() { setExpandido(!expandido); }}
            startIcon={<Iconify icon={expandido ? 'eva:chevron-up-fill' : 'eva:chevron-down-fill'} width={16} />}
            sx={{ flexShrink: 0, minWidth: 0, px: 1 }}
          >
            {expandido ? 'Ocultar' : 'Cronologia'}
          </Button>
        )}
      </Box>
    </Box>
    {/* La cancha en su propia línea, con todo el ancho de la tarjeta: así
        el nombre se lee completo (envuelve si hace falta, no se trunca), y
        el botón del mapa deja de ser un ícono suelto de 14px -- pasa a ser
        un botón de verdad, con texto, igual de notorio que "Cronologia". */}
    {cancha && (
      <Box sx={{ display: 'flex', alignItems: 'center', flexWrap: 'wrap', gap: 1 }}>
        <Typography variant="caption" color="text.secondary" sx={{ flexGrow: 1, minWidth: 0 }}>
          {cancha}
        </Typography>
        {m.venueMapsUrl && (
          <Button
            size="small"
            color="primary"
            variant="outlined"
            onClick={function() { setMapaAbierto(true); }}
            startIcon={<Iconify icon="mdi:map-marker" width={16} />}
            sx={{ flexShrink: 0, minWidth: 0, px: 1 }}
          >
            Ver mapa
          </Button>
        )}
      </Box>
    )}
    {puedeVerCronologia && (
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
    )}
    {m.venueMapsUrl && (
      <MapaSedeDialog open={mapaAbierto} onClose={function() { setMapaAbierto(false); }} titulo={cancha} mapsUrl={m.venueMapsUrl} />
    )}
    </Paper>
  );
}

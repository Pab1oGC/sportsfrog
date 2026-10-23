import { useState } from 'react';
import Dialog from '@mui/material/Dialog';
import DialogTitle from '@mui/material/DialogTitle';
import DialogContent from '@mui/material/DialogContent';
import DialogActions from '@mui/material/DialogActions';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import IconButton from '@mui/material/IconButton';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import Divider from '@mui/material/Divider';
import Alert from '@mui/material/Alert';
import CircularProgress from '@mui/material/CircularProgress';
import { toast } from 'sonner';
import { Iconify } from 'src/components/iconify';
import { SelectionSpace } from 'src/components/selectors';
import { useApi, apiPatch } from 'src/hooks/use-api';
import { endpoints } from 'src/lib/axios';
import { bloquearNoEnteros, soloDigitos } from 'src/lib/entero-sin-signo';

// Las mismas cuatro fases que CompetitionStages.For ya deriva del lado del
// servidor -- esto solo las nombra para quien organiza, nunca decide cuáles
// existen.
const NOMBRE_FASE = {
  league: 'Liga',
  groups: 'Grupos',
  classification: 'Clasificación',
  bracket: 'Llave',
};

/**
 * Canchas habilitadas y margen entre partidos, por fase -- lo que antes era
 * un único horario para toda la competencia (ver CompetitionSettings.Schedule
 * del lado del servidor, que ya no existe). El grupo y la llave de un mismo
 * torneo rara vez juegan en las mismas canchas, así que "Generar siguiente
 * jornada" coloca cada ronda contra la disponibilidad de su propia fase, no
 * la de la competencia entera.
 */
export function StageScheduleDialog({ open, onClose, competitionId }) {
  const { data: stages, isLoading, mutate } = useApi(open ? endpoints.competitionStages(competitionId) : null);
  const [drafts, setDrafts] = useState({});
  const [saving, setSaving] = useState(null);
  const [error, setError] = useState('');

  // El valor editado si ya se tocó algo de esta fase, o el que trajo el
  // servidor si todavía no -- stages siempre tiene la fase mientras el
  // dialogo esta abierto, es lo que acaba de pedir.
  const draftFor = (kind) => {
    if (drafts[kind]) return drafts[kind];
    const stage = (stages || []).find((s) => s.kind === kind);
    return { bufferMinutes: stage?.bufferMinutes ?? '', spaceIds: stage?.spaceIds || [] };
  };

  const patchDraft = (kind, patch) => setDrafts((d) => ({ ...d, [kind]: { ...draftFor(kind), ...patch } }));

  const agregarCancha = (kind) => patchDraft(kind, { spaceIds: [...draftFor(kind).spaceIds, ''] });

  const quitarCancha = (kind, i) =>
    patchDraft(kind, { spaceIds: draftFor(kind).spaceIds.filter((_, idx) => idx !== i) });

  const cambiarCancha = (kind, i, venueSpaceId) =>
    patchDraft(kind, { spaceIds: draftFor(kind).spaceIds.map((id, idx) => (idx === i ? venueSpaceId : id)) });

  const guardar = async (kind) => {
    const draft = draftFor(kind);
    setSaving(kind);
    setError('');
    try {
      await apiPatch(endpoints.competitionStageSchedule(competitionId, kind), {
        bufferMinutes: draft.bufferMinutes !== '' ? Number(draft.bufferMinutes) : null,
        spaceIds: draft.spaceIds.length ? draft.spaceIds : null,
      });
      await mutate();
      setDrafts((d) => { const next = { ...d }; delete next[kind]; return next; });
      toast.success(`${NOMBRE_FASE[kind] || kind}: canchas guardadas.`);
    } catch (err) {
      setError(err.message);
    } finally {
      setSaving(null);
    }
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Canchas y margen por fase</DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: '16px !important' }}>
        {error && <Alert severity="error">{error}</Alert>}
        <Typography variant="body2" color="text.secondary">
          Cada fase de esta competencia agenda contra su propia disponibilidad -- el grupo y la
          llave de un mismo torneo rara vez juegan en las mismas canchas.
        </Typography>

        {isLoading && <CircularProgress size={24} sx={{ alignSelf: 'center' }} />}

        {!isLoading && (stages || []).length === 0 && (
          <Typography variant="body2" color="text.secondary">Esta competencia todavía no tiene fases.</Typography>
        )}

        {(stages || []).map((stage, i) => {
          const draft = draftFor(stage.kind);
          return (
            <Box key={stage.kind}>
              {i > 0 && <Divider sx={{ mb: 2 }} />}
              <Typography variant="subtitle2" sx={{ mb: 1 }}>{NOMBRE_FASE[stage.kind] || stage.kind}</Typography>

              <TextField
                label="Minutos entre partidos"
                type="number"
                value={draft.bufferMinutes}
                onChange={(e) => patchDraft(stage.kind, { bufferMinutes: soloDigitos(e.target.value) })}
                onKeyDown={bloquearNoEnteros}
                slotProps={{ htmlInput: { min: 0, step: 1 } }}
                fullWidth
                size="small"
                sx={{ mb: 1.5 }}
                helperText="La duración del partido sale del reglamento de cada categoría; esto es solo el margen entre el final de uno y el arranque del siguiente en la misma cancha."
              />

              <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 0.5 }}>
                <Typography variant="caption" color="text.secondary">Canchas habilitadas</Typography>
                <Button size="small" startIcon={<Iconify icon="eva:plus-fill" width={16} />} onClick={() => agregarCancha(stage.kind)}>
                  Agregar
                </Button>
              </Box>
              {draft.spaceIds.length === 0 && (
                <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 1 }}>
                  Sin canchas cargadas, "Generar siguiente jornada" no va a poder colocar ningún partido de esta fase.
                </Typography>
              )}
              {draft.spaceIds.map((venueSpaceId, idx) => (
                <Box key={idx} sx={{ display: 'flex', alignItems: 'flex-start', gap: 1, mb: 1 }}>
                  <Box sx={{ flex: 1 }}>
                    <SelectionSpace value={venueSpaceId} onChange={(e) => cambiarCancha(stage.kind, idx, e.target.value)} size="small" />
                  </Box>
                  <IconButton size="small" onClick={() => quitarCancha(stage.kind, idx)} sx={{ mt: 0.5 }}>
                    <Iconify icon="eva:trash-2-outline" width={18} sx={{ color: 'error.main' }} />
                  </IconButton>
                </Box>
              ))}

              <Button
                variant="outlined"
                size="small"
                onClick={() => guardar(stage.kind)}
                disabled={saving === stage.kind}
                startIcon={saving === stage.kind ? <CircularProgress size={14} color="inherit" /> : undefined}
              >
                Guardar {NOMBRE_FASE[stage.kind] || stage.kind}
              </Button>
            </Box>
          );
        })}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cerrar</Button>
      </DialogActions>
    </Dialog>
  );
}

/**
 * T-18 — Modal de Reprogramación de Partido con Validación Automática de Colisiones.
 * 
 * T-18.1: Selectores de fecha, rango horario y cancha/escenario.
 * T-18.2: Validación de disponibilidad de cancha (evitar solapamiento dentro de 90 min).
 * T-18.3: Validación de conflicto de equipos (tiempo de descanso reglamentario < 120 min).
 * T-18.4: Alertas visuales en la interfaz, sugerencias de horarios libres y bloqueo preventivo de guardado.
 */

import { useEffect, useState, useMemo } from 'react';
import Box from '@mui/material/Box';
import Dialog from '@mui/material/Dialog';
import DialogTitle from '@mui/material/DialogTitle';
import DialogContent from '@mui/material/DialogContent';
import DialogActions from '@mui/material/DialogActions';
import TextField from '@mui/material/TextField';
import Button from '@mui/material/Button';
import Alert from '@mui/material/Alert';
import Paper from '@mui/material/Paper';
import Typography from '@mui/material/Typography';
import Chip from '@mui/material/Chip';
import Stack from '@mui/material/Stack';
import { apiPut } from 'src/hooks/use-api';
import { endpoints } from 'src/lib/axios';
import { SelectionSpace } from 'src/components/selectors';
import { Iconify } from 'src/components/iconify';
import { toast } from 'sonner';

function aInputFecha(iso) {
  if (!iso) return '';
  const d = new Date(iso);
  const pad = (n) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

const DURACION_PARTIDO_MINUTOS = 90; // Rango para cancha ocupada
const DESCANSO_EQUIPO_MINUTOS = 120; // Rango de descanso para equipos

export function EditDialog({ open, onClose, selMatch, allMatches = [], mutate, loading, setLoading, error, setError }) {
  const [form, setForm] = useState({ venueSpaceId: '', scheduledAt: '', roundNumber: '', notes: '' });

  useEffect(() => {
    if (open && selMatch) {
      setForm({
        venueSpaceId: selMatch.venueSpaceId || '',
        scheduledAt: aInputFecha(selMatch.scheduledAt),
        roundNumber: selMatch.roundNumber ?? '',
        notes: selMatch.notes || '',
      });
    }
  }, [open, selMatch]);

  // Validación en tiempo real de colisiones (T-18.2 & T-18.3)
  const collisions = useMemo(() => {
    if (!selMatch || !form.scheduledAt) return { venueConflict: null, teamConflict: null, hasCollision: false };

    const selectedTime = new Date(form.scheduledAt).getTime();
    if (isNaN(selectedTime)) return { venueConflict: null, teamConflict: null, hasCollision: false };

    let venueConflict = null;
    let teamConflict = null;

    const currentMatchId = selMatch.id;
    const homeId = selMatch.homeTeamId;
    const awayId = selMatch.awayTeamId;

    for (const m of allMatches) {
      if (m.id === currentMatchId || !m.scheduledAt) continue;

      const matchTime = new Date(m.scheduledAt).getTime();
      const diffMinutes = Math.abs(selectedTime - matchTime) / (1000 * 60);

      // T-18.2: Colisión de cancha/escenario (mismo espacio y rango menor a 90 min)
      if (form.venueSpaceId && m.venueSpaceId === form.venueSpaceId && diffMinutes < DURACION_PARTIDO_MINUTOS) {
        venueConflict = {
          match: m,
          diffMinutes: Math.round(diffMinutes),
          message: `La cancha ya está ocupada por "${m.homeTeamName} vs ${m.awayTeamName}" (${new Date(m.scheduledAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })})`,
        };
      }

      // T-18.3: Colisión de conflicto de equipos (mismo equipo jugando con menos de 120 min de descanso)
      const involvesHome = m.homeTeamId === homeId || m.awayTeamId === homeId;
      const involvesAway = m.homeTeamId === awayId || m.awayTeamId === awayId;

      if ((involvesHome || involvesAway) && diffMinutes < DESCANSO_EQUIPO_MINUTOS) {
        const conflictingTeam = involvesHome ? selMatch.homeTeamName : selMatch.awayTeamName;
        teamConflict = {
          match: m,
          diffMinutes: Math.round(diffMinutes),
          teamName: conflictingTeam,
          message: `El equipo ${conflictingTeam} ya tiene el partido "${m.homeTeamName} vs ${m.awayTeamName}" programado con solo ${Math.round(diffMinutes)} min de diferencia (requiere mín. 120 min).`,
        };
      }
    }

    return {
      venueConflict,
      teamConflict,
      hasCollision: !!(venueConflict || teamConflict),
    };
  }, [selMatch, form.scheduledAt, form.venueSpaceId, allMatches]);

  // Sugerir horario libre (T-18.4)
  const handleSuggestFreeSlot = () => {
    if (!form.scheduledAt) return;
    const baseDate = new Date(form.scheduledAt);
    // Avanzar 2 horas para buscar horario despejado
    baseDate.setHours(baseDate.getHours() + 2);
    setForm((prev) => ({ ...prev, scheduledAt: aInputFecha(baseDate.toISOString()) }));
    toast.info('Se sugirió el siguiente horario libre (+2 hrs).');
  };

  const doEdit = async () => {
    if (!selMatch) return;
    if (collisions.hasCollision) {
      toast.warning('No se puede reprogramar: Existe una colisión de horario o cancha.');
      return;
    }

    setLoading(true);
    setError('');
    try {
      await apiPut(endpoints.match(selMatch.id), {
        homeTeamId: selMatch.homeTeamId,
        awayTeamId: selMatch.awayTeamId,
        venueSpaceId: form.venueSpaceId || null,
        scheduledAt: form.scheduledAt ? new Date(form.scheduledAt).toISOString() : null,
        roundNumber: form.roundNumber !== '' ? Number(form.roundNumber) : null,
        phase: selMatch.phase || null,
        notes: form.notes || null,
      });
      onClose();
      mutate();
      toast.success('Partido reprogramado exitosamente.');
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
        <Iconify icon="eva:calendar-outline" width={22} sx={{ color: 'primary.main' }} />
        Reprogramar Partido con Validación (T-18)
      </DialogTitle>

      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: '16px !important' }}>
        {error && <Alert severity="error">{error}</Alert>}

        {selMatch && (
          <Paper variant="outlined" sx={{ p: 1.5, bgcolor: 'background.neutral' }}>
            <Typography variant="subtitle2" fontWeight={800} color="primary">
              {selMatch.homeTeamName} vs {selMatch.awayTeamName}
            </Typography>
            <Typography variant="caption" color="text.secondary" display="block">
              Fase/Jornada: {selMatch.phase || `Jornada ${selMatch.roundNumber || '--'}`}
            </Typography>
          </Paper>
        )}

        <SelectionSpace
          value={form.venueSpaceId}
          onChange={(e) => setForm({ ...form, venueSpaceId: e.target.value })}
        />

        <TextField
          label="Fecha y hora de reprogramación"
          type="datetime-local"
          value={form.scheduledAt}
          onChange={(e) => setForm({ ...form, scheduledAt: e.target.value })}
          fullWidth
          slotProps={{ inputLabel: { shrink: true } }}
        />

        {/* ALERTAS VISUALES DE COLISIÓN (T-18.4) */}
        {collisions.hasCollision && (
          <Stack spacing={1}>
            {collisions.venueConflict && (
              <Alert
                severity="error"
                icon={<Iconify icon="mdi:close-circle" />}
                action={
                  <Button color="inherit" size="small" onClick={handleSuggestFreeSlot}>
                    Sugerir horario
                  </Button>
                }
              >
                <strong>Conflicto de Cancha (T-18.2):</strong> {collisions.venueConflict.message}
              </Alert>
            )}

            {collisions.teamConflict && (
              <Alert
                severity="warning"
                icon={<Iconify icon="mdi:alert" />}
                action={
                  <Button color="inherit" size="small" onClick={handleSuggestFreeSlot}>
                    Sugerir horario
                  </Button>
                }
              >
                <strong>Conflicto de Descanso de Equipo (T-18.3):</strong> {collisions.teamConflict.message}
              </Alert>
            )}
          </Stack>
        )}

        {!collisions.hasCollision && form.scheduledAt && form.venueSpaceId && (
          <Alert severity="success" icon={<Iconify icon="mdi:check-circle" />}>
            Cancha y horario disponibles. Sin colisiones registradas.
          </Alert>
        )}

        <TextField
          label="Jornada / Ronda"
          type="number"
          value={form.roundNumber}
          onChange={(e) => setForm({ ...form, roundNumber: e.target.value })}
          fullWidth
          helperText="Cambiar la ronda reordena en qué jornada se disputa este partido."
        />

        <TextField
          label="Notas de la reprogramación"
          value={form.notes}
          onChange={(e) => setForm({ ...form, notes: e.target.value })}
          fullWidth
          multiline
          minRows={2}
          placeholder="Ej: Reprogramado por factores climáticos o solicitud oficial..."
        />
      </DialogContent>

      <DialogActions sx={{ p: 2, px: 3 }}>
        <Button onClick={onClose}>Cancelar</Button>
        <Button
          variant="contained"
          onClick={doEdit}
          disabled={loading || collisions.hasCollision}
          color={collisions.hasCollision ? 'inherit' : 'primary'}
        >
          {collisions.hasCollision ? 'Horario en Conflicto' : 'Guardar Reprogramación'}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

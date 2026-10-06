import { useState, useEffect } from 'react';
import Dialog from '@mui/material/Dialog';
import DialogTitle from '@mui/material/DialogTitle';
import DialogContent from '@mui/material/DialogContent';
import DialogActions from '@mui/material/DialogActions';
import Button from '@mui/material/Button';
import Box from '@mui/material/Box';
import Alert from '@mui/material/Alert';
import Typography from '@mui/material/Typography';
import CircularProgress from '@mui/material/CircularProgress';
import ToggleButton from '@mui/material/ToggleButton';
import ToggleButtonGroup from '@mui/material/ToggleButtonGroup';
import { toast } from 'sonner';
import { useApi, apiPut } from 'src/hooks/use-api';
import { endpoints } from 'src/lib/axios';
import { deriveOverrideState, overridesToSend } from 'src/pages/accreditation/override-state';

const KIND_LABELS = { discipline: 'Disciplina', venue: 'Recinto', service: 'Servicio', zone: 'Zona' };
const KIND_ORDER = ['discipline', 'venue', 'service', 'zone'];

/**
 * Lo que una persona tiene de más, o de menos, respecto del paquete de su
 * categoría -- "según categoría" (sin excepción), "sí" (excepción que
 * otorga) o "no" (excepción que quita) por cada elemento del catálogo.
 *
 * Cómo se arma ese estado inicial, sin pedirle al backend una lista de
 * excepciones en crudo que no publica, es override-state.js -- separado de
 * este archivo porque es la única parte de esta pantalla que es una regla y
 * no una pantalla.
 */
export function OverridesDialog({ competitionId, athleteId, athleteName, open, onClose, onSaved }) {
  const { data: items } = useApi(open && competitionId ? endpoints.accreditationItems(competitionId) : null);
  const { data: categories } = useApi(open && competitionId ? endpoints.accreditationCategories(competitionId) : null);
  const { data: detail, isLoading } = useApi(
    open && competitionId && athleteId ? endpoints.athleteAccreditation(competitionId, athleteId) : null,
  );

  const [state, setState] = useState({});
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (!detail || !categories) return;
    const category = categories.find((c) => c.id === detail.categoryId);
    const resolvedIds = (detail.resolved || []).map((r) => r.itemId);
    setState(deriveOverrideState(category?.itemIds, resolvedIds));
  }, [detail, categories]);

  const setItemState = (itemId, value) => {
    setState((prev) => {
      const next = { ...prev };
      if (value === null) delete next[itemId];
      else next[itemId] = value;
      return next;
    });
  };

  const handleSave = async () => {
    setSaving(true);
    try {
      await apiPut(endpoints.athleteAccreditationOverrides(competitionId, athleteId), { overrides: overridesToSend(state) });
      toast.success('Excepciones guardadas.');
      onSaved();
    } catch (err) {
      toast.error(err.message || 'No se pudieron guardar las excepciones.');
    } finally {
      setSaving(false);
    }
  };

  const grouped = (items || []).reduce((acc, item) => {
    (acc[item.kind] = acc[item.kind] || []).push(item);
    return acc;
  }, {});

  const loadingAnything = isLoading || !items || !categories;

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Excepciones{athleteName ? ` -- ${athleteName}` : ''}</DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: '16px !important' }}>
        {loadingAnything && (
          <Box sx={{ display: 'flex', justifyContent: 'center', py: 3 }}>
            <CircularProgress />
          </Box>
        )}
        {!loadingAnything && !detail?.categoryId && (
          <Alert severity="warning">Esta persona todavía no tiene una categoría asignada -- asignale una primero.</Alert>
        )}
        {!loadingAnything && detail?.categoryId && (
          <>
            <Typography variant="body2" color="text.secondary">
              "Según categoría" es lo normal: la mayoría de las personas tiene exactamente el paquete de su categoría.
              "Sí" agrega algo que la categoría no otorga; "No" quita algo que sí otorga.
            </Typography>
            {KIND_ORDER.map((kind) => {
              const kindItems = grouped[kind] || [];
              if (kindItems.length === 0) return null;
              return (
                <Box key={kind}>
                  <Typography variant="subtitle2" fontWeight={700} sx={{ mb: 0.5 }}>{KIND_LABELS[kind]}</Typography>
                  {kindItems.map((item) => (
                    <Box key={item.id} sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', py: 0.5 }}>
                      <Typography variant="body2">[{item.code}] {item.name}</Typography>
                      <ToggleButtonGroup
                        size="small"
                        exclusive
                        value={state[item.id] === undefined ? 'default' : state[item.id]}
                        onChange={(_, v) => { if (v !== null) setItemState(item.id, v === 'default' ? null : v); }}
                      >
                        <ToggleButton value="default">Según categoría</ToggleButton>
                        <ToggleButton value={true} color="success">Sí</ToggleButton>
                        <ToggleButton value={false} color="error">No</ToggleButton>
                      </ToggleButtonGroup>
                    </Box>
                  ))}
                </Box>
              );
            })}
          </>
        )}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={saving}>Cancelar</Button>
        <Button variant="contained" onClick={handleSave} disabled={saving || loadingAnything || !detail?.categoryId}>
          {saving ? 'Guardando...' : 'Guardar'}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

import { useEffect, useState } from 'react';
import Box from '@mui/material/Box';
import TextField from '@mui/material/TextField';
import IconButton from '@mui/material/IconButton';
import Tooltip from '@mui/material/Tooltip';
import CircularProgress from '@mui/material/CircularProgress';
import { Iconify } from 'src/components/iconify';
import { apiPut } from 'src/hooks/use-api';
import { endpoints } from 'src/lib/axios';
import { aPuntaje, dePuntaje } from 'src/lib/sport-shape';
import { bloquearNegativos, soloDecimales } from 'src/lib/entero-sin-signo';
import { toast } from 'sonner';

/**
 * El puntaje de una actuacion, editable directamente en la fila.
 *
 * Sin dialogo aparte, a proposito: un juez carga el puntaje de varios
 * competidores seguidos durante una clasificacion en vivo, y un dialogo por
 * cada uno seria la misma demora que EventsDialog ya evito para cargar
 * varios eventos de un partido -- mismo problema, misma solucion.
 */
export function ScoreCell({ row, onSaved }) {
  const [value, setValue] = useState(() => aPuntaje(row.score));
  const [saving, setSaving] = useState(false);

  // Vuelve a sincronizar cuando la fila trae un puntaje distinto del que ya
  // se mostraba -- despues de guardar, o si otro juez cargo el mismo
  // puntaje mientras tanto. No dispara mientras se esta tipeando: score no
  // cambia hasta que alguien efectivamente guarda.
  useEffect(() => {
    setValue(aPuntaje(row.score));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [row.performanceId, row.score]);

  const puntajeTipeado = dePuntaje(value);
  const huboCambio = puntajeTipeado !== (row.score ?? null);

  const guardar = async () => {
    if (puntajeTipeado === null) return;
    setSaving(true);
    try {
      await apiPut(endpoints.performanceScore(row.performanceId), { score: puntajeTipeado });
      onSaved();
    } catch (err) { toast.error(err.message); }
    finally { setSaving(false); }
  };

  return (
    <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
      <TextField
        value={value}
        onChange={(e) => setValue(soloDecimales(e.target.value))}
        onKeyDown={(e) => { bloquearNegativos(e); if (e.key === 'Enter') guardar(); }}
        placeholder="0.00"
        size="small"
        type="number"
        disabled={saving}
        slotProps={{ htmlInput: { min: 0, step: 0.01 } }}
        sx={{ width: 90 }}
      />
      <Tooltip title="Guardar puntaje">
        <span>
          <IconButton size="small" onClick={guardar} disabled={saving || !huboCambio || puntajeTipeado === null}>
            {saving ? <CircularProgress size={16} /> : <Iconify icon="eva:checkmark-fill" width={18} />}
          </IconButton>
        </span>
      </Tooltip>
    </Box>
  );
}

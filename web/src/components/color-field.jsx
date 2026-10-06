import { useState, useEffect, useRef } from 'react';
import Box from '@mui/material/Box';
import TextField from '@mui/material/TextField';
import IconButton from '@mui/material/IconButton';
import { Iconify } from 'src/components/iconify';
import { isHex } from 'src/lib/portal-theme';

/**
 * Selector de color "#rrggbb": el cuadrito nativo del navegador + un campo de
 * texto, para quien prefiere escribir el código a mano.
 *
 * Extraído del estudio del portal, donde nació por un problema de rendimiento
 * específico de ese panel (ver el comentario de abajo) — ahora lo necesita
 * también el catálogo de acreditación, para el color de cada categoría y
 * cada zona.
 *
 * El selector nativo `<input type="color">` dispara su onChange en cada tick
 * mientras se arrastra adentro del picker -- no una vez al soltar -- así que
 * sin el draft de abajo, cualquier consumidor que reconstruya algo caro en
 * cada cambio (el estudio del portal reconstruía el tema entero) lo haría
 * docenas de veces por segundo mientras se arrastra el color.
 *
 * `draft` responde al instante -- el cuadrito y el campo de texto se sienten
 * fluidos arrastrando o tipeando -- y lo que sube al formulario (`onChange`)
 * se demora un instante corto, así que solo corre una vez cuando la persona
 * deja de mover el color.
 */
export function ColorField({ label, value, placeholder, onChange, help }) {
  const [draft, setDraft] = useState(value);
  const timeoutRef = useRef(null);

  useEffect(() => { setDraft(value); }, [value]);
  useEffect(() => () => clearTimeout(timeoutRef.current), []);

  const commit = (v) => {
    setDraft(v);
    clearTimeout(timeoutRef.current);
    timeoutRef.current = setTimeout(() => onChange(v), 120);
  };

  const clear = () => {
    clearTimeout(timeoutRef.current);
    setDraft('');
    onChange('');
  };

  const valid = isHex(draft);
  return (
    <Box>
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
        <Box
          component="input"
          type="color"
          value={valid ? draft : (isHex(placeholder) ? placeholder : '#F50057')}
          onChange={(e) => commit(e.target.value)}
          sx={{ width: 40, height: 40, p: 0, border: '1px solid', borderColor: 'divider', borderRadius: 1, bgcolor: 'transparent', cursor: 'pointer', flexShrink: 0 }}
        />
        <TextField
          label={label}
          value={draft}
          onChange={(e) => commit(e.target.value)}
          placeholder={placeholder}
          size="small"
          fullWidth
          error={!!draft && !valid}
          helperText={!!draft && !valid ? 'Se escribe como #rrggbb.' : help}
        />
        {draft && (
          <IconButton size="small" onClick={clear} aria-label={`Quitar ${label}`}>
            <Iconify icon="eva:close-outline" width={16} />
          </IconButton>
        )}
      </Box>
    </Box>
  );
}

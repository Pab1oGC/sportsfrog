import dayjs from 'dayjs';
import { DatePicker } from '@mui/x-date-pickers/DatePicker';

/**
 * Reemplazo de `<TextField type="date">`. Un `<input type="date">` nativo
 * muestra mm/dd/aaaa o dd/mm/aaaa segun el idioma del NAVEGADOR, no el de
 * la pagina -- pese al `lang="es"` del documento, un navegador en ingles
 * sigue mostrando el formato invertido. Este componente fuerza dd/mm/aaaa
 * sin importar el navegador (ver el LocalizationProvider con
 * adapterLocale="es" en app.jsx, que fija el idioma del calendario y los
 * nombres de mes).
 *
 * Entra y sale un string ISO "AAAA-MM-DD" (o "" sin fecha) -- mismo shape
 * que ya devolvia `<input type="date">`, mismo contrato que espera el
 * backend (DateOnly). `onChange` llega envuelto en un evento sintetico
 * `{ target: { value } }` para que cada formulario pueda seguir
 * escribiendo `(e) => ...e.target.value` sin reescribirse.
 */
export function DateField({ label, value, onChange, required, fullWidth, size, sx, helperText, disabled }) {
  const parsed = value ? dayjs(value) : null;

  return (
    <DatePicker
      label={label}
      value={parsed && parsed.isValid() ? parsed : null}
      onChange={(next) => {
        onChange({ target: { value: next && next.isValid() ? next.format('YYYY-MM-DD') : '' } });
      }}
      format="DD/MM/YYYY"
      disabled={disabled}
      slotProps={{ textField: { fullWidth, size, required, helperText, sx } }}
    />
  );
}

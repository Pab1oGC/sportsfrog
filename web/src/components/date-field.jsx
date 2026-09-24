import dayjs from 'dayjs';
import { DatePicker } from '@mui/x-date-pickers/DatePicker';
import { DesktopTimePicker } from '@mui/x-date-pickers/DesktopTimePicker';
import { DesktopDateTimePicker } from '@mui/x-date-pickers/DesktopDateTimePicker';
import { renderMultiSectionDigitalClockTimeView } from '@mui/x-date-pickers/timeViewRenderers';

// dayjs no tiene un tipo "solo hora": para parsear "14:30" hace falta
// colgarlo de una fecha, cualquiera, ya que TimeField nunca la lee de vuelta
// (ver TimeField mas abajo, que solo formatea con "HH:mm"). Evita sumar el
// plugin customParseFormat de dayjs solo para esto.
const TIME_ONLY_REFERENCE_DATE = '2000-01-01';

// Cada columna (MuiMultiSectionDigitalClockSection-root) trae de fabrica un
// ancho fijo de 56px -- de ahi las tres apretadas contra el borde izquierdo
// del popup con hueco vacio a la derecha. `flex: 1` las hace repartirse todo
// el ancho disponible por partes iguales en vez de quedarse en ese fijo.
//
// El riel de scroll se oculta sin sacarle el scroll: la columna sigue
// andando con la rueda del mouse o arrastrando, solo que sin la barra visible
// al lado -- el mismo aspecto que tiene el selector de hora nativo del
// sistema, que tampoco la muestra. `scrollbarWidth` es Firefox;
// `::-webkit-scrollbar` cubre Chrome, Edge y Safari.
const COMPACT_TIME_COLUMN_SX = {
  '& .MuiMultiSectionDigitalClockSection-root': {
    flex: 1,
    width: 'auto',
    scrollbarWidth: 'none',
    '&::-webkit-scrollbar': { display: 'none' },
  },
};

// Misma columna de siempre (MultiSectionDigitalClock), solo que con el sx de
// arriba sumado -- viewRenderers llama a esta funcion por cada columna
// (horas, minutos, y el AM/PM que agrega `ampm`), asi que las tres quedan
// anchas y sin riel visible.
//
// `sx` se agrega al arreglo que ya trae `props`, no lo reemplaza: el propio
// DesktopDateTimePicker le manda ahi su gridColumn (la ubicacion de esta
// columna al lado del calendario) y un maxHeight que iguala su alto al del
// calendario -- pisarlo entero fue lo que dejaba la columna con el alto
// fijo, mas bajo, de fabrica del componente (232px) en vez de ese alto
// igualado, que es la causa real de que se viera mas corta y con hueco vacio
// debajo.
const renderCompactTimeColumn = (props) => renderMultiSectionDigitalClockTimeView({
  ...props,
  sx: [...(Array.isArray(props.sx) ? props.sx : props.sx ? [props.sx] : []), COMPACT_TIME_COLUMN_SX],
});

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
 *
 * `minDate` y `maxDate` (strings "AAAA-MM-DD") apagan en el calendario los
 * dias fuera de rango, para que no se puedan elegir. Una fecha tipeada a mano
 * fuera de rango si llega a `onChange` -- el selector la marca en rojo pero no
 * la descarta --, asi que quien fija un limite tambien tiene que validarlo al
 * guardar. `error` fuerza el estado de error cuando lo decide el formulario.
 */
export function DateField({ label, value, onChange, required, fullWidth, size, sx, helperText, disabled, minDate, maxDate, error }) {
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
      minDate={minDate ? dayjs(minDate) : undefined}
      maxDate={maxDate ? dayjs(maxDate) : undefined}
      slotProps={{ textField: { fullWidth, size, required, helperText, sx, error: error || undefined } }}
    />
  );
}

/**
 * Como DateField, pero con hora -- para lo que necesita un instante y no
 * solo un dia: agendar un partido, no la fecha de nacimiento de alguien.
 *
 * Entra y sale un string local sin zona "AAAA-MM-DDTHH:mm" (o "" sin
 * fecha) -- mismo shape que ya devolvia `<input type="datetime-local">`, asi
 * que quien ya hacia `new Date(valor).toISOString()` para mandarselo al
 * backend (la hora que se ve es la hora local; ese `new Date` es lo que le
 * dice a JS cual, antes de convertirla) sigue funcionando igual sin
 * reescribirse.
 *
 * DesktopDateTimePicker en vez del `DateTimePicker` responsivo: ese elige
 * solo entre esta misma vista de escritorio y una variante "mobile" a
 * pantalla completa segun el tipo de puntero, y esa variante es la que se
 * sentia "como una pagina" para desplazar. Fijar la de escritorio siempre da
 * el mismo popup compacto sin importar el dispositivo. La hora se elige de
 * una columna que se desplaza -- eso esta bien -- solo que ancha (ocupando
 * todo el largo disponible) y sin el riel de scroll a la vista (ver
 * COMPACT_TIME_COLUMN_SX arriba), el mismo aspecto que el selector de hora
 * nativo del sistema. `ampm` agrega la columna AM/PM: es el mismo formato de
 * 12 horas que ya usa el resto de la app (ver lib/format-date.js).
 */
export function DateTimeField({ label, value, onChange, required, fullWidth, size, sx, helperText, disabled }) {
  const parsed = value ? dayjs(value) : null;

  return (
    <DesktopDateTimePicker
      label={label}
      value={parsed && parsed.isValid() ? parsed : null}
      onChange={(next) => {
        onChange({ target: { value: next && next.isValid() ? next.format('YYYY-MM-DDTHH:mm') : '' } });
      }}
      format="DD/MM/YYYY hh:mm A"
      ampm
      viewRenderers={{
        hours: renderCompactTimeColumn,
        minutes: renderCompactTimeColumn,
        meridiem: renderCompactTimeColumn,
      }}
      disabled={disabled}
      slotProps={{ textField: { fullWidth, size, required, helperText, sx } }}
    />
  );
}

/**
 * Como DateField, pero para un campo que es solo hora -- "Hora de inicio" al
 * generar la siguiente jornada, que ancla el primer partido sin llevar
 * fecha propia (la fecha, si hace falta, es el campo de al lado). Mismo
 * reloj digital compacto que ya usa DateTimeField (columnas anchas, AM/PM,
 * sin riel de scroll a la vista) para lo mismo: que un campo de hora se vea
 * y se sienta igual en cualquier parte de la app, en vez de que cada
 * formulario tenga el suyo -- antes era un `<input type="time">` nativo acá.
 *
 * Entra y sale un string "HH:mm" (o "" sin hora) -- mismo shape que ya
 * devolvia `<input type="time">`, mismo contrato que espera el backend
 * (TimeOnly).
 */
export function TimeField({ label, value, onChange, required, fullWidth, size, sx, helperText, disabled }) {
  const parsed = value ? dayjs(`${TIME_ONLY_REFERENCE_DATE}T${value}`) : null;

  return (
    <DesktopTimePicker
      label={label}
      value={parsed && parsed.isValid() ? parsed : null}
      onChange={(next) => {
        onChange({ target: { value: next && next.isValid() ? next.format('HH:mm') : '' } });
      }}
      format="hh:mm A"
      ampm
      viewRenderers={{
        hours: renderCompactTimeColumn,
        minutes: renderCompactTimeColumn,
        meridiem: renderCompactTimeColumn,
      }}
      disabled={disabled}
      slotProps={{ textField: { fullWidth, size, required, helperText, sx } }}
    />
  );
}

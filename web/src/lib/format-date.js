// dd/mm/aaaa hh:mm AM/PM, fijo — no el que toLocaleString() arma segun el
// idioma y la configuracion regional del navegador de quien lo mire, que
// hacia que el mismo partido se leyera distinto de una maquina a otra.
export function fechaHora(iso) {
  if (!iso) return '';
  const d = new Date(iso);
  const pad = (n) => String(n).padStart(2, '0');

  const dd = pad(d.getDate());
  const mm = pad(d.getMonth() + 1);
  const yyyy = d.getFullYear();

  let horas = d.getHours();
  const ampm = horas >= 12 ? 'PM' : 'AM';
  horas = horas % 12 || 12;

  return `${dd}/${mm}/${yyyy} ${pad(horas)}:${pad(d.getMinutes())} ${ampm}`;
}

// El input datetime-local quiere hora local sin zona ("2026-05-01T16:00"), y
// lo que guarda el partido es una fecha con zona ("...Z" o "+00:00").
// Formatear a mano evita el redondeo raro que a veces da toISOString con la
// hora local. Compartida entre EditDialog y el reprogramado en bloque —
// los dos editan la misma fecha del mismo partido.
export function aFechaInput(iso) {
  if (!iso) return '';
  const d = new Date(iso);
  const pad = (n) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

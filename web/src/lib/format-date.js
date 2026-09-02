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

import { ventanaDeMinuto } from 'src/lib/sport-shape';

/**
 * El minuto de un evento tiene que caber en el periodo elegido: el 99 no
 * existe en el 1.er tiempo de un partido de 45 (ver ventanaDeMinuto). El
 * servidor lo exige igual; esto lo avisa antes de mandar y le pone límites
 * al campo de EventsDialog.
 *
 * `ayudaDeMinuto` tiene cuatro caminos: sin ventana (deporte por sets o
 * juzgado, o todavía sin período elegido) no dice nada; fuera de rango
 * explica el motivo con el rango válido; dentro del adicional lo aclara
 * ("Adicional: 45+3"); dentro de lo regular solo muestra el rango como
 * ayuda pasiva.
 */
export function validarMinuto(sportInfo, periodNumber, minute) {
  const ventana = ventanaDeMinuto(sportInfo, Number(periodNumber));
  const minuto = minute === '' ? null : Number(minute);
  const minutoFuera = Boolean(ventana) && minuto !== null && (minuto < ventana.desde || minuto > ventana.hasta);
  const rangoDeMinuto = ventana ? `${Math.max(1, ventana.desde)}–${ventana.regular} (+${ventana.adicional})` : null;
  const ayudaDeMinuto = !ventana ? undefined
    : minutoFuera ? `Fuera de ${sportInfo.periodLabel} ${periodNumber}: va de ${rangoDeMinuto}`
    : minuto > ventana.regular ? `Adicional: ${ventana.regular}+${minuto - ventana.regular}`
    : rangoDeMinuto;

  return { ventana, minutoFuera, ayudaDeMinuto };
}

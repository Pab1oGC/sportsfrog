/**
 * El copy y la estructura de las secciones de la landing -- lo único que
 * cambia si se agrega, reordena o reescribe una sección. Ni section-panel.jsx
 * ni section-nav.jsx conocen "Competencias" o "Portal": solo recorren este
 * array, así que agregar una sección nueva no toca ninguno de los dos.
 *
 * Cada entrada describe un módulo real de la API (ver api/src/SportFrog.Api/
 * Features/) -- el copy no inventa una sola cifra ni una funcionalidad que no
 * exista. Cuatro pilares, no cinco: "Competencias" junta lo que antes eran
 * tres secciones separadas (Reglamento, Calendario, Resultados) en una sola,
 * como sus tres sub-características -- son un solo momento para quien
 * compra esto ("¿puedo crear y correr mi competencia acá?"), no tres
 * funcionalidades sueltas. "Escuelas" es nueva: el motor de escuelitas
 * deportivas con pensión mensual (Features/Courses + Features/Tuition) es,
 * para muchos clubes, el ingreso recurrente principal -- no un añadido.
 *
 * @typedef {object} LandingSection
 * @property {string} id - Ancla de la sección (`#id`) y clave de React.
 * @property {string} navLabel - Texto corto para la pill de section-nav.jsx.
 * @property {string} navIcon - Ícono de Iconify para esa misma pill.
 * @property {string} eyebrow - Rótulo pequeño sobre el título (mayúsculas).
 * @property {string[]} title - Líneas del titular (una `<span>` por línea).
 * @property {string} description - Párrafo bajo el titular.
 * @property {{icon: string, title: string, desc: string}[]} subFeatures -
 *   Exactamente tres, como la fila de tres columnas que las muestra.
 * @property {string} insteadOf - La alternativa manual que reemplaza, para
 *   dar contexto sin inventar una métrica.
 */

/** @type {LandingSection[]} */
export const LANDING_SECTIONS = [
  {
    id: 'competencias',
    navLabel: 'Competencias',
    navIcon: 'mdi:trophy-outline',
    eyebrow: 'Competencias',
    title: ['Una competencia por deporte,', 'lista para correr'],
    description:
      'Fútbol, futsal, vóleibol, básquetbol o wally: elegís el deporte del catálogo y la competencia corre con las reglas, el calendario y la tabla de ese deporte -- de borrador a finalizada, sin salir de la misma pantalla.',
    subFeatures: [
      {
        icon: 'mdi:gavel',
        title: 'Reglamento propio por deporte',
        desc: 'Por puntos, por sets, contra el reloj o con la nota de un juez: cada deporte valida su propia forma de resultado antes de guardarse.',
      },
      {
        icon: 'mdi:calendar-clock-outline',
        title: 'Calendario que se arma solo',
        desc: 'El sistema reparte los cruces de cada jornada entre sedes y horarios, y avanza de fase a fase solo cuando el reglamento lo permite.',
      },
      {
        icon: 'mdi:refresh',
        title: 'Resultados que nunca quedan viejos',
        desc: 'La tabla de posiciones se recalcula en cada lectura: corregir un resultado no deja nada desactualizado en otro lado.',
      },
    ],
    insteadOf: 'En vez de armar cada deporte a mano, con sus propias reglas sueltas',
  },
  {
    id: 'portal',
    navLabel: 'Portal',
    navIcon: 'mdi:earth',
    eyebrow: 'Portal público',
    title: ['Tu portal, publicado', 'con tu marca'],
    description:
      'El portal público sale con los colores, la tipografía y el diseño que elija la organización, y queda legible sin importar la combinación: el contraste se valida antes de guardar. Se abre sin cuenta, para cualquiera que tenga el enlace.',
    subFeatures: [
      {
        icon: 'mdi:palette-outline',
        title: 'Con tu marca, no la nuestra',
        desc: 'Colores, tipografía, estilo del encabezado y qué secciones mostrar se eligen por competencia, desde un editor visual.',
      },
      {
        icon: 'mdi:eye-check-outline',
        title: 'Legible siempre',
        desc: 'El contraste entre el color elegido y el texto se verifica antes de guardarlo, para que una combinación llamativa no vuelva el portal ilegible.',
      },
      {
        icon: 'mdi:link-variant',
        title: 'Público, sin cuenta',
        desc: 'Resultados, tablas y calendario se ven desde el enlace, sin que quien lo abre necesite una cuenta ni haya iniciado sesión.',
      },
    ],
    insteadOf: 'En vez de mandar el PDF del fixture por grupo',
  },
  {
    id: 'credenciales',
    navLabel: 'Credenciales',
    navIcon: 'mdi:certificate-outline',
    eyebrow: 'Credenciales',
    title: ['Credenciales en lote,', 'verificables una por una'],
    description:
      'Los datos de cada persona se acomodan sobre el arte que subió la organización, y la emisión sale en lote para toda una categoría. Cada credencial queda verificable con un QR: válida, vencida o revocada, nunca solo un PDF.',
    subFeatures: [
      {
        icon: 'mdi:image-edit-outline',
        title: 'Tu diseño, sus datos',
        desc: 'La plantilla la define la organización; el sistema solo acomoda nombre, foto y categoría de cada persona sobre ese diseño.',
      },
      {
        icon: 'mdi:format-list-checks',
        title: 'Un lote no falla en silencio',
        desc: 'Si una credencial no puede emitirse --diseño retirado, falta el arte-- queda registrado contra esa credencial puntual, no contra todo el lote.',
      },
      {
        icon: 'mdi:qrcode-scan',
        title: 'Verificable con QR',
        desc: 'Cualquiera puede escanear una credencial y saber si sigue vigente, si ya venció o si la organización la revocó.',
      },
    ],
    insteadOf: 'En vez de diseñar e imprimir cada credencial a mano',
  },
  {
    id: 'escuelas',
    navLabel: 'Escuelas',
    navIcon: 'mdi:school-outline',
    eyebrow: 'Escuelas',
    title: ['La escuela del club,', 'con la pensión al día'],
    description:
      'Cursos con cupo y horario semanal, inscripción de alumnos y una pensión mensual que se devenga sola, mes a mes, sin que nadie tenga que acordarse de cobrarla.',
    subFeatures: [
      {
        icon: 'mdi:calendar-account-outline',
        title: 'Cursos con cupo y horario',
        desc: 'Cada curso define su horario semanal, su cupo y su pensión; inscribir a un alumno o a varios de una vez lleva un clic.',
      },
      {
        icon: 'mdi:refresh',
        title: 'La pensión se devenga sola',
        desc: 'Una vez al mes, el sistema genera el cargo de cada inscripción vigente. Correrlo dos veces no duplica nada.',
      },
      {
        icon: 'mdi:receipt-text-check-outline',
        title: 'Cobranza sin perseguir a nadie',
        desc: 'Morosos, comprobantes por revisar y el estado de cada cuenta, siempre al día -- pendiente, parcial, pagada o condonada.',
      },
    ],
    insteadOf: 'En vez de un cuaderno de pensiones y recordatorios por WhatsApp',
  },
];

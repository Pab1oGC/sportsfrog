// Traduce la forma generica que manda la API (columnas tipadas + secciones
// de filas-como-arreglo) a lo que un DataGrid de MUI necesita: columnas con
// field/headerName y cada fila como un objeto por campo.
//
// Las filas llegan como arreglos -- ListSection.Rows en el backend -- en vez
// de objetos con nombre de columna porque la API no sabe de antemano cuántas
// columnas va a tener cada lista; esta es la única traducción a lo que el
// grid espera, y vive separada de la página para poder pinchar la alineación
// numérica y el recorrido de filas sin montar ningún componente.

// Un ancho fijo por tipo para todo lo que es corto por naturaleza (un
// puesto, un booleano, una fecha) -- darle el mismo flex:1 que a una columna
// de texto libre (lo que esto hacia antes de este ancho por kind) era
// exactamente por que "Pos." salia tan ancha como "Equipo": las dos se
// repartian el espacio en partes iguales sin que importara cuanto
// necesitaba cada una. Solo el texto (nombres, lo que sea de largo
// variable) se deja crecer con flex; todo lo demas pide nada mas que lo
// que su propio contenido ocupa.
const WIDTH_BY_KIND = {
  number: 90,
  boolean: 100,
  date: 120,
  date_time: 170,
};

// Un numero se lee por su ultima cifra y un booleano por su propio centro --
// ninguno de los dos se alinea como una etiqueta, el mismo criterio que
// ListPdf.AlignmentFor ya aplica (y que el encabezado tiene que seguir
// exactamente, o el dato queda descentrado respecto de su propio titulo).
//
// La excepcion es la primera columna: ahi un numero es siempre un puesto,
// nunca una cantidad para comparar por magnitud -- "Pos." alineado a la
// derecha queda pegado al borde interno con puro espacio vacio por delante
// en cada fila, que es exactamente el "ocupa mucho espacio hacia su
// izquierda" que esto corrige. Centrado, como ya muestra cualquier tabla de
// posiciones.
export function alignmentFor(kind, isFirstColumn) {
  if (isFirstColumn && kind === 'number') return 'center';
  if (kind === 'number') return 'right';
  if (kind === 'boolean') return 'center';
  return 'left';
}

export function gridColumns(columns) {
  return (columns || []).map((column, index) => {
    const align = alignmentFor(column.kind, index === 0);
    const width = WIDTH_BY_KIND[column.kind];

    return {
      field: `c${index}`,
      headerName: column.header,
      align,
      headerAlign: align,
      ...(width ? { width } : { flex: 1, minWidth: 160 }),
    };
  });
}

export function gridRows(rows) {
  return (rows || []).map((row, index) => {
    const record = { id: index };
    row.forEach((value, column) => { record[`c${column}`] = value; });
    return record;
  });
}

// Traduce la forma generica que manda la API (columnas tipadas + secciones
// de filas-como-arreglo) a lo que un DataGrid de MUI necesita: columnas con
// field/headerName y cada fila como un objeto por campo.
//
// Las filas llegan como arreglos -- ListSection.Rows en el backend -- en vez
// de objetos con nombre de columna porque la API no sabe de antemano cuántas
// columnas va a tener cada lista; esta es la única traducción a lo que el
// grid espera, y vive separada de la página para poder pinchar la alineación
// numérica y el recorrido de filas sin montar ningún componente.

export function gridColumns(columns) {
  return (columns || []).map((column, index) => ({
    field: `c${index}`,
    headerName: column.header,
    flex: 1,
    minWidth: 120,
    // Un numero se lee por su ultima cifra, una etiqueta no -- el mismo
    // criterio que ListPdf ya aplica alineando a la derecha.
    align: column.kind === 'number' ? 'right' : 'left',
    headerAlign: column.kind === 'number' ? 'right' : 'left',
  }));
}

export function gridRows(rows) {
  return (rows || []).map((row, index) => {
    const record = { id: index };
    row.forEach((value, column) => { record[`c${column}`] = value; });
    return record;
  });
}

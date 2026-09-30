export var MAXIMO_CAMPOS = 60; // LayoutPolicy.MaximumFields

/** Las únicas propiedades que TemplateField conoce. Cualquier otra es un 400. */
export var PROPIEDADES = ['source', 'x', 'y', 'w', 'h', 'size', 'font', 'align', 'fit', 'minSize', 'color', 'bold', 'text'];

export function caraVacia(proporcion) {
  return { backgroundKey: null, aspectRatio: proporcion || 1.5875, fields: [] };
}

export function limpiarCampo(campo) {
  var limpio = {};
  PROPIEDADES.forEach(function (p) {
    if (campo[p] !== undefined) limpio[p] = campo[p];
  });
  return limpio;
}

/** Solo lo que TemplateFace declara, con los campos podados igual. */
export function limpiarCara(cara) {
  if (!cara) return null;
  return {
    backgroundKey: cara.backgroundKey || null,
    aspectRatio: cara.aspectRatio || 1.5875,
    fields: (cara.fields || []).map(limpiarCampo),
  };
}

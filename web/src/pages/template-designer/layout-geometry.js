/* ---------------------------------------------------------------------------
   Los límites son los de LayoutPolicy.cs, repetidos acá a propósito: sujetar
   el arrastre dentro de lo que la API acepta es la única forma de que mover
   una caja con el mouse no termine en un 400 al guardar. "La imagen se sale
   de la cara" es literalmente el error que un editor de arrastre provoca.
   --------------------------------------------------------------------------- */

export var MINIMO = 0.005;      // LayoutPolicy.MinimumExtent
export var TAMANO_MAXIMO = 0.5; // LayoutPolicy.MaximumTextSize

export function limitar(v, min, max) {
  return Math.min(max, Math.max(min, v));
}

/**
 * Un campo de texto también es una caja: su alto es `size`, que es como lo
 * trata el generador. Tratar los dos por igual deja un solo modelo de
 * arrastre en lugar de dos que se parecen.
 */
export function cajaDe(campo, imagen) {
  return {
    x: campo.x || 0,
    y: campo.y || 0,
    w: campo.w != null ? campo.w : (1 - (campo.x || 0)),
    h: imagen ? (campo.h || 0.1) : (campo.size || 0.06),
  };
}

/**
 * Lo que hay que escribir de vuelta en el campo tras mover o redimensionar
 * su caja. El mínimo del ajuste "shrink" (`minSize`) no puede quedar por
 * encima del tamaño nuevo, y achicar la caja con el mouse es justo lo que lo
 * dejaría por encima -- si pasa, se baja `minSize` junto con `size`.
 */
export function cambiosDesdeCaja(caja, imagen, campoActual) {
  if (imagen) {
    return { x: caja.x, y: caja.y, w: caja.w, h: caja.h };
  }

  var tam = Math.min(caja.h, TAMANO_MAXIMO);
  var cambios = { x: caja.x, y: caja.y, w: caja.w, size: tam };

  if (campoActual && campoActual.minSize != null && campoActual.minSize > tam) cambios.minSize = tam;

  return cambios;
}

/**
 * El álgebra de redimensionado: sin asa (arrastrando el cuerpo), mueve la
 * caja entera sin cambiarle el tamaño. Con una asa, crece o achica desde el
 * borde que le toca a esa asa -- las cuatro banderas (izq/der/arr/aba) de la
 * asa son independientes, así que una esquina las combina de a dos.
 *
 * `techo`: una imagen puede ocupar hasta toda la cara (1); un texto no puede
 * pasar de TAMANO_MAXIMO (LayoutPolicy.MaximumTextSize), sin importar cuánto
 * se estire la caja con el mouse.
 */
export function calcular(g, dx, dy) {
  var c = g.caja;
  var techo = g.imagen ? 1 : TAMANO_MAXIMO;

  if (!g.asa) {
    return {
      x: limitar(c.x + dx, 0, Math.max(0, 1 - c.w)),
      y: limitar(c.y + dy, 0, Math.max(0, 1 - c.h)),
      w: c.w,
      h: c.h,
    };
  }

  var x = c.x, y = c.y, w = c.w, h = c.h;

  if (g.asa.izq) {
    var nx = limitar(c.x + dx, 0, c.x + c.w - MINIMO);
    w = c.w + (c.x - nx);
    x = nx;
  }
  if (g.asa.der) {
    w = limitar(c.w + dx, MINIMO, 1 - c.x);
  }
  if (g.asa.arr) {
    var ny = limitar(c.y + dy, 0, c.y + c.h - MINIMO);
    h = limitar(c.h + (c.y - ny), MINIMO, techo);
    y = c.y + c.h - h;
  }
  if (g.asa.aba) {
    h = limitar(c.h + dy, MINIMO, Math.min(techo, 1 - c.y));
  }

  return { x: x, y: y, w: w, h: h };
}

import { sinMovimiento } from 'src/lib/motion';

/**
 * Lleva el scroll al elemento con ese id, respetando `prefers-reduced-motion`
 * -- lo que antes vivía suelto dentro del handler de click de section-nav.jsx
 * y ahora también usa site-footer.jsx (los enlaces de "Producto" apuntan a
 * las mismas anclas que las pills del nav, así que tienen que llevar a lo
 * mismo exactamente de la misma forma).
 */
export function scrollToSection(id) {
  const target = document.getElementById(id);
  if (target) target.scrollIntoView({ behavior: sinMovimiento() ? 'auto' : 'smooth', block: 'start' });
}

import { toast } from 'sonner';

/**
 * Muestra error en toast. Centraliza el manejo de errores de API en una
 * función que todas las páginas pueden llamar sin repetir try/catch.
 */
export function showError(err) {
  const msg = err?.message || 'Ocurrió un error inesperado.';
  toast.error(msg);
}

export function showSuccess(msg) {
  toast.success(msg);
}
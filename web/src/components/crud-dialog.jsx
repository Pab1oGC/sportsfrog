import { useEffect, useState } from 'react';
import Dialog from '@mui/material/Dialog';
import DialogTitle from '@mui/material/DialogTitle';
import DialogContent from '@mui/material/DialogContent';
import DialogActions from '@mui/material/DialogActions';
import Button from '@mui/material/Button';
import Alert from '@mui/material/Alert';
import CircularProgress from '@mui/material/CircularProgress';

/**
 * Diálogo CRUD genérico: acepta children como contenido.
 *
 * entityGender ('m' por defecto, 'f' para "sede", "inscripción", etc.)
 * decide "Nuevo"/"Nueva" -- mismo criterio de genero que ya usa
 * useCrudDialog para "eliminado"/"eliminada", asi que un llamador que ya lo
 * calcula para el borrado no tiene que inventar un segundo valor aca.
 */
export function CrudDialog({ open, editId, entityName, entityGender = 'm', error, saving, onClose, onSave, children, maxWidth = 'sm' }) {
  // El "×" del Alert de abajo tiene que descartar solo el mensaje de error,
  // no el diálogo entero -- antes reusaba el mismo onClose del diálogo y de
  // paso perdía el formulario cargado. errorDescartado guarda el último
  // mensaje ya cerrado y se compara por valor (no un booleano) para que un
  // error nuevo -- incluso con el mismo texto, tras reintentar guardar -- se
  // vuelva a mostrar: se limpia apenas arranca un guardado nuevo (saving
  // pasa a true), que es el único momento en que un mensaje viejo deja de
  // ser válido.
  const [errorDescartado, setErrorDescartado] = useState(null);

  useEffect(() => {
    if (saving) setErrorDescartado(null);
  }, [saving]);

  const mostrarError = Boolean(error) && error !== errorDescartado;

  return (
    <Dialog
      open={open}
      onClose={onClose}
      maxWidth={maxWidth}
      fullWidth
      // Explicito, no confiado al comportamiento por defecto de MUI: un
      // formulario largo (Competiciones, con sus tres acordeones) puede
      // crecer mas alto que la ventana, y sin un tope fijo acá el Paper del
      // diálogo simplemente sigue creciendo hacia afuera de la pantalla en
      // vez de quedarse del tamaño de la ventana y dejar que el contenido
      // interno (el DialogContent de abajo) sea lo que scrollea.
      slotProps={{ paper: { sx: { maxHeight: 'calc(100% - 64px)' } } }}
    >
      <DialogTitle>{editId ? 'Editar' : (entityGender === 'f' ? 'Nueva' : 'Nuevo')} {entityName}</DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: '16px !important', overflowY: 'auto' }}>
        {mostrarError && <Alert severity="error" onClose={() => setErrorDescartado(error)}>{error}</Alert>}
        {children}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={saving}>Cancelar</Button>
        <Button variant="contained" onClick={onSave} disabled={saving}>
          {saving ? <CircularProgress size={20} sx={{ mr: 1 }} /> : null}
          Guardar
        </Button>
      </DialogActions>
    </Dialog>
  );
}
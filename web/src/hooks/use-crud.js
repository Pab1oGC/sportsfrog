import { useState, useCallback } from 'react';
import { useApi, apiPost, apiPut, apiDelete } from 'src/hooks/use-api';
import { withQueryParams } from 'src/lib/query-string';
import { useConfirm } from 'src/components/confirm-dialog';
import { toast } from 'sonner';

/**
 * Hook que encapsula el patrón CRUD repetido en cada página.
 *
 * Encapsula: lista, diálogo abierto/cerrado, formulario, guardado y borrado.
 * Cada página que antes copiaba 80 líneas de useState + handleOpen + handleSave
 * + handleDelete ahora llama a useCrudDialog y le queda un componente limpio.
 *
 * @param {object} opts
 * @param {string} opts.resourceUrl - Endpoint de la colección (endpoints.clubs).
 *   Tambien de donde se lee la lista (GET) y, salvo que se pase createUrl, a
 *   donde se manda el alta (POST).
 * @param {string|function} [opts.createUrl] - Endpoint del alta, si es distinto
 *   de resourceUrl -- caso real: EnrollIndividual vive en una direccion propia
 *   (/categories/{id}/individuals), no en la misma coleccion que su lectura
 *   (/categories/{id}/teams). Por defecto, resourceUrl. Tambien puede ser una
 *   funcion (form) => string, para cuando la propia direccion depende de lo
 *   que se está por mandar -- caso real: NominaPage manda a un endpoint
 *   distinto si se eligio mas de un deportista a la vez. Se resuelve en
 *   save(), con el `form` mas reciente, no con el que existia cuando se
 *   armaron las opciones del hook.
 * @param {object|function} opts.emptyForm - Estado inicial del formulario, o una
 *   función que lo devuelve (para no compartir el mismo objeto entre aperturas)
 * @param {string} opts.entityName - Nombre legible ("club", "reglamento") para
 *   diálogos de confirmación y mensajes — en minúscula y género masculino;
 *   ver entityGender para sustantivos femeninos ("sede", "categoria")
 * @param {'m'|'f'} [opts.entityGender] - Género gramatical de entityName, solo
 *   para el participio del mensaje de borrado: "eliminado" vs "eliminada".
 *   Por defecto masculino.
 * @param {string|function} [opts.savedMessage] - Mensaje de éxito al guardar,
 *   o una función (result, wasEdit) => mensaje. Sin esto, guardar no muestra
 *   toast — el diálogo cerrándose ya es la confirmación.
 * @param {function} [opts.buildUrl] - Custom URL builder: (base, editId) => string
 * @param {function} [opts.mapToForm] - Transforma row del DataGrid a objeto form
 * @param {function} [opts.mapToSend] - (form, wasEdit) => body a enviar. wasEdit
 *   solo importa cuando alta y edicion piden contratos distintos -- ver
 *   createUrl arriba para el mismo problema del lado de la direccion.
 * @param {function} [opts.onSaved] - Callback (result, wasEdit) después de guardar
 * @param {{skip: number, take: number}} [opts.pageParams] - Activa paginación
 *   de servidor: agrega ?skip=&take= a resourceUrl y expone `rowCount` (leído
 *   de la cabecera X-Total-Count que el backend manda solo cuando se pide
 *   una página — ver PagedListing). Ausente, que es el caso de la mayoría de
 *   las páginas que usan este hook, el listado se pide entero, exactamente
 *   como siempre: nada de esto se activa a menos que la propia página pida
 *   una página.
 */
export function useCrudDialog(opts) {
  const {
    resourceUrl,
    createUrl = resourceUrl,
    emptyForm,
    entityName = 'registro',
    entityGender = 'm',
    savedMessage,
    buildUrl,
    mapToForm = (row) => row,
    mapToSend = (form) => form,
    onSaved,
    pageParams,
  } = opts;

  const confirm = useConfirm();
  const listUrl = pageParams ? withQueryParams(resourceUrl, pageParams) : resourceUrl;
  const { data, totalCount, mutate, isLoading } = useApi(listUrl);
  const [open, setOpen] = useState(false);
  const [editId, setEditId] = useState(null);
  const [form, setForm] = useState(emptyForm);
  const [error, setError] = useState('');
  const [saving, setSaving] = useState(false);

  const getUrl = useCallback(
    (id) => (buildUrl ? buildUrl(resourceUrl, id) : `${resourceUrl}/${id}`),
    [resourceUrl, buildUrl],
  );

  const openCreate = useCallback(() => {
    setEditId(null);
    setForm(typeof emptyForm === 'function' ? emptyForm() : { ...emptyForm });
    setError('');
    setOpen(true);
  }, [emptyForm]);

  const openEdit = useCallback(
    (row) => {
      setEditId(row.id);
      setForm(mapToForm(row));
      setError('');
      setOpen(true);
    },
    [mapToForm],
  );

  const close = useCallback(() => setOpen(false), []);

  const save = useCallback(async () => {
    setSaving(true);
    setError('');
    try {
      const wasEdit = Boolean(editId);
      const body = mapToSend(form, wasEdit);
      const target = typeof createUrl === 'function' ? createUrl(form) : createUrl;
      const result = wasEdit ? await apiPut(getUrl(editId), body) : await apiPost(target, body);
      setOpen(false);
      mutate();
      if (savedMessage) {
        toast.success(typeof savedMessage === 'function' ? savedMessage(result, wasEdit) : savedMessage);
      }
      onSaved?.(result, wasEdit);
    } catch (err) {
      setError(err.message || 'No se pudo guardar.');
    } finally {
      setSaving(false);
    }
  }, [editId, form, getUrl, createUrl, mutate, mapToSend, savedMessage, onSaved]);

  // "eliminado"/"eliminada": el único lugar donde entityName necesita
  // concordancia de género, porque es el único mensaje con participio.
  const participio = entityGender === 'f' ? 'eliminada' : 'eliminado';

  const remove = useCallback(
    async (id) => {
      const ok = await confirm(`Eliminar ${entityName}?`, { confirmLabel: 'Eliminar', danger: true });
      if (!ok) return false;
      try {
        await apiDelete(getUrl(id));
        mutate();
        toast.success(`${entityName} ${participio}.`);
        return true;
      } catch (err) {
        toast.error(err.message || 'Error al eliminar.');
        return false;
      }
    },
    [confirm, entityName, participio, getUrl, mutate],
  );

  return {
    rows: data || [],
    // null sin pageParams: no hay cabecera que leer porque no se pidió una
    // página, y un 0 ahí leería como "sin resultados" en vez de "no
    // aplica" -- ver DataGrid en cada página, que solo pasa rowCount cuando
    // realmente pagina.
    rowCount: pageParams ? (totalCount ?? 0) : null,
    isLoading,
    mutate,
    // Diálogo
    open,
    editId,
    form,
    setForm,
    error,
    setError,
    saving,
    openCreate,
    openEdit,
    close,
    save,
    remove,
  };
}
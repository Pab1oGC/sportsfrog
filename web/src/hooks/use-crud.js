import { useState, useCallback } from 'react';
import { useApi, apiPost, apiPut, apiDelete } from 'src/hooks/use-api';
import { toast } from 'sonner';

/**
 * Hook que encapsula el patrón CRUD repetido en cada página.
 *
 * Encapsula: lista, diálogo abierto/cerrado, formulario, guardado y borrado.
 * Cada página que antes copiaba 80 líneas de useState + handleOpen + handleSave
 * + handleDelete ahora llama a useCrudDialog y le queda un componente limpio.
 *
 * @param {object} opts
 * @param {string} opts.resourceUrl - Endpoint de la colección (endpoints.clubs)
 * @param {object} opts.emptyForm - Estado inicial del formulario
 * @param {string} opts.entityName - Nombre legible ("club", "deportista") para diálogos
 * @param {function} [opts.buildUrl] - Custom URL builder: (base, editId) => string
 * @param {function} [opts.mapToForm] - Transforma row del DataGrid a objeto form
 * @param {function} [opts.mapToSend] - Transforma form antes de enviarlo a la API
 * @param {function} [opts.onSaved] - Callback después de guardar exitosamente
 */
export function useCrudDialog(opts) {
  const {
    resourceUrl,
    emptyForm,
    entityName = 'registro',
    buildUrl,
    mapToForm = (row) => row,
    mapToSend = (form) => form,
    onSaved,
  } = opts;

  const { data, mutate, isLoading } = useApi(resourceUrl);
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
      const body = mapToSend(form);
      if (editId) {
        await apiPut(getUrl(editId), body);
      } else {
        await apiPost(resourceUrl, body);
      }
      setOpen(false);
      mutate();
      onSaved?.();
    } catch (err) {
      setError(err.message || 'No se pudo guardar.');
    } finally {
      setSaving(false);
    }
  }, [editId, form, getUrl, resourceUrl, mutate, mapToSend, onSaved]);

  const remove = useCallback(
    async (id) => {
      if (!window.confirm(`Eliminar ${entityName}?`)) return;
      try {
        await apiDelete(getUrl(id));
        mutate();
        toast.success(`${entityName} eliminado.`);
      } catch (err) {
        toast.error(err.message || 'Error al eliminar.');
      }
    },
    [entityName, getUrl, mutate],
  );

  return {
    rows: data || [],
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
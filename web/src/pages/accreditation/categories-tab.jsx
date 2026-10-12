import { useState, useEffect } from 'react';
import TextField from '@mui/material/TextField';
import Box from '@mui/material/Box';
import Chip from '@mui/material/Chip';
import Dialog from '@mui/material/Dialog';
import DialogTitle from '@mui/material/DialogTitle';
import DialogContent from '@mui/material/DialogContent';
import DialogActions from '@mui/material/DialogActions';
import Button from '@mui/material/Button';
import Alert from '@mui/material/Alert';
import FormGroup from '@mui/material/FormGroup';
import FormControlLabel from '@mui/material/FormControlLabel';
import Checkbox from '@mui/material/Checkbox';
import Typography from '@mui/material/Typography';
import IconButton from '@mui/material/IconButton';
import Tooltip from '@mui/material/Tooltip';
import { DataGrid } from '@mui/x-data-grid';
import { toast } from 'sonner';
import { useCrudDialog } from 'src/hooks/use-crud';
import { useApi, apiPut } from 'src/hooks/use-api';
import { endpoints } from 'src/lib/axios';
import { PageHeader } from 'src/components/page-header';
import { CrudDialog } from 'src/components/crud-dialog';
import { EditDeleteActions } from 'src/components/edit-delete-actions';
import { ColorField } from 'src/components/color-field';
import { Iconify } from 'src/components/iconify';

const KIND_LABELS = { discipline: 'Disciplina', venue: 'Recinto', service: 'Servicio', zone: 'Zona' };
const DEFAULT_COLOR = '#1F3864';

function emptyForm() {
  return { code: '', name: '', colorHex: DEFAULT_COLOR, displayOrder: 0 };
}

/**
 * Las categorías de acreditación de una competencia -- Aa, Ao -- y qué
 * paquete de elementos otorga cada una.
 *
 * El paquete se guarda aparte del resto del formulario (ver
 * PackageDialog más abajo), con su propio botón: es una decisión completa en
 * sí misma -- "esta categoría abre estas puertas" -- y guardarla a medias,
 * un elemento a la vez, es exactamente lo que SetAccreditationCategoryItems
 * del lado del servidor evita reemplazando el paquete entero de una vez.
 */
export function AccreditationCategoriesTab({ competitionId }) {
  const [packageTarget, setPackageTarget] = useState(null);

  const resourceUrl = competitionId ? endpoints.accreditationCategories(competitionId) : null;
  const { data: items } = useApi(competitionId ? endpoints.accreditationItems(competitionId) : null);

  const {
    rows, isLoading, mutate, open, editId, form, setForm, error, saving, openCreate, openEdit, close, save, remove,
  } = useCrudDialog({
    resourceUrl,
    emptyForm,
    entityName: 'categoría',
    entityGender: 'f',
    savedMessage: 'Categoría guardada.',
    buildUrl: (base, id) => endpoints.accreditationCategory(competitionId, id),
    mapToForm: (row) => ({
      code: row.code || '', name: row.name || '', colorHex: row.colorHex || DEFAULT_COLOR, displayOrder: row.displayOrder || 0,
    }),
    mapToSend: (f) => ({ code: f.code.trim(), name: f.name.trim(), colorHex: f.colorHex, displayOrder: Number(f.displayOrder) }),
  });

  const itemsById = Object.fromEntries((items || []).map((i) => [i.id, i]));

  const columns = [
    {
      field: 'code', headerName: 'Código', width: 90,
      renderCell: ({ row }) => <Chip label={row.code} size="small" sx={{ bgcolor: row.colorHex, color: '#fff', fontWeight: 700 }} />,
    },
    { field: 'name', headerName: 'Nombre', flex: 1, minWidth: 160 },
    { field: 'displayOrder', headerName: 'Orden', width: 80 },
    {
      field: 'itemIds', headerName: 'Paquete', flex: 1, minWidth: 200, sortable: false,
      renderCell: ({ value }) => {
        const codes = (value || []).map((id) => itemsById[id]?.code).filter(Boolean);
        return codes.length > 0
          ? <Typography variant="body2" color="text.secondary" noWrap>{codes.join(', ')}</Typography>
          : <Typography variant="body2" color="text.disabled">Sin elementos todavía</Typography>;
      },
    },
    {
      field: 'accreditedCount', headerName: 'Personas', width: 90,
      renderCell: ({ value }) => value > 0 ? <Chip label={value} size="small" variant="outlined" /> : '--',
    },
    {
      field: 'actions', headerName: 'Acciones', width: 130, align: 'center', headerAlign: 'center', sortable: false,
      renderCell: ({ row }) => (
        <Box sx={{ display: 'flex', alignItems: 'center' }}>
          <Tooltip title="Editar el paquete">
            <IconButton size="small" onClick={() => setPackageTarget(row)}>
              <Iconify icon="eva:cube-outline" width={20} sx={{ color: 'text.secondary' }} />
            </IconButton>
          </Tooltip>
          <EditDeleteActions onEdit={() => openEdit(row)} onDelete={() => remove(row.id)} />
        </Box>
      ),
    },
  ];

  return (
    <Box>
      <PageHeader title="Categorías" actionLabel="Nueva categoría" onAction={openCreate} actionDisabled={!competitionId} />
      <DataGrid
        rows={rows}
        columns={columns}
        loading={isLoading}
        autoHeight
        disableRowSelectionOnClick
        getRowId={(r) => r.id}
        initialState={{ sorting: { sortModel: [{ field: 'displayOrder', sort: 'asc' }] } }}
      />
      <CrudDialog open={open} editId={editId} entityName="categoría" entityGender="f" error={error} saving={saving} onClose={close} onSave={save}>
        <TextField
          label="Código" value={form.code} onChange={(e) => setForm({ ...form, code: e.target.value })}
          fullWidth required helperText="Lo que se imprime grande en la caja de categoría -- ej. Aa." slotProps={{ htmlInput: { maxLength: 4 } }}
        />
        <TextField label="Nombre" value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} fullWidth required helperText="Ej. Deportista." />
        <ColorField label="Color" value={form.colorHex} onChange={(v) => setForm({ ...form, colorHex: v })} help="Rellena la caja de categoría y, si nadie en esta categoría tiene una zona con color propio, también la franja del pie." />
        <TextField
          label="Orden" type="number" value={form.displayOrder} onChange={(e) => setForm({ ...form, displayOrder: e.target.value })}
          fullWidth slotProps={{ htmlInput: { min: 0, step: 1 } }}
        />
      </CrudDialog>
      <PackageDialog
        competitionId={competitionId}
        category={packageTarget}
        items={items || []}
        onClose={() => setPackageTarget(null)}
        onSaved={() => { setPackageTarget(null); mutate(); }}
      />
    </Box>
  );
}

function PackageDialog({ competitionId, category, items, onClose, onSaved }) {
  const [selected, setSelected] = useState([]);
  const [saving, setSaving] = useState(false);

  // Se reinicia la selección cada vez que se abre sobre una categoría
  // distinta -- category.id entra en las dependencias, no category entero,
  // porque la fila que llega desde el DataGrid es un objeto nuevo en cada
  // repintado y correr esto en cada uno pisaría una selección a medio
  // marcar con el paquete guardado otra vez.
  useEffect(() => {
    if (category) setSelected(category.itemIds || []);
  }, [category?.id]);

  const grouped = groupByKind(items);
  const kindOrder = ['discipline', 'venue', 'service', 'zone'];

  const toggle = (itemId) => {
    setSelected((prev) => prev.includes(itemId) ? prev.filter((id) => id !== itemId) : [...prev, itemId]);
  };

  const handleSave = async () => {
    setSaving(true);
    try {
      await apiPut(endpoints.accreditationCategoryItems(competitionId, category.id), { itemIds: selected });
      toast.success('Paquete guardado.');
      onSaved();
    } catch (err) {
      toast.error(err.message || 'No se pudo guardar el paquete.');
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open={!!category} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Paquete de {category?.name}</DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: '16px !important' }}>
        {items.length === 0 && <Alert severity="info">Esta competencia todavía no tiene elementos cargados -- empezá por la pestaña Elementos.</Alert>}
        {kindOrder.map((kind) => {
          const kindItems = grouped[kind] || [];
          if (kindItems.length === 0) return null;
          return (
            <Box key={kind}>
              <Typography variant="subtitle2" fontWeight={700} sx={{ mb: 0.5 }}>{KIND_LABELS[kind]}</Typography>
              <FormGroup>
                {kindItems.map((item) => (
                  <FormControlLabel
                    key={item.id}
                    control={<Checkbox size="small" checked={selected.includes(item.id)} onChange={() => toggle(item.id)} />}
                    label={`[${item.code}] ${item.name}`}
                  />
                ))}
              </FormGroup>
            </Box>
          );
        })}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={saving}>Cancelar</Button>
        <Button variant="contained" onClick={handleSave} disabled={saving || items.length === 0}>Guardar</Button>
      </DialogActions>
    </Dialog>
  );
}

function groupByKind(items) {
  return items.reduce((acc, item) => {
    (acc[item.kind] = acc[item.kind] || []).push(item);
    return acc;
  }, {});
}

import TextField from '@mui/material/TextField';
import MenuItem from '@mui/material/MenuItem';
import Box from '@mui/material/Box';
import Chip from '@mui/material/Chip';
import { DataGrid } from '@mui/x-data-grid';
import { useCrudDialog } from 'src/hooks/use-crud';
import { endpoints } from 'src/lib/axios';
import { PageHeader } from 'src/components/page-header';
import { CrudDialog } from 'src/components/crud-dialog';
import { EditDeleteActions } from 'src/components/edit-delete-actions';
import { ColorField } from 'src/components/color-field';

// AccreditationItemKind, en el orden en que se imprimen en la credencial --
// disciplina y recintos abren la primera fila, servicios llenan la segunda,
// zona corre aparte en la franja del pie. El mismo orden que ya declara
// CredentialLayout del lado del servidor.
const KINDS = ['discipline', 'venue', 'service', 'zone'];
const KIND_LABELS = { discipline: 'Disciplina', venue: 'Recinto', service: 'Servicio', zone: 'Zona' };

function emptyForm() {
  return { kind: 'venue', code: '', name: '', colorHex: '', displayOrder: 0 };
}

/**
 * El catálogo que una credencial imprime: la disciplina, los recintos, los
 * servicios y las zonas de una competencia.
 *
 * El color solo importa de verdad para una zona -- es lo que colorea la
 * franja del pie de la tarjeta, ver CredentialPrint.BandColorHex del lado
 * del servidor -- pero el campo está disponible para cualquier tipo, porque
 * nada obliga a que una zona sea la única forma de pintar un detalle.
 */
export function AccreditationItemsTab({ competitionId }) {
  const resourceUrl = competitionId ? endpoints.accreditationItems(competitionId) : null;

  const {
    rows, isLoading, open, editId, form, setForm, error, saving, openCreate, openEdit, close, save, remove,
  } = useCrudDialog({
    resourceUrl,
    emptyForm,
    entityName: 'elemento',
    savedMessage: 'Elemento guardado.',
    buildUrl: (base, id) => endpoints.accreditationItem(competitionId, id),
    mapToForm: (row) => ({
      kind: row.kind, code: row.code || '', name: row.name || '',
      colorHex: row.colorHex || '', displayOrder: row.displayOrder || 0,
    }),
    mapToSend: (f) => ({
      kind: f.kind, code: f.code.trim(), name: f.name.trim(),
      colorHex: f.colorHex || null, displayOrder: Number(f.displayOrder),
    }),
  });

  const columns = [
    { field: 'kind', headerName: 'Tipo', width: 110, renderCell: ({ value }) => KIND_LABELS[value] || value },
    { field: 'code', headerName: 'Código', width: 100 },
    { field: 'name', headerName: 'Nombre', flex: 1, minWidth: 180 },
    {
      field: 'colorHex', headerName: 'Color', width: 90, sortable: false,
      renderCell: ({ value }) => value
        ? <Box sx={{ width: 22, height: 22, borderRadius: 0.5, bgcolor: value, border: '1px solid', borderColor: 'divider' }} />
        : '--',
    },
    { field: 'displayOrder', headerName: 'Orden', width: 80 },
    {
      field: 'categoryCount', headerName: 'En categorías', width: 120,
      renderCell: ({ value }) => value > 0 ? <Chip label={value} size="small" variant="outlined" /> : '--',
    },
    {
      field: 'actions', headerName: 'Acciones', width: 90, align: 'center', headerAlign: 'center', sortable: false,
      renderCell: ({ row }) => <EditDeleteActions onEdit={() => openEdit(row)} onDelete={() => remove(row.id)} />,
    },
  ];

  return (
    <Box>
      <PageHeader title="Elementos" actionLabel="Nuevo elemento" onAction={openCreate} actionDisabled={!competitionId} />
      <DataGrid
        rows={rows}
        columns={columns}
        loading={isLoading}
        autoHeight
        disableRowSelectionOnClick
        getRowId={(r) => r.id}
        initialState={{ sorting: { sortModel: [{ field: 'kind', sort: 'asc' }, { field: 'displayOrder', sort: 'asc' }] } }}
      />
      <CrudDialog open={open} editId={editId} entityName="elemento" error={error} saving={saving} onClose={close} onSave={save}>
        <TextField select label="Tipo" value={form.kind} onChange={(e) => setForm({ ...form, kind: e.target.value })} fullWidth required>
          {KINDS.map((k) => <MenuItem key={k} value={k}>{KIND_LABELS[k]}</MenuItem>)}
        </TextField>
        <TextField
          label="Código" value={form.code} onChange={(e) => setForm({ ...form, code: e.target.value.toUpperCase() })}
          fullWidth required helperText="Lo que se imprime en la caja -- hasta 4 caracteres, sin espacios." slotProps={{ htmlInput: { maxLength: 4 } }}
        />
        <TextField label="Nombre" value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} fullWidth required helperText="Lo que explica el glosario del reverso." />
        <ColorField
          label="Color" value={form.colorHex} onChange={(v) => setForm({ ...form, colorHex: v })}
          help={form.kind === 'zone' ? 'Pinta la franja del pie de la credencial.' : 'Sin uso salvo en una zona.'}
        />
        <TextField
          label="Orden" type="number" value={form.displayOrder} onChange={(e) => setForm({ ...form, displayOrder: e.target.value })}
          fullWidth helperText="De izquierda a derecha, dentro de su mismo tipo." slotProps={{ htmlInput: { min: 0, step: 1 } }}
        />
      </CrudDialog>
    </Box>
  );
}

import Avatar from '@mui/material/Avatar';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Chip from '@mui/material/Chip';
import FormControlLabel from '@mui/material/FormControlLabel';
import Checkbox from '@mui/material/Checkbox';
import Stack from '@mui/material/Stack';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import { DataGrid } from '@mui/x-data-grid';
import { Iconify } from 'src/components/iconify';
import { PageHeader } from 'src/components/page-header';
import { CrudDialog } from 'src/components/crud-dialog';
import { EditDeleteActions } from 'src/components/edit-delete-actions';
import { useCrudDialog } from 'src/hooks/use-crud';
import { endpoints } from 'src/lib/axios';
import { leerComoDataUrl } from 'src/lib/data-url';

// Lo que el texto legal puede tener: el mismo tope que valida el backend, y
// que el carnet puede imprimir en sus dos columnas sin achicarse.
const LEGAL_TEXT_MAX = 800;

// Imagen de un diseño, con la misma regla que los clubes y el portal: una
// clave ya guardada se conserva tal cual, una data URL es una subida nueva,
// y null la quita. El enlace solo sirve para mostrarla.
const emptyForm = () => ({
  name: '',
  legalText: '',
  accentColorHex: '',
  backgroundKey: null,
  backgroundLink: null,
  logoKey: null,
  logoLink: null,
  isDefault: false,
  wasDefault: false,
});

export default function CredentialDesignsPage() {
  const {
    rows, isLoading, open, editId, form, setForm, error, saving, openCreate, openEdit, close, save, remove,
  } = useCrudDialog({
    resourceUrl: endpoints.credentialDesigns,
    emptyForm,
    entityName: 'diseño de credencial',
    entityGender: 'm',
    mapToForm: (row) => ({
      name: row.name || '',
      legalText: row.legalText || '',
      accentColorHex: row.accentColorHex || '',
      backgroundKey: row.backgroundKey || null,
      backgroundLink: row.backgroundUrl || null,
      logoKey: row.logoKey || null,
      logoLink: row.logoUrl || null,
      isDefault: Boolean(row.isDefault),
      wasDefault: Boolean(row.isDefault),
    }),
    mapToSend: (f) => ({
      name: f.name.trim(),
      legalText: f.legalText.trim() || null,
      accentColorHex: f.accentColorHex || null,
      backgroundKey: f.backgroundKey,
      logoKey: f.logoKey,
      isDefault: f.isDefault,
    }),
    savedMessage: 'Diseño guardado.',
  });

  // Los dos campos de cada imagen se nombran al armar el handler, para que
  // la clave que se envía y el enlace que se muestra no puedan desfasarse.
  const elegirImagen = (claveCampo, enlaceCampo) => async (e) => {
    const file = e.target.files?.[0];
    e.target.value = '';
    if (!file) return;
    const dataUrl = await leerComoDataUrl(file);
    setForm((previo) => ({ ...previo, [claveCampo]: dataUrl, [enlaceCampo]: dataUrl }));
  };

  const quitarImagen = (claveCampo, enlaceCampo) => () =>
    setForm((previo) => ({ ...previo, [claveCampo]: null, [enlaceCampo]: null }));

  // Solo se puede quitar el default marcando otro diseño, así que el casillero
  // queda fijo mientras este siga siendo el predeterminado.
  const esDefaultFijo = form.wasDefault;

  const columns = [
    { field: 'backgroundUrl', headerName: '', width: 60, sortable: false, renderCell: ({ value }) => (
      <Avatar src={value || undefined} variant="rounded" sx={{ width: 32, height: 32, bgcolor: 'action.hover', '& img': { objectFit: 'contain' } }}>
        {!value && <Iconify icon="mdi:image-outline" width={18} sx={{ color: 'text.disabled' }} />}
      </Avatar>
    )},
    { field: 'name', headerName: 'Nombre', flex: 1, minWidth: 200 },
    { field: 'isDefault', headerName: 'Predeterminado', width: 150, sortable: false, renderCell: ({ value }) => (
      value ? <Chip size="small" color="primary" label="Predeterminado" /> : '--'
    )},
    { field: 'accentColorHex', headerName: 'Acento', width: 110, sortable: false, renderCell: ({ value }) => (
      value
        ? <Box sx={{ width: 22, height: 22, borderRadius: 1, bgcolor: value, border: 1, borderColor: 'divider' }} />
        : <Typography variant="body2" color="text.secondary">Del portal</Typography>
    )},
    { field: 'actions', headerName: 'Acciones', width: 90, align: 'center', headerAlign: 'center', sortable: false, renderCell: ({ row }) => (
      <EditDeleteActions onEdit={() => openEdit(row)} onDelete={() => remove(row.id)} />
    )},
  ];

  return (
    <Box>
      <PageHeader title="Diseños de credencial" actionLabel="Nuevo diseño" onAction={openCreate} />
      <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
        La estructura de la credencial es fija. Acá se elige lo que cambia de una competencia a otra: el texto legal,
        el fondo, el color de acento y un logo de respaldo. Cada competencia usa el diseño que le asignes, o el predeterminado.
      </Typography>
      <DataGrid
        rows={rows}
        columns={columns}
        loading={isLoading}
        autoHeight
        disableRowSelectionOnClick
        getRowId={(r) => r.id}
        rowHeight={52}
        hideFooter
      />

      <CrudDialog open={open} editId={editId} entityName="Diseño de credencial" entityGender="m" error={error} saving={saving} onClose={close} onSave={save} maxWidth="md">
        <TextField label="Nombre" value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} fullWidth required />

        <TextField
          label="Texto legal"
          value={form.legalText}
          onChange={(e) => setForm({ ...form, legalText: e.target.value })}
          fullWidth
          multiline
          minRows={3}
          inputProps={{ maxLength: LEGAL_TEXT_MAX }}
          helperText={`${form.legalText.length}/${LEGAL_TEXT_MAX}. Vacío usa el texto por defecto.`}
        />

        <Stack direction="row" spacing={2} alignItems="center">
          <TextField
            label="Color de acento"
            value={form.accentColorHex}
            onChange={(e) => setForm({ ...form, accentColorHex: e.target.value })}
            placeholder="#1f3864"
            helperText="Vacío usa el color del portal de la competencia."
            sx={{ flex: 1 }}
          />
          <TextField
            type="color"
            value={form.accentColorHex || '#1f3864'}
            onChange={(e) => setForm({ ...form, accentColorHex: e.target.value })}
            sx={{ width: 90 }}
          />
        </Stack>

        <ImagenCampo
          titulo="Fondo"
          ayuda="Se dibuja una sola vez, tenue, detrás de las dos caras. Si no hay, se usa la portada de la competencia."
          vista={form.backgroundLink}
          onElegir={elegirImagen('backgroundKey', 'backgroundLink')}
          onQuitar={quitarImagen('backgroundKey', 'backgroundLink')}
        />

        <ImagenCampo
          titulo="Logo de respaldo"
          ayuda="Se usa cuando la competencia no tiene logo propio. El de la competencia siempre gana."
          vista={form.logoLink}
          onElegir={elegirImagen('logoKey', 'logoLink')}
          onQuitar={quitarImagen('logoKey', 'logoLink')}
        />

        <FormControlLabel
          control={
            <Checkbox
              checked={form.isDefault}
              disabled={esDefaultFijo}
              onChange={(e) => setForm({ ...form, isDefault: e.target.checked })}
            />
          }
          label="Usar como predeterminado de la organización"
        />
        {esDefaultFijo && (
          <Typography variant="caption" color="text.secondary">
            Este es el predeterminado. Para cambiarlo, marcá otro diseño como predeterminado.
          </Typography>
        )}
      </CrudDialog>
    </Box>
  );
}

// Campo de imagen: vista previa, elegir archivo y quitar. Separado porque el
// fondo y el logo se editan igual, y repetirlo dentro del diálogo sería dos
// copias de la misma regla.
function ImagenCampo({ titulo, ayuda, vista, onElegir, onQuitar }) {
  return (
    <Stack direction="row" spacing={2} alignItems="center">
      <Avatar src={vista || undefined} variant="rounded" sx={{ width: 64, height: 64, bgcolor: 'action.hover', '& img': { objectFit: 'contain' } }}>
        {!vista && <Iconify icon="mdi:image-outline" width={24} sx={{ color: 'text.disabled' }} />}
      </Avatar>
      <Box sx={{ flex: 1 }}>
        <Typography variant="subtitle2">{titulo}</Typography>
        <Typography variant="caption" color="text.secondary">{ayuda}</Typography>
      </Box>
      <Button component="label" variant="outlined" size="small">
        {vista ? 'Cambiar' : 'Subir'}
        <input type="file" accept="image/png,image/jpeg,image/webp" hidden onChange={onElegir} />
      </Button>
      {vista && (
        <Button size="small" color="inherit" onClick={onQuitar}>Quitar</Button>
      )}
    </Stack>
  );
}

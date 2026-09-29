import { useState } from 'react';
import Avatar from '@mui/material/Avatar';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import { DataGrid } from '@mui/x-data-grid';
import { toast } from 'sonner';
import { Iconify } from 'src/components/iconify';
import { useCrudDialog } from 'src/hooks/use-crud';
import { apiPut } from 'src/hooks/use-api';
import { endpoints } from 'src/lib/axios';
import { leerComoDataUrl } from 'src/lib/data-url';
import { PageHeader } from 'src/components/page-header';
import { CrudDialog } from 'src/components/crud-dialog';
import { EditDeleteActions } from 'src/components/edit-delete-actions';
import { EstadoChip } from 'src/components/estado-chip';
import { useConfirm } from 'src/components/confirm-dialog';

// El logo viaja como data URL, igual que la foto de un deportista: el
// backend la normaliza y la guarda en el almacenamiento de objetos, no en la
// columna. Tres estados en el formulario, no dos:
//   - logoUrl: null      -> no se toca (por defecto, al editar)
//   - logoUrl: ''        -> quitar el logo actual
//   - logoUrl: 'data:..' -> reemplazarlo por este
// currentLogo es aparte y nunca se envia: es solo lo que ya hay, para la
// vista previa.
const emptyForm = () => ({ name: '', shortName: '', logoUrl: null, currentLogo: null, contactEmail: '', isActive: true });

export default function ClubsPage() {
  const confirm = useConfirm();

  // 0-indexado, como lo pide DataGrid -- skip/take (lo que el backend
  // realmente entiende, ver PagedListing) se arman a partir de esto adentro
  // de useCrudDialog.
  const [paginationModel, setPaginationModel] = useState({ page: 0, pageSize: 25 });

  const { rows, rowCount, isLoading, mutate, open, editId, form, setForm, error, saving, openCreate, openEdit, close, save, remove } = useCrudDialog({
    resourceUrl: endpoints.clubs,
    pageParams: { skip: paginationModel.page * paginationModel.pageSize, take: paginationModel.pageSize },
    emptyForm,
    entityName: 'club',
    // isActive es obligatorio para el backend al editar y no tiene valor por
    // defecto: si no se manda, el club queda inactivo en silencio con
    // cualquier edicion, aunque nadie haya tocado el interruptor.
    mapToForm: (row) => ({
      name: row.name || '', shortName: row.shortName || '', logoUrl: null, currentLogo: row.logoUrl || null,
      contactEmail: row.contactEmail || '', isActive: row.isActive !== false,
    }),
    mapToSend: (f) => ({
      name: f.name, shortName: f.shortName || null, logoUrl: f.logoUrl,
      contactEmail: f.contactEmail || null, isActive: f.isActive,
    }),
  });

  const elegirLogo = async (e) => {
    const file = e.target.files[0];
    if (!file) return;
    setForm({ ...form, logoUrl: await leerComoDataUrl(file) });
  };

  const quitarLogo = () => setForm({ ...form, logoUrl: '', currentLogo: null });

  const vistaPrevia = form.logoUrl || form.currentLogo;

  // Activar es reversible con el mismo click, pero desactivar saca al club de
  // la oferta para competencias nuevas (ver UpdateClub en el backend) — vale
  // la pena avisar antes. Reenvia el registro completo porque UpdateClub
  // exige IsActive como parte de una correccion entera; logoUrl: null es "no
  // tocar el logo" (ver el comentario de emptyForm).
  const toggleClubActive = async (row, event) => {
    event.stopPropagation();
    if (row.isActive) {
      const ok = await confirm(
        `Desactivar "${row.name}"? Deja de ofrecerse para competencias nuevas. Lo que ya tiene inscrito no se ve afectado.`,
        { confirmLabel: 'Desactivar', danger: true },
      );
      if (!ok) return;
    }
    try {
      await apiPut(endpoints.club(row.id), {
        name: row.name, shortName: row.shortName || null, logoUrl: null,
        contactEmail: row.contactEmail || null, isActive: !row.isActive,
      });
      mutate();
    } catch (err) { toast.error(err.message); }
  };

  const columns = [
    { field: 'logoUrl', headerName: '', width: 60, sortable: false, renderCell: ({ value }) => (
      <Avatar src={value || undefined} variant="rounded" sx={{ width: 32, height: 32, bgcolor: 'action.hover', '& img': { objectFit: 'contain' } }}>
        {!value && <Iconify icon="mdi:office-building-outline" width={18} sx={{ color: 'text.disabled' }} />}
      </Avatar>
    )},
    { field: 'name', headerName: 'Nombre', flex: 1, minWidth: 200 },
    { field: 'shortName', headerName: 'Abrev.', width: 120 },
    { field: 'contactEmail', headerName: 'Correo de contacto', width: 200, renderCell: ({ value }) => value || '--' },
    { field: 'isActive', headerName: 'Estado', width: 120, sortable: false, renderCell: ({ value, row }) => (
      <EstadoChip activo={value} onClick={(e) => toggleClubActive(row, e)} />
    )},
    { field: 'actions', headerName: 'Acciones', width: 90, align: 'center', headerAlign: 'center', renderCell: ({ row }) => (
      <EditDeleteActions onEdit={() => openEdit(row)} onDelete={() => remove(row.id)} />
    )},
  ];

  return (
    <Box>
      <PageHeader title="Clubes" actionLabel="Nuevo club" onAction={openCreate} />
      <DataGrid
        rows={rows}
        columns={columns}
        loading={isLoading}
        autoHeight
        disableRowSelectionOnClick
        getRowId={(r) => r.id}
        rowHeight={52}
        paginationMode="server"
        rowCount={rowCount}
        paginationModel={paginationModel}
        onPaginationModelChange={setPaginationModel}
        pageSizeOptions={[25, 50, 100]}
      />
      <CrudDialog open={open} editId={editId} entityName="Club" error={error} saving={saving} onClose={close} onSave={save}>
        <TextField label="Nombre" value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} fullWidth />
        <TextField label="Abreviatura" value={form.shortName} onChange={(e) => setForm({ ...form, shortName: e.target.value })} fullWidth />
        <TextField
          label="Correo de contacto" type="email" value={form.contactEmail}
          onChange={(e) => setForm({ ...form, contactEmail: e.target.value })} fullWidth
          helperText="Adonde se avisa si se reprograma un partido de este club. Opcional."
        />

        <Box sx={{ display: 'flex', alignItems: 'center', gap: 2 }}>
          {/* object-fit: contain, no el "cover" que trae Avatar por defecto —
              un escudo que no es cuadrado se recortaba en vez de verse
              entero. */}
          <Avatar src={vistaPrevia || undefined} variant="rounded" sx={{ width: 56, height: 56, bgcolor: 'action.hover', '& img': { objectFit: 'contain' } }}>
            {!vistaPrevia && <Iconify icon="mdi:office-building-outline" width={28} sx={{ color: 'text.disabled' }} />}
          </Avatar>
          <Box sx={{ display: 'flex', flexDirection: 'column', gap: 0.5 }}>
            <Button variant="outlined" component="label" size="small" startIcon={<Iconify icon="eva:upload-outline" width={16} />}>
              {vistaPrevia ? 'Reemplazar logo' : 'Subir logo'}
              <input type="file" accept="image/png,image/jpeg,image/webp" hidden onChange={elegirLogo} />
            </Button>
            {vistaPrevia && <Button size="small" color="inherit" onClick={quitarLogo}>Quitar</Button>}
          </Box>
        </Box>
        <Typography variant="caption" color="text.secondary" sx={{ mt: -1 }}>
          PNG, JPEG o WebP. Opcional — un club sin logo se muestra con un icono generico.
        </Typography>
        {/* El estado activo/inactivo se alterna desde la columna "Estado" de
            la grilla (EstadoChip). form.isActive sigue viajando en el
            guardado (UpdateClub lo exige), solo que ya no se toca desde aca. */}
      </CrudDialog>
    </Box>
  );
}

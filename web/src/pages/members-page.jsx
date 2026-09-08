import TextField from '@mui/material/TextField';
import MenuItem from '@mui/material/MenuItem';
import { DataGrid } from '@mui/x-data-grid';
import { Iconify } from 'src/components/iconify';
import { useCrudDialog } from 'src/hooks/use-crud';
import { endpoints } from 'src/lib/axios';
import { PageHeader } from 'src/components/page-header';
import { CrudDialog } from 'src/components/crud-dialog';

const emptyForm = () => ({ email: '', fullName: '', password: '', role: 'viewer' });

export default function MembersPage() {
  const { rows: data, isLoading, open, form, setForm, error, saving, openCreate, close, save } = useCrudDialog({
    resourceUrl: endpoints.organizations.members,
    emptyForm,
    entityName: 'miembro',
    savedMessage: 'Miembro agregado.',
  });

  const columns = [
    { field: 'email', headerName: 'Correo', flex: 1, minWidth: 200 },
    { field: 'fullName', headerName: 'Nombre', flex: 1, minWidth: 200 },
    { field: 'role', headerName: 'Rol', width: 120 },
  ];

  return (
    <div>
      <PageHeader title="Miembros" actionLabel="Agregar miembro" onAction={openCreate} />
      <DataGrid rows={data || []} columns={columns} loading={isLoading} autoHeight disableRowSelectionOnClick />
      <CrudDialog open={open} editId={null} entityName="Miembro" error={error} saving={saving} onClose={close} onSave={save}>
        <TextField label="Nombre completo" value={form.fullName} onChange={(e) => setForm({ ...form, fullName: e.target.value })} fullWidth />
        <TextField label="Correo" value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} fullWidth />
        <TextField label="Contrasena" type="password" value={form.password} onChange={(e) => setForm({ ...form, password: e.target.value })} fullWidth />
        <TextField select label="Rol" value={form.role} onChange={(e) => setForm({ ...form, role: e.target.value })} fullWidth>
          <MenuItem value="viewer">Consultor</MenuItem>
          <MenuItem value="recorder">Captador</MenuItem>
          <MenuItem value="operator">Operador</MenuItem>
          <MenuItem value="admin">Administrador</MenuItem>
        </TextField>
      </CrudDialog>
    </div>
  );
}
import { useState } from 'react';
import TextField from '@mui/material/TextField';
import MenuItem from '@mui/material/MenuItem';
import { DataGrid } from '@mui/x-data-grid';
import { Iconify } from 'src/components/iconify';
import { useApi, apiPost } from 'src/hooks/use-api';
import { endpoints } from 'src/lib/axios';
import { PageHeader } from 'src/components/page-header';
import { CrudDialog } from 'src/components/crud-dialog';
import { toast } from 'sonner';

export default function MembersPage() {
  const { data, mutate, isLoading } = useApi(endpoints.organizations.members);
  const [open, setOpen] = useState(false);
  const [form, setForm] = useState({ email: '', fullName: '', password: '', role: 'viewer' });
  const [saving, setSaving] = useState(false);

  const handleSave = async () => {
    setSaving(true);
    try {
      await apiPost(endpoints.organizations.members, form);
      setOpen(false); mutate(); toast.success('Miembro agregado.');
    } catch (err) { toast.error(err.message); }
    finally { setSaving(false); }
  };

  const columns = [
    { field: 'email', headerName: 'Correo', flex: 1, minWidth: 200 },
    { field: 'fullName', headerName: 'Nombre', flex: 1, minWidth: 200 },
    { field: 'role', headerName: 'Rol', width: 120 },
  ];

  return (
    <div>
      <PageHeader title="Miembros" actionLabel="Agregar miembro" onAction={() => setOpen(true)} />
      <DataGrid rows={data || []} columns={columns} loading={isLoading} autoHeight disableRowSelectionOnClick />
      <CrudDialog open={open} editId={null} entityName="Miembro" saving={saving} onClose={() => setOpen(false)} onSave={handleSave}>
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
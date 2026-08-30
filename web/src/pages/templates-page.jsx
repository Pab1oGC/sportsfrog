import { useState } from 'react';
import { useNavigate } from 'react-router';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import CardActions from '@mui/material/CardActions';
import Dialog from '@mui/material/Dialog';
import DialogTitle from '@mui/material/DialogTitle';
import DialogContent from '@mui/material/DialogContent';
import DialogActions from '@mui/material/DialogActions';
import TextField from '@mui/material/TextField';
import MenuItem from '@mui/material/MenuItem';
import Chip from '@mui/material/Chip';
import IconButton from '@mui/material/IconButton';
import Tooltip from '@mui/material/Tooltip';
import Alert from '@mui/material/Alert';
import { Iconify } from 'src/components/iconify';
import { useApi, apiPost, apiPut, apiDelete } from 'src/hooks/use-api';
import { endpoints } from 'src/lib/axios';
import { useConfirm } from 'src/components/confirm-dialog';

var KINDS = ['credential', 'certificate'];
var KIND_LABELS = { credential: 'Credencial', certificate: 'Certificado' };
var PAGE_SIZES = ['letter', 'a4', 'half-letter', 'id-card'];

function emptyForm() {
  return { name: '', kind: 'credential', pageSize: 'letter', isDefault: false };
}

export default function TemplatesPage() {
  var confirm = useConfirm();
  var navigate = useNavigate();
  var { data, mutate, isLoading } = useApi(endpoints.templates);
  var [open, setOpen] = useState(false);
  var [editId, setEditId] = useState(null);
  var [form, setForm] = useState(emptyForm());
  var [error, setError] = useState('');

  var handleOpen = function(row) {
    if (row) {
      setEditId(row.id);
      setForm({ name: row.name || '', kind: row.kind || 'credential', pageSize: row.pageSize || 'letter', isDefault: row.isDefault || false });
    } else {
      setEditId(null);
      setForm(emptyForm());
    }
    setError('');
    setOpen(true);
  };

  var handleSave = async function() {
    setError('');
    try {
      var body = {
        name: form.name,
        kind: form.kind,
        pageSize: form.pageSize,
        isDefault: form.isDefault,
        layout: { fields: [] }
      };
      if (editId) {
        await apiPut(endpoints.template(editId), body);
      } else {
        var result = await apiPost(endpoints.templates, body);
        setOpen(false);
        mutate();
        if (result && result.id) {
          navigate('/dashboard/templates/' + result.id + '/design');
        }
        return;
      }
      setOpen(false);
      mutate();
    } catch (err) {
      setError(err.message || 'Error al guardar plantilla');
    }
  };

  var handleDelete = async function(id) {
    var ok = await confirm('Eliminar plantilla?', { confirmLabel: 'Eliminar', danger: true });
    if (ok) {
      await apiDelete(endpoints.template(id));
      mutate();
    }
  };

  return (
    <Box>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 3, flexWrap: 'wrap', gap: 1 }}>
        <Typography variant="h4" fontWeight={700}>Plantillas de documentos</Typography>
        <Button variant="contained" startIcon={<Iconify icon="eva:plus-fill" />} onClick={function() { handleOpen(null); }}>
          Nueva plantilla
        </Button>
      </Box>
      <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(300px, 1fr))', gap: 3 }}>
        {(data || []).map(function(t) {
          return (
            <Card key={t.id} variant="outlined">
              <CardContent>
                <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', mb: 1 }}>
                  <Typography variant="subtitle1" fontWeight={600}>{t.name}</Typography>
                  {t.isDefault && <Chip label="Default" color="primary" size="small" />}
                </Box>
                <Box sx={{ display: 'flex', gap: 1, mb: 1 }}>
                  <Chip label={KIND_LABELS[t.kind] || t.kind} size="small" variant="outlined" />
                  <Chip label={'v' + t.version} size="small" variant="outlined" />
                  <Chip label={t.pageSize || 'letter'} size="small" variant="outlined" />
                </Box>
              </CardContent>
              <CardActions sx={{ justifyContent: 'flex-end', pt: 0 }}>
                <Tooltip title="Disenar">
                  <IconButton size="small" onClick={function() { navigate('/dashboard/templates/' + t.id + '/design'); }}>
                    <Iconify icon="eva:brush-outline" width={18} sx={{ color: 'primary.main' }} />
                  </IconButton>
                </Tooltip>
                <Tooltip title="Editar">
                  <IconButton size="small" onClick={function() { handleOpen(t); }}>
                    <Iconify icon="eva:edit-fill" width={18} />
                  </IconButton>
                </Tooltip>
                <Tooltip title="Eliminar">
                  <IconButton size="small" onClick={function() { handleDelete(t.id); }}>
                    <Iconify icon="eva:trash-2-outline" width={18} sx={{ color: 'error.main' }} />
                  </IconButton>
                </Tooltip>
              </CardActions>
            </Card>
          );
        })}
        {!isLoading && (!data || data.length === 0) && (
          <Box sx={{ gridColumn: '1 / -1', textAlign: 'center', py: 4 }}>
            <Typography color="text.secondary">No hay plantillas creadas. Crea una para generar credenciales y certificados.</Typography>
          </Box>
        )}
      </Box>
      <Dialog open={open} onClose={function() { setOpen(false); }} maxWidth="sm" fullWidth>
        <DialogTitle>{editId ? 'Editar' : 'Nueva'} Plantilla</DialogTitle>
        <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: '16px !important' }}>
          {error && <Alert severity="error" sx={{ mb: 1 }}>{error}</Alert>}
          <TextField label="Nombre" value={form.name} onChange={function(e) { setForm(Object.assign({}, form, { name: e.target.value })); }} fullWidth required />
          <TextField select label="Tipo" value={form.kind} onChange={function(e) { setForm(Object.assign({}, form, { kind: e.target.value })); }} fullWidth>
            {KINDS.map(function(k) { return <MenuItem key={k} value={k}>{KIND_LABELS[k]}</MenuItem>; })}
          </TextField>
          <TextField select label="Tamano de papel" value={form.pageSize} onChange={function(e) { setForm(Object.assign({}, form, { pageSize: e.target.value })); }} fullWidth>
            {PAGE_SIZES.map(function(s) { return <MenuItem key={s} value={s}>{s}</MenuItem>; })}
          </TextField>
        </DialogContent>
        <DialogActions>
          <Button onClick={function() { setOpen(false); }}>Cancelar</Button>
          <Button variant="contained" onClick={handleSave}>Crear plantilla</Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
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
import { endpoints, default as axios } from 'src/lib/axios';
import { useConfirm } from 'src/components/confirm-dialog';

var KIND_LABELS = { certificate: 'Certificado' };

var PAGE_SIZES = [
  { code: 'a4', label: 'A4 vertical (210 × 297 mm)', ratio: 210 / 297 },
  { code: 'a4_landscape', label: 'A4 horizontal (297 × 210 mm)', ratio: 297 / 210 },
  { code: 'a5', label: 'A5 horizontal (210 × 148 mm)', ratio: 210 / 148 },
  { code: 'letter', label: 'Carta vertical (216 × 279 mm)', ratio: 216 / 279 },
];

function emptyForm() {
  return { name: '', kind: 'certificate', pageSize: 'a4', isDefault: false, layout: null };
}

export default function TemplatesPage() {
  var confirm = useConfirm();
  var navigate = useNavigate();
  var { data, mutate, isLoading } = useApi(endpoints.templates);
  var [open, setOpen] = useState(false);
  var [editId, setEditId] = useState(null);
  var [form, setForm] = useState(emptyForm());
  var [error, setError] = useState('');

  var handleOpen = async function(row) {
    if (row) {
      setEditId(row.id);
      setForm({
        name: row.name || '',
        kind: row.kind || 'certificate',
        pageSize: row.pageSize || 'a4',
        isDefault: row.isDefault || false,
        layout: null,
      });
      try {
        var detail = await axios.get(endpoints.template(row.id)).then(function(r) { return r.data; });
        if (detail && detail.layout) {
          setForm(function(prev) { return Object.assign({}, prev, { layout: detail.layout }); });
        }
      } catch (e) {
        // Ignorar si falla precarga
      }
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
      var selectedSize = PAGE_SIZES.find(function(s) { return s.code === form.pageSize; });
      var defaultRatio = selectedSize ? selectedSize.ratio : 1.585;

      var layout = form.layout || {
        front: {
          backgroundKey: null,
          aspectRatio: Number(defaultRatio.toFixed(4)),
          fields: [],
        },
        back: null,
      };

      var body = {
        name: form.name,
        kind: form.kind,
        pageSize: form.pageSize,
        isDefault: form.isDefault,
        layout: layout,
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
    var ok = await confirm('¿Eliminar plantilla?', { confirmLabel: 'Eliminar', danger: true });
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
          var sizeObj = PAGE_SIZES.find(function(s) { return s.code === t.pageSize; });
          return (
            <Card key={t.id} variant="outlined">
              <CardContent>
                <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', mb: 1 }}>
                  <Typography variant="subtitle1" fontWeight={600}>{t.name}</Typography>
                  {t.isDefault && <Chip label="Por defecto" color="primary" size="small" />}
                </Box>
                <Box sx={{ display: 'flex', gap: 1, mb: 1, flexWrap: 'wrap' }}>
                  <Chip label={KIND_LABELS[t.kind] || t.kind} size="small" variant="outlined" />
                  <Chip label={'v' + t.version} size="small" variant="outlined" />
                  <Chip label={(sizeObj && sizeObj.label) || t.pageSize} size="small" variant="outlined" />
                </Box>
              </CardContent>
              <CardActions sx={{ justifyContent: 'flex-end', pt: 0 }}>
                <Tooltip title="Diseñar">
                  <IconButton size="small" onClick={function() { navigate('/dashboard/templates/' + t.id + '/design'); }}>
                    <Iconify icon="eva:brush-outline" width={18} sx={{ color: 'primary.main' }} />
                  </IconButton>
                </Tooltip>
                <Tooltip title="Editar metadatos">
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
            <Typography color="text.secondary">No hay plantillas creadas. Crea una para generar certificados.</Typography>
          </Box>
        )}
      </Box>
      <Dialog open={open} onClose={function() { setOpen(false); }} maxWidth="sm" fullWidth>
        <DialogTitle>{editId ? 'Editar' : 'Nueva'} Plantilla</DialogTitle>
        <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: '16px !important' }}>
          {error && <Alert severity="error" sx={{ mb: 1 }}>{error}</Alert>}
          <TextField label="Nombre" value={form.name} onChange={function(e) { setForm(Object.assign({}, form, { name: e.target.value })); }} fullWidth required />
          <TextField select label="Tamaño de papel" value={form.pageSize} onChange={function(e) { setForm(Object.assign({}, form, { pageSize: e.target.value })); }} fullWidth>
            {PAGE_SIZES.map(function(s) { return <MenuItem key={s.code} value={s.code}>{s.label}</MenuItem>; })}
          </TextField>
        </DialogContent>
        <DialogActions>
          <Button onClick={function() { setOpen(false); }}>Cancelar</Button>
          <Button variant="contained" onClick={handleSave}>{editId ? 'Guardar' : 'Crear plantilla'}</Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
import { useState } from 'react';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Chip from '@mui/material/Chip';
import Dialog from '@mui/material/Dialog';
import DialogTitle from '@mui/material/DialogTitle';
import DialogContent from '@mui/material/DialogContent';
import DialogActions from '@mui/material/DialogActions';
import Button from '@mui/material/Button';
import TextField from '@mui/material/TextField';
import Alert from '@mui/material/Alert';
import { DataGrid } from '@mui/x-data-grid';
import { toast } from 'sonner';
import { PageHeader } from 'src/components/page-header';
import { RowActionsMenu } from 'src/components/row-actions-menu';
import { useApi, apiPost } from 'src/hooks/use-api';
import { endpoints } from 'src/lib/axios';
import { useAuthContext } from 'src/auth/hooks';
import { DateField } from 'src/components/date-field';
import { SelectionCompetition, SelectionCategory, SelectionTeam } from 'src/components/selectors';

// Los cuatro estados de CredentialRequestState.
var ES = { pending: 'info', approved: 'success', rejected: 'error', cancelled: 'default' };
var EL = { pending: 'Pendiente', approved: 'Aprobada', rejected: 'Rechazada', cancelled: 'Cancelada' };

function emptyForm() {
  return { competitionId: '', categoryId: '', teamId: '', validFrom: '', validTo: '' };
}

// Quien emite credenciales es SportFrog (frogtech-solutions), nunca la
// organizacion que las necesita -- ver PlatformAuthority y el gate de
// RequestDocumentBatch del lado del servidor. Esta pagina es las dos mitades
// de lo que queda para cada lado: pedir, para cualquier otra organizacion; y
// revisar, solo para la plataforma. Una sola ruta porque son la misma
// pantalla desde dos lugares distintos, no dos funcionalidades aparte.
export default function CredentialRequestsPage() {
  var { user } = useAuthContext();
  var esPlataforma = Boolean(
    user?.organizations?.some(function(o) { return o.slug === 'frogtech-solutions'; })
  );

  return esPlataforma ? <BandejaDeRevision /> : <MisSolicitudes />;
}

function MisSolicitudes() {
  var { data: comps } = useApi(endpoints.competitions);
  var { data: requests, mutate, isLoading } = useApi(endpoints.credentialRequests);
  var [open, setOpen] = useState(false);
  var [form, setForm] = useState(emptyForm());
  var [error, setError] = useState('');
  var [enviando, setEnviando] = useState(false);

  var nombreCompetencia = function(id) {
    var c = (comps || []).find(function(x) { return x.id === id; });
    return c ? c.name : id;
  };

  var handleOpen = function() {
    setForm(emptyForm());
    setError('');
    setOpen(true);
  };

  var handleCreate = async function() {
    setError('');
    setEnviando(true);
    try {
      await apiPost(endpoints.credentialRequests, {
        competitionId: form.competitionId,
        categoryId: form.categoryId || null,
        teamId: form.teamId || null,
        validFrom: form.validFrom || null,
        validTo: form.validTo || null
      });
      setOpen(false);
      mutate();
      toast.success('Solicitud enviada.');
    } catch (err) {
      setError(err.message || 'No se pudo enviar la solicitud.');
    } finally {
      setEnviando(false);
    }
  };

  var handleCancel = async function(id) {
    try {
      await apiPost(endpoints.cancelCredentialRequest(id));
      mutate();
      toast.success('Solicitud cancelada.');
    } catch (err) {
      toast.error(err.message || 'No se pudo cancelar la solicitud.');
    }
  };

  var columns = [
    { field: 'competitionId', headerName: 'Competencia', flex: 1, minWidth: 180, renderCell: function(p) { return nombreCompetencia(p.value); } },
    { field: 'validFrom', headerName: 'Desde', width: 110, renderCell: function(p) { return p.value || '--'; } },
    { field: 'validTo', headerName: 'Hasta', width: 110, renderCell: function(p) { return p.value || '--'; } },
    { field: 'status', headerName: 'Estado', width: 130, renderCell: function(p) {
      return <Chip label={EL[p.value] || p.value} color={ES[p.value] || 'default'} size="small" />;
    }},
    { field: 'createdAt', headerName: 'Solicitado', width: 160, valueFormatter: function(v) { return v ? new Date(v).toLocaleString() : '--'; } },
    { field: 'acciones', headerName: 'Acciones', width: 90, align: 'center', headerAlign: 'center', sortable: false, renderCell: function(p) {
      return (
        <RowActionsMenu
          actions={[
            p.row.status === 'pending' && {
              icon: 'eva:close-circle-outline',
              label: 'Cancelar solicitud',
              color: 'error.main',
              onClick: function() { handleCancel(p.row.id); },
            },
          ].filter(Boolean)}
        />
      );
    }}
  ];

  return (
    <Box>
      <PageHeader title="Solicitudes de credenciales" actionLabel="Nueva solicitud" onAction={handleOpen} />
      <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
        SportFrog emite las credenciales de esta organizacion. Pedi un lote aca y vas a ver su estado una vez que lo revisen.
      </Typography>
      <DataGrid
        rows={requests || []}
        columns={columns}
        loading={isLoading}
        autoHeight
        disableRowSelectionOnClick
        getRowId={function(r) { return r.id; }}
      />

      <Dialog open={open} onClose={function() { setOpen(false); }} maxWidth="sm" fullWidth>
        <DialogTitle>Nueva solicitud de credenciales</DialogTitle>
        <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: '16px !important' }}>
          {error && <Alert severity="error">{error}</Alert>}
          <SelectionCompetition
            value={form.competitionId}
            onChange={function(e) { setForm(Object.assign({}, form, { competitionId: e.target.value, categoryId: '', teamId: '' })); }}
            required
          />
          <SelectionCategory
            competitionId={form.competitionId}
            value={form.categoryId}
            onChange={function(e) { setForm(Object.assign({}, form, { categoryId: e.target.value, teamId: '' })); }}
            label="Categoria (toda la competencia si se deja vacia)"
          />
          <SelectionTeam
            categoryId={form.categoryId}
            value={form.teamId}
            onChange={function(e) { setForm(Object.assign({}, form, { teamId: e.target.value })); }}
            label="Equipo (toda la categoria si se deja vacio)"
          />
          <Box sx={{ display: 'flex', gap: 2, flexWrap: 'wrap' }}>
            <DateField label="Vigente desde" value={form.validFrom} onChange={function(e) { setForm(Object.assign({}, form, { validFrom: e.target.value })); }} sx={{ flex: '1 1 160px' }} />
            <DateField label="Vigente hasta" value={form.validTo} onChange={function(e) { setForm(Object.assign({}, form, { validTo: e.target.value })); }} sx={{ flex: '1 1 160px' }} />
          </Box>
        </DialogContent>
        <DialogActions>
          <Button onClick={function() { setOpen(false); }} disabled={enviando}>Cancelar</Button>
          <Button variant="contained" onClick={handleCreate} disabled={enviando || !form.competitionId}>
            {enviando ? 'Enviando...' : 'Enviar solicitud'}
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}

function BandejaDeRevision() {
  var { data: pending, mutate, isLoading } = useApi(endpoints.pendingCredentialRequests);
  var [rejectTarget, setRejectTarget] = useState(null);
  var [rejectReason, setRejectReason] = useState('');
  var [procesando, setProcesando] = useState(null);

  var handleApprove = async function(row) {
    setProcesando(row.id);
    try {
      await apiPost(endpoints.approveCredentialRequest(row.id));
      mutate();
      toast.success('Solicitud aprobada. El lote quedo encolado para imprimir.');
    } catch (err) {
      toast.error(err.message || 'No se pudo aprobar la solicitud.');
    } finally {
      setProcesando(null);
    }
  };

  var handleReject = async function() {
    if (!rejectTarget || !rejectReason.trim()) return;
    try {
      await apiPost(endpoints.rejectCredentialRequest(rejectTarget.id), { reason: rejectReason });
      setRejectTarget(null);
      setRejectReason('');
      mutate();
      toast.success('Solicitud rechazada.');
    } catch (err) {
      toast.error(err.message || 'No se pudo rechazar la solicitud.');
    }
  };

  var columns = [
    { field: 'orgName', headerName: 'Organizacion', flex: 1, minWidth: 160 },
    { field: 'orgSlug', headerName: 'Direccion', width: 160, renderCell: function(p) { return p.value || '--'; } },
    { field: 'competitionId', headerName: 'Competencia', width: 260, renderCell: function(p) {
      // Cruza organizaciones: no hay con que resolver el nombre sin
      // ensanchar la excepcion de RLS a competitions tambien, y eso es una
      // decision propia -- ver AddCredentialRequestReview del lado del
      // servidor.
      return <Typography variant="caption" sx={{ fontFamily: 'monospace' }}>{p.value}</Typography>;
    }},
    { field: 'validFrom', headerName: 'Desde', width: 110, renderCell: function(p) { return p.value || '--'; } },
    { field: 'validTo', headerName: 'Hasta', width: 110, renderCell: function(p) { return p.value || '--'; } },
    { field: 'createdAt', headerName: 'Solicitado', width: 160, valueFormatter: function(v) { return v ? new Date(v).toLocaleString() : '--'; } },
    { field: 'acciones', headerName: 'Acciones', width: 100, align: 'center', headerAlign: 'center', sortable: false, renderCell: function(p) {
      return (
        <RowActionsMenu
          primary={{
            icon: 'eva:checkmark-circle-2-outline',
            label: 'Aprobar',
            color: 'success.main',
            disabled: procesando === p.row.id,
            onClick: function() { handleApprove(p.row); },
          }}
          actions={[
            {
              icon: 'eva:close-circle-outline',
              label: 'Rechazar',
              color: 'error.main',
              onClick: function() { setRejectTarget(p.row); setRejectReason(''); },
            },
          ]}
        />
      );
    }}
  ];

  return (
    <Box>
      <PageHeader title="Solicitudes de credenciales" />
      <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
        Pedidos de todas las organizaciones, pendientes de revision. Aprobar arma el lote y lo encola para imprimir.
      </Typography>
      <DataGrid
        rows={pending || []}
        columns={columns}
        loading={isLoading}
        autoHeight
        disableRowSelectionOnClick
        getRowId={function(r) { return r.id; }}
      />

      <Dialog open={!!rejectTarget} onClose={function() { setRejectTarget(null); }} maxWidth="sm" fullWidth>
        <DialogTitle>Rechazar solicitud</DialogTitle>
        <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: '16px !important' }}>
          {rejectTarget && (
            <Alert severity="warning">
              Se rechazara la solicitud de <strong>{rejectTarget.orgName}</strong>.
            </Alert>
          )}
          <TextField
            label="Motivo del rechazo"
            value={rejectReason}
            onChange={function(e) { setRejectReason(e.target.value); }}
            fullWidth
            required
            multiline
            rows={2}
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={function() { setRejectTarget(null); }}>Cancelar</Button>
          <Button variant="contained" color="error" onClick={handleReject} disabled={!rejectReason.trim()}>Rechazar</Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}

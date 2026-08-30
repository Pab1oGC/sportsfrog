import { useState } from 'react';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
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
import Divider from '@mui/material/Divider';
import Paper from '@mui/material/Paper';
import Stack from '@mui/material/Stack';
import LinearProgress from '@mui/material/LinearProgress';
import CircularProgress from '@mui/material/CircularProgress';
import { DataGrid } from '@mui/x-data-grid';
import { Iconify } from 'src/components/iconify';
import { useApi, apiPost } from 'src/hooks/use-api';
import { endpoints } from 'src/lib/axios';
import { SelectionCompetition, SelectionCategory, SelectionTeam } from 'src/components/selectors';
import { toast } from 'sonner';

// Los cuatro estados de DocumentBatchState. El mapa anterior decia
// "processing" y "completed", que la API no usa: un lote terminado llegaba
// como "finished", no encontraba etiqueta, y se dibujaba en gris con el texto
// crudo en ingles — parecia que no habia pasado nada.
var BS = { queued: 'info', running: 'warning', finished: 'success', failed: 'error' };
var BL = { queued: 'En cola', running: 'Procesando', finished: 'Terminado', failed: 'Fallido' };

/** Mientras esta en alguno de estos, el lote todavia se mueve solo. */
var EN_VUELO = { queued: true, running: true };

var KINDS = ['credential', 'certificate'];
var KL = { credential: 'Credencial', certificate: 'Certificado' };
var DS = { active: 'success', revoked: 'error' };
var DL = { active: 'Vigente', revoked: 'Revocado' };

// SkipReason. Nombrar el motivo es el punto: "7 saltados" no es accionable,
// "Pedro no tiene foto" si.
var MOTIVOS = {
  no_photo: 'No tiene fotografia cargada',
  already_issued: 'Ya tiene un documento de esta competencia',
  could_not_print: 'No se pudo componer la credencial'
};

function emptyForm(compId, catId, teamId) {
  return {
    kind: 'credential', templateId: '',
    competitionId: compId || '', categoryId: catId || '', teamId: teamId || '',
    certificateType: '', validFrom: '', validTo: ''
  };
}

export default function DocumentsPage() {
  var [compId, setCompId] = useState('');
  var [catId, setCatId] = useState('');
  var [teamId, setTeamId] = useState('');
  var [requestOpen, setRequestOpen] = useState(false);
  var [revokeOpen, setRevokeOpen] = useState(false);
  var [revokeTarget, setRevokeTarget] = useState(null);
  var [detalleId, setDetalleId] = useState(null);
  var [form, setForm] = useState(emptyForm());
  var [revokeReason, setRevokeReason] = useState('');
  var [error, setError] = useState('');
  var [enviando, setEnviando] = useState(false);

  // Un lote se procesa en segundo plano: el POST contesta "en cola" y nada
  // mas vuelve a preguntar. Antes se refrescaba una sola vez, justo cuando
  // todavia no habia empezado, y la fila se quedaba en "En cola" hasta
  // recargar la pagina a mano.
  //
  // El intervalo es una funcion de lo ultimo que llego, que es lo que deja
  // que la consulta se apague sola: mientras haya algo en vuelo pregunta, y
  // cuando todos terminaron deja de preguntar.
  var { data: batches, mutate: mutateBatches, isLoading: loadingBatches } = useApi(endpoints.documentBatches, {
    refreshInterval: function(ultimo) {
      return (ultimo || []).some(function(b) { return EN_VUELO[b.status]; }) ? 2000 : 0;
    }
  });

  var enVuelo = (batches || []).some(function(b) { return EN_VUELO[b.status]; });

  var urlEmitidos = endpoints.issuedDocuments + (compId ? '?competitionId=' + compId : '');
  var { data: issued, mutate: mutateIssued, isLoading: loadingIssued } = useApi(urlEmitidos);
  var { data: templates } = useApi(endpoints.templates);

  var { data: detalle } = useApi(detalleId ? endpoints.documentBatch(detalleId) : null, {
    refreshInterval: function(ultimo) { return ultimo && EN_VUELO[ultimo.status] ? 1500 : 0; }
  });

  var handleOpenRequest = function() {
    setForm(emptyForm(compId, catId, teamId));
    setError('');
    setRequestOpen(true);
  };

  var handleRequest = async function() {
    setError('');
    setEnviando(true);
    try {
      var resp = await apiPost(endpoints.documentBatches, {
        kind: form.kind,
        templateId: form.templateId,
        competitionId: form.competitionId,
        categoryId: form.categoryId || null,
        teamId: form.teamId || null,
        certificateType: form.certificateType || null,
        validFrom: form.validFrom || null,
        validTo: form.validTo || null
      });
      setRequestOpen(false);
      mutateBatches();

      // Se abre el lote recien pedido en lugar de dejarlo perderse entre las
      // filas: quien pide cuatrocientas credenciales quiere mirar esa, no
      // buscarla.
      setDetalleId(resp.id);
    } catch (err) {
      setError(err.message || 'No se pudo solicitar el lote.');
    } finally {
      setEnviando(false);
    }
  };

  var handleRevoke = async function() {
    if (!revokeTarget || !revokeReason) return;
    try {
      await apiPost(endpoints.revokeDocument(revokeTarget.id), { reason: revokeReason });
      setRevokeOpen(false);
      setRevokeTarget(null);
      setRevokeReason('');
      mutateIssued();
    } catch (err) {
      toast.error(err.message || 'No se pudo revocar el documento.');
    }
  };

  var abrir = function(url) {
    if (url) window.open(url, '_blank', 'noopener');
  };

  var batchColumns = [
    { field: 'kind', headerName: 'Tipo', width: 100, renderCell: function(p) { return KL[p.value] || p.value; } },
    { field: 'status', headerName: 'Estado', width: 130, renderCell: function(p) {
      return (
        <Chip
          label={BL[p.value] || p.value}
          color={BS[p.value] || 'default'}
          size="small"
          icon={EN_VUELO[p.value] ? <CircularProgress size={11} sx={{ ml: 1 }} color="inherit" /> : undefined}
        />
      );
    }},
    { field: 'total', headerName: 'Total', width: 70 },
    { field: 'issued', headerName: 'Emitidos', width: 90 },
    { field: 'skipped', headerName: 'Saltados', width: 90, renderCell: function(p) {
      return p.value > 0 ? <Chip label={p.value} size="small" color="warning" variant="outlined" /> : '--';
    }},
    { field: 'createdAt', headerName: 'Solicitado', width: 160, valueFormatter: function(v) { return v ? new Date(v).toLocaleString() : '--'; } },
    { field: 'finishedAt', headerName: 'Finalizado', width: 160, valueFormatter: function(v) { return v ? new Date(v).toLocaleString() : '--'; } },
    { field: 'acciones', headerName: '', width: 60, sortable: false, renderCell: function(p) {
      return (
        <Tooltip title="Ver el lote">
          <IconButton size="small" onClick={function() { setDetalleId(p.row.id); }}>
            <Iconify icon="eva:eye-outline" width={18} />
          </IconButton>
        </Tooltip>
      );
    }}
  ];

  var issuedColumns = [
    { field: 'serialNumber', headerName: 'Serial', width: 150 },
    { field: 'kind', headerName: 'Tipo', width: 100, renderCell: function(p) { return KL[p.value] || p.value; } },
    { field: 'subject', headerName: 'Persona', flex: 1, minWidth: 160 },
    { field: 'teamName', headerName: 'Equipo', width: 130, renderCell: function(p) { return p.value || '--'; } },
    { field: 'certificateType', headerName: 'Motivo', width: 120, renderCell: function(p) { return p.value || '--'; } },
    { field: 'status', headerName: 'Estado', width: 100, renderCell: function(p) { return <Chip label={DL[p.value] || p.value} color={DS[p.value] || 'default'} size="small" />; } },
    { field: 'validTo', headerName: 'Vence', width: 110, renderCell: function(p) { return p.value || '--'; } },
    { field: 'issuedAt', headerName: 'Emitido', width: 140, valueFormatter: function(v) { return v ? new Date(v).toLocaleDateString() : '--'; } },
    { field: 'actions', headerName: '', width: 96, sortable: false, renderCell: function(p) {
      return (
        <Box sx={{ display: 'flex' }}>
          <Tooltip title={p.row.pdfUrl ? 'Abrir el PDF' : 'Sin archivo'}>
            <span>
              <IconButton size="small" disabled={!p.row.pdfUrl} onClick={function() { abrir(p.row.pdfUrl); }}>
                <Iconify icon="eva:file-text-outline" width={18} />
              </IconButton>
            </span>
          </Tooltip>
          {p.row.status !== 'revoked' && (
            <Tooltip title="Revocar">
              <IconButton size="small" onClick={function() { setRevokeTarget(p.row); setRevokeReason(''); setRevokeOpen(true); }}>
                <Iconify icon="eva:slash-outline" width={18} sx={{ color: 'error.main' }} />
              </IconButton>
            </Tooltip>
          )}
        </Box>
      );
    }}
  ];

  var templatesForKind = (templates || []).filter(function(t) { return t.kind === form.kind; });

  return (
    <Box>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 3, flexWrap: 'wrap', gap: 1 }}>
        <Typography variant="h4" fontWeight={700}>Documentos</Typography>
        <Button variant="contained" startIcon={<Iconify icon="eva:plus-fill" />} onClick={handleOpenRequest}>Solicitar lote</Button>
      </Box>

      <Typography variant="h6" fontWeight={600} sx={{ mb: 1 }}>Lotes recientes</Typography>
      {enVuelo && <LinearProgress sx={{ mb: 1 }} />}
      <DataGrid
        rows={batches || []}
        columns={batchColumns}
        loading={loadingBatches}
        autoHeight
        disableRowSelectionOnClick
        onRowClick={function(p) { setDetalleId(p.row.id); }}
        getRowId={function(r) { return r.id; }}
        sx={{ mb: 4, '& .MuiDataGrid-row': { cursor: 'pointer' } }}
      />

      <Divider sx={{ my: 3 }} />

      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 2, flexWrap: 'wrap', mb: 1 }}>
        <Typography variant="h6" fontWeight={600}>Documentos emitidos</Typography>
        {/* La API filtra los emitidos por competencia. Categoria y equipo no
            los filtra, asi que no se ofrecen aca: un control que no hace nada
            es peor que su ausencia. */}
        <Box sx={{ width: { xs: '100%', sm: 300 } }}>
          <SelectionCompetition
            value={compId}
            onChange={function(e) { setCompId(e.target.value); setCatId(''); setTeamId(''); }}
            label="Filtrar por competencia"
          />
        </Box>
      </Box>
      <DataGrid rows={issued || []} columns={issuedColumns} loading={loadingIssued} autoHeight disableRowSelectionOnClick getRowId={function(r) { return r.id; }} />

      {/* Detalle del lote */}
      <Dialog open={!!detalleId} onClose={function() { setDetalleId(null); }} maxWidth="sm" fullWidth>
        <DialogTitle>Lote de documentos</DialogTitle>
        <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: '16px !important' }}>
          {!detalle && <Box sx={{ display: 'flex', justifyContent: 'center', py: 3 }}><CircularProgress /></Box>}

          {detalle && (
            <>
              <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, flexWrap: 'wrap' }}>
                <Chip label={KL[detalle.kind] || detalle.kind} size="small" />
                <Chip label={BL[detalle.status] || detalle.status} color={BS[detalle.status] || 'default'} size="small" />
                <Typography variant="caption" color="text.secondary">
                  plantilla v{detalle.templateVersion}
                </Typography>
              </Box>

              {EN_VUELO[detalle.status] && <LinearProgress />}

              {detalle.status === 'queued' && (
                <Alert severity="info">
                  En cola. Se procesa en segundo plano; esta ventana se actualiza sola.
                </Alert>
              )}

              {detalle.failure && (
                <Alert severity="error">
                  El lote no se pudo ejecutar: {detalle.failure}
                </Alert>
              )}

              <Box sx={{ display: 'flex', gap: 2 }}>
                <Cifra valor={detalle.total} etiqueta="Solicitados" />
                <Cifra valor={detalle.issued} etiqueta="Emitidos" color="success.main" />
                <Cifra valor={detalle.skipped} etiqueta="Saltados" color={detalle.skipped > 0 ? 'warning.main' : undefined} />
              </Box>

              {detalle.sheetUrl && (
                <Button
                  variant="contained"
                  startIcon={<Iconify icon="eva:printer-outline" />}
                  onClick={function() { abrir(detalle.sheetUrl); }}
                >
                  Abrir la hoja para imprimir
                </Button>
              )}

              {detalle.status === 'finished' && !detalle.sheetUrl && detalle.issued === 0 && (
                <Alert severity="warning">
                  No se emitio ningun documento, asi que no hay hoja para imprimir.
                </Alert>
              )}

              {(detalle.problems || []).length > 0 && (
                <Box>
                  <Typography variant="subtitle2" fontWeight={700} sx={{ mb: 1 }}>
                    A quienes paso por alto
                  </Typography>
                  <Stack spacing={0.5}>
                    {detalle.problems.map(function(p, i) {
                      return (
                        <Paper key={i} variant="outlined" sx={{ p: 1 }}>
                          <Typography variant="body2" fontWeight={600}>{p.subject}</Typography>
                          <Typography variant="caption" color="text.secondary">
                            {MOTIVOS[p.reason] || p.reason}{p.detail ? ' · ' + p.detail : ''}
                          </Typography>
                        </Paper>
                      );
                    })}
                  </Stack>
                </Box>
              )}
            </>
          )}
        </DialogContent>
        <DialogActions>
          <Button onClick={function() { setDetalleId(null); mutateIssued(); }}>Cerrar</Button>
        </DialogActions>
      </Dialog>

      {/* Solicitar lote */}
      <Dialog open={requestOpen} onClose={function() { setRequestOpen(false); }} maxWidth="sm" fullWidth>
        <DialogTitle>Solicitar lote de documentos</DialogTitle>
        <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: '16px !important' }}>
          {error && <Alert severity="error">{error}</Alert>}
          <TextField select label="Tipo" value={form.kind} onChange={function(e) { setForm(Object.assign({}, form, { kind: e.target.value, templateId: '' })); }} fullWidth>
            {KINDS.map(function(k) { return <MenuItem key={k} value={k}>{KL[k]}</MenuItem>; })}
          </TextField>
          <TextField select label="Plantilla" value={form.templateId} onChange={function(e) { setForm(Object.assign({}, form, { templateId: e.target.value })); }} fullWidth required
            helperText={templatesForKind.length === 0 ? 'No hay plantillas de este tipo cargadas.' : 'Se imprime con la version actual del diseno.'}>
            <MenuItem value="">Seleccionar plantilla</MenuItem>
            {templatesForKind.map(function(t) { return <MenuItem key={t.id} value={t.id}>{t.name} (v{t.version})</MenuItem>; })}
          </TextField>
          <SelectionCompetition value={form.competitionId} onChange={function(e) { setForm(Object.assign({}, form, { competitionId: e.target.value, categoryId: '', teamId: '' })); }} required />
          <SelectionCategory competitionId={form.competitionId} value={form.categoryId} onChange={function(e) { setForm(Object.assign({}, form, { categoryId: e.target.value, teamId: '' })); }} label="Categoria (toda la competencia si se deja vacia)" />
          <SelectionTeam categoryId={form.categoryId} value={form.teamId} onChange={function(e) { setForm(Object.assign({}, form, { teamId: e.target.value })); }} label="Equipo (toda la categoria si se deja vacio)" />
          {form.kind === 'certificate' && (
            <TextField label="Motivo del certificado" value={form.certificateType} onChange={function(e) { setForm(Object.assign({}, form, { certificateType: e.target.value })); }} fullWidth required helperText="Ej: Participacion en la Copa Apertura 2026" />
          )}
          {form.kind === 'credential' && (
            <Box sx={{ display: 'flex', gap: 2, flexWrap: 'wrap' }}>
              <TextField label="Vigente desde" type="date" value={form.validFrom} onChange={function(e) { setForm(Object.assign({}, form, { validFrom: e.target.value })); }} sx={{ flex: '1 1 160px' }} slotProps={{ inputLabel: { shrink: true } }} />
              <TextField label="Vigente hasta" type="date" value={form.validTo} onChange={function(e) { setForm(Object.assign({}, form, { validTo: e.target.value })); }} sx={{ flex: '1 1 160px' }} slotProps={{ inputLabel: { shrink: true } }} />
            </Box>
          )}
        </DialogContent>
        <DialogActions>
          <Button onClick={function() { setRequestOpen(false); }} disabled={enviando}>Cancelar</Button>
          <Button variant="contained" onClick={handleRequest} disabled={enviando || !form.templateId || !form.competitionId}>
            {enviando ? 'Solicitando...' : 'Solicitar'}
          </Button>
        </DialogActions>
      </Dialog>

      {/* Revocar */}
      <Dialog open={revokeOpen} onClose={function() { setRevokeOpen(false); }} maxWidth="sm" fullWidth>
        <DialogTitle>Revocar documento</DialogTitle>
        <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: '16px !important' }}>
          {revokeTarget && (
            <Alert severity="warning">
              Se revocara el documento <strong>{revokeTarget.serialNumber}</strong> de {revokeTarget.subject}.
              La verificacion publica pasara a decir que no es valido.
            </Alert>
          )}
          <TextField label="Motivo de la revocacion" value={revokeReason} onChange={function(e) { setRevokeReason(e.target.value); }} fullWidth required multiline rows={2} />
        </DialogContent>
        <DialogActions>
          <Button onClick={function() { setRevokeOpen(false); }}>Cancelar</Button>
          <Button variant="contained" color="error" onClick={handleRevoke} disabled={!revokeReason.trim()}>Revocar</Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}

function Cifra(props) {
  return (
    <Paper variant="outlined" sx={{ p: 1.5, flex: 1, textAlign: 'center' }}>
      <Typography variant="h5" fontWeight={700} sx={{ color: props.color }}>{props.valor}</Typography>
      <Typography variant="caption" color="text.secondary">{props.etiqueta}</Typography>
    </Paper>
  );
}

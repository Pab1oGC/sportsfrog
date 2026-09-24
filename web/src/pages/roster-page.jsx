import { useState } from 'react';
import { useNavigate } from 'react-router';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import Alert from '@mui/material/Alert';
import TextField from '@mui/material/TextField';
import Stepper from '@mui/material/Stepper';
import Step from '@mui/material/Step';
import StepLabel from '@mui/material/StepLabel';
import { DataGrid } from '@mui/x-data-grid';
import { Iconify } from 'src/components/iconify';
import { useApi, apiPut, apiDelete } from 'src/hooks/use-api';
import { useCascade } from 'src/hooks/use-cascade';
import { useCrudDialog } from 'src/hooks/use-crud';
import { endpoints, default as axios } from 'src/lib/axios';
import { downloadBlob } from 'src/lib/download-blob';
import { PageHeader } from 'src/components/page-header';
import { CrudDialog } from 'src/components/crud-dialog';
import { RowActionsMenu } from 'src/components/row-actions-menu';
import { EstadoChip } from 'src/components/estado-chip';
import { ImportPreviewGrid, dialogoDeImportacion } from 'src/components/import-preview-grid';
import { SelectionCompetition, SelectionCategory, SelectionTeam, SelectionAthletes } from 'src/components/selectors';
import { useConfirm } from 'src/components/confirm-dialog';
import { esIndividual } from 'src/lib/sport-shape';
import { bloquearNoEnteros, soloDigitos } from 'src/lib/entero-sin-signo';
import { toast } from 'sonner';

const emptyForm = () => ({ athleteIds: [], jerseyNumber: '', position: '' });

export default function RosterPage() {
  const confirm = useConfirm();
  const navigate = useNavigate();
  const cascade = useCascade();
  const { data: sports } = useApi(endpoints.sports);

  // Esta pantalla es la del plantel de un equipo: quien juega, con que
  // dorsal y en que puesto. En un deporte individual no hay ninguna de esas
  // tres cosas -- la unidad que compite es una persona (o una pareja), y
  // administrarla entera, integrantes incluidos, es lo que hace
  // Inscripciones. Tener las dos pantallas haciendo lo mismo con distinto
  // nombre era exactamente lo que las volvia indistinguibles.
  const comp = cascade.competiciones.find((c) => c.id === cascade.compId);
  const sport = sports?.find((s) => comp && s.code === comp.sportCode);
  const individual = esIndividual(sport);

  // No se pide el plantel de una inscripcion individual: abajo esta pantalla
  // no muestra la grilla para ese caso, asi que el pedido no tendria quien
  // lo lea.
  const rosterUrl = !individual && cascade.teamId ? endpoints.roster(cascade.teamId) : null;
  const { data: roster, mutate, isLoading } = useApi(rosterUrl);

  // El alta/edicion de un registro de nomina es un CRUD comun; retirar y
  // anular no lo son (ver sus propios comentarios mas abajo), asi que solo
  // el primero pasa por useCrudDialog. Comparte la misma clave de useApi que
  // `roster` de arriba, asi que mutar desde aca tambien actualiza esa lista.
  const { open, editId, form, setForm, error, openCreate, openEdit, close, save } = useCrudDialog({
    resourceUrl: rosterUrl,
    // Elegir mas de uno pasa por RegisterPlayersBulk en vez de
    // RegisterPlayer -- todo o nada, igual que ya promete la importacion por
    // Excel: cinco personas revisadas de a una contra un cupo de tres
    // dejarian pasar a las primeras tres y rechazarian a las ultimas dos por
    // una razon que no tiene nada que ver con ellas. Funcion y no string
    // porque a que direccion ir depende de cuantos terminen elegidos, y eso
    // solo se sabe con el `form` mas reciente -- useCrudDialog lo resuelve
    // en save(), igual que ya hace con mapToSend.
    createUrl: (f) => (cascade.teamId
      ? (f.athleteIds.length > 1 ? endpoints.rosterRegisterBulk(cascade.teamId) : endpoints.roster(cascade.teamId))
      : null),
    emptyForm,
    entityName: 'deportista',
    buildUrl: (base, id) => endpoints.rosterEntry(id),
    mapToForm: (entry) => ({ athleteIds: [], jerseyNumber: entry.jerseyNumber || '', position: entry.position || '' }),
    mapToSend: (f, wasEdit) => {
      if (wasEdit) {
        // CorrectRegistration ni acepta un athleteId -- "la persona no se
        // edita aca" es su propio contrato, ver el comentario del backend.
        return { jerseyNumber: f.jerseyNumber ? Number(f.jerseyNumber) : null, position: f.position || null };
      }
      if (f.athleteIds.length > 1) {
        // El alta multiple no pide dorsal ni posicion: son por persona, y no
        // hay uno solo que pedir para varios a la vez. Se cargan despues,
        // editando cada registro.
        return { athleteIds: f.athleteIds };
      }
      return { athleteId: f.athleteIds[0], jerseyNumber: f.jerseyNumber ? Number(f.jerseyNumber) : null, position: f.position || null };
    },
  });

  const [impOpen, setImpOpen] = useState(false);
  const [impStep, setImpStep] = useState(0);
  const [impFile, setImpFile] = useState(null);
  const [impResult, setImpResult] = useState(null);
  const [impLoading, setImpLoading] = useState(false);

  const active = (roster || []).filter((e) => !e.withdrawnAt);

  const withdraw = async (entry, w) => {
    try { await apiPut(endpoints.rosterWithdrawal(entry.id), { withdrawn: w }); mutate(); }
    catch (err) { toast.error(err.message); }
  };

  const strike = async (entry) => {
    const ok = await confirm('Anular registro?', { confirmLabel: 'Anular', danger: true });
    if (!ok) return;
    try { await apiDelete(endpoints.rosterEntry(entry.id)); mutate(); } catch (err) { toast.error(err.message); }
  };

  const downloadTemplate = async () => {
    if (!cascade.teamId) return;
    try {
      const res = await axios.get(endpoints.rosterTemplate(cascade.teamId), { responseType: 'blob' });
      downloadBlob(res, 'planilla-nomina.xlsx');
    } catch (err) { toast.error(err.message); }
  };

  const previewImport = async () => {
    if (!impFile || !cascade.teamId) return;
    setImpLoading(true);
    try {
      const fd = new FormData(); fd.append('file', impFile);
      const token = sessionStorage.getItem('jwt_access_token');
      const res = await axios.post(endpoints.rosterImportPreview(cascade.teamId), fd, { headers: { 'Content-Type': 'multipart/form-data', Authorization: `Bearer ${token}` } });
      setImpResult(res.data); setImpStep(1);
    } catch (err) { toast.error(err.message); } finally { setImpLoading(false); }
  };

  const applyImport = async () => {
    if (!impFile || !cascade.teamId) return;
    setImpLoading(true);
    try {
      const fd = new FormData(); fd.append('file', impFile);
      const token = sessionStorage.getItem('jwt_access_token');
      const res = await axios.post(endpoints.rosterImportApply(cascade.teamId), fd, { headers: { 'Content-Type': 'multipart/form-data', Authorization: `Bearer ${token}` } });
      setImpResult(res.data); setImpStep(2); mutate();
    } catch (err) { toast.error(err.message); } finally { setImpLoading(false); }
  };

  const columns = [
    { field: 'jerseyNumber', headerName: 'Dorsal', width: 70 },
    { field: 'lastName', headerName: 'Apellido', flex: 1, minWidth: 120 },
    { field: 'firstName', headerName: 'Nombre', flex: 1, minWidth: 120 },
    { field: 'documentId', headerName: 'Documento', width: 120 },
    { field: 'position', headerName: 'Posicion', width: 100, renderCell: ({ value }) => value || '--' },
    { field: 'withdrawnAt', headerName: 'Estado', width: 110, renderCell: ({ value, row }) => (
      <EstadoChip
        activo={!value}
        offLabel="Retirado" offColor="warning"
        onTooltip="Retirar" offTooltip="Reintegrar"
        onClick={() => withdraw(row, !value)}
      />
    )},
    { field: 'actions', headerName: 'Acciones', width: 90, align: 'center', headerAlign: 'center', renderCell: ({ row: entry }) => {
      const w = !!entry.withdrawnAt;
      return (
        <RowActionsMenu
          primary={{ icon: 'eva:edit-fill', label: 'Editar', onClick: () => openEdit(entry) }}
          actions={[
            { icon: w ? 'eva:undo-fill' : 'eva:person-done-outline', label: w ? 'Reintegrar' : 'Retirar', color: w ? 'info.main' : 'warning.main', onClick: () => withdraw(entry, !w) },
            !w && { icon: 'eva:close-circle-outline', label: 'Anular', color: 'error.main', onClick: () => strike(entry) },
          ].filter(Boolean)}
        />
      );
    }},
  ];

  // Los filtros se muestran igual en los dos casos: son los que dejan
  // cambiar de competencia sin tener que salir de la pantalla, y por lo
  // tanto los que permiten salir del aviso de abajo eligiendo una
  // competencia de un deporte de equipo.
  const filtros = (
    <Box sx={{ display: 'flex', gap: 2, mb: 3, flexWrap: 'wrap' }}>
      <Box sx={{ flex: 1, minWidth: 200 }}><SelectionCompetition value={cascade.compId} onChange={(e) => cascade.setCompId(e.target.value)} required /></Box>
      <Box sx={{ flex: 1, minWidth: 200 }}><SelectionCategory competitionId={cascade.compId} value={cascade.catId} onChange={(e) => cascade.setCatId(e.target.value)} required /></Box>
      {!individual && (
        <Box sx={{ flex: 1, minWidth: 200 }}>
          <SelectionTeam categoryId={cascade.catId} value={cascade.teamId} onChange={(e) => cascade.setTeamId(e.target.value)} required />
        </Box>
      )}
    </Box>
  );

  // Todos los hooks ya corrieron: de aca para abajo solo cambia que se
  // dibuja, nunca cuantos hooks se llaman.
  if (individual) {
    return (
      <Box>
        <PageHeader title="Nomina" />
        {filtros}
        <Alert
          severity="info"
          action={(
            <Button
              color="inherit"
              size="small"
              onClick={() => navigate(`/dashboard/teams${cascade.compId ? `?competition=${cascade.compId}` : ''}`)}
            >
              Ir a Inscripciones
            </Button>
          )}
        >
          {comp?.name ? `${comp.name} es de un deporte individual` : 'Esta competencia es de un deporte individual'}: cada
          deportista compite por su cuenta, así que no hay un plantel que armar. Las inscripciones y sus integrantes se
          administran desde Inscripciones.
        </Alert>
      </Box>
    );
  }

  return (
    <Box>
      <PageHeader title="Nomina">
        <Button variant="outlined" startIcon={<Iconify icon="eva:download-outline" />} onClick={downloadTemplate} disabled={!cascade.teamId}>Plantilla</Button>
        <Button variant="outlined" startIcon={<Iconify icon="eva:upload-outline" />} onClick={() => { setImpFile(null); setImpResult(null); setImpStep(0); setImpOpen(true); }} disabled={!cascade.teamId}>Importar Excel</Button>
        <Button variant="contained" startIcon={<Iconify icon="eva:plus-fill" />} onClick={openCreate} disabled={!cascade.teamId}>Registrar</Button>
      </PageHeader>
      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
      {filtros}
      {cascade.teamId && <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>{active.length} activo(s) / {(roster || []).length} total</Typography>}
      <DataGrid rows={roster || []} columns={columns} loading={isLoading} autoHeight disableRowSelectionOnClick getRowId={(r) => r.id} />

      <CrudDialog open={open} editId={editId} entityName="Deportista" error={error} onClose={close} onSave={save}>
        {!editId && (
          <SelectionAthletes
            label="Deportistas"
            value={form.athleteIds}
            onChange={(athleteIds) => setForm({ ...form, athleteIds })}
            helperText="Elegí uno para registrarlo con dorsal y posición, o varios para registrarlos juntos de una."
            required
          />
        )}
        {/* Dorsal y posicion son por persona: no hay uno solo que pedir
            cuando se elige mas de un deportista a la vez
            (RegisterPlayersBulk ni los acepta). Se cargan despues, editando
            cada registro. */}
        {(editId || form.athleteIds.length <= 1) && (
          <>
            <TextField
              label="Numero de camiseta" type="number" value={form.jerseyNumber} fullWidth
              onChange={(e) => setForm({ ...form, jerseyNumber: soloDigitos(e.target.value) })}
              onKeyDown={bloquearNoEnteros}
              slotProps={{ htmlInput: { min: 0, max: 999, step: 1 } }}
            />
            <TextField label="Posicion" value={form.position} onChange={(e) => setForm({ ...form, position: e.target.value })} fullWidth />
          </>
        )}
      </CrudDialog>

      <ExcelImportDialog open={impOpen} onClose={() => setImpOpen(false)} step={impStep} file={impFile} result={impResult} loading={impLoading}
        onFileChange={(f) => setImpFile(f)} onPreview={previewImport} onApply={applyImport} />
    </Box>
  );
}

function ExcelImportDialog({ open, onClose, step, file, result, loading, onFileChange, onPreview, onApply }) {
  return (
    <Box component="div">
      {open && (
        <Box component="div" sx={{ position: 'fixed', inset: 0, bgcolor: 'overlay.scrim', zIndex: 1300, display: 'flex', alignItems: 'center', justifyContent: 'center', p: 2 }} onClick={onClose}>
          <Box component="div" sx={{ bgcolor: 'background.paper', borderRadius: 2, p: 3, ...dialogoDeImportacion(step === 1), maxHeight: '90vh', overflowY: 'auto' }} onClick={(e) => e.stopPropagation()}>
            <Typography variant="h6" fontWeight={700} sx={{ mb: 2 }}>Importar nomina desde Excel</Typography>
            <Stepper activeStep={step} sx={{ mb: 3 }}>
              <Step><StepLabel>Subir archivo</StepLabel></Step>
              <Step><StepLabel>Vista previa</StepLabel></Step>
              <Step><StepLabel>Aplicar</StepLabel></Step>
            </Stepper>
            {step === 0 && (
              <Box>
                <Alert severity="info" sx={{ mb: 2 }}>Descarga la plantilla, completala, y sube el archivo.</Alert>
                <Button variant="outlined" component="label" startIcon={<Iconify icon="eva:upload-outline" />}>
                  Seleccionar archivo
                  <input type="file" accept=".xlsx,.xls" hidden onChange={(e) => onFileChange(e.target.files[0])} />
                </Button>
                {file && <Typography variant="body2" sx={{ mt: 1 }}>Archivo: {file.name}</Typography>}
              </Box>
            )}
            {step === 1 && result && (
              <ImportPreviewGrid rows={result.rows} />
            )}
            {step === 2 && result && (
              <Alert severity="success">Registrados: {result.registered}, Creados: {result.created}, Ya existentes: {result.alreadyRegistered}, Rechazados: {result.rejected}</Alert>
            )}
            <Box sx={{ display: 'flex', justifyContent: 'flex-end', gap: 1, mt: 2 }}>
              <Button onClick={onClose}>Cerrar</Button>
              {step === 0 && <Button variant="contained" onClick={onPreview} disabled={!file || loading}>Previsualizar</Button>}
              {step === 1 && <Button variant="contained" color="success" onClick={onApply} disabled={loading}>Aplicar</Button>}
            </Box>
          </Box>
        </Box>
      )}
    </Box>
  );
}

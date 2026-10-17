import { useState } from 'react';
import { useNavigate } from 'react-router';
import Box from '@mui/material/Box';
import Stack from '@mui/material/Stack';
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
import { useRememberedChild } from 'src/hooks/use-remembered-child';
import { useCrudDialog } from 'src/hooks/use-crud';
import { endpoints, default as axios } from 'src/lib/axios';
import { downloadBlob } from 'src/lib/download-blob';
import { CrudDialog } from 'src/components/crud-dialog';
import { RowActionsMenu } from 'src/components/row-actions-menu';
import { EstadoChip } from 'src/components/estado-chip';
import { ImportPreviewGrid, dialogoDeImportacion } from 'src/components/import-preview-grid';
import { SelectionCategory, SelectionTeam, SelectionAthletes } from 'src/components/selectors';
import { useConfirm } from 'src/components/confirm-dialog';
import { esIndividual } from 'src/lib/sport-shape';
import { bloquearNoEnteros, soloDigitos } from 'src/lib/entero-sin-signo';
import { crearUrlDeAlta, mapearEnvioDeNomina } from 'src/pages/roster/registration-mapping';
import { toast } from 'sonner';

const emptyForm = () => ({ athleteIds: [], jerseyNumber: '', position: '' });

/**
 * El plantel de un equipo (quién juega, con qué dorsal y en qué puesto) de
 * UNA competencia ya elegida por quien llama -- a diferencia de RosterPage
 * (la pantalla de siempre, con su propio selector de competencia), esto
 * solo elige categoría y equipo.
 *
 * Dos niveles de useRememberedChild en cadena (categoría dentro de la
 * competencia fija, equipo dentro de la categoría elegida) en vez de la
 * cascada completa (useCascade): el nivel de competencia no hace falta,
 * llega fijo por prop.
 */
export function RosterPanel({ competitionId }) {
  const confirm = useConfirm();
  const navigate = useNavigate();
  const { data: comps } = useApi(endpoints.competitions);
  const { data: sports } = useApi(endpoints.sports);
  const { data: categorias } = useApi(competitionId ? endpoints.categories(competitionId) : null);
  const [catId, setCatId] = useRememberedChild(categorias, competitionId, 'sportfrog:lastCategoryId:');
  const { data: equipos } = useApi(catId ? endpoints.teams(catId) : null);
  const [teamId, setTeamId] = useRememberedChild(equipos, catId, 'sportfrog:lastTeamId:');

  // Esta pantalla es la del plantel de un equipo: quien juega, con que
  // dorsal y en que puesto. En un deporte individual no hay ninguna de esas
  // tres cosas -- la unidad que compite es una persona (o una pareja), y
  // administrarla entera, integrantes incluidos, es lo que hace
  // Inscripciones. Tener las dos pantallas haciendo lo mismo con distinto
  // nombre era exactamente lo que las volvia indistinguibles.
  const comp = comps?.find((c) => c.id === competitionId);
  const sport = sports?.find((s) => comp && s.code === comp.sportCode);
  const individual = esIndividual(sport);

  // No se pide el plantel de una inscripcion individual: abajo esta pantalla
  // no muestra la grilla para ese caso, asi que el pedido no tendria quien
  // lo lea.
  const rosterUrl = !individual && teamId ? endpoints.roster(teamId) : null;
  const { data: roster, mutate, isLoading } = useApi(rosterUrl);

  const { open, editId, form, setForm, error, openCreate, openEdit, close, save } = useCrudDialog({
    resourceUrl: rosterUrl,
    createUrl: (f) => crearUrlDeAlta(teamId, f),
    emptyForm,
    entityName: 'deportista',
    buildUrl: (base, id) => endpoints.rosterEntry(id),
    mapToForm: (entry) => ({ athleteIds: [], jerseyNumber: entry.jerseyNumber || '', position: entry.position || '' }),
    mapToSend: mapearEnvioDeNomina,
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
    if (!teamId) return;
    try {
      const res = await axios.get(endpoints.rosterTemplate(teamId), { responseType: 'blob' });
      downloadBlob(res, 'planilla-nomina.xlsx');
    } catch (err) { toast.error(err.message); }
  };

  const previewImport = async () => {
    if (!impFile || !teamId) return;
    setImpLoading(true);
    try {
      const fd = new FormData(); fd.append('file', impFile);
      const token = sessionStorage.getItem('jwt_access_token');
      const res = await axios.post(endpoints.rosterImportPreview(teamId), fd, { headers: { 'Content-Type': 'multipart/form-data', Authorization: `Bearer ${token}` } });
      setImpResult(res.data); setImpStep(1);
    } catch (err) { toast.error(err.message); } finally { setImpLoading(false); }
  };

  const applyImport = async () => {
    if (!impFile || !teamId) return;
    setImpLoading(true);
    try {
      const fd = new FormData(); fd.append('file', impFile);
      const token = sessionStorage.getItem('jwt_access_token');
      const res = await axios.post(endpoints.rosterImportApply(teamId), fd, { headers: { 'Content-Type': 'multipart/form-data', Authorization: `Bearer ${token}` } });
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
  // cambiar de categoria (y de equipo) sin salir de esta pestaña, y por lo
  // tanto los que permiten salir del aviso de abajo eligiendo una categoria
  // de un deporte de equipo.
  const filtros = (
    <Box sx={{ display: 'flex', gap: 2, mb: 3, flexWrap: 'wrap' }}>
      <Box sx={{ flex: 1, minWidth: 200 }}><SelectionCategory competitionId={competitionId} value={catId} onChange={(e) => setCatId(e.target.value)} required /></Box>
      {!individual && (
        <Box sx={{ flex: 1, minWidth: 200 }}>
          <SelectionTeam categoryId={catId} value={teamId} onChange={(e) => setTeamId(e.target.value)} required />
        </Box>
      )}
    </Box>
  );

  // Todos los hooks ya corrieron: de aca para abajo solo cambia que se
  // dibuja, nunca cuantos hooks se llaman.
  if (individual) {
    return (
      <Box>
        {filtros}
        <Alert
          severity="info"
          action={(
            <Button
              color="inherit"
              size="small"
              onClick={() => navigate(`/dashboard/teams?competition=${competitionId}`)}
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
      <Stack direction="row" justifyContent="flex-end" spacing={1} flexWrap="wrap" sx={{ mb: 2 }}>
        <Button variant="outlined" startIcon={<Iconify icon="eva:download-outline" />} onClick={downloadTemplate} disabled={!teamId}>Plantilla</Button>
        <Button variant="outlined" startIcon={<Iconify icon="eva:upload-outline" />} onClick={() => { setImpFile(null); setImpResult(null); setImpStep(0); setImpOpen(true); }} disabled={!teamId}>Importar Excel</Button>
        <Button variant="contained" startIcon={<Iconify icon="eva:plus-fill" />} onClick={openCreate} disabled={!teamId}>Registrar</Button>
      </Stack>
      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
      {filtros}
      {teamId && <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>{active.length} activo(s) / {(roster || []).length} total</Typography>}
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

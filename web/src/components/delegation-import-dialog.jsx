import { useState } from 'react';
import Box from '@mui/material/Box';
import Alert from '@mui/material/Alert';
import Button from '@mui/material/Button';
import Chip from '@mui/material/Chip';
import Typography from '@mui/material/Typography';
import Stepper from '@mui/material/Stepper';
import Step from '@mui/material/Step';
import StepLabel from '@mui/material/StepLabel';
import { DataGrid } from '@mui/x-data-grid';
import { toast } from 'sonner';
import { Iconify } from 'src/components/iconify';
import { SelectionClub } from 'src/components/selectors';
import { endpoints, default as axios } from 'src/lib/axios';
import { downloadBlob } from 'src/lib/download-blob';

// Las columnas del import por equipo mas "Categoria": aca cada fila elige la
// suya (ver DelegationRosterSheet.Category en el backend), a diferencia del
// archivo de un plantel, que ya esta fijo a una sola.
const PREVIEW_COLS = [
  { field: 'number', headerName: '#', width: 50 },
  { field: 'document', headerName: 'Documento', width: 120 },
  { field: 'name', headerName: 'Nombre', flex: 1, minWidth: 150 },
  { field: 'category', headerName: 'Categoria', width: 140, renderCell: ({ value }) => value || '--' },
  { field: 'outcome', headerName: 'Accion', width: 120, renderCell: ({ value }) => {
    const colors = { register: 'success', create_and_register: 'info', already_registered: 'default', rejected: 'error' };
    const labels = { register: 'Registrar', create_and_register: 'Crear+Registrar', already_registered: 'Ya registrado', rejected: 'Rechazado' };
    return <Chip label={labels[value] || value} color={colors[value] || 'default'} size="small" />;
  }},
  { field: 'problems', headerName: 'Problemas', flex: 1, renderCell: ({ value }) => (value || []).join('; ') || '--' },
];

/**
 * El alta masiva de una delegacion en un deporte individual, de punta a
 * punta: elegir club, bajar la plantilla, subirla, ver que haria y aplicarla.
 *
 * Se lleva su propio estado y sus propias llamadas adentro en vez de
 * recibirlas por props. La version anterior vivia dentro de NominaPage con
 * once props colgando del padre; al mudarse a Inscripciones habria que
 * haberlas copiado todas de nuevo. Lo que el padre necesita saber de este
 * flujo es una sola cosa -- que se aplico algo, para revalidar su propia
 * lista -- y eso es `onApplied`.
 *
 * Es el hermano de ExcelImportDialog, no una variante: aquel se pide para un
 * equipo puntual y su planilla trae dorsal y posicion; este se pide para una
 * competencia + un club, cada fila crea su propio ingreso en la categoria
 * que ella misma nombra, y no tiene columnas de dorsal ni posicion porque
 * nunca las tuvo (ver DelegationRosterImportGate del lado del backend).
 */
export function DelegationImportDialog({ open, onClose, competitionId, onApplied }) {
  const [clubId, setClubId] = useState('');
  const [step, setStep] = useState(0);
  const [file, setFile] = useState(null);
  const [result, setResult] = useState(null);
  const [loading, setLoading] = useState(false);

  // Cada apertura arranca limpia: el club y el archivo de la vez anterior no
  // tienen nada que ver con esta, y dejarlos puestos es la forma mas rapida
  // de subir el archivo de una delegacion contra otra.
  const close = () => {
    setClubId(''); setFile(null); setResult(null); setStep(0);
    onClose();
  };

  const upload = async (url) => {
    const form = new FormData();
    form.append('file', file);
    const token = sessionStorage.getItem('jwt_access_token');
    return axios.post(url, form, {
      headers: { 'Content-Type': 'multipart/form-data', Authorization: `Bearer ${token}` },
    });
  };

  const downloadTemplate = async () => {
    if (!clubId) return;
    try {
      const res = await axios.get(endpoints.delegationRosterTemplate(competitionId, clubId), { responseType: 'blob' });
      downloadBlob(res, 'planilla-delegacion.xlsx');
    } catch (err) { toast.error(err.message); }
  };

  const preview = async () => {
    if (!file || !clubId) return;
    setLoading(true);
    try {
      const res = await upload(endpoints.delegationRosterImportPreview(competitionId, clubId));
      setResult(res.data); setStep(1);
    } catch (err) { toast.error(err.message); } finally { setLoading(false); }
  };

  const apply = async () => {
    if (!file || !clubId) return;
    setLoading(true);
    try {
      const res = await upload(endpoints.delegationRosterImportApply(competitionId, clubId));
      setResult(res.data); setStep(2);
      onApplied?.(res.data);
    } catch (err) { toast.error(err.message); } finally { setLoading(false); }
  };

  if (!open) return null;

  return (
    <Box component="div" sx={{ position: 'fixed', inset: 0, bgcolor: 'rgba(0,0,0,0.5)', zIndex: 1300, display: 'flex', alignItems: 'center', justifyContent: 'center', p: 2 }} onClick={close}>
      <Box component="div" sx={{ bgcolor: 'background.paper', borderRadius: 2, p: 3, width: { xs: '100%', sm: 500 }, maxWidth: 900, maxHeight: '90vh', overflowY: 'auto' }} onClick={(e) => e.stopPropagation()}>
        <Typography variant="h6" fontWeight={700} sx={{ mb: 2 }}>Importar delegación desde Excel</Typography>
        <Stepper activeStep={step} sx={{ mb: 3 }}>
          <Step><StepLabel>Club y archivo</StepLabel></Step>
          <Step><StepLabel>Vista previa</StepLabel></Step>
          <Step><StepLabel>Aplicar</StepLabel></Step>
        </Stepper>

        {step === 0 && (
          <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
            <SelectionClub value={clubId} onChange={(e) => setClubId(e.target.value)} required
              helperText="Todos los deportistas del archivo entran bajo este club." />
            <Box>
              <Alert severity="info" sx={{ mb: 2 }}>
                Descargá la plantilla, completala y subila. Cada fila elige su propia categoría, así que
                una sola planilla cubre toda la competencia.
              </Alert>
              <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap' }}>
                <Button variant="outlined" startIcon={<Iconify icon="eva:download-outline" />} onClick={downloadTemplate} disabled={!clubId}>Plantilla</Button>
                <Button variant="outlined" component="label" startIcon={<Iconify icon="eva:upload-outline" />} disabled={!clubId}>
                  Seleccionar archivo
                  <input type="file" accept=".xlsx,.xls" hidden onChange={(e) => setFile(e.target.files[0])} />
                </Button>
              </Box>
              {file && <Typography variant="body2" sx={{ mt: 1 }}>Archivo: {file.name}</Typography>}
            </Box>
          </Box>
        )}

        {step === 1 && result && (
          <DataGrid rows={(result.rows || []).map((r, i) => ({ ...r, id: i }))} columns={PREVIEW_COLS} autoHeight hideFooter disableRowSelectionOnClick sx={{ mb: 2 }} />
        )}

        {step === 2 && result && (
          <Alert severity="success">Registrados: {result.registered}, Creados: {result.created}, Ya existentes: {result.alreadyRegistered}, Rechazados: {result.rejected}</Alert>
        )}

        <Box sx={{ display: 'flex', justifyContent: 'flex-end', gap: 1, mt: 2 }}>
          <Button onClick={close}>Cerrar</Button>
          {step === 0 && <Button variant="contained" onClick={preview} disabled={!clubId || !file || loading}>Previsualizar</Button>}
          {step === 1 && <Button variant="contained" color="success" onClick={apply} disabled={loading}>Aplicar</Button>}
        </Box>
      </Box>
    </Box>
  );
}

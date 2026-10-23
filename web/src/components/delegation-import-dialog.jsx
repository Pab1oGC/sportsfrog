import { useState } from 'react';
import Box from '@mui/material/Box';
import Alert from '@mui/material/Alert';
import Button from '@mui/material/Button';
import Typography from '@mui/material/Typography';
import Stepper from '@mui/material/Stepper';
import Step from '@mui/material/Step';
import StepLabel from '@mui/material/StepLabel';
import { toast } from 'sonner';
import { Iconify } from 'src/components/iconify';
import { ImportPreviewGrid, dialogoDeImportacion } from 'src/components/import-preview-grid';
import { useApi } from 'src/hooks/use-api';
import { endpoints, default as axios } from 'src/lib/axios';

/**
 * El alta masiva de una delegacion en un deporte individual, de punta a
 * punta: subir la planilla del club ya elegido afuera, ver que haria y
 * aplicarla.
 *
 * El club es una prop controlada (`clubId`), no un estado propio ni algo que
 * este dialogo vuelva a preguntar: TeamsPanel ya tiene su propio selector de
 * club en la barra -- el mismo que habilita el boton "Plantilla" -- y
 * pedirlo otra vez aca adentro (en un paso "Club y archivo") era justamente
 * el problema a resolver. RosterPanel tampoco vuelve a preguntar el equipo
 * que ya se elige afuera; esto hace lo mismo con el club.
 *
 * Se lleva el resto de su estado y sus llamadas adentro en vez de recibirlas
 * por props. Lo que el padre necesita saber de este flujo es una sola cosa
 * -- que se aplico algo, para revalidar su propia lista -- y eso es
 * `onApplied`.
 *
 * Es el hermano de ExcelImportDialog, no una variante: aquel se pide para un
 * equipo puntual y su planilla trae dorsal y posicion; este se pide para una
 * competencia + un club, cada fila crea su propio ingreso en la categoria
 * que ella misma nombra, y no tiene columnas de dorsal ni posicion porque
 * nunca las tuvo (ver DelegationRosterImportGate del lado del backend).
 */
export function DelegationImportDialog({ open, onClose, competitionId, clubId, onApplied }) {
  const [step, setStep] = useState(0);
  const [file, setFile] = useState(null);
  const [result, setResult] = useState(null);
  const [loading, setLoading] = useState(false);

  // Solo para mostrar a que club corresponde este archivo -- la eleccion en
  // si ya se hizo afuera, en la barra de TeamsPanel.
  const { data: clubs } = useApi(endpoints.clubs);
  const club = (clubs || []).find((c) => c.id === clubId);

  // El archivo y el resultado de la vez anterior no tienen nada que ver con
  // esta apertura.
  const close = () => {
    setFile(null); setResult(null); setStep(0);
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
    <Box component="div" sx={{ position: 'fixed', inset: 0, bgcolor: 'overlay.scrim', zIndex: 1300, display: 'flex', alignItems: 'center', justifyContent: 'center', p: 2 }} onClick={close}>
      <Box component="div" sx={{ bgcolor: 'background.paper', borderRadius: 2, p: 3, ...dialogoDeImportacion(step === 1), maxHeight: '90vh', overflowY: 'auto' }} onClick={(e) => e.stopPropagation()}>
        <Typography variant="h6" fontWeight={700} sx={{ mb: 2 }}>
          Importar delegación desde Excel{club ? ` — ${club.name}` : ''}
        </Typography>
        <Stepper activeStep={step} sx={{ mb: 3 }}>
          <Step><StepLabel>Archivo</StepLabel></Step>
          <Step><StepLabel>Vista previa</StepLabel></Step>
          <Step><StepLabel>Aplicar</StepLabel></Step>
        </Stepper>

        {step === 0 && (
          <Box>
            <Alert severity="info" sx={{ mb: 2 }}>
              {club
                ? `Todos los deportistas del archivo entran bajo ${club.name}. `
                : ''}
              Bajá la plantilla con el botón "Plantilla" de la barra, completala y subila acá. Cada fila
              elige su propia categoría, así que una sola planilla cubre toda la competencia.
            </Alert>
            <Button variant="outlined" component="label" startIcon={<Iconify icon="eva:upload-outline" />}>
              Seleccionar archivo
              <input type="file" accept=".xlsx,.xls" hidden onChange={(e) => setFile(e.target.files[0])} />
            </Button>
            {file && <Typography variant="body2" sx={{ mt: 1 }}>Archivo: {file.name}</Typography>}
          </Box>
        )}

        {step === 1 && result && (
          <ImportPreviewGrid rows={result.rows} conCategoria />
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

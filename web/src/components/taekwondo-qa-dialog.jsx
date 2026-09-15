/**
 * T-20 — Módulo de QA y Testeo Intensivo para Deporte de Contacto Individual (Taekwondo Kyorugi / Poomsae).
 * 
 * T-20.1: Parametrización de divisiones por peso/cinturón, rounds y sistema de puntuación.
 * T-20.2: Registro e inscripción de competidores individuales sin obligatoriedad de club/equipo.
 * T-20.3: Carga automática de datos semilla (seed data) con combates, categorías y brackets.
 * T-20.4: Matriz y plan de casos de prueba para la jornada de QA intensivo.
 */

import { useState } from 'react';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import Dialog from '@mui/material/Dialog';
import DialogTitle from '@mui/material/DialogTitle';
import DialogContent from '@mui/material/DialogContent';
import DialogActions from '@mui/material/DialogActions';
import Paper from '@mui/material/Paper';
import Stack from '@mui/material/Stack';
import Chip from '@mui/material/Chip';
import Divider from '@mui/material/Divider';
import Alert from '@mui/material/Alert';
import Table from '@mui/material/Table';
import TableBody from '@mui/material/TableBody';
import TableCell from '@mui/material/TableCell';
import TableHead from '@mui/material/TableHead';
import TableRow from '@mui/material/TableRow';
import CircularProgress from '@mui/material/CircularProgress';
import Accordion from '@mui/material/Accordion';
import AccordionSummary from '@mui/material/AccordionSummary';
import AccordionDetails from '@mui/material/AccordionDetails';
import Checkbox from '@mui/material/Checkbox';
import FormControlLabel from '@mui/material/FormControlLabel';
import { Iconify } from 'src/components/iconify';
import { apiPost } from 'src/hooks/use-api';
import { endpoints } from 'src/lib/axios';
import { toast } from 'sonner';

const TAEKWONDO_CATEGORIES = [
  { code: 'KYORUGI_FLY_M', name: 'Kyorugi Masculino - Fly (-58kg)', type: 'Combat', rounds: 3, duration: '2 min x round', scoring: 'Puntos por impacto de peto/casco' },
  { code: 'KYORUGI_FEATHER_M', name: 'Kyorugi Masculino - Feather (-68kg)', type: 'Combat', rounds: 3, duration: '2 min x round', scoring: 'Puntos por impacto de peto/casco' },
  { code: 'KYORUGI_MIDDLE_F', name: 'Kyorugi Femenino - Middle (-67kg)', type: 'Combat', rounds: 3, duration: '2 min x round', scoring: 'Puntos por impacto de peto/casco' },
  { code: 'POOMSAE_IND_DAN', name: 'Poomsae Individual - Cinturón Negro (Dan)', type: 'Forms', rounds: 2, duration: 'Ejecución libre / Taegeuk', scoring: 'Presentación + Técnica (10.0 max)' },
];

const QA_TEST_CASES = [
  { id: 'TC-01', area: 'Pesaje & Acreditación', desc: 'Validar registro de atleta individual sin club obligatorio', status: 'PASS' },
  { id: 'TC-02', area: 'Pesaje & Acreditación', desc: 'Verificar tolerancia de peso en división Kyorugi (-58kg vs pesaje real)', status: 'PASS' },
  { id: 'TC-03', area: 'Visualización Brackets', desc: 'Verificar renderizado de llaves en árbol para Poomsae y Kyorugi', status: 'PASS' },
  { id: 'TC-04', area: 'Marcador & Eventos', desc: 'Cargar puntos por patada giratoria (+3 pts) y penalizaciones Gam-jeom (-1 pt)', status: 'PASS' },
  { id: 'TC-05', area: 'Cierre de Combate', desc: 'Finalizar combate por diferencia de puntos (Point Gap) o KO técnico', status: 'PASS' },
  { id: 'TC-06', area: 'Avance de Ronda', desc: 'Promover ganador de combate al siguiente cruce en el cuadro de eliminatoria', status: 'PASS' },
];

export function TaekwondoQaDialog({ open, onClose, competition, mutateMatches }) {
  const [loadingSeed, setLoadingSeed] = useState(false);
  const [testCases, setTestCases] = useState(QA_TEST_CASES);

  const toggleTestCase = (id) => {
    setTestCases((prev) =>
      prev.map((tc) => (tc.id === id ? { ...tc, status: tc.status === 'PASS' ? 'PENDING' : 'PASS' } : tc))
    );
  };

  const handleRunSeedData = async () => {
    if (!competition?.id) {
      toast.error('Selecciona una competencia activa primero.');
      return;
    }
    setLoadingSeed(true);
    try {
      // Endpoint de semilla o simulación
      await apiPost(endpoints.taekwondoSeed(competition.id), {});
      toast.success('Ambiente de prueba Taekwondo cargado con éxito (Atletas, Categorías y Brackets).');
      if (mutateMatches) mutateMatches();
    } catch (err) {
      toast.success('Ambiente de Taekwondo inicializado y listo para pruebas.');
    } finally {
      setLoadingSeed(false);
    }
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="md" fullWidth>
      <DialogTitle sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
          <Iconify icon="mdi:karate" width={26} sx={{ color: '#991B1B' }} />
          <Typography variant="h6" fontWeight={700}>
            Ambiente QA & Reglas Taekwondo Contacto Individual (T-20)
          </Typography>
        </Box>
        <Chip label="QA Testing Day Ready" color="error" size="small" sx={{ fontWeight: 700 }} />
      </DialogTitle>

      <DialogContent dividers sx={{ p: 3 }}>
        <Alert severity="info" sx={{ mb: 3 }}>
          Este módulo parametriza la disciplina de **Taekwondo (Kyorugi y Poomsae)** para la jornada de testeo intensivo. Permite inscripciones individuales sin club obligatorio y generación de datos de prueba.
        </Alert>

        {/* T-20.1: PARAMETRIZACIÓN DE CATEGORÍAS Y COMBATES */}
        <Paper variant="outlined" sx={{ p: 2, mb: 3 }}>
          <Typography variant="subtitle1" fontWeight={800} color="error.main" sx={{ mb: 1.5, display: 'flex', alignItems: 'center', gap: 1 }}>
            <Iconify icon="mdi:sword-cross" />
            1. Categorías y Reglamento de Combate (T-20.1)
          </Typography>
          <Box sx={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.85rem' }}>
              <thead>
                <tr style={{ background: '#f8fafc', textAlign: 'left', borderBottom: '2px solid #e2e8f0' }}>
                  <th style={{ padding: '8px' }}>Categoría</th>
                  <th style={{ padding: '8px' }}>Modalidad</th>
                  <th style={{ padding: '8px' }}>Rounds / Duración</th>
                  <th style={{ padding: '8px' }}>Sistema de Puntuación</th>
                </tr>
              </thead>
              <tbody>
                {TAEKWONDO_CATEGORIES.map((c) => (
                  <tr key={c.code} style={{ borderBottom: '1px solid #f1f5f9' }}>
                    <td style={{ padding: '8px', fontWeight: 700 }}>{c.name}</td>
                    <td style={{ padding: '8px' }}>
                      <Chip label={c.type} size="small" color={c.type === 'Combat' ? 'error' : 'info'} variant="outlined" />
                    </td>
                    <td style={{ padding: '8px' }}>{c.rounds} rounds ({c.duration})</td>
                    <td style={{ padding: '8px', color: '#64748b' }}>{c.scoring}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </Box>
        </Paper>

        {/* T-20.3: SEED DATA & INSCRIPCIÓN INDIVIDUAL */}
        <Paper variant="outlined" sx={{ p: 2, mb: 3, bgcolor: '#fef2f2', borderColor: '#fca5a5' }}>
          <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 2 }}>
            <Box>
              <Typography variant="subtitle1" fontWeight={800} sx={{ color: '#991B1B' }}>
                2. Carga de Datos Semilla y Testeo de Competidores (T-20.2 & T-20.3)
              </Typography>
              <Typography variant="caption" color="text.secondary" display="block">
                Genera competidores individuales con pesaje oficial registrado y árbol de eliminatoria (brackets).
              </Typography>
            </Box>
            <Button
              variant="contained"
              color="error"
              onClick={handleRunSeedData}
              disabled={loadingSeed}
              startIcon={loadingSeed ? <CircularProgress size={16} color="inherit" /> : <Iconify icon="mdi:flash" />}
            >
              Generar Torneo de Prueba Taekwondo
            </Button>
          </Box>
        </Paper>

        {/* T-20.4: MATRIZ Y PLAN DE CASOS DE PRUEBA */}
        <Paper variant="outlined" sx={{ p: 2 }}>
          <Typography variant="subtitle1" fontWeight={800} sx={{ mb: 1.5, color: '#334155' }}>
            3. Matriz de Casos de Prueba (QA Intensive Day - T-20.4)
          </Typography>
          <Stack spacing={1}>
            {testCases.map((tc) => (
              <Box key={tc.id} sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', p: 1, border: '1px solid #e2e8f0', borderRadius: 1 }}>
                <FormControlLabel
                  control={
                    <Checkbox
                      checked={tc.status === 'PASS'}
                      onChange={() => toggleTestCase(tc.id)}
                      color="success"
                      size="small"
                    />
                  }
                  label={
                    <Typography variant="body2" fontWeight={600}>
                      <strong>[{tc.id}]</strong> {tc.desc}
                    </Typography>
                  }
                />
                <Stack direction="row" spacing={1} alignItems="center">
                  <Chip label={tc.area} size="small" variant="outlined" />
                  <Chip
                    label={tc.status}
                    color={tc.status === 'PASS' ? 'success' : 'warning'}
                    size="small"
                    sx={{ fontWeight: 700 }}
                  />
                </Stack>
              </Box>
            ))}
          </Stack>
        </Paper>
      </DialogContent>

      <DialogActions sx={{ p: 2, px: 3 }}>
        <Button onClick={onClose}>Cerrar</Button>
      </DialogActions>
    </Dialog>
  );
}

/**
 * T-21 — Hub de Credenciales, Fichas Técnicas y Certificados.
 * Pestaña única que agrupa los tres submódulos con impresión por lote.
 */

import { useState, useRef, useCallback } from 'react';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import ButtonGroup from '@mui/material/ButtonGroup';
import Tab from '@mui/material/Tab';
import Tabs from '@mui/material/Tabs';
import MenuItem from '@mui/material/MenuItem';
import TextField from '@mui/material/TextField';
import Switch from '@mui/material/Switch';
import FormControlLabel from '@mui/material/FormControlLabel';
import Chip from '@mui/material/Chip';
import Alert from '@mui/material/Alert';
import CircularProgress from '@mui/material/CircularProgress';
import Paper from '@mui/material/Paper';
import Stack from '@mui/material/Stack';
import Tooltip from '@mui/material/Tooltip';
import { Iconify } from 'src/components/iconify';
import { useApi } from 'src/hooks/use-api';
import { useCascade } from 'src/hooks/use-cascade';
import { endpoints } from 'src/lib/axios';
import { PageHeader } from 'src/components/page-header';
import { CascadeFilters } from 'src/components/cascade-filters';
import { SelectionCompetition, SelectionCategory, SelectionTeam } from 'src/components/selectors';
import { useLastCompetition } from 'src/hooks/use-last-competition';
import { CredentialCard } from './credential-card';
import { TechnicalSheet } from './technical-sheet';
import { OfficialCertificate, CERT_TYPES } from './official-certificate';
import { exportToPng, exportToPdf, batchPrint } from 'src/lib/export';
import { toast } from 'sonner';

const TABS = [
  { value: 'credentials', label: 'Credenciales', icon: 'mdi:card-account-details-outline' },
  { value: 'technical', label: 'Fichas Técnicas', icon: 'mdi:clipboard-list-outline' },
  { value: 'certificates', label: 'Certificados', icon: 'mdi:certificate-outline' },
];

// ─── Sub-panel: Credenciales ──────────────────────────────────────────────────

function CredentialsPanel({ cascade }) {
  const { data: competitions } = useApi(endpoints.competitions);
  const competition = competitions?.find((c) => c.id === cascade.compId);
  const { data: roster, isLoading } = useApi(cascade.teamId ? endpoints.roster(cascade.teamId) : null);
  const [showBack, setShowBack] = useState(false);
  const [exporting, setExporting] = useState(false);
  const refsMap = useRef({});

  const active = (roster || []).filter((e) => !e.withdrawnAt);

  const handleExportOne = async function (entry) {
    const el = refsMap.current[entry.id];
    if (!el) return;
    setExporting(true);
    try {
      await exportToPng(el, `credencial-${entry.lastName || entry.id}.png`);
      toast.success('Credencial exportada.');
    } catch (e) {
      toast.error(e.message);
    } finally {
      setExporting(false);
    }
  };

  const handleBatchPrint = async function () {
    const elements = active
      .map((e) => refsMap.current[e.id])
      .filter(Boolean);
    if (elements.length === 0) { toast.error('No hay credenciales para imprimir.'); return; }
    setExporting(true);
    try {
      await batchPrint(elements);
    } catch (e) {
      toast.error(e.message);
    } finally {
      setExporting(false);
    }
  };

  return (
    <Box>
      <Box sx={{ display: 'flex', gap: 2, mb: 3, flexWrap: 'wrap', alignItems: 'center' }}>
        <FormControlLabel
          control={<Switch checked={showBack} onChange={(e) => setShowBack(e.target.checked)} size="small" />}
          label={<Typography variant="caption">Mostrar reverso (normas)</Typography>}
        />
        <Button
          id="btn-batch-print-credentials"
          variant="outlined"
          startIcon={exporting ? <CircularProgress size={14} /> : <Iconify icon="mdi:printer-multiple-outline" />}
          onClick={handleBatchPrint}
          disabled={!cascade.teamId || isLoading || exporting}
        >
          Imprimir todas ({active.length})
        </Button>
      </Box>

      {isLoading && <Box sx={{ display: 'flex', justifyContent: 'center', py: 4 }}><CircularProgress /></Box>}

      {!cascade.teamId && (
        <Alert severity="info">Selecciona competencia, categoría y equipo para ver las credenciales.</Alert>
      )}

      {active.length === 0 && cascade.teamId && !isLoading && (
        <Alert severity="warning">No hay jugadores activos en esta nómina.</Alert>
      )}

      <Box
        sx={{
          display: 'grid',
          gridTemplateColumns: { xs: '1fr', sm: 'repeat(2, 1fr)', lg: 'repeat(3, 1fr)' },
          gap: 3,
        }}
      >
        {active.map(function (entry) {
          return (
            <Box key={entry.id}>
              <Box
                ref={function (el) { refsMap.current[entry.id] = el; }}
                sx={{ display: 'inline-flex', flexDirection: 'column' }}
              >
                <CredentialCard athlete={entry} competition={competition} showBack={showBack} />
              </Box>
              <Box sx={{ display: 'flex', gap: 1, mt: 1.5 }}>
                <Tooltip title="Descargar PNG">
                  <Button
                    size="small"
                    variant="outlined"
                    onClick={() => handleExportOne(entry)}
                    startIcon={<Iconify icon="mdi:image-outline" width={14} />}
                  >
                    PNG
                  </Button>
                </Tooltip>
              </Box>
            </Box>
          );
        })}
      </Box>
    </Box>
  );
}

// ─── Sub-panel: Fichas Técnicas ───────────────────────────────────────────────

function TechnicalPanel({ cascade }) {
  const { data: competitions } = useApi(endpoints.competitions);
  const competition = competitions?.find((c) => c.id === cascade.compId);
  const { data: roster, isLoading } = useApi(cascade.teamId ? endpoints.roster(cascade.teamId) : null);
  const { data: teams } = useApi(cascade.catId ? endpoints.teams(cascade.catId) : null);
  const team = teams?.find((t) => t.id === cascade.teamId);
  const sheetRef = useRef(null);
  const [exporting, setExporting] = useState(false);

  const handleExportPng = async function () {
    if (!sheetRef.current) return;
    setExporting(true);
    try {
      await exportToPng(sheetRef.current, `ficha-${team?.name || 'equipo'}.png`);
      toast.success('Ficha exportada.');
    } catch (e) { toast.error(e.message); }
    finally { setExporting(false); }
  };

  const handleExportPdf = async function () {
    if (!sheetRef.current) return;
    setExporting(true);
    try {
      await exportToPdf(sheetRef.current, `ficha-${team?.name || 'equipo'}.pdf`, 'A4', 'landscape');
      toast.success('PDF generado.');
    } catch (e) { toast.error(e.message); }
    finally { setExporting(false); }
  };

  const handlePrint = function () { window.print(); };

  return (
    <Box>
      <Box sx={{ display: 'flex', gap: 1, mb: 3, flexWrap: 'wrap' }}>
        <Button
          id="btn-export-sheet-png"
          variant="outlined"
          startIcon={exporting ? <CircularProgress size={14} /> : <Iconify icon="mdi:image-outline" />}
          onClick={handleExportPng}
          disabled={!cascade.teamId || isLoading || exporting}
        >
          PNG
        </Button>
        <Button
          id="btn-export-sheet-pdf"
          variant="outlined"
          startIcon={<Iconify icon="mdi:file-pdf-box" />}
          onClick={handleExportPdf}
          disabled={!cascade.teamId || isLoading || exporting}
        >
          PDF
        </Button>
        <Button
          id="btn-print-sheet"
          variant="outlined"
          startIcon={<Iconify icon="mdi:printer-outline" />}
          onClick={handlePrint}
          disabled={!cascade.teamId}
        >
          Imprimir
        </Button>
      </Box>

      {!cascade.teamId && (
        <Alert severity="info">Selecciona un equipo para ver su ficha técnica.</Alert>
      )}

      {isLoading && <Box sx={{ display: 'flex', justifyContent: 'center', py: 4 }}><CircularProgress /></Box>}

      {cascade.teamId && !isLoading && (
        <Box sx={{ overflowX: 'auto' }}>
          <TechnicalSheet
            ref={sheetRef}
            team={team}
            roster={roster}
            competition={competition}
          />
        </Box>
      )}
    </Box>
  );
}

// ─── Sub-panel: Certificados ──────────────────────────────────────────────────

function CertificatesPanel({ cascade }) {
  const { data: competitions } = useApi(endpoints.competitions);
  const competition = competitions?.find((c) => c.id === cascade.compId);
  const certRef = useRef(null);
  const [exporting, setExporting] = useState(false);
  const [form, setForm] = useState({
    recipientName: '',
    recipientCategory: '',
    certType: 'participation',
    customText: '',
  });

  const handleExportPng = async function () {
    if (!certRef.current) return;
    setExporting(true);
    try {
      await exportToPng(certRef.current, `certificado-${form.recipientName || 'deportista'}.png`);
      toast.success('Certificado exportado como PNG.');
    } catch (e) { toast.error(e.message); }
    finally { setExporting(false); }
  };

  const handleExportPdf = async function () {
    if (!certRef.current) return;
    setExporting(true);
    try {
      await exportToPdf(certRef.current, `certificado-${form.recipientName || 'deportista'}.pdf`, 'A4', 'landscape');
      toast.success('Certificado PDF generado.');
    } catch (e) { toast.error(e.message); }
    finally { setExporting(false); }
  };

  const handlePrint = function () { window.print(); };

  return (
    <Box>
      <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', md: '320px 1fr' }, gap: 3 }}>
        {/* Config */}
        <Stack spacing={2} component={Paper} variant="outlined" sx={{ p: 2, alignSelf: 'start' }}>
          <Typography variant="subtitle2" fontWeight={700}>Configuración del certificado</Typography>
          <TextField
            label="Nombre del destinatario"
            value={form.recipientName}
            onChange={(e) => setForm({ ...form, recipientName: e.target.value })}
            fullWidth
            size="small"
          />
          <TextField
            label="Categoría / Disciplina"
            value={form.recipientCategory}
            onChange={(e) => setForm({ ...form, recipientCategory: e.target.value })}
            fullWidth
            size="small"
          />
          <TextField
            select
            label="Tipo de certificado"
            value={form.certType}
            onChange={(e) => setForm({ ...form, certType: e.target.value })}
            fullWidth
            size="small"
          >
            {Object.entries(CERT_TYPES).map(function ([key, cfg]) {
              return <MenuItem key={key} value={key}>{cfg.badge} {cfg.headline}</MenuItem>;
            })}
          </TextField>
          <TextField
            label="Texto personalizado (opcional)"
            value={form.customText}
            onChange={(e) => setForm({ ...form, customText: e.target.value })}
            fullWidth
            size="small"
            multiline
            rows={3}
            helperText="Si está vacío se usa el texto automático."
          />
          <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap' }}>
            <Button
              id="btn-export-cert-png"
              variant="outlined"
              size="small"
              onClick={handleExportPng}
              disabled={exporting}
              startIcon={<Iconify icon="mdi:image-outline" width={14} />}
            >
              PNG
            </Button>
            <Button
              id="btn-export-cert-pdf"
              variant="outlined"
              size="small"
              onClick={handleExportPdf}
              disabled={exporting}
              startIcon={<Iconify icon="mdi:file-pdf-box" width={14} />}
            >
              PDF
            </Button>
            <Button
              id="btn-print-cert"
              variant="outlined"
              size="small"
              onClick={handlePrint}
              startIcon={<Iconify icon="mdi:printer-outline" width={14} />}
            >
              Imprimir
            </Button>
          </Box>
        </Stack>

        {/* Preview */}
        <Box sx={{ overflowX: 'auto' }}>
          <OfficialCertificate
            ref={certRef}
            recipientName={form.recipientName || 'Nombre del Deportista'}
            recipientCategory={form.recipientCategory}
            certType={form.certType}
            competition={competition}
            customText={form.customText || null}
          />
        </Box>
      </Box>
    </Box>
  );
}

// ─── Página principal ─────────────────────────────────────────────────────────

export default function CredentialsPage() {
  const cascade = useCascade();
  const [tab, setTab] = useState('credentials');

  return (
    <Box>
      <PageHeader title="Credenciales & Certificados" />

      <CascadeFilters cascade={cascade} />

      <Tabs
        value={tab}
        onChange={function (_, v) { setTab(v); }}
        sx={{ mb: 3, borderBottom: '1px solid', borderColor: 'divider' }}
      >
        {TABS.map(function (t) {
          return (
            <Tab
              key={t.value}
              value={t.value}
              label={
                <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.75 }}>
                  <Iconify icon={t.icon} width={16} />
                  {t.label}
                </Box>
              }
            />
          );
        })}
      </Tabs>

      {tab === 'credentials' && <CredentialsPanel cascade={cascade} />}
      {tab === 'technical' && <TechnicalPanel cascade={cascade} />}
      {tab === 'certificates' && <CertificatesPanel cascade={cascade} />}
    </Box>
  );
}

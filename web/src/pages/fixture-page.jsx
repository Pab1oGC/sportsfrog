/**
 * T-17 — Página principal de Fixture Oficial con identidad corporativa y exportación.
 * Integra: FixtureHeader (T-19 branding), GroupStage, BracketTree y motor de export.
 */

import { useRef, useState, useEffect } from 'react';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import ButtonGroup from '@mui/material/ButtonGroup';
import Tab from '@mui/material/Tab';
import Tabs from '@mui/material/Tabs';
import Chip from '@mui/material/Chip';
import CircularProgress from '@mui/material/CircularProgress';
import Alert from '@mui/material/Alert';
import Tooltip from '@mui/material/Tooltip';
import { Iconify } from 'src/components/iconify';
import { useApi } from 'src/hooks/use-api';
import { useCascade } from 'src/hooks/use-cascade';
import { endpoints } from 'src/lib/axios';
import { PageHeader } from 'src/components/page-header';
import { CascadeFilters } from 'src/components/cascade-filters';
import { FixtureHeader } from 'src/components/fixture/fixture-header';
import { GroupStage } from 'src/components/fixture/group-stage';
import { BracketTree } from 'src/components/fixture/bracket-tree';
import { useTournamentTheme } from 'src/context/tournament-theme-context';
import { exportToPng, exportToPdf } from 'src/lib/export';
import { toast } from 'sonner';

const VIEWS = [
  { value: 'groups', label: 'Fase de Grupos', icon: 'mdi:account-group-outline' },
  { value: 'bracket', label: 'Llaves', icon: 'mdi:tournament' },
  { value: 'all', label: 'Todo', icon: 'mdi:view-list-outline' },
];

export default function FixturePage() {
  const cascade = useCascade();
  const { applyTheme } = useTournamentTheme();
  const exportRef = useRef(null);
  const [view, setView] = useState('all');
  const [exporting, setExporting] = useState(false);

  const { data: competitions } = useApi(endpoints.competitions);
  const { data: matches, isLoading } = useApi(
    cascade.compId ? endpoints.competitionMatches(cascade.compId) : null
  );

  const competition = competitions?.find((c) => c.id === cascade.compId);

  // Aplica el tema de la competencia seleccionada al contexto global
  useEffect(
    function () {
      if (competition) applyTheme(competition);
    },
    [competition, applyTheme]
  );

  // Separa grupos y eliminatoria
  const filtered = (matches || []).filter(
    (m) => !cascade.catId || m.categoryId === cascade.catId
  );
  const groupMatches = filtered.filter((m) => !m.phase);
  const bracketMatches = filtered.filter((m) => !!m.phase);
  const formato = competition?.format;

  // Determina qué vistas mostrar según el formato
  const availableViews = VIEWS.filter(function (v) {
    if (v.value === 'groups' && formato === 'knockout') return false;
    if (v.value === 'bracket' && formato === 'league') return false;
    return true;
  });

  const handleExportPng = async function () {
    if (!exportRef.current) return;
    setExporting(true);
    try {
      const name = competition?.name || 'fixture';
      await exportToPng(exportRef.current, `${name}-fixture.png`);
      toast.success('Imagen PNG descargada.');
    } catch (e) {
      toast.error('Error al exportar PNG: ' + e.message);
    } finally {
      setExporting(false);
    }
  };

  const handleExportPdf = async function () {
    if (!exportRef.current) return;
    setExporting(true);
    try {
      const name = competition?.name || 'fixture';
      await exportToPdf(exportRef.current, `${name}-fixture.pdf`, 'A4', 'landscape');
      toast.success('PDF descargado.');
    } catch (e) {
      toast.error('Error al exportar PDF: ' + e.message);
    } finally {
      setExporting(false);
    }
  };

  const handlePrint = function () {
    window.print();
  };

  return (
    <Box>
      <PageHeader title="Fixture Oficial">
        <ButtonGroup variant="outlined" size="small" disabled={!cascade.compId || exporting}>
          <Tooltip title="Exportar como imagen PNG">
            <Button
              id="btn-export-png"
              onClick={handleExportPng}
              startIcon={exporting ? <CircularProgress size={14} /> : <Iconify icon="mdi:image-outline" />}
            >
              PNG
            </Button>
          </Tooltip>
          <Tooltip title="Exportar como PDF A4 apaisado">
            <Button
              id="btn-export-pdf"
              onClick={handleExportPdf}
              startIcon={<Iconify icon="mdi:file-pdf-box" />}
            >
              PDF
            </Button>
          </Tooltip>
          <Tooltip title="Imprimir">
            <Button
              id="btn-print-fixture"
              onClick={handlePrint}
              startIcon={<Iconify icon="mdi:printer-outline" />}
            >
              Imprimir
            </Button>
          </Tooltip>
        </ButtonGroup>
      </PageHeader>

      <CascadeFilters cascade={cascade} />

      {!cascade.compId && (
        <Alert severity="info" sx={{ mb: 3 }}>
          Selecciona una competencia para ver el fixture.
        </Alert>
      )}

      {isLoading && (
        <Box sx={{ display: 'flex', justifyContent: 'center', py: 6 }}>
          <CircularProgress />
        </Box>
      )}

      {cascade.compId && !isLoading && (
        <>
          {/* Vista de pestaña */}
          <Tabs
            value={view}
            onChange={function (_, v) { setView(v); }}
            sx={{ mb: 2 }}
            className="no-print"
          >
            {availableViews.map(function (v) {
              return (
                <Tab
                  key={v.value}
                  value={v.value}
                  label={
                    <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.75 }}>
                      <Iconify icon={v.icon} width={16} />
                      {v.label}
                    </Box>
                  }
                />
              );
            })}
          </Tabs>

          {/* Contenido exportable */}
          <Box ref={exportRef} sx={{ bgcolor: 'background.default', p: { xs: 0, md: 1 } }}>
            <FixtureHeader competition={competition} />

            {(view === 'groups' || view === 'all') && groupMatches.length > 0 && (
              <Box sx={{ mb: 4 }}>
                <Typography variant="h6" fontWeight={700} sx={{ mb: 2 }}>
                  Fase de Grupos
                  <Chip label={groupMatches.length} size="small" sx={{ ml: 1 }} />
                </Typography>
                <GroupStage matches={groupMatches} />
              </Box>
            )}

            {(view === 'bracket' || view === 'all') && (
              <Box>
                <Typography variant="h6" fontWeight={700} sx={{ mb: 2 }}>
                  Cuadro Eliminatorio
                  <Chip label={bracketMatches.length} size="small" sx={{ ml: 1 }} />
                </Typography>
                <BracketTree matches={bracketMatches} />
              </Box>
            )}

            {filtered.length === 0 && (
              <Alert severity="info">No hay partidos para mostrar con los filtros actuales.</Alert>
            )}
          </Box>
        </>
      )}
    </Box>
  );
}

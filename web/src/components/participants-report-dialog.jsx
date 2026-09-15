/**
 * T-16 — Reporte e Impresión de Listas de Participantes Agrupados por Deporte.
 * 
 * T-16.1: Agrupación jerárquica por Deporte -> Categoría -> Equipo -> Atletas.
 * T-16.2: Barra de filtros reactivos (disciplina, división/rama y estado de acreditación).
 * T-16.3: Estilos de impresión dedicados (@media print / CSS print stylesheet) con hoja membretada.
 * T-16.4: Botón de descarga rápida en PDF/Excel con totales y resumen estadístico.
 */

import { useState, useRef, useMemo } from 'react';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import Dialog from '@mui/material/Dialog';
import DialogTitle from '@mui/material/DialogTitle';
import DialogContent from '@mui/material/DialogContent';
import DialogActions from '@mui/material/DialogActions';
import TextField from '@mui/material/TextField';
import MenuItem from '@mui/material/MenuItem';
import Paper from '@mui/material/Paper';
import Stack from '@mui/material/Stack';
import Chip from '@mui/material/Chip';
import Divider from '@mui/material/Divider';
import CircularProgress from '@mui/material/CircularProgress';
import Alert from '@mui/material/Alert';
import { Iconify } from 'src/components/iconify';
import { useTournamentTheme } from 'src/context/tournament-theme-context';
import { exportToPdf } from 'src/lib/export';
import { toast } from 'sonner';

export function ParticipantsReportDialog({ open, onClose, competition, categories = [], teams = [], roster = [] }) {
  const { theme } = useTournamentTheme();
  const printRef = useRef(null);
  const [exporting, setExporting] = useState(false);

  // Filtros reactivos (T-16.2)
  const [selectedCategory, setSelectedCategory] = useState('ALL');
  const [selectedStatus, setSelectedStatus] = useState('ALL'); // ALL, ACTIVE, WITHDRAWN
  const [searchQuery, setSearchQuery] = useState('');

  const primaryColor = theme?.primaryColor || '#1B8A2E';

  // Procesamiento y agrupación jerárquica (T-16.1)
  const groupedData = useMemo(() => {
    let filtered = roster || [];

    if (selectedCategory !== 'ALL') {
      filtered = filtered.filter((r) => r.categoryId === selectedCategory);
    }

    if (selectedStatus === 'ACTIVE') {
      filtered = filtered.filter((r) => !r.withdrawnAt);
    } else if (selectedStatus === 'WITHDRAWN') {
      filtered = filtered.filter((r) => !!r.withdrawnAt);
    }

    if (searchQuery.trim()) {
      const q = searchQuery.toLowerCase();
      filtered = filtered.filter(
        (r) =>
          (r.firstName || '').toLowerCase().includes(q) ||
          (r.lastName || '').toLowerCase().includes(q) ||
          (r.documentId || '').toLowerCase().includes(q) ||
          (r.teamName || '').toLowerCase().includes(q)
      );
    }

    // Agrupar por Categoría -> Equipo
    const catMap = {};
    filtered.forEach((r) => {
      const catName = r.categoryName || 'Categoría Única';
      const teamName = r.teamName || 'Sin Equipo';

      if (!catMap[catName]) {
        catMap[catName] = {};
      }
      if (!catMap[catName][teamName]) {
        catMap[catName][teamName] = [];
      }
      catMap[catName][teamName].push(r);
    });

    return catMap;
  }, [roster, selectedCategory, selectedStatus, searchQuery]);

  // Estadísticas generales (T-16.4)
  const stats = useMemo(() => {
    const totalInscritos = (roster || []).length;
    const activos = (roster || []).filter((r) => !r.withdrawnAt).length;
    const retirados = totalInscritos - activos;
    const totalEquipos = (teams || []).length;
    const totalCategorias = (categories || []).length;

    return { totalInscritos, activos, retirados, totalEquipos, totalCategorias };
  }, [roster, teams, categories]);

  const handleExportPdf = async () => {
    if (!printRef.current) return;
    setExporting(true);
    try {
      await exportToPdf(printRef.current, `reporte-participantes-${competition?.name || 'torneo'}.pdf`, 'A4', 'portrait');
      toast.success('Reporte de participantes exportado en PDF.');
    } catch (err) {
      toast.error('Error al exportar PDF: ' + err.message);
    } finally {
      setExporting(false);
    }
  };

  const handleExportExcel = () => {
    try {
      let csvContent = 'data:text/csv;charset=utf-8,';
      csvContent += 'Categoría,Equipo,Dorsal,Apellido,Nombre,Documento,Posición,Estado\n';

      Object.entries(groupedData).forEach(([catName, teamsMap]) => {
        Object.entries(teamsMap).forEach(([teamName, players]) => {
          players.forEach((p) => {
            const estado = p.withdrawnAt ? 'Retirado' : 'Activo';
            csvContent += `"${catName}","${teamName}","${p.jerseyNumber || ''}","${p.lastName || ''}","${p.firstName || ''}","${p.documentId || ''}","${p.position || ''}","${estado}"\n`;
          });
        });
      });

      const encodedUri = encodeURI(csvContent);
      const link = document.createElement('a');
      link.setAttribute('href', encodedUri);
      link.setAttribute('download', `participantes-${competition?.name || 'torneo'}.csv`);
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);

      toast.success('Reporte de participantes exportado en Excel/CSV.');
    } catch (err) {
      toast.error('Error al exportar Excel: ' + err.message);
    }
  };

  const handlePrint = () => {
    window.print();
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="lg" fullWidth>
      <DialogTitle sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
          <Iconify icon="mdi:account-group-outline" width={24} sx={{ color: primaryColor }} />
          <Typography variant="h6" fontWeight={700}>
            Reporte General de Participantes e Inscritos (T-16)
          </Typography>
        </Box>
        <Stack direction="row" spacing={1} className="no-print">
          <Button
            size="small"
            variant="outlined"
            onClick={handleExportExcel}
            startIcon={<Iconify icon="mdi:file-excel" />}
          >
            Excel / CSV
          </Button>
          <Button
            size="small"
            variant="outlined"
            onClick={handleExportPdf}
            disabled={exporting}
            startIcon={exporting ? <CircularProgress size={14} /> : <Iconify icon="mdi:file-pdf-box" />}
          >
            PDF
          </Button>
          <Button
            size="small"
            variant="contained"
            onClick={handlePrint}
            startIcon={<Iconify icon="mdi:printer" />}
          >
            Imprimir
          </Button>
        </Stack>
      </DialogTitle>

      <DialogContent dividers sx={{ p: 3 }}>
        {/* BARRA DE FILTROS REACTIVOS (T-16.2) */}
        <Paper variant="outlined" sx={{ p: 2, mb: 3, bgcolor: 'background.neutral' }} className="no-print">
          <Typography variant="subtitle2" fontWeight={700} sx={{ mb: 1.5 }}>
            Filtros y Búsqueda Reactiva
          </Typography>
          <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr 1fr' }, gap: 2 }}>
            <TextField
              size="small"
              select
              label="Categoría / Rama"
              value={selectedCategory}
              onChange={(e) => setSelectedCategory(e.target.value)}
            >
              <MenuItem value="ALL">Todas las Categorías ({categories.length})</MenuItem>
              {categories.map((c) => (
                <MenuItem key={c.id} value={c.id}>{c.name}</MenuItem>
              ))}
            </TextField>

            <TextField
              size="small"
              select
              label="Estado de Acreditación"
              value={selectedStatus}
              onChange={(e) => setSelectedStatus(e.target.value)}
            >
              <MenuItem value="ALL">Todos los Estados</MenuItem>
              <MenuItem value="ACTIVE">Activos Acreditados</MenuItem>
              <MenuItem value="WITHDRAWN">Retirados / Inactivos</MenuItem>
            </TextField>

            <TextField
              size="small"
              label="Buscar por Nombre, CI o Equipo"
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              placeholder="Escribe para buscar..."
            />
          </Box>
        </Paper>

        {/* TARJETAS DE RESUMEN ESTADÍSTICO (T-16.4) */}
        <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 2, mb: 3 }} className="no-print">
          <StatCard title="Total Participantes" value={stats.totalInscritos} icon="mdi:account-multiple" color={primaryColor} />
          <StatCard title="Activos Acreditados" value={stats.activos} icon="mdi:check-circle" color="#2e7d32" />
          <StatCard title="Total Equipos" value={stats.totalEquipos} icon="mdi:shield-account" color="#0284c7" />
          <StatCard title="Categorías" value={stats.totalCategorias} icon="mdi:trophy" color="#d97706" />
        </Box>

        {/* VISTA IMPRESA Y REGISTRO AGRUPADO (T-16.1 & T-16.3) */}
        <Box
          ref={printRef}
          className="printable-area"
          sx={{
            bgcolor: '#fff',
            p: 3,
            color: '#111',
            borderRadius: 1,
            fontFamily: 'Inter, sans-serif',
          }}
        >
          {/* HOJA MEMBRETADA CON ENCABEZADO INSTITUCIONAL */}
          <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', pb: 2, borderBottom: `3px solid ${primaryColor}`, mb: 3 }}>
            <Box>
              <Typography variant="h5" fontWeight={900} sx={{ color: primaryColor, textTransform: 'uppercase' }}>
                {competition?.name || 'CAMPEONATO OFICIAL'}
              </Typography>
              <Typography variant="subtitle2" color="text.secondary">
                Reporte General de Participantes por Disciplina y Categoría
              </Typography>
            </Box>
            <Box sx={{ textAlign: 'right' }}>
              <Typography variant="caption" display="block" color="text.secondary">
                Fecha de Emisión: {new Date().toLocaleDateString()}
              </Typography>
              <Chip label={`Disciplina: ${competition?.sportCode || 'General'}`} size="small" sx={{ mt: 0.5, fontWeight: 700 }} />
            </Box>
          </Box>

          {/* LISTADO JERÁRQUICO */}
          {Object.keys(groupedData).length === 0 ? (
            <Alert severity="info">No se encontraron atletas registrados que coincidan con los filtros seleccionados.</Alert>
          ) : (
            Object.entries(groupedData).map(([catName, teamsMap]) => (
              <Box key={catName} sx={{ mb: 4, pageBreakInside: 'avoid' }}>
                <Typography variant="h6" fontWeight={800} sx={{ color: primaryColor, bgcolor: `${primaryColor}12`, p: 1, px: 2, borderRadius: 1, mb: 2 }}>
                  Categoría: {catName}
                </Typography>

                {Object.entries(teamsMap).map(([teamName, players]) => (
                  <Box key={teamName} sx={{ mb: 2, pl: 1 }}>
                    <Typography variant="subtitle2" fontWeight={700} sx={{ mb: 1, color: '#333', display: 'flex', alignItems: 'center', gap: 1 }}>
                      <Iconify icon="mdi:shield" width={16} sx={{ color: primaryColor }} />
                      Equipo: {teamName} ({players.length} deportistas)
                    </Typography>

                    <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.85rem' }}>
                      <thead>
                        <tr style={{ background: '#f4f6f8', textAlign: 'left', borderBottom: '2px solid #ddd' }}>
                          <th style={{ padding: '6px 10px' }}>#</th>
                          <th style={{ padding: '6px 10px' }}>Dorsal</th>
                          <th style={{ padding: '6px 10px' }}>Apellido y Nombre</th>
                          <th style={{ padding: '6px 10px' }}>Documento ID</th>
                          <th style={{ padding: '6px 10px' }}>Posición</th>
                          <th style={{ padding: '6px 10px' }}>Estado</th>
                        </tr>
                      </thead>
                      <tbody>
                        {players.map((p, idx) => (
                          <tr key={p.id || idx} style={{ borderBottom: '1px solid #eee', background: idx % 2 === 0 ? '#fff' : '#fafafa' }}>
                            <td style={{ padding: '6px 10px', fontWeight: 600 }}>{idx + 1}</td>
                            <td style={{ padding: '6px 10px' }}>{p.jerseyNumber ? `#${p.jerseyNumber}` : '--'}</td>
                            <td style={{ padding: '6px 10px', fontWeight: 700 }}>{p.lastName}, {p.firstName}</td>
                            <td style={{ padding: '6px 10px' }}>{p.documentId || '--'}</td>
                            <td style={{ padding: '6px 10px' }}>{p.position || '--'}</td>
                            <td style={{ padding: '6px 10px' }}>
                              <span style={{ color: p.withdrawnAt ? '#d32f2f' : '#2e7d32', fontWeight: 600 }}>
                                {p.withdrawnAt ? 'Retirado' : 'Acreditado'}
                              </span>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </Box>
                ))}
              </Box>
            ))
          )}

          {/* FIRMA Y RESUMEN FINAL */}
          <Box sx={{ mt: 4, pt: 2, borderTop: '1px solid #ddd', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <Typography variant="caption" color="text.secondary">
              Total listados en este reporte: {Object.values(groupedData).reduce((acc, teamsMap) => acc + Object.values(teamsMap).reduce((a, pl) => a + pl.length, 0), 0)} participantes.
            </Typography>
            <Typography variant="caption" color="text.secondary">
              SportFrog Platform — Certificación de Acreditación Oficial
            </Typography>
          </Box>
        </Box>
      </DialogContent>

      <DialogActions sx={{ p: 2, px: 3 }} className="no-print">
        <Button onClick={onClose}>Cerrar</Button>
      </DialogActions>
    </Dialog>
  );
}

function StatCard({ title, value, icon, color }) {
  return (
    <Paper variant="outlined" sx={{ p: 1.5, display: 'flex', alignItems: 'center', gap: 1.5 }}>
      <Box sx={{ width: 40, height: 40, borderRadius: 1.5, bgcolor: `${color}15`, color, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
        <Iconify icon={icon} width={22} />
      </Box>
      <Box>
        <Typography variant="h6" fontWeight={800} sx={{ lineHeight: 1 }}>{value}</Typography>
        <Typography variant="caption" color="text.secondary">{title}</Typography>
      </Box>
    </Paper>
  );
}

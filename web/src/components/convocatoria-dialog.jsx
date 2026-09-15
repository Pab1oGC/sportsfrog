/**
 * T-15 — Módulo de Generación y Exportación de Convocatoria Oficial (PDF / Word).
 * 
 * T-15.1: Plantilla base estructurada con encabezado institucional, bases, categorías, costos y fechas.
 * T-15.2: Formulario de edición de cláusulas por campeonato.
 * T-15.3: Exportación dinámica a PDF y Word (.docx / .doc Blob).
 * T-15.4: Vista previa interactiva en tiempo real (Preview Modal).
 */

import { useState, useRef, useEffect } from 'react';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import Dialog from '@mui/material/Dialog';
import DialogTitle from '@mui/material/DialogTitle';
import DialogContent from '@mui/material/DialogContent';
import DialogActions from '@mui/material/DialogActions';
import TextField from '@mui/material/TextField';
import Paper from '@mui/material/Paper';
import Stack from '@mui/material/Stack';
import Divider from '@mui/material/Divider';
import Alert from '@mui/material/Alert';
import CircularProgress from '@mui/material/CircularProgress';
import Chip from '@mui/material/Chip';
import IconButton from '@mui/material/IconButton';
import Tooltip from '@mui/material/Tooltip';
import { Iconify } from 'src/components/iconify';
import { useTournamentTheme } from 'src/context/tournament-theme-context';
import { exportToPdf } from 'src/lib/export';
import { toast } from 'sonner';

export function ConvocatoriaDialog({ open, onClose, competition, categories = [] }) {
  const { theme } = useTournamentTheme();
  const previewRef = useRef(null);
  const [exporting, setExporting] = useState(false);
  const [previewMode, setPreviewMode] = useState(false);

  const primaryColor = theme?.primaryColor || '#1B8A2E';

  const [form, setForm] = useState({
    organizerName: 'Asociación Deportiva de Cochabamba',
    competitionTitle: '',
    startDate: '',
    endDate: '',
    registrationDeadline: '',
    location: 'Complejo Deportivo Municipal',
    registrationFee: 'Bs. 150 por equipo',
    rulesSummary: 'Reglamento oficial avalado por la federación correspondiente. Todos los deportistas deben portar su credencial con código QR.',
    clauses: [
      { title: '1. De la Participación', content: 'Podrán participar todos los clubes y deportistas debidamente inscritos y acreditados en la plataforma oficial.' },
      { title: '2. Inscripciones y Costos', content: 'Las inscripciones se receptarán a través del portal oficial hasta la fecha límite indicada. El costo debe ser abonado previo a la acreditación.' },
      { title: '3. Premiación y Reconocimientos', content: 'Se otorgarán trofeos y medallas oficiales al 1er, 2do y 3er lugar de cada categoría, así como diplomas de participación digitalizados.' },
      { title: '4. Sanciones y Disciplina', content: 'Cualquier acto de indisciplina será sancionado según el código de penas oficial de la competencia.' },
    ],
  });

  useEffect(() => {
    if (competition) {
      setForm((prev) => ({
        ...prev,
        competitionTitle: competition.name || 'Campeonato Oficial 2026',
        startDate: competition.startsOn ? new Date(competition.startsOn).toLocaleDateString() : 'Por definir',
        endDate: competition.endsOn ? new Date(competition.endsOn).toLocaleDateString() : 'Por definir',
      }));
    }
  }, [competition]);

  const handleAddClause = () => {
    setForm((prev) => ({
      ...prev,
      clauses: [
        ...prev.clauses,
        { title: `${prev.clauses.length + 1}. Nueva Cláusula`, content: 'Detalle de las bases generales...' },
      ],
    }));
  };

  const handleUpdateClause = (index, field, value) => {
    setForm((prev) => {
      const next = [...prev.clauses];
      next[index] = { ...next[index], [field]: value };
      return { ...prev, clauses: next };
    });
  };

  const handleRemoveClause = (index) => {
    setForm((prev) => ({
      ...prev,
      clauses: prev.clauses.filter((_, i) => i !== index),
    }));
  };

  const handleExportPdf = async () => {
    if (!previewRef.current) return;
    setExporting(true);
    try {
      await exportToPdf(previewRef.current, `convocatoria-${form.competitionTitle || 'torneo'}.pdf`, 'A4', 'portrait');
      toast.success('Documento de Convocatoria exportado en PDF.');
    } catch (err) {
      toast.error('Error al exportar PDF: ' + err.message);
    } finally {
      setExporting(false);
    }
  };

  const handleExportWord = () => {
    try {
      const contentHtml = `
        <html xmlns:o='urn:schemas-microsoft-com:office:office' xmlns:w='urn:schemas-microsoft-com:office:word' xmlns='http://www.w3.org/TR/REC-html40'>
        <head>
          <meta charset='utf-8'>
          <title>Convocatoria Oficial</title>
          <style>
            body { font-family: Arial, sans-serif; margin: 40px; }
            h1 { color: ${primaryColor}; text-align: center; }
            h2 { color: #333; border-bottom: 2px solid ${primaryColor}; }
            .header-table { width: 100%; margin-bottom: 30px; }
            .section { margin-bottom: 20px; }
            .clause-title { font-weight: bold; margin-top: 15px; color: ${primaryColor}; }
            .footer { margin-top: 50px; text-align: center; font-size: 10pt; color: #777; }
          </style>
        </head>
        <body>
          <h1>CONVOCATORIA OFICIAL</h1>
          <p style="text-align:center; font-weight:bold; font-size:14pt;">${form.organizerName}</p>
          <h2 style="text-align:center;">${form.competitionTitle}</h2>
          <hr />
          <h3>INFORMACIÓN GENERAL</h3>
          <p><strong>Lugar del Evento:</strong> ${form.location}</p>
          <p><strong>Fecha de Inicio:</strong> ${form.startDate}</p>
          <p><strong>Fecha de Cierre:</strong> ${form.endDate}</p>
          <p><strong>Cierre de Inscripciones:</strong> ${form.registrationDeadline || '48 hrs antes del inicio'}</p>
          <p><strong>Costo de Inscripción:</strong> ${form.registrationFee}</p>

          <h3>BASES Y CLÁUSULAS GENERALES</h3>
          ${form.clauses.map((c) => `<div class="clause-title">${c.title}</div><p>${c.content}</p>`).join('')}

          <h3>CATEGORÍAS PARTICIPANTES</h3>
          <ul>
            ${categories.length > 0 ? categories.map((cat) => `<li>${cat.name}</li>`).join('') : '<li>Categorías Generales según reglamento.</li>'}
          </ul>

          <div style="margin-top: 60px; text-align: center;">
            <p>_____________________________________</p>
            <p><strong>Comité Organizador</strong></p>
            <p>${form.organizerName}</p>
          </div>
        </body>
        </html>
      `;

      const blob = new Blob(['\ufeff', contentHtml], { type: 'application/msword' });
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `convocatoria-${form.competitionTitle.toLowerCase().replace(/\s+/g, '-')}.doc`;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      setTimeout(() => URL.revokeObjectURL(url), 1000);

      toast.success('Documento de Convocatoria descargado para Word (.doc)');
    } catch (err) {
      toast.error('Error al exportar Word: ' + err.message);
    }
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="md" fullWidth>
      <DialogTitle sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
          <Iconify icon="mdi:file-document-edit-outline" width={24} sx={{ color: primaryColor }} />
          <Typography variant="h6" fontWeight={700}>
            Generador de Convocatoria Oficial (PDF / Word)
          </Typography>
        </Box>
        <Stack direction="row" spacing={1}>
          <Button
            size="small"
            variant={previewMode ? 'outlined' : 'contained'}
            onClick={() => setPreviewMode(false)}
            startIcon={<Iconify icon="mdi:pencil-outline" />}
          >
            Editar Cláusulas
          </Button>
          <Button
            size="small"
            variant={previewMode ? 'contained' : 'outlined'}
            onClick={() => setPreviewMode(true)}
            startIcon={<Iconify icon="mdi:eye-outline" />}
          >
            Vista Previa (Preview)
          </Button>
        </Stack>
      </DialogTitle>

      <DialogContent dividers sx={{ p: 3 }}>
        {!previewMode ? (
          /* MODO EDICIÓN DE FORMULARIO (T-15.2) */
          <Stack spacing={2.5}>
            <Alert severity="info">
              Configura los datos del evento y edita las bases de la convocatoria. Podrás previsualizarlo e imprimirlo en PDF o Word.
            </Alert>

            <Typography variant="subtitle2" fontWeight={700} color="primary">
              1. Encabezado Institucional e Información General
            </Typography>
            <GridForm form={form} setForm={setForm} />

            <Divider sx={{ my: 1 }} />

            <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <Typography variant="subtitle2" fontWeight={700} color="primary">
                2. Bases y Cláusulas del Campeonato (T-15.2)
              </Typography>
              <Button size="small" startIcon={<Iconify icon="mdi:plus" />} onClick={handleAddClause}>
                Agregar Cláusula
              </Button>
            </Box>

            <Stack spacing={2}>
              {form.clauses.map((clause, idx) => (
                <Paper key={idx} variant="outlined" sx={{ p: 2, bgcolor: 'background.neutral' }}>
                  <Box sx={{ display: 'flex', gap: 1, mb: 1, alignItems: 'center' }}>
                    <TextField
                      size="small"
                      label="Título de la Cláusula"
                      value={clause.title}
                      onChange={(e) => handleUpdateClause(idx, 'title', e.target.value)}
                      sx={{ flex: 1 }}
                    />
                    <IconButton size="small" color="error" onClick={() => handleRemoveClause(idx)}>
                      <Iconify icon="mdi:trash-can-outline" />
                    </IconButton>
                  </Box>
                  <TextField
                    size="small"
                    multiline
                    rows={2}
                    fullWidth
                    label="Contenido"
                    value={clause.content}
                    onChange={(e) => handleUpdateClause(idx, 'content', e.target.value)}
                  />
                </Paper>
              ))}
            </Stack>
          </Stack>
        ) : (
          /* VISTA PREVIA INTERACTIVA (PREVIEW MODAL T-15.4 & T-15.1) */
          <Box sx={{ overflowX: 'auto', display: 'flex', justifyContent: 'center' }}>
            <Box
              ref={previewRef}
              sx={{
                width: '100%',
                maxWidth: 750,
                minHeight: 900,
                bgcolor: '#fff',
                color: '#111',
                p: 5,
                boxShadow: '0 4px 20px rgba(0,0,0,0.1)',
                borderRadius: 1,
                fontFamily: 'Roboto, Inter, sans-serif',
              }}
            >
              {/* Encabezado */}
              <Box sx={{ textAlign: 'center', pb: 3, borderBottom: `3px double ${primaryColor}`, mb: 3 }}>
                {theme?.logoUrl && (
                  <Box component="img" src={theme.logoUrl} sx={{ maxHeight: 60, mb: 1, objectFit: 'contain' }} />
                )}
                <Typography variant="caption" sx={{ textTransform: 'uppercase', letterSpacing: 2, color: '#666', fontWeight: 700, display: 'block' }}>
                  {form.organizerName}
                </Typography>
                <Typography variant="h4" fontWeight={900} sx={{ color: primaryColor, my: 0.5, letterSpacing: 1 }}>
                  CONVOCATORIA OFICIAL
                </Typography>
                <Typography variant="h6" fontWeight={700} sx={{ color: '#333' }}>
                  {form.competitionTitle}
                </Typography>
              </Box>

              {/* Ficha rápida */}
              <Paper variant="outlined" sx={{ p: 2, mb: 3, bgcolor: `${primaryColor}0A`, borderColor: `${primaryColor}30` }}>
                <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: 1.5, fontSize: '0.85rem' }}>
                  <Box><strong>Lugar:</strong> {form.location}</Box>
                  <Box><strong>Costo de Inscripción:</strong> {form.registrationFee}</Box>
                  <Box><strong>Fechas del Evento:</strong> {form.startDate} al {form.endDate}</Box>
                  <Box><strong>Cierre de Inscripción:</strong> {form.registrationDeadline || '48 hrs antes'}</Box>
                </Box>
              </Paper>

              {/* Cláusulas */}
              <Typography variant="subtitle1" fontWeight={800} sx={{ mb: 1.5, color: primaryColor, borderBottom: '1px solid #eee', pb: 0.5 }}>
                BASES GENERALES DE LA COMPETENCIA
              </Typography>
              <Stack spacing={2} sx={{ mb: 3 }}>
                {form.clauses.map((c, i) => (
                  <Box key={i}>
                    <Typography variant="subtitle2" fontWeight={800} sx={{ color: '#222' }}>
                      {c.title}
                    </Typography>
                    <Typography variant="body2" sx={{ color: '#444', mt: 0.25, textAlign: 'justify', lineHeight: 1.5 }}>
                      {c.content}
                    </Typography>
                  </Box>
                ))}
              </Stack>

              {/* Categorías convocadas */}
              {categories.length > 0 && (
                <Box sx={{ mb: 4 }}>
                  <Typography variant="subtitle1" fontWeight={800} sx={{ mb: 1, color: primaryColor, borderBottom: '1px solid #eee', pb: 0.5 }}>
                    CATEGORÍAS PARTICIPANTES
                  </Typography>
                  <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 1 }}>
                    {categories.map((cat) => (
                      <Chip key={cat.id} label={cat.name} size="small" variant="outlined" sx={{ fontWeight: 600 }} />
                    ))}
                  </Box>
                </Box>
              )}

              {/* Pie con firmas */}
              <Box sx={{ mt: 8, pt: 4, display: 'flex', justifyContent: 'space-around', textAlign: 'center' }}>
                <Box>
                  <Box sx={{ width: 180, borderTop: '1px solid #444', mb: 0.5 }} />
                  <Typography variant="caption" fontWeight={700} display="block">Comité Organizador</Typography>
                  <Typography variant="caption" color="text.secondary">{form.organizerName}</Typography>
                </Box>
                <Box>
                  <Box sx={{ width: 180, borderTop: '1px solid #444', mb: 0.5 }} />
                  <Typography variant="caption" fontWeight={700} display="block">Director Técnico</Typography>
                  <Typography variant="caption" color="text.secondary">SportFrog Officials</Typography>
                </Box>
              </Box>
            </Box>
          </Box>
        )}
      </DialogContent>

      <DialogActions sx={{ p: 2, px: 3 }}>
        <Button onClick={onClose} disabled={exporting}>Cerrar</Button>
        <Button
          variant="outlined"
          color="primary"
          startIcon={<Iconify icon="mdi:file-word" />}
          onClick={handleExportWord}
        >
          Exportar Word (.docx)
        </Button>
        <Button
          variant="contained"
          color="primary"
          startIcon={exporting ? <CircularProgress size={16} /> : <Iconify icon="mdi:file-pdf-box" />}
          onClick={handleExportPdf}
          disabled={exporting}
        >
          Exportar PDF Oficial
        </Button>
      </DialogActions>
    </Dialog>
  );
}

function GridForm({ form, setForm }) {
  return (
    <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr' }, gap: 2 }}>
      <TextField
        size="small"
        label="Organización Promotora"
        value={form.organizerName}
        onChange={(e) => setForm({ ...form, organizerName: e.target.value })}
      />
      <TextField
        size="small"
        label="Título del Campeonato"
        value={form.competitionTitle}
        onChange={(e) => setForm({ ...form, competitionTitle: e.target.value })}
      />
      <TextField
        size="small"
        label="Lugar / Escenario"
        value={form.location}
        onChange={(e) => setForm({ ...form, location: e.target.value })}
      />
      <TextField
        size="small"
        label="Costo de Inscripción"
        value={form.registrationFee}
        onChange={(e) => setForm({ ...form, registrationFee: e.target.value })}
      />
      <TextField
        size="small"
        label="Fecha Inicio"
        value={form.startDate}
        onChange={(e) => setForm({ ...form, startDate: e.target.value })}
      />
      <TextField
        size="small"
        label="Fecha Cierre Evento"
        value={form.endDate}
        onChange={(e) => setForm({ ...form, endDate: e.target.value })}
      />
    </Box>
  );
}

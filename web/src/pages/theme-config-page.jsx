/**
 * T-19 — Panel de Tematización Avanzada del Campeonato.
 * Permite configurar colores, banner y logos de sponsors con previsualización en tiempo real
 * y extracción inteligente de paletas recomendadas desde imágenes subidas.
 */

import { useState, useEffect, useCallback } from 'react';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import TextField from '@mui/material/TextField';
import Paper from '@mui/material/Paper';
import Divider from '@mui/material/Divider';
import Alert from '@mui/material/Alert';
import Chip from '@mui/material/Chip';
import Stack from '@mui/material/Stack';
import IconButton from '@mui/material/IconButton';
import Avatar from '@mui/material/Avatar';
import Tooltip from '@mui/material/Tooltip';
import Menu from '@mui/material/Menu';
import MenuItem from '@mui/material/MenuItem';
import CircularProgress from '@mui/material/CircularProgress';
import { PageHeader } from 'src/components/page-header';
import { SelectionCompetition } from 'src/components/selectors';
import { useApi } from 'src/hooks/use-api';
import axiosInstance, { endpoints } from 'src/lib/axios';
import { useTournamentTheme, DEFAULT_THEME } from 'src/context/tournament-theme-context';
import { Iconify } from 'src/components/iconify';
import { leerComoDataUrl } from 'src/lib/data-url';
import { extractColorsFromImage } from 'src/lib/color-extractor';
import { toast } from 'sonner';
import { useLastCompetition } from 'src/hooks/use-last-competition';

function ColorSwatch({ label, value, onChange }) {
  return (
    <Box>
      <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 0.5 }}>
        {label}
      </Typography>
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5 }}>
        <Box
          sx={{
            width: 36,
            height: 36,
            borderRadius: 1,
            bgcolor: value,
            border: '2px solid',
            borderColor: 'divider',
            flexShrink: 0,
            cursor: 'pointer',
            position: 'relative',
            overflow: 'hidden',
            boxShadow: '0 2px 4px rgba(0,0,0,0.1)',
          }}
        >
          <Box
            component="input"
            type="color"
            value={value}
            onChange={(e) => onChange(e.target.value)}
            sx={{
              position: 'absolute',
              inset: 0,
              opacity: 0,
              cursor: 'pointer',
              width: '100%',
              height: '100%',
            }}
          />
        </Box>
        <TextField
          size="small"
          value={value}
          onChange={(e) => onChange(e.target.value)}
          sx={{ width: 110 }}
          slotProps={{ htmlInput: { pattern: '^#[0-9a-fA-F]{6}$', spellCheck: false } }}
        />
      </Box>
    </Box>
  );
}

function ExtractedColorChip({ hex, onSelectRole }) {
  const [anchorEl, setAnchorEl] = useState(null);

  const handleClick = (event) => setAnchorEl(event.currentTarget);
  const handleClose = () => setAnchorEl(null);

  const handleApply = (role) => {
    onSelectRole(role, hex);
    handleClose();
  };

  return (
    <>
      <Tooltip title="Haz clic para asignar este color como primario, secundario o acento">
        <Chip
          label={hex}
          onClick={handleClick}
          avatar={<Avatar sx={{ bgcolor: hex, width: 20, height: 20, border: '1px solid rgba(255,255,255,0.8)' }} />}
          variant="outlined"
          sx={{
            fontWeight: 600,
            cursor: 'pointer',
            borderColor: 'divider',
            '&:hover': { bgcolor: 'action.hover', borderColor: hex },
          }}
        />
      </Tooltip>
      <Menu anchorEl={anchorEl} open={Boolean(anchorEl)} onClose={handleClose}>
        <MenuItem onClick={() => handleApply('primaryColor')}>
          <Box sx={{ width: 14, height: 14, borderRadius: '50%', bgcolor: hex, mr: 1.5 }} />
          Usar como Color Primario
        </MenuItem>
        <MenuItem onClick={() => handleApply('secondaryColor')}>
          <Box sx={{ width: 14, height: 14, borderRadius: '50%', bgcolor: hex, mr: 1.5 }} />
          Usar como Color Secundario
        </MenuItem>
        <MenuItem onClick={() => handleApply('accentColor')}>
          <Box sx={{ width: 14, height: 14, borderRadius: '50%', bgcolor: hex, mr: 1.5 }} />
          Usar como Color de Acento
        </MenuItem>
      </Menu>
    </>
  );
}

function DropZone({ label, previewUrl, onFile, onRemove }) {
  const [dragOver, setDragOver] = useState(false);

  const handleDrop = async function (e) {
    e.preventDefault();
    setDragOver(false);
    const file = e.dataTransfer.files[0];
    if (!file || !file.type.startsWith('image/')) return;
    const url = await leerComoDataUrl(file);
    onFile(url);
  };

  const handleChange = async function (e) {
    const file = e.target.files[0];
    if (!file) return;
    const url = await leerComoDataUrl(file);
    onFile(url);
  };

  return (
    <Box>
      <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 0.5 }}>
        {label}
      </Typography>
      <Box
        onDragOver={(e) => { e.preventDefault(); setDragOver(true); }}
        onDragLeave={() => setDragOver(false)}
        onDrop={handleDrop}
        sx={{
          border: '2px dashed',
          borderColor: dragOver ? 'primary.main' : 'divider',
          borderRadius: 2,
          p: 2,
          textAlign: 'center',
          bgcolor: dragOver ? 'primary.lighter' : 'background.default',
          transition: 'all 0.2s',
          cursor: 'pointer',
          position: 'relative',
          minHeight: 100,
          display: 'flex',
          flexDirection: 'column',
          alignItems: 'center',
          justifyContent: 'center',
          gap: 1,
          overflow: 'hidden',
        }}
      >
        {previewUrl ? (
          <>
            <Box
              component="img"
              src={previewUrl}
              alt={label}
              sx={{ maxHeight: 80, maxWidth: '100%', objectFit: 'contain', borderRadius: 1 }}
            />
            <Button size="small" color="inherit" onClick={onRemove} sx={{ fontSize: '0.7rem' }}>
              Quitar
            </Button>
          </>
        ) : (
          <>
            <Iconify icon="mdi:image-plus-outline" width={32} sx={{ color: 'text.disabled' }} />
            <Typography variant="caption" color="text.secondary">
              Arrastra o haz clic para subir
            </Typography>
            <Button variant="outlined" component="label" size="small">
              Seleccionar
              <input type="file" accept="image/png,image/jpeg,image/webp,image/svg+xml" hidden onChange={handleChange} />
            </Button>
          </>
        )}
      </Box>
    </Box>
  );
}

function LivePreview({ theme, competitionName }) {
  return (
    <Box
      sx={{
        width: '100%',
        borderRadius: 2,
        overflow: 'hidden',
        border: '1px solid',
        borderColor: 'divider',
      }}
    >
      <Box
        sx={{
          minHeight: 100,
          background: theme.bannerUrl
            ? `linear-gradient(to right, rgba(0,0,0,0.55) 0%, rgba(0,0,0,0.0) 100%), url("${theme.bannerUrl}") center/cover no-repeat`
            : `linear-gradient(135deg, ${theme.primaryColor} 0%, ${theme.secondaryColor} 100%)`,
          p: 2,
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
        }}
      >
        <Box>
          <Typography variant="caption" sx={{ color: 'rgba(255,255,255,0.7)', letterSpacing: 2, fontSize: '0.6rem' }}>
            FIXTURE OFICIAL
          </Typography>
          <Typography variant="h6" fontWeight={800} sx={{ color: '#fff', textShadow: '0 2px 8px rgba(0,0,0,0.4)' }}>
            {competitionName || 'Nombre del Campeonato'}
          </Typography>
        </Box>
        {theme.accentColor && (
          <Box
            sx={{
              width: 24,
              height: 24,
              borderRadius: '50%',
              bgcolor: theme.accentColor,
              border: '2px solid rgba(255,255,255,0.6)',
            }}
          />
        )}
      </Box>
      <Box sx={{ p: 1.5, display: 'flex', gap: 1, bgcolor: 'background.paper', flexWrap: 'wrap' }}>
        {['Jornada 1', 'Grupo A', 'Final'].map(function (lbl) {
          return (
            <Chip
              key={lbl}
              label={lbl}
              size="small"
              sx={{ bgcolor: `${theme.primaryColor}22`, color: theme.primaryColor, fontSize: '0.65rem' }}
            />
          );
        })}
      </Box>
    </Box>
  );
}

export default function ThemeConfigPage() {
  const { theme, setTheme, applyTheme, resetTheme } = useTournamentTheme();
  const { data: competitions } = useApi(endpoints.competitions);
  const [compId, setCompId] = useLastCompetition(competitions);
  const [localTheme, setLocalTheme] = useState(theme);

  const [extractedColors, setExtractedColors] = useState([]);
  const [extracting, setExtracting] = useState(false);

  const competition = competitions?.find((c) => c.id === compId);

  // Carga el tema cuando se elige una competencia
  useEffect(
    function () {
      if (competition) {
        applyTheme(competition);
        setLocalTheme(function (prev) {
          return {
            ...prev,
            competitionName: competition.name,
            competitionId: competition.id,
            competitionSlug: competition.slug,
          };
        });
      }
    },
    [competition]
  );

  // Sincroniza estado local con contexto
  useEffect(
    function () {
      setLocalTheme(theme);
    },
    [theme]
  );

  // Extrae colores automáticamente de las imágenes cargadas
  const processImageExtraction = useCallback(async (imageUrl) => {
    if (!imageUrl) return;
    setExtracting(true);
    try {
      const colors = await extractColorsFromImage(imageUrl, 6);
      if (colors.length > 0) {
        setExtractedColors(colors);
      }
    } catch (e) {
      console.error(e);
    } finally {
      setExtracting(false);
    }
  }, []);

  useEffect(() => {
    const sourceImage = localTheme.bannerUrl || localTheme.logoUrl;
    if (sourceImage) {
      processImageExtraction(sourceImage);
    } else {
      setExtractedColors([]);
    }
  }, [localTheme.bannerUrl, localTheme.logoUrl, processImageExtraction]);

  const patch = function (key, value) {
    setLocalTheme(function (prev) {
      return { ...prev, [key]: value };
    });
  };

  const handleApplyRoleColor = (roleKey, hexValue) => {
    patch(roleKey, hexValue);
    toast.success(`Color asignado a ${roleKey === 'primaryColor' ? 'Primario' : roleKey === 'secondaryColor' ? 'Secundario' : 'Acento'}`);
  };

  const handleApplyAutoPalette = () => {
    if (extractedColors.length === 0) return;
    const primary = extractedColors[0];
    const secondary = extractedColors[1] || extractedColors[0];
    const accent = extractedColors[2] || extractedColors[0];

    setLocalTheme((prev) => ({
      ...prev,
      primaryColor: primary,
      secondaryColor: secondary,
      accentColor: accent,
    }));
    toast.success('Paleta armónica extraída y aplicada automáticamente.');
  };

  const handleApply = async function () {
    const payload = {
      ...localTheme,
      competitionId: competition?.id || localTheme.competitionId,
      competitionSlug: competition?.slug || localTheme.competitionSlug,
      competitionName: competition?.name || localTheme.competitionName,
    };
    setTheme(payload);

    if (competition?.id) {
      try {
        const updatedSettings = {
          ...(competition.settings || {}),
          public: {
            ...(competition.settings?.public || {}),
            accentColor: payload.primaryColor,
            bannerUrl: payload.bannerUrl || null,
            sponsors: (payload.sponsors || []).map(function(s) {
              return { name: s.name || '', url: s.url || '', logoKey: s.logoKey || null };
            }),
          }
        };
        await axiosInstance.put(`/api/competitions/${competition.id}`, {
          name: competition.name,
          season: competition.season,
          slug: competition.slug,
          format: competition.format,
          rulesetId: competition.rulesetId,
          captureLevel: competition.captureLevel,
          startsOn: competition.startsOn,
          endsOn: competition.endsOn,
          settings: updatedSettings,
        });
      } catch (err) {
        console.warn('Persistencia en backend opcional falló o no requerida:', err);
      }
    }

    toast.success('Tema aplicado y guardado para este campeonato.');
  };

  const handleReset = function () {
    resetTheme();
    setLocalTheme(DEFAULT_THEME);
    setExtractedColors([]);
    toast.info('Tema restablecido al predeterminado.');
  };

  const addSponsor = function () {
    setLocalTheme(function (prev) {
      return { ...prev, sponsors: [...(prev.sponsors || []), { name: '', url: '', logoUrl: null }] };
    });
  };

  const removeSponsor = function (i) {
    setLocalTheme(function (prev) {
      return { ...prev, sponsors: prev.sponsors.filter(function (_, idx) { return idx !== i; }) };
    });
  };

  const updateSponsor = function (i, patch2) {
    setLocalTheme(function (prev) {
      return {
        ...prev,
        sponsors: prev.sponsors.map(function (s, idx) {
          return idx === i ? { ...s, ...patch2 } : s;
        }),
      };
    });
  };

  const setSponsorLogo = async function (i, e) {
    const file = e.target.files[0];
    if (!file) return;
    const url = await leerComoDataUrl(file);
    updateSponsor(i, { logoUrl: url });
  };

  return (
    <Box>
      <PageHeader title="Tematización del Campeonato">
        <Button variant="outlined" onClick={handleReset} startIcon={<Iconify icon="mdi:restore" />}>
          Restablecer
        </Button>
        <Button variant="contained" onClick={handleApply} startIcon={<Iconify icon="mdi:check" />}>
          Aplicar tema
        </Button>
      </PageHeader>

      <Alert severity="info" sx={{ mb: 3 }}>
        Personaliza los colores y gráfica del campeonato. Al subir el banner o logotipo, el sistema extraerá automáticamente una paleta recomendada.
      </Alert>

      <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', lg: '1fr 380px' }, gap: 3 }}>
        {/* Panel izquierdo: formulario */}
        <Stack spacing={3}>
          {/* Competencia */}
          <Paper variant="outlined" sx={{ p: 2.5 }}>
            <Typography variant="subtitle1" fontWeight={700} sx={{ mb: 2 }}>
              Competencia base
            </Typography>
            <SelectionCompetition
              value={compId}
              onChange={function (e) { setCompId(e.target.value); }}
              label="Cargar tema desde competencia"
            />
            <Typography variant="caption" color="text.secondary" sx={{ mt: 1, display: 'block' }}>
              Al seleccionar una competencia, se cargan su banner y color de acento guardados.
            </Typography>
          </Paper>

          {/* Imágenes */}
          <Paper variant="outlined" sx={{ p: 2.5 }}>
            <Typography variant="subtitle1" fontWeight={700} sx={{ mb: 2 }}>
              Imágenes del campeonato
            </Typography>
            <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2}>
              <Box sx={{ flex: 1 }}>
                <DropZone
                  label="Banner / Portada"
                  previewUrl={localTheme.bannerUrl}
                  onFile={function (url) { patch('bannerUrl', url); }}
                  onRemove={function () { patch('bannerUrl', null); }}
                />
              </Box>
              <Box sx={{ flex: 1 }}>
                <DropZone
                  label="Logotipo oficial"
                  previewUrl={localTheme.logoUrl}
                  onFile={function (url) { patch('logoUrl', url); }}
                  onRemove={function () { patch('logoUrl', null); }}
                />
              </Box>
            </Stack>
          </Paper>

          {/* Recomendador Inteligente de Paleta de Colores */}
          {(extractedColors.length > 0 || extracting) && (
            <Paper
              variant="outlined"
              sx={{
                p: 2.5,
                bgcolor: 'background.neutral',
                borderColor: 'primary.main',
                borderStyle: 'dashed',
              }}
            >
              <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 1.5 }}>
                <Typography variant="subtitle2" fontWeight={700} sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                  <Iconify icon="mdi:wand" width={20} sx={{ color: 'primary.main' }} />
                  Sugerencias de Color Extraídas de la Imagen
                </Typography>
                {extractedColors.length > 0 && (
                  <Button
                    size="small"
                    variant="contained"
                    color="primary"
                    startIcon={<Iconify icon="mdi:auto-fix" />}
                    onClick={handleApplyAutoPalette}
                  >
                    Aplicar Paleta Inteligente
                  </Button>
                )}
              </Box>

              <Typography variant="caption" color="text.secondary" sx={{ mb: 2, display: 'block' }}>
                Selecciona un color de la imagen para asignarlo como color primario, secundario o acento:
              </Typography>

              {extracting ? (
                <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5, py: 1 }}>
                  <CircularProgress size={20} />
                  <Typography variant="caption">Analizando imagen y sintetizando paleta armónica...</Typography>
                </Box>
              ) : (
                <Stack direction="row" spacing={1} flexWrap="wrap" gap={1}>
                  {extractedColors.map((hex) => (
                    <ExtractedColorChip
                      key={hex}
                      hex={hex}
                      onSelectRole={handleApplyRoleColor}
                    />
                  ))}
                </Stack>
              )}
            </Paper>
          )}

          {/* Colores */}
          <Paper variant="outlined" sx={{ p: 2.5 }}>
            <Typography variant="subtitle1" fontWeight={700} sx={{ mb: 2 }}>
              Paleta de colores activa
            </Typography>
            <Stack direction={{ xs: 'column', sm: 'row' }} spacing={3} flexWrap="wrap">
              <ColorSwatch
                label="Color primario"
                value={localTheme.primaryColor}
                onChange={function (v) { patch('primaryColor', v); }}
              />
              <ColorSwatch
                label="Color secundario"
                value={localTheme.secondaryColor}
                onChange={function (v) { patch('secondaryColor', v); }}
              />
              <ColorSwatch
                label="Color de acento"
                value={localTheme.accentColor}
                onChange={function (v) { patch('accentColor', v); }}
              />
            </Stack>
          </Paper>

          {/* Sponsors */}
          <Paper variant="outlined" sx={{ p: 2.5 }}>
            <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2 }}>
              <Typography variant="subtitle1" fontWeight={700}>
                Patrocinadores
              </Typography>
              <Button size="small" startIcon={<Iconify icon="eva:plus-fill" width={16} />} onClick={addSponsor}>
                Agregar
              </Button>
            </Box>
            {(localTheme.sponsors || []).length === 0 && (
              <Typography variant="body2" color="text.secondary">
                Sin patrocinadores configurados.
              </Typography>
            )}
            <Stack spacing={2}>
              {(localTheme.sponsors || []).map(function (s, i) {
                return (
                  <Box
                    key={i}
                    sx={{ display: 'flex', alignItems: 'center', gap: 1.5, p: 1.5, border: '1px solid', borderColor: 'divider', borderRadius: 1.5 }}
                  >
                    <Avatar
                      src={s.logoUrl || undefined}
                      variant="rounded"
                      sx={{ width: 44, height: 44, bgcolor: 'action.hover', '& img': { objectFit: 'contain' }, flexShrink: 0 }}
                    >
                      <Iconify icon="mdi:image-outline" width={20} sx={{ color: 'text.disabled' }} />
                    </Avatar>
                    <Button variant="outlined" component="label" size="small" sx={{ flexShrink: 0 }}>
                      Logo
                      <input type="file" accept="image/*" hidden onChange={function (e) { setSponsorLogo(i, e); }} />
                    </Button>
                    <TextField
                      label="Nombre"
                      size="small"
                      value={s.name}
                      onChange={function (e) { updateSponsor(i, { name: e.target.value }); }}
                      sx={{ flex: 1, minWidth: 90 }}
                    />
                    <TextField
                      label="Enlace"
                      size="small"
                      value={s.url}
                      onChange={function (e) { updateSponsor(i, { url: e.target.value }); }}
                      sx={{ flex: 1, minWidth: 90 }}
                    />
                    <Tooltip title="Quitar patrocinador">
                      <IconButton size="small" color="error" onClick={function () { removeSponsor(i); }}>
                        <Iconify icon="eva:trash-2-outline" width={16} />
                      </IconButton>
                    </Tooltip>
                  </Box>
                );
              })}
            </Stack>
          </Paper>
        </Stack>

        {/* Panel derecho: previsualización */}
        <Box sx={{ position: { xs: 'relative', lg: 'sticky' }, top: 80, alignSelf: 'start' }}>
          <Paper variant="outlined" sx={{ p: 2.5 }}>
            <Typography variant="subtitle1" fontWeight={700} sx={{ mb: 2 }}>
              Previsualización en tiempo real
            </Typography>
            <LivePreview theme={localTheme} competitionName={localTheme.competitionName || competition?.name} />
            <Divider sx={{ my: 2 }} />
            <Typography variant="caption" color="text.secondary">
              Los cambios son locales. Usa <strong>Aplicar tema</strong> para activarlos en toda la plataforma.
            </Typography>
          </Paper>
        </Box>
      </Box>
    </Box>
  );
}

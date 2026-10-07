import { useEffect, useState } from 'react';
import Alert from '@mui/material/Alert';
import Avatar from '@mui/material/Avatar';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Chip from '@mui/material/Chip';
import CircularProgress from '@mui/material/CircularProgress';
import Paper from '@mui/material/Paper';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import { Iconify } from 'src/components/iconify';
import { leerComoDataUrl } from 'src/lib/data-url';
import { recommendThemesFromLogo } from './logo-theme-recommendations';

const STYLE_LABELS = {
  identity: ['Equilibrado', 'Versátil'],
  sport: ['Alto impacto', 'Compacto'],
  premium: ['Editorial', 'Espacioso'],
};

export function LogoThemeAssistant({ logoUrl, onLogoChange, onClear, onApplyTheme, compact = false }) {
  const [recommendations, setRecommendations] = useState([]);
  const [analyzing, setAnalyzing] = useState(false);
  const [error, setError] = useState('');
  const [applied, setApplied] = useState('');

  useEffect(() => {
    if (!logoUrl) {
      setRecommendations([]);
      setError('');
      setApplied('');
    }
  }, [logoUrl]);

  const analyze = async (source) => {
    setAnalyzing(true);
    setError('');
    setApplied('');
    try {
      setRecommendations(await recommendThemesFromLogo(source));
    } catch (e) {
      setRecommendations([]);
      setError(e.message || 'No pudimos analizar ese logo.');
    } finally {
      setAnalyzing(false);
    }
  };

  const pick = async (event) => {
    const file = event.target.files?.[0];
    event.target.value = '';
    if (!file) return;
    if (file.size > 8 * 1024 * 1024) {
      setError('El logo debe pesar menos de 8 MB.');
      return;
    }
    try {
      const source = await leerComoDataUrl(file);
      onLogoChange(source);
      await analyze(source);
    } catch (e) {
      setError(e.message || 'No pudimos leer ese archivo.');
    }
  };

  const apply = (recommendation) => {
    onApplyTheme(recommendation.theme);
    setApplied(recommendation.id);
  };

  return (
    <Stack spacing={1.5}>
      <Paper
        variant="outlined"
        sx={{
          p: 1.5, display: 'flex', alignItems: 'center', gap: 1.5,
          background: 'linear-gradient(135deg, rgba(245,0,87,0.045), rgba(33,150,243,0.035))',
        }}
      >
        <Avatar
          src={logoUrl || undefined}
          variant="rounded"
          sx={{ width: compact ? 68 : 82, height: compact ? 68 : 82, bgcolor: 'background.paper', border: '1px solid', borderColor: 'divider', '& img': { objectFit: 'contain', p: 0.5 } }}
        >
          {!logoUrl && <Iconify icon="mdi:shield-star-outline" width={30} sx={{ color: 'text.disabled' }} />}
        </Avatar>
        <Box sx={{ flex: 1, minWidth: 0 }}>
          <Typography variant="subtitle2">Logo e identidad visual</Typography>
          <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 1 }}>
            Subí el escudo y armamos una identidad profesional a partir de sus colores.
          </Typography>
          <Stack direction="row" spacing={0.75} sx={{ flexWrap: 'wrap', rowGap: 0.75 }}>
            <Button component="label" variant="outlined" size="small" startIcon={<Iconify icon="eva:upload-outline" width={16} />}>
              {logoUrl ? 'Cambiar logo' : 'Subir logo'}
              <input type="file" accept="image/png,image/jpeg,image/webp" hidden onChange={pick} />
            </Button>
            {logoUrl && !recommendations.length && !analyzing && (
              <Button size="small" onClick={() => analyze(logoUrl)} startIcon={<Iconify icon="mdi:auto-fix" width={16} />}>
                Analizar
              </Button>
            )}
            {logoUrl && <Button size="small" color="inherit" onClick={onClear}>Quitar</Button>}
          </Stack>
        </Box>
      </Paper>

      {analyzing && (
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, color: 'text.secondary' }}>
          <CircularProgress size={16} /><Typography variant="caption">Buscando la mejor combinación…</Typography>
        </Box>
      )}
      {error && <Alert severity="warning" sx={{ py: 0 }}>{error}</Alert>}

      {!!recommendations.length && (
        <Box>
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.75, mb: 1 }}>
            <Iconify icon="mdi:creation-outline" width={18} sx={{ color: 'secondary.main' }} />
            <Typography variant="subtitle2">Temas recomendados para tu logo</Typography>
          </Box>
          <Box sx={{ display: 'grid', gridTemplateColumns: compact ? '1fr' : { xs: '1fr', sm: 'repeat(3, 1fr)' }, gap: 1 }}>
            {recommendations.map((recommendation) => {
              const colors = [recommendation.theme.primary, recommendation.theme.secondary, recommendation.theme.surface, recommendation.theme.heroGradientTo];
              const selected = applied === recommendation.id;
              return (
                <Paper key={recommendation.id} variant="outlined" sx={{ p: 1.25, borderColor: selected ? 'success.main' : 'divider', position: 'relative' }}>
                  <Box sx={{ display: 'flex', height: 34, borderRadius: 1, overflow: 'hidden', mb: 1, border: '1px solid', borderColor: 'divider' }}>
                    {colors.map((color, index) => <Box key={`${color}-${index}`} sx={{ flex: 1, bgcolor: color }} />)}
                  </Box>
                  <Typography variant="subtitle2">{recommendation.name}</Typography>
                  <Typography variant="caption" color="text.secondary" sx={{ display: 'block', minHeight: compact ? 0 : 36, mb: 0.75 }}>
                    {recommendation.description}
                  </Typography>
                  <Stack direction="row" spacing={0.5} sx={{ mb: 1, flexWrap: 'wrap', rowGap: 0.5 }}>
                    {STYLE_LABELS[recommendation.id].map((label) => <Chip key={label} label={label} size="small" variant="outlined" sx={{ height: 22, fontSize: 11 }} />)}
                  </Stack>
                  <Button
                    fullWidth size="small" variant={selected ? 'contained' : 'outlined'} color={selected ? 'success' : 'primary'}
                    onClick={() => apply(recommendation)}
                    startIcon={<Iconify icon={selected ? 'eva:checkmark-circle-2-fill' : 'mdi:palette-outline'} width={16} />}
                  >
                    {selected ? 'Tema aplicado' : 'Aplicar tema'}
                  </Button>
                </Paper>
              );
            })}
          </Box>
          <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 0.75 }}>
            Podés aplicar una base ahora y ajustar cada detalle después en el estudio del portal.
          </Typography>
        </Box>
      )}
    </Stack>
  );
}

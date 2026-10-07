import { useEffect, useMemo, useState } from 'react';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import CircularProgress from '@mui/material/CircularProgress';
import Dialog from '@mui/material/Dialog';
import DialogActions from '@mui/material/DialogActions';
import DialogContent from '@mui/material/DialogContent';
import DialogTitle from '@mui/material/DialogTitle';
import MenuItem from '@mui/material/MenuItem';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import { Iconify } from 'src/components/iconify';
import {
  createFixtureFlyer, downloadCanvas, flyerFileName, fixtureTitle, FLYER_FORMATS,
} from './fixture-flyer';

const FORMAT_ICONS = {
  story: { width: 18, height: 32 },
  portrait: { width: 23, height: 29 },
  square: { width: 27, height: 27 },
};

export function SocialFlyerDialog({ open, onClose, matches, competition }) {
  const fixtures = useMemo(() => matches.filter((match) => match.status === 'scheduled' || match.status === 'postponed'), [matches]);
  const results = useMemo(() => matches.filter((match) => match.status === 'finished' || match.status === 'walkover'), [matches]);
  const [type, setType] = useState('fixture');
  const [selected, setSelected] = useState('');
  const [format, setFormat] = useState('story');
  const [canvas, setCanvas] = useState(null);
  const [creating, setCreating] = useState(false);
  const rows = useMemo(() => type === 'fixture' ? fixtures : results, [type, fixtures, results]);
  const match = rows.find((row) => row.id === selected);
  const selectedFormat = FLYER_FORMATS.find((item) => item.value === format);

  useEffect(() => { if (open) setSelected(rows[0]?.id || ''); }, [open, type, rows[0]?.id]);
  useEffect(() => {
    if (!match) { setCanvas(null); return undefined; }
    let alive = true;
    setCreating(true);
    createFixtureFlyer(match, competition, format, type)
      .then((next) => { if (alive) setCanvas(next); })
      .finally(() => { if (alive) setCreating(false); });
    return () => { alive = false; };
  }, [match, competition, format, type]);

  const download = () => {
    if (canvas && match) downloadCanvas(canvas, flyerFileName(match, format, type === 'result' ? 'resultado' : 'fixture'));
  };

  const downloadAll = async () => {
    setCreating(true);
    for (const row of rows) {
      const next = await createFixtureFlyer(row, competition, format, type);
      downloadCanvas(next, flyerFileName(row, format, type === 'result' ? 'resultado' : 'fixture'));
      await new Promise((resolve) => setTimeout(resolve, 180));
    }
    setCreating(false);
  };

  return <Dialog
    open={open}
    onClose={onClose}
    maxWidth="lg"
    fullWidth
    slotProps={{ paper: { sx: { overflow: 'hidden', bgcolor: '#F5F7FA' } } }}
  >
    <DialogTitle sx={{ bgcolor: '#FFFFFF', borderBottom: '1px solid', borderColor: 'divider', py: 2.25 }}>
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5 }}>
        <Box sx={{
          width: 42, height: 42, borderRadius: 1.5, display: 'grid', placeItems: 'center',
          bgcolor: 'primary.main', color: 'primary.contrastText',
        }}>
          <Iconify icon="mdi:creation-outline" width={24} />
        </Box>
        <Box>
          <Typography variant="h6" fontWeight={800}>Estudio para redes</Typography>
          <Typography variant="body2" color="text.secondary">
            Diseños listos para publicar con la identidad de tu competencia.
          </Typography>
        </Box>
      </Box>
    </DialogTitle>

    <DialogContent sx={{ p: { xs: 2, md: 3 } }}>
      <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', md: '320px minmax(0, 1fr)' }, gap: 3 }}>
        <Box sx={{
          bgcolor: '#FFFFFF', border: '1px solid', borderColor: 'divider', borderRadius: 2.5,
          p: 2.25, alignSelf: 'start',
        }}>
          <Typography variant="overline" color="text.secondary" fontWeight={800}>1 · Tipo de pieza</Typography>
          <Box sx={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 0.75, mt: 0.75, mb: 2.5 }}>
            <Button
              size="small"
              variant={type === 'fixture' ? 'contained' : 'outlined'}
              onClick={() => setType('fixture')}
              startIcon={<Iconify icon="mdi:calendar-star" />}
              sx={{ minHeight: 42 }}
            >
              Fixture · {fixtures.length}
            </Button>
            <Button
              size="small"
              variant={type === 'result' ? 'contained' : 'outlined'}
              onClick={() => setType('result')}
              startIcon={<Iconify icon="mdi:scoreboard-outline" />}
              sx={{ minHeight: 42 }}
            >
              Resultado · {results.length}
            </Button>
          </Box>

          <Typography variant="overline" color="text.secondary" fontWeight={800}>2 · Partido</Typography>
          {rows.length ? <TextField
            select fullWidth size="small" value={selected}
            onChange={(event) => setSelected(event.target.value)}
            sx={{ mt: 0.75, mb: 2.5 }}
          >
            {rows.map((row) => <MenuItem key={row.id} value={row.id}>{fixtureTitle(row)}</MenuItem>)}
          </TextField> : <Box sx={{
            mt: 0.75, mb: 2.5, p: 1.5, borderRadius: 1.5, bgcolor: 'grey.100',
            border: '1px dashed', borderColor: 'divider',
          }}>
            <Typography variant="body2" color="text.secondary">
              {type === 'fixture'
                ? 'No hay partidos programados para convertir en piezas.'
                : 'No hay resultados finalizados para publicar.'}
            </Typography>
          </Box>}

          <Typography variant="overline" color="text.secondary" fontWeight={800}>3 · Formato</Typography>
          <Box sx={{ display: 'grid', gap: 1, mt: 0.75 }}>
            {FLYER_FORMATS.map((item) => {
              const active = format === item.value;
              const shape = FORMAT_ICONS[item.value];
              return <Button
                key={item.value}
                variant={active ? 'contained' : 'outlined'}
                color={active ? 'primary' : 'inherit'}
                onClick={() => setFormat(item.value)}
                sx={{
                  minHeight: 54, px: 1.5, justifyContent: 'flex-start', textAlign: 'left',
                  borderColor: active ? 'primary.main' : 'divider',
                }}
              >
                <Box sx={{
                  width: 40, mr: 1.25, display: 'grid', placeItems: 'center',
                  color: active ? 'inherit' : 'text.secondary',
                }}>
                  <Box sx={{
                    width: shape.width, height: shape.height, border: '2px solid currentColor',
                    borderRadius: 0.5, bgcolor: active ? 'rgba(255,255,255,0.12)' : 'transparent',
                  }} />
                </Box>
                <Box>
                  <Typography component="span" variant="body2" fontWeight={800} sx={{ display: 'block', lineHeight: 1.2 }}>
                    {item.label}
                  </Typography>
                  <Typography component="span" variant="caption" sx={{ display: 'block', opacity: 0.72 }}>
                    {item.detail}
                  </Typography>
                </Box>
              </Button>;
            })}
          </Box>

          <Box sx={{ display: 'flex', gap: 1, mt: 2.5, p: 1.5, borderRadius: 1.5, bgcolor: 'info.lighter' }}>
            <Iconify icon="mdi:lightbulb-outline" width={19} sx={{ color: 'info.dark', mt: 0.15, flexShrink: 0 }} />
            <Typography variant="caption" color="text.secondary">
              Usamos los colores principal y secundario, el logo y la portada de la competición. El texto ajusta su contraste para seguir siendo legible.
            </Typography>
          </Box>
        </Box>

        <Box sx={{
          minWidth: 0, minHeight: { xs: 460, md: 680 }, bgcolor: '#0A1220', borderRadius: 2.5,
          border: '1px solid rgba(255,255,255,0.08)', overflow: 'hidden',
          display: 'flex', flexDirection: 'column',
        }}>
          <Box sx={{
            px: 2, py: 1.25, borderBottom: '1px solid rgba(255,255,255,0.09)',
            display: 'flex', alignItems: 'center', justifyContent: 'space-between',
          }}>
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
              <Box sx={{ width: 8, height: 8, borderRadius: '50%', bgcolor: creating ? 'warning.main' : 'success.main' }} />
              <Typography variant="caption" fontWeight={800} sx={{ color: '#FFFFFF' }}>
                {creating ? 'GENERANDO VISTA PREVIA' : 'VISTA PREVIA'}
              </Typography>
            </Box>
            <Typography variant="caption" sx={{ color: 'rgba(255,255,255,0.55)' }}>
              {selectedFormat?.label} · PNG
            </Typography>
          </Box>

          <Box sx={{
            position: 'relative', flex: 1, display: 'grid', placeItems: 'center',
            p: { xs: 2, sm: 3 }, backgroundImage: 'radial-gradient(circle at 50% 42%, #23334A 0, #101A29 46%, #09111E 100%)',
          }}>
            {!rows.length ? <Box sx={{ textAlign: 'center', maxWidth: 320, px: 2 }}>
              <Iconify icon="mdi:image-off-outline" width={48} sx={{ color: 'rgba(255,255,255,0.3)', mb: 1.5 }} />
              <Typography color="rgba(255,255,255,0.72)" fontWeight={700}>Todavía no hay una pieza para mostrar</Typography>
              <Typography variant="body2" color="rgba(255,255,255,0.45)" sx={{ mt: 0.5 }}>
                Elegí una sección que tenga partidos disponibles.
              </Typography>
            </Box> : <>
              {canvas && <Box
                component="img"
                src={canvas.toDataURL('image/png')}
                alt={match ? `Vista previa de ${fixtureTitle(match)}` : 'Vista previa de la pieza'}
                sx={{
                  display: 'block', maxWidth: '100%', maxHeight: { xs: 520, md: 610 },
                  width: 'auto', height: 'auto', objectFit: 'contain',
                  boxShadow: '0 28px 70px rgba(0,0,0,0.46)', borderRadius: 0.5,
                  opacity: creating ? 0.48 : 1, transition: 'opacity 180ms ease',
                }}
              />}
              {creating && <CircularProgress sx={{ position: 'absolute' }} />}
            </>}
          </Box>
        </Box>
      </Box>
    </DialogContent>

    <DialogActions sx={{ px: 3, py: 2, bgcolor: '#FFFFFF', borderTop: '1px solid', borderColor: 'divider' }}>
      <Button onClick={onClose}>Cerrar</Button>
      <Box sx={{ flex: 1 }} />
      {rows.length > 1 && <Button
        disabled={creating}
        onClick={downloadAll}
        startIcon={<Iconify icon="mdi:download-multiple" />}
      >
        Descargar todos
      </Button>}
      <Button
        variant="contained"
        disabled={creating || !canvas}
        onClick={download}
        startIcon={<Iconify icon="mdi:download-outline" />}
      >
        Descargar PNG
      </Button>
    </DialogActions>
  </Dialog>;
}

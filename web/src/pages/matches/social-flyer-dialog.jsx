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

export function SocialFlyerDialog({ open, onClose, matches, competition }) {
  const fixtures = useMemo(() => matches.filter((match) => match.status === 'scheduled' || match.status === 'postponed'), [matches]);
  const results = useMemo(() => matches.filter((match) => match.status === 'finished' || match.status === 'walkover'), [matches]);
  const [type, setType] = useState('fixture');
  const [selected, setSelected] = useState('');
  const [format, setFormat] = useState('story');
  const [canvas, setCanvas] = useState(null);
  const [creating, setCreating] = useState(false);
  const rows = useMemo(() => type === 'fixture' ? fixtures : results, [type, fixtures, results]);

  useEffect(() => { if (open) setSelected(rows[0]?.id || ''); }, [open, type, rows[0]?.id]);
  useEffect(() => {
    const match = rows.find((row) => row.id === selected);
    if (!match) { setCanvas(null); return; }
    let alive = true;
    setCreating(true);
    createFixtureFlyer(match, competition, format, type).then((next) => { if (alive) setCanvas(next); }).finally(() => { if (alive) setCreating(false); });
    return () => { alive = false; };
  }, [selected, competition, format, type, rows]);

  const download = () => {
    const match = rows.find((row) => row.id === selected);
    if (canvas && match) downloadCanvas(canvas, flyerFileName(match, format, type === 'result' ? 'resultado' : 'fixture'));
  };

  const downloadAll = async () => {
    setCreating(true);
    for (const match of rows) {
      const next = await createFixtureFlyer(match, competition, format, type);
      downloadCanvas(next, flyerFileName(match, format, type === 'result' ? 'resultado' : 'fixture'));
      await new Promise((resolve) => setTimeout(resolve, 180));
    }
    setCreating(false);
  };

  return <Dialog open={open} onClose={onClose} maxWidth="md" fullWidth>
    <DialogTitle>Crear piezas para redes</DialogTitle>
    <DialogContent sx={{ pt: '16px !important' }}>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
        Una composición editorial lista para publicar: identidad de la competencia, escudos, fecha, horario y sede. Los datos cambian con cada partido; el estilo se conserva.
      </Typography>
      <Box sx={{ display: 'flex', gap: 1, mb: 2 }}>
        <Button size="small" variant={type === 'fixture' ? 'contained' : 'outlined'} onClick={() => setType('fixture')}>
          Fixtures ({fixtures.length})
        </Button>
        <Button size="small" variant={type === 'result' ? 'contained' : 'outlined'} onClick={() => setType('result')}>
          Resultados ({results.length})
        </Button>
      </Box>
      {!rows.length ? <Typography color="text.secondary">{type === 'fixture' ? 'No hay partidos programados para convertir en flyers.' : 'No hay resultados finalizados para convertir en piezas.'}</Typography> : <>
        <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: '1.2fr 1fr' }, gap: 2 }}>
          <TextField select fullWidth label="Partido" value={selected} onChange={(event) => setSelected(event.target.value)}>
            {rows.map((match) => <MenuItem key={match.id} value={match.id}>{fixtureTitle(match)}</MenuItem>)}
          </TextField>
          <Box>
            <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 0.75 }}>Formato de publicación</Typography>
            <Box sx={{ display: 'flex', gap: 0.75, flexWrap: 'wrap' }}>
              {FLYER_FORMATS.map((item) => <Button
                key={item.value} size="small" variant={format === item.value ? 'contained' : 'outlined'}
                color={format === item.value ? 'primary' : 'inherit'} onClick={() => setFormat(item.value)}
                sx={{ minWidth: 0, px: 1.25, lineHeight: 1.15 }}
              >
                <Box component="span" sx={{ display: 'block' }}>{item.label}</Box>
                <Box component="span" sx={{ display: 'block', fontSize: 10, opacity: 0.78 }}>{item.detail}</Box>
              </Button>)}
            </Box>
          </Box>
        </Box>
        <Box sx={{ mt: 2, minHeight: 430, bgcolor: '#101722', borderRadius: 2, display: 'flex', justifyContent: 'center', alignItems: 'center', overflow: 'hidden', p: 2 }}>
          {creating || !canvas ? <CircularProgress /> : <Box component="img" src={canvas.toDataURL('image/png')} alt="Vista previa del flyer" sx={{ height: 520, maxWidth: '100%', objectFit: 'contain', borderRadius: 1 }} />}
        </Box>
      </>}
    </DialogContent>
    <DialogActions>
      <Button onClick={onClose}>Cerrar</Button>
      {rows.length > 1 && <Button disabled={creating} onClick={downloadAll}>Descargar todos</Button>}
      <Button variant="contained" disabled={creating || !canvas} onClick={download} startIcon={<Iconify icon="mdi:download-outline" />}>Descargar PNG</Button>
    </DialogActions>
  </Dialog>;
}

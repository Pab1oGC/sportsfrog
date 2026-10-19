import Dialog from '@mui/material/Dialog';
import DialogTitle from '@mui/material/DialogTitle';
import DialogContent from '@mui/material/DialogContent';
import DialogActions from '@mui/material/DialogActions';
import Button from '@mui/material/Button';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Stack from '@mui/material/Stack';
import Alert from '@mui/material/Alert';
import { useNavigate } from 'react-router';
import { Iconify } from 'src/components/iconify';

/**
 * Lo que RecordResult.cs devuelve al finalizar un partido, además del
 * marcador: tarjetas rojas sin sanción todavía, y jugadores de cualquiera
 * de los dos planteles que ya cargan una sanción activa. Ninguna de las dos
 * cosas se decide acá -- ver RecordResult.SuggestedSanction y
 * .SuspendedPlayerWarning del lado del servidor para por qué son una
 * sugerencia y un aviso, nunca una acción tomada sola.
 *
 * `competitionId` viaja para armar el link a Sanciones con todo lo que ya
 * se sabe (SanctionsPage lee ese prefill desde el state de la navegación);
 * sin eso, "Imponer sanción" abriría la pantalla a pedir de nuevo algo que
 * esta misma pantalla ya sabe.
 */
export function FinishAlertsDialog({ open, onClose, competitionId, result }) {
  const navigate = useNavigate();
  const suggestions = result?.suggestedSanctions || [];
  const warnings = result?.suspendedPlayerWarnings || [];

  const imponer = (suggestion) => {
    onClose();
    navigate('/dashboard/sanctions', {
      state: {
        prefill: {
          competitionId,
          teamId: suggestion.teamId,
          teamName: suggestion.teamName,
          athleteId: suggestion.athleteId,
          athleteName: `${suggestion.athleteFirstName} ${suggestion.athleteLastName}`,
          matchId: suggestion.matchId,
          reason: 'Tarjeta roja',
        },
      },
    });
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Resultado cargado</DialogTitle>
      <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: '16px !important' }}>
        {suggestions.length > 0 && (
          <Box>
            <Typography variant="subtitle2" fontWeight={700} sx={{ mb: 1 }}>
              Tarjeta roja sin sanción
            </Typography>
            <Stack spacing={1}>
              {suggestions.map((s) => (
                <Box key={s.athleteId} sx={{ display: 'flex', alignItems: 'center', gap: 1, flexWrap: 'wrap', p: 1, border: '1px solid', borderColor: 'divider', borderRadius: 1 }}>
                  <Typography variant="body2" sx={{ flexGrow: 1 }}>
                    {s.athleteFirstName} {s.athleteLastName} — {s.teamName}
                  </Typography>
                  <Button size="small" variant="outlined" startIcon={<Iconify icon="eva:alert-triangle-outline" width={16} />} onClick={() => imponer(s)}>
                    Imponer sanción
                  </Button>
                </Box>
              ))}
            </Stack>
          </Box>
        )}

        {warnings.length > 0 && (
          <Box>
            <Typography variant="subtitle2" fontWeight={700} sx={{ mb: 1 }}>
              Jugadores con sanción activa en el plantel
            </Typography>
            <Alert severity="warning" sx={{ mb: 1 }}>
              Confirmá que no participaron de este partido.
            </Alert>
            <Stack spacing={0.5}>
              {warnings.map((w) => (
                <Typography key={w.athleteId} variant="body2">
                  {w.athleteFirstName} {w.athleteLastName} — {w.teamName}: {w.sanctionReason}
                </Typography>
              ))}
            </Stack>
          </Box>
        )}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cerrar</Button>
      </DialogActions>
    </Dialog>
  );
}

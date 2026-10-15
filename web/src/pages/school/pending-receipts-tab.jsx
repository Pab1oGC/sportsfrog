import { useState } from 'react';
import Box from '@mui/material/Box';
import Paper from '@mui/material/Paper';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import TextField from '@mui/material/TextField';
import CircularProgress from '@mui/material/CircularProgress';
import { toast } from 'sonner';
import { useApi, apiPost } from 'src/hooks/use-api';
import { endpoints } from 'src/lib/axios';
import { CrudDialog } from 'src/components/crud-dialog';
import { PAYMENT_METHOD_LABEL } from 'src/lib/payment-labels';
import { periodo } from 'src/lib/format-date';

// Un link firmado de PDF no trae un content-type que este lado pueda leer
// sin descargarlo -- pero PaymentReceipt.StoreAsync solo le pone extensión
// .pdf a lo que efectivamente guardó como PDF, así que basta mirarla acá.
const esPdf = (url) => !!url && url.split('?')[0].toLowerCase().endsWith('.pdf');

/**
 * La bandeja de comprobantes: todo pago con un comprobante todavía sin
 * revisar, organización entera -- no por curso, mismo motivo que
 * RecordPayment no acepta scope de curso en ninguno de sus endpoints.
 */
export function PendingReceiptsTab() {
  const { data: receipts, isLoading, mutate } = useApi(endpoints.pendingReceipts);

  const [rejectDialog, setRejectDialog] = useState({ open: false, receipt: null });
  const [reason, setReason] = useState('');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');

  const verify = async (receipt) => {
    try {
      await apiPost(endpoints.paymentReview(receipt.id), { verified: true, reason: null });
      toast.success('Comprobante verificado.');
      mutate();
    } catch (err) { toast.error(err.message); }
  };

  const openReject = (receipt) => {
    setReason('');
    setError('');
    setRejectDialog({ open: true, receipt });
  };

  const saveReject = async () => {
    setSaving(true);
    setError('');
    try {
      await apiPost(endpoints.paymentReview(rejectDialog.receipt.id), { verified: false, reason });
      setRejectDialog({ open: false, receipt: null });
      mutate();
    } catch (err) { setError(err.message); } finally { setSaving(false); }
  };

  const rows = receipts || [];

  return (
    <Box>
      {isLoading && (
        <Box sx={{ display: 'flex', justifyContent: 'center', py: 6 }}><CircularProgress /></Box>
      )}

      {!isLoading && rows.length === 0 && (
        <Typography color="text.secondary" sx={{ py: 2 }}>No hay comprobantes esperando revisión.</Typography>
      )}

      <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
        {rows.map((receipt) => (
          <Paper key={receipt.id} variant="outlined" sx={{ p: 2, display: 'flex', gap: 2, flexWrap: 'wrap' }}>
            <Box sx={{ width: 220, flexShrink: 0 }}>
              {!receipt.receiptUrl && (
                <Typography color="text.secondary" variant="body2">Sin comprobante.</Typography>
              )}
              {receipt.receiptUrl && esPdf(receipt.receiptUrl) && (
                <Button href={receipt.receiptUrl} target="_blank" rel="noreferrer" variant="outlined" fullWidth>
                  Ver PDF
                </Button>
              )}
              {receipt.receiptUrl && !esPdf(receipt.receiptUrl) && (
                <Box
                  component="img" src={receipt.receiptUrl} alt="Comprobante"
                  sx={{ width: '100%', borderRadius: 1, border: '1px solid', borderColor: 'divider' }}
                />
              )}
            </Box>

            <Box sx={{ flex: 1, minWidth: 200 }}>
              <Typography fontWeight={600}>{receipt.athleteFirstName} {receipt.athleteLastName}</Typography>
              <Typography variant="body2" color="text.secondary">
                {receipt.courseName} — {periodo(receipt.period)}
              </Typography>
              <Typography variant="body2">
                Bs {receipt.amountBob} — {PAYMENT_METHOD_LABEL[receipt.method] || receipt.method}
              </Typography>
            </Box>

            <Box sx={{ display: 'flex', gap: 1, alignItems: 'flex-start' }}>
              <Button variant="contained" color="success" onClick={() => verify(receipt)}>Verificar</Button>
              <Button variant="outlined" color="error" onClick={() => openReject(receipt)}>Rechazar</Button>
            </Box>
          </Paper>
        ))}
      </Box>

      <CrudDialog
        open={rejectDialog.open} editId={null} entityName="rechazo"
        error={error} saving={saving}
        onClose={() => setRejectDialog({ open: false, receipt: null })}
        onSave={saveReject}
      >
        <TextField
          label="Motivo" value={reason}
          onChange={(e) => setReason(e.target.value)}
          fullWidth required multiline minRows={2}
        />
      </CrudDialog>
    </Box>
  );
}

import { useState } from 'react';
import Dialog from '@mui/material/Dialog';
import DialogTitle from '@mui/material/DialogTitle';
import DialogContent from '@mui/material/DialogContent';
import DialogActions from '@mui/material/DialogActions';
import Button from '@mui/material/Button';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Chip from '@mui/material/Chip';
import CircularProgress from '@mui/material/CircularProgress';
import TextField from '@mui/material/TextField';
import MenuItem from '@mui/material/MenuItem';
import { toast } from 'sonner';
import { Iconify } from 'src/components/iconify';
import { useApi, apiPost } from 'src/hooks/use-api';
import { endpoints } from 'src/lib/axios';
import { CrudDialog } from 'src/components/crud-dialog';
import { readInlineReceipt, INLINE_RECEIPT_REQUIREMENT } from 'src/lib/inline-photo';
import { bloquearNegativos, soloDecimales } from 'src/lib/entero-sin-signo';
import { PAYMENT_METHODS, PAYMENT_METHOD_LABEL } from 'src/lib/payment-labels';

// Los tres valores de ReceiptReviewState -- mismo mapa que
// TuitionAccountDialog ya usa para un pago de pensión.
const ESTADOS_REVISION = {
  pending: { label: 'Por revisar', color: 'warning' },
  verified: { label: 'Verificado', color: 'success' },
  rejected: { label: 'Rechazado', color: 'error' },
};

const emptyPaymentForm = () => ({ amount: '', method: 'cash', receiptDataUrl: '' });

/**
 * Los pagos de la multa de una sanción: registrar uno, y anular uno que no
 * corresponde. Mismo patrón que TuitionAccountDialog para una pensión --
 * ver ese componente para la razón de cada decisión, que acá es idéntica
 * salvo que esto cuelga de una sanción en vez de un cargo.
 *
 * `sanction` nulo mantiene el diálogo cerrado.
 */
export function SanctionPaymentsDialog({ competitionId, sanction, onClose, onChanged }) {
  const open = !!sanction;
  const paymentsUrl = open ? endpoints.sanction(competitionId, sanction.id) : null;
  const { data: current, isLoading, mutate } = useApi(paymentsUrl);
  const payments = current?.payments || sanction?.payments || [];

  const [paymentForm, setPaymentForm] = useState(emptyPaymentForm());
  const [paymentFileName, setPaymentFileName] = useState('');
  const [paymentOpen, setPaymentOpen] = useState(false);
  const [paymentSaving, setPaymentSaving] = useState(false);
  const [paymentError, setPaymentError] = useState('');

  const [voidDialog, setVoidDialog] = useState({ open: false, payment: null });
  const [voidReason, setVoidReason] = useState('');
  const [voidSaving, setVoidSaving] = useState(false);
  const [voidError, setVoidError] = useState('');

  const refresh = () => {
    mutate();
    onChanged?.();
  };

  const openPayment = () => {
    setPaymentForm(emptyPaymentForm());
    setPaymentFileName('');
    setPaymentError('');
    setPaymentOpen(true);
  };

  const onPickReceipt = async (e) => {
    const file = e.target.files[0];
    e.target.value = '';
    if (!file) return;
    try {
      const dataUrl = await readInlineReceipt(file);
      setPaymentForm({ ...paymentForm, receiptDataUrl: dataUrl });
      setPaymentFileName(file.name);
    } catch (err) { toast.error(err.message); }
  };

  const savePayment = async () => {
    setPaymentSaving(true);
    setPaymentError('');
    try {
      await apiPost(endpoints.sanctionPayments(competitionId, sanction.id), {
        amount: Number(paymentForm.amount),
        method: paymentForm.method,
        receiptDataUrl: paymentForm.method === 'cash' ? null : paymentForm.receiptDataUrl || null,
      });
      setPaymentOpen(false);
      refresh();
    } catch (err) { setPaymentError(err.message); } finally { setPaymentSaving(false); }
  };

  const openVoid = (payment) => {
    setVoidReason('');
    setVoidError('');
    setVoidDialog({ open: true, payment });
  };

  const saveVoid = async () => {
    setVoidSaving(true);
    setVoidError('');
    try {
      await apiPost(endpoints.sanctionPaymentVoid(competitionId, voidDialog.payment.id), { reason: voidReason });
      setVoidDialog({ open: false, payment: null });
      refresh();
    } catch (err) { setVoidError(err.message); } finally { setVoidSaving(false); }
  };

  return (
    <>
      <Dialog open={open} onClose={() => onClose()} maxWidth="sm" fullWidth>
        <DialogTitle>
          Pagos de la multa{sanction ? ` — ${sanction.athleteFirstName} ${sanction.athleteLastName}` : ''}
        </DialogTitle>
        <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 1.5, pt: '16px !important' }}>
          {sanction && (
            <Typography variant="body2" color="text.secondary">
              Multa: Bs {sanction.fineAmount} — saldo: Bs {sanction.fineBalance ?? sanction.fineAmount}
            </Typography>
          )}

          {isLoading && (
            <Box sx={{ display: 'flex', justifyContent: 'center', py: 3 }}><CircularProgress /></Box>
          )}
          {!isLoading && payments.length === 0 && (
            <Typography color="text.secondary">Todavía no se registró ningún pago.</Typography>
          )}

          {payments.map((payment) => {
            const revision = ESTADOS_REVISION[payment.reviewState] || ESTADOS_REVISION.pending;
            const anulado = !!payment.voidedAt;
            return (
              <Box key={payment.id} sx={{ display: 'flex', alignItems: 'center', gap: 1, flexWrap: 'wrap', p: 1, border: '1px solid', borderColor: 'divider', borderRadius: 1 }}>
                <Typography variant="body2" sx={{ flexGrow: 1 }}>
                  Bs {payment.amount} — {PAYMENT_METHOD_LABEL[payment.method] || payment.method}
                </Typography>
                <Chip size="small" label={anulado ? 'Anulado' : revision.label} color={anulado ? 'error' : revision.color} />
                {!anulado && (
                  <Button size="small" color="error" onClick={() => openVoid(payment)}>Anular</Button>
                )}
              </Box>
            );
          })}

          <Button
            variant="outlined" startIcon={<Iconify icon="mdi:cash-plus" width={16} />}
            onClick={openPayment} sx={{ alignSelf: 'flex-start', mt: 1 }}
          >
            Registrar pago
          </Button>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => onClose()}>Cerrar</Button>
        </DialogActions>
      </Dialog>

      <CrudDialog
        open={paymentOpen} editId={null} entityName="pago" error={paymentError} saving={paymentSaving}
        onClose={() => setPaymentOpen(false)} onSave={savePayment}
      >
        <TextField
          label="Monto (Bs)" type="number" value={paymentForm.amount} fullWidth required
          onChange={(e) => setPaymentForm({ ...paymentForm, amount: soloDecimales(e.target.value) })}
          onKeyDown={bloquearNegativos}
          slotProps={{ htmlInput: { min: 0.01, step: 0.01 } }}
        />
        <TextField
          select label="Método" value={paymentForm.method} fullWidth
          onChange={(e) => setPaymentForm({ ...paymentForm, method: e.target.value, receiptDataUrl: '' })}
        >
          {PAYMENT_METHODS.map((m) => <MenuItem key={m.value} value={m.value}>{m.label}</MenuItem>)}
        </TextField>
        {paymentForm.method !== 'cash' && (
          <Box sx={{ display: 'flex', flexDirection: 'column', alignItems: 'flex-start', gap: 0.5 }}>
            <Button variant="outlined" size="small" component="label" startIcon={<Iconify icon="eva:attach-outline" width={16} />}>
              {paymentFileName ? 'Cambiar comprobante' : 'Adjuntar comprobante'}
              <input type="file" accept="image/png,image/jpeg,image/webp,application/pdf" hidden onChange={onPickReceipt} />
            </Button>
            {paymentFileName && <Typography variant="caption">{paymentFileName}</Typography>}
            <Typography variant="caption" color="text.secondary">{INLINE_RECEIPT_REQUIREMENT}</Typography>
          </Box>
        )}
      </CrudDialog>

      <CrudDialog
        open={voidDialog.open} editId={null} entityName="anulación de pago" entityGender="f"
        error={voidError} saving={voidSaving}
        onClose={() => setVoidDialog({ open: false, payment: null })} onSave={saveVoid}
      >
        <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
          Anular este pago es definitivo -- si fue un error, se vuelve a registrar el pago correcto
          en vez de deshacer esta anulación.
        </Typography>
        <TextField
          label="Motivo" value={voidReason}
          onChange={(e) => setVoidReason(e.target.value)}
          fullWidth required multiline minRows={2}
        />
      </CrudDialog>
    </>
  );
}

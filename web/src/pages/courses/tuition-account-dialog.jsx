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
import { useApi, apiPut, apiPost } from 'src/hooks/use-api';
import { endpoints } from 'src/lib/axios';
import { CrudDialog } from 'src/components/crud-dialog';
import { readInlineReceipt, INLINE_RECEIPT_REQUIREMENT } from 'src/lib/inline-photo';
import { bloquearNegativos, soloDecimales } from 'src/lib/entero-sin-signo';
import { PAYMENT_METHODS, PAYMENT_METHOD_LABEL } from 'src/lib/payment-labels';
import { periodo } from 'src/lib/format-date';

// Los cinco valores de ChargeStatus (SnakeCaseEnumConverter), tal cual viajan.
const ESTADOS_CARGO = {
  pending: { label: 'Pendiente', color: 'default' },
  partial: { label: 'Parcial', color: 'info' },
  paid: { label: 'Pagada', color: 'success' },
  waived: { label: 'Condonada', color: 'secondary' },
  voided: { label: 'Anulada', color: 'error' },
};

// Los tres valores de ReceiptReviewState.
const ESTADOS_REVISION = {
  pending: { label: 'Por revisar', color: 'warning' },
  verified: { label: 'Verificado', color: 'success' },
  rejected: { label: 'Rechazado', color: 'error' },
};

const emptyPaymentForm = () => ({ amountBob: '', method: 'cash', receiptDataUrl: '' });

/**
 * Las pensiones de una inscripción puntual: una fila por mes devengado, con
 * su estado, sus pagos, y las acciones que corresponden a cada uno --
 * condonar/anular el cargo, registrar un pago, anular un pago. El estado de
 * cada cargo nunca lo decide esta pantalla: lo manda ReadTuitionAccount,
 * recalculado del lado del servidor cada vez que algo cambia.
 *
 * `enrollment` nulo mantiene el diálogo cerrado -- mismo patrón que
 * AttendanceSheetDialog ya usa con `session`.
 */
export function TuitionAccountDialog({ enrollment, onClose }) {
  const open = !!enrollment;
  const { data: charges, isLoading, mutate } = useApi(open ? endpoints.enrollmentTuitionAccount(enrollment.id) : null);

  // Condonar / anular un cargo -- un toggle, con o sin motivo según si se
  // activa o se limpia (ver WaiveCharge/VoidCharge del lado del servidor).
  const [prompt, setPrompt] = useState({ open: false, charge: null, kind: null });
  const [reason, setReason] = useState('');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');

  const endpointFor = (kind, chargeId) =>
    kind === 'waive' ? endpoints.chargeWaiver(chargeId) : endpoints.chargeVoid(chargeId);

  const clear = async (kind, charge) => {
    try {
      await apiPut(endpointFor(kind, charge.id), { reason: null });
      mutate();
    } catch (err) { toast.error(err.message); }
  };

  const openPrompt = (kind, charge) => {
    setReason('');
    setError('');
    setPrompt({ open: true, charge, kind });
  };

  const savePrompt = async () => {
    setSaving(true);
    setError('');
    try {
      await apiPut(endpointFor(prompt.kind, prompt.charge.id), { reason });
      setPrompt({ open: false, charge: null, kind: null });
      mutate();
    } catch (err) { setError(err.message); } finally { setSaving(false); }
  };

  // Registrar un pago -------------------------------------------------

  const [paymentDialog, setPaymentDialog] = useState({ open: false, charge: null });
  const [paymentForm, setPaymentForm] = useState(emptyPaymentForm());
  const [paymentFileName, setPaymentFileName] = useState('');
  const [paymentSaving, setPaymentSaving] = useState(false);
  const [paymentError, setPaymentError] = useState('');

  const openPaymentDialog = (charge) => {
    setPaymentForm(emptyPaymentForm());
    setPaymentFileName('');
    setPaymentError('');
    setPaymentDialog({ open: true, charge });
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
      await apiPost(endpoints.chargePayments(paymentDialog.charge.id), {
        amountBob: Number(paymentForm.amountBob),
        method: paymentForm.method,
        receiptDataUrl: paymentForm.method === 'cash' ? null : paymentForm.receiptDataUrl || null,
      });
      setPaymentDialog({ open: false, charge: null });
      mutate();
    } catch (err) { setPaymentError(err.message); } finally { setPaymentSaving(false); }
  };

  // Anular un pago -- a diferencia de condonar/anular un cargo, esto no
  // tiene vuelta atrás (ver VoidPayment del lado del servidor).

  const [voidPaymentDialog, setVoidPaymentDialog] = useState({ open: false, payment: null });
  const [voidPaymentReason, setVoidPaymentReason] = useState('');
  const [voidPaymentSaving, setVoidPaymentSaving] = useState(false);
  const [voidPaymentError, setVoidPaymentError] = useState('');

  const openVoidPayment = (payment) => {
    setVoidPaymentReason('');
    setVoidPaymentError('');
    setVoidPaymentDialog({ open: true, payment });
  };

  const saveVoidPayment = async () => {
    setVoidPaymentSaving(true);
    setVoidPaymentError('');
    try {
      await apiPost(endpoints.paymentVoid(voidPaymentDialog.payment.id), { reason: voidPaymentReason });
      setVoidPaymentDialog({ open: false, payment: null });
      mutate();
    } catch (err) { setVoidPaymentError(err.message); } finally { setVoidPaymentSaving(false); }
  };

  const rows = charges || [];

  return (
    <>
      <Dialog open={open} onClose={() => onClose()} maxWidth="sm" fullWidth>
        <DialogTitle>
          Pensión{enrollment ? ` — ${enrollment.firstName} ${enrollment.lastName}` : ''}
        </DialogTitle>
        <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 1.5, pt: '16px !important' }}>
          {isLoading && (
            <Box sx={{ display: 'flex', justifyContent: 'center', py: 3 }}><CircularProgress /></Box>
          )}
          {!isLoading && rows.length === 0 && (
            <Typography color="text.secondary">Todavía no se le devengó ninguna pensión.</Typography>
          )}
          {rows.map((charge) => {
            const estado = ESTADOS_CARGO[charge.status] || ESTADOS_CARGO.pending;
            const condonada = charge.status === 'waived';
            const anulada = charge.status === 'voided';
            const admitePago = !condonada && !anulada;
            return (
              <Box key={charge.id} sx={{ display: 'flex', flexDirection: 'column', gap: 0.5, p: 1.5, border: '1px solid', borderColor: 'divider', borderRadius: 1 }}>
                <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 1 }}>
                  <Typography fontWeight={600}>{periodo(charge.period)} — Bs {charge.amountBob}</Typography>
                  <Chip size="small" label={estado.label} color={estado.color} />
                </Box>
                <Typography variant="caption" color="text.secondary">
                  Vence el {charge.dueOn} — pagado Bs {charge.paidBob}, saldo Bs {charge.balanceBob}
                </Typography>
                {charge.waivedAt && (
                  <Typography variant="caption" color="text.secondary">Condonada: {charge.waiverReason}</Typography>
                )}
                {charge.voidedAt && (
                  <Typography variant="caption" color="text.secondary">Anulada: {charge.voidReason}</Typography>
                )}

                {charge.payments.length > 0 && (
                  <Box sx={{ display: 'flex', flexDirection: 'column', gap: 0.5, mt: 0.5, pl: 1, borderLeft: '2px solid', borderColor: 'divider' }}>
                    {charge.payments.map((payment) => {
                      const revision = ESTADOS_REVISION[payment.reviewState] || ESTADOS_REVISION.pending;
                      const pagoAnulado = !!payment.voidedAt;
                      return (
                        <Box key={payment.id} sx={{ display: 'flex', alignItems: 'center', gap: 1, flexWrap: 'wrap' }}>
                          <Typography variant="body2">
                            Bs {payment.amountBob} — {PAYMENT_METHOD_LABEL[payment.method] || payment.method}
                          </Typography>
                          <Chip size="small" label={pagoAnulado ? 'Anulado' : revision.label} color={pagoAnulado ? 'error' : revision.color} />
                          {!pagoAnulado && (
                            <Button size="small" color="error" onClick={() => openVoidPayment(payment)}>Anular</Button>
                          )}
                        </Box>
                      );
                    })}
                  </Box>
                )}

                <Box sx={{ display: 'flex', gap: 1, mt: 0.5, flexWrap: 'wrap' }}>
                  {admitePago && (
                    <Button size="small" variant="outlined" startIcon={<Iconify icon="mdi:cash-plus" width={16} />} onClick={() => openPaymentDialog(charge)}>
                      Registrar pago
                    </Button>
                  )}
                  <Button size="small" onClick={() => (condonada ? clear('waive', charge) : openPrompt('waive', charge))}>
                    {condonada ? 'Quitar condonación' : 'Condonar'}
                  </Button>
                  <Button size="small" color="error" onClick={() => (anulada ? clear('void', charge) : openPrompt('void', charge))}>
                    {anulada ? 'Quitar anulación' : 'Anular'}
                  </Button>
                </Box>
              </Box>
            );
          })}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => onClose()}>Cerrar</Button>
        </DialogActions>
      </Dialog>

      <CrudDialog
        open={prompt.open}
        editId={null}
        entityName={prompt.kind === 'waive' ? 'condonación' : 'anulación'}
        entityGender="f"
        error={error}
        saving={saving}
        onClose={() => setPrompt({ open: false, charge: null, kind: null })}
        onSave={savePrompt}
      >
        <TextField
          label="Motivo" value={reason}
          onChange={(e) => setReason(e.target.value)}
          fullWidth required multiline minRows={2}
        />
      </CrudDialog>

      <CrudDialog
        open={paymentDialog.open}
        editId={null}
        entityName="pago"
        error={paymentError}
        saving={paymentSaving}
        onClose={() => setPaymentDialog({ open: false, charge: null })}
        onSave={savePayment}
      >
        <TextField
          label="Monto (Bs)" type="number" value={paymentForm.amountBob} fullWidth required
          onChange={(e) => setPaymentForm({ ...paymentForm, amountBob: soloDecimales(e.target.value) })}
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
        open={voidPaymentDialog.open}
        editId={null}
        entityName="anulación de pago"
        entityGender="f"
        error={voidPaymentError}
        saving={voidPaymentSaving}
        onClose={() => setVoidPaymentDialog({ open: false, payment: null })}
        onSave={saveVoidPayment}
      >
        <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
          Anular este pago es definitivo -- si fue un error, se vuelve a registrar el pago correcto
          en vez de deshacer esta anulación.
        </Typography>
        <TextField
          label="Motivo" value={voidPaymentReason}
          onChange={(e) => setVoidPaymentReason(e.target.value)}
          fullWidth required multiline minRows={2}
        />
      </CrudDialog>
    </>
  );
}

import { useState } from 'react';
import Dialog from '@mui/material/Dialog';
import DialogTitle from '@mui/material/DialogTitle';
import DialogContent from '@mui/material/DialogContent';
import DialogActions from '@mui/material/DialogActions';
import Button from '@mui/material/Button';
import Typography from '@mui/material/Typography';
import Chip from '@mui/material/Chip';
import TextField from '@mui/material/TextField';
import MenuItem from '@mui/material/MenuItem';
import { DataGrid } from '@mui/x-data-grid';
import { toast } from 'sonner';
import { Iconify } from 'src/components/iconify';
import { useApi, apiPost } from 'src/hooks/use-api';
import { endpoints } from 'src/lib/axios';
import { CrudDialog } from 'src/components/crud-dialog';
import { RowActionsMenu } from 'src/components/row-actions-menu';
import { ReceiptAttachField, ReceiptViewerDialog } from 'src/components/receipt-attach-field';
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

  // El comprobante de un pago YA guardado -- ver abrirComprobante más
  // abajo. El del archivo recién elegido en "Registrar pago", todavía sin
  // subir, lo maneja ReceiptAttachField por su cuenta.
  const [visor, setVisor] = useState({ open: false, url: '', isPdf: false, title: '' });

  const [voidDialog, setVoidDialog] = useState({ open: false, payment: null });
  const [voidReason, setVoidReason] = useState('');
  const [voidSaving, setVoidSaving] = useState(false);
  const [voidError, setVoidError] = useState('');

  const [rejectDialog, setRejectDialog] = useState({ open: false, payment: null });
  const [rejectReason, setRejectReason] = useState('');
  const [rejectSaving, setRejectSaving] = useState(false);
  const [rejectError, setRejectError] = useState('');

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

  // El comprobante de un pago YA guardado -- el link firmado llega junto
  // con la sanción (ver ReadSanctions.ReadAsync del lado del API, que
  // firma el de cada uno de sus propios pagos, a diferencia de la lista de
  // sanciones, que solo trae HasReceipt): nada que pedir aparte, se abre
  // directo.
  const abrirComprobante = (payment) => setVisor({
    open: true,
    url: payment.receiptUrl,
    isPdf: payment.receiptUrl.split('?')[0].toLowerCase().endsWith('.pdf'),
    title: `Comprobante — Bs ${payment.amount}`,
  });

  const verificar = async (payment) => {
    try {
      await apiPost(endpoints.sanctionPaymentReview(competitionId, payment.id), { verified: true, reason: null });
      toast.success('Comprobante verificado.');
      refresh();
    } catch (err) { toast.error(err.message); }
  };

  const openReject = (payment) => {
    setRejectReason('');
    setRejectError('');
    setRejectDialog({ open: true, payment });
  };

  const saveReject = async () => {
    setRejectSaving(true);
    setRejectError('');
    try {
      await apiPost(
        endpoints.sanctionPaymentReview(competitionId, rejectDialog.payment.id), { verified: false, reason: rejectReason });
      setRejectDialog({ open: false, payment: null });
      refresh();
    } catch (err) { setRejectError(err.message); } finally { setRejectSaving(false); }
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

  const paymentColumns = [
    { field: 'amount', headerName: 'Monto', width: 80, renderCell: ({ row }) => `Bs ${row.amount}` },
    { field: 'method', headerName: 'Método', width: 100, renderCell: ({ row }) => PAYMENT_METHOD_LABEL[row.method] || row.method },
    { field: 'reviewState', headerName: 'Estado', width: 110, sortable: false, renderCell: ({ row }) => {
      const revision = ESTADOS_REVISION[row.reviewState] || ESTADOS_REVISION.pending;
      const anulado = !!row.voidedAt;
      return <Chip size="small" label={anulado ? 'Anulado' : revision.label} color={anulado ? 'error' : revision.color} />;
    }},
    // El ojo y el visto son dos íconos sueltos, los dos al mismo nivel --
    // ninguno de los dos detrás del "..." -- porque son las dos acciones
    // que en verdad se usan seguido. Rechazar y Anular sí van detrás: son
    // las menos comunes de las cuatro.
    { field: 'actions', headerName: 'Acciones', width: 110, align: 'center', headerAlign: 'center', sortable: false, renderCell: ({ row }) => {
      const pendiente = row.reviewState === 'pending' && !row.voidedAt;
      return (
        <RowActionsMenu
          primary={[
            row.hasReceipt && { icon: 'eva:eye-outline', label: 'Ver comprobante', onClick: () => abrirComprobante(row) },
            pendiente && { icon: 'eva:checkmark-circle-2-outline', label: 'Verificar', color: 'success.main', onClick: () => verificar(row) },
          ]}
          actions={[
            pendiente && { icon: 'eva:close-circle-outline', label: 'Rechazar', color: 'warning.main', onClick: () => openReject(row) },
            { icon: 'eva:close-circle-outline', label: 'Anular', color: 'error.main', onClick: () => openVoid(row), hidden: !!row.voidedAt },
          ]}
        />
      );
    }},
  ];

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

          {!isLoading && payments.length === 0 && (
            <Typography color="text.secondary">Todavía no se registró ningún pago.</Typography>
          )}

          {/*
            Una tabla de verdad, no una tarjeta por pago -- "Ver
            comprobante" es un ícono de ojo suelto en la columna Acciones
            (RowActionsMenu.primary), al mismo nivel que Verificar, nunca
            detrás del "...": son las dos acciones que en verdad se usan
            seguido. Un pago "Por revisar" muestra Verificar/Rechazar ahí
            mismo -- antes esto vivía en una pestaña aparte (Comprobantes
            pendientes) que, al ser por competencia entera y no por
            sanción, la mayoría de las veces no tenía nada que mostrar.
          */}
          <DataGrid
            rows={payments}
            columns={paymentColumns}
            loading={isLoading}
            autoHeight
            hideFooter
            disableRowSelectionOnClick
            getRowId={(row) => row.id}
          />

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
          <ReceiptAttachField
            dataUrl={paymentForm.receiptDataUrl}
            fileName={paymentFileName}
            onChange={(url, name) => { setPaymentForm({ ...paymentForm, receiptDataUrl: url }); setPaymentFileName(name); }}
            onRemove={() => { setPaymentForm({ ...paymentForm, receiptDataUrl: '' }); setPaymentFileName(''); }}
          />
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

      <CrudDialog
        open={rejectDialog.open} editId={null} entityName="rechazo"
        error={rejectError} saving={rejectSaving}
        onClose={() => setRejectDialog({ open: false, payment: null })} onSave={saveReject}
      >
        <TextField
          label="Motivo" value={rejectReason}
          onChange={(e) => setRejectReason(e.target.value)}
          fullWidth required multiline minRows={2}
        />
      </CrudDialog>

      {/* Visor del comprobante de un pago ya guardado -- ver abrirComprobante. */}
      <ReceiptViewerDialog
        open={visor.open} onClose={() => setVisor({ ...visor, open: false })}
        url={visor.url} isPdf={visor.isPdf} title={visor.title}
      />
    </>
  );
}

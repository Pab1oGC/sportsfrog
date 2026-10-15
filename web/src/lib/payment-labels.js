// Los tres valores de PaymentMethod (SnakeCaseEnumConverter del lado del
// servidor), tal cual viajan. Compartido entre TuitionAccountDialog y
// PendingReceiptsTab -- las dos pantallas que muestran el método de un pago.
export const PAYMENT_METHODS = [
  { value: 'cash', label: 'Efectivo' },
  { value: 'transfer', label: 'Transferencia' },
  { value: 'other', label: 'Otro' },
];

export const PAYMENT_METHOD_LABEL = Object.fromEntries(PAYMENT_METHODS.map((m) => [m.value, m.label]));

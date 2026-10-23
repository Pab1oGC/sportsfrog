import { useEffect, useState } from 'react';
import Box from '@mui/material/Box';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import Alert from '@mui/material/Alert';
import TextField from '@mui/material/TextField';
import MenuItem from '@mui/material/MenuItem';
import Chip from '@mui/material/Chip';
import { DataGrid } from '@mui/x-data-grid';
import { toast } from 'sonner';
import { Iconify } from 'src/components/iconify';
import { useApi, apiPost, apiPut } from 'src/hooks/use-api';
import { endpoints } from 'src/lib/axios';
import { CrudDialog } from 'src/components/crud-dialog';
import { RowActionsMenu } from 'src/components/row-actions-menu';
import { EstadoChip } from 'src/components/estado-chip';
import { SelectionCategory, SelectionTeam, SelectionAthletes } from 'src/components/selectors';
import { bloquearNoEnteros, soloDigitos } from 'src/lib/entero-sin-signo';
import { bloquearNegativos, soloDecimales } from 'src/lib/entero-sin-signo';
import { SanctionPaymentsDialog } from 'src/pages/sanctions/sanction-payments-dialog';

// Los tres valores de SanctionCompletionRule (SnakeCaseEnumConverter), en el
// orden en que se eligen -- el mismo orden que la pregunta "¿esto se cumple
// con partidos, con una multa, o con las dos?" se hace en la vida real.
const COMPLETION_RULES = [
  { value: 'matches', label: 'Partidos' },
  { value: 'fine', label: 'Multa' },
  { value: 'both', label: 'Partidos y multa' },
];

const COMPLETION_RULE_LABEL = Object.fromEntries(COMPLETION_RULES.map((r) => [r.value, r.label]));

// Los cuatro valores de SanctionStatus. Nunca llegan guardados -- SanctionLedger
// los resuelve cada vez del lado del servidor a partir de los términos de la
// sanción y de lo que realmente se cumplió.
const SANCTION_STATUS = {
  active: { label: 'Activa', color: 'error' },
  served: { label: 'Cumplida', color: 'success' },
  lifted: { label: 'Levantada', color: 'info' },
  voided: { label: 'Anulada', color: 'default' },
};

const emptyImposeForm = () => ({
  categoryId: '', teamId: '', athleteId: '', matchId: null,
  reason: '', completionRule: 'matches', matchesRequired: '', fineAmount: '',
});

const PROMPT_COPY = {
  lift: { noun: 'levantamiento', gender: 'm', helper: 'La apelación tuvo lugar: la sanción queda sin efecto, nunca estuvo realmente en deuda.' },
  void: { noun: 'anulación', gender: 'f', helper: 'Se cargó por error -- a la persona equivocada, o duplicada. Queda en el registro, pero deja de contar.' },
  fine: { noun: 'condonación de la multa', gender: 'f', helper: 'Solo perdona la multa: si la sanción también pide partidos, esos se siguen cumpliendo igual.' },
};

function endpointFor(kind, competitionId, sanctionId) {
  if (kind === 'lift') return endpoints.sanctionLift(competitionId, sanctionId);
  if (kind === 'void') return endpoints.sanctionVoid(competitionId, sanctionId);
  return endpoints.sanctionFineWaiver(competitionId, sanctionId);
}

/**
 * Las sanciones disciplinarias de una competencia: imponer una nueva,
 * levantarla por apelación, anularla por error, condonar su multa, y
 * retener o devolver la credencial física de quien la tiene. El estado
 * (activa/cumplida/levantada/anulada) nunca lo decide esta pantalla -- lo
 * manda ReadSanctions, resuelto del lado del servidor por SanctionLedger a
 * partir de los términos de la sanción y lo que realmente se cumplió.
 *
 * `prefill` es la sugerencia de "tarjeta roja sin sanción" que
 * RecordResult.cs devuelve al finalizar un partido (ver matches-page.jsx):
 * trae el deportista y el equipo ya resueltos, así que el diálogo de alta
 * los muestra fijos en vez de pedirlos de nuevo por categoría/equipo --
 * nunca los impone solo, solo abre el formulario con lo que ya se sabe.
 */
export function SanctionsPanel({ competitionId, prefill }) {
  const sanctionsUrl = competitionId ? endpoints.sanctions(competitionId) : null;
  const { data: sanctions, isLoading, mutate } = useApi(sanctionsUrl);
  const rows = sanctions || [];

  // Imponer ------------------------------------------------------------

  const [imposeOpen, setImposeOpen] = useState(false);
  const [form, setForm] = useState(emptyImposeForm());
  const [imposeSaving, setImposeSaving] = useState(false);
  const [imposeError, setImposeError] = useState('');

  // Quién y qué equipo ya están resueltos por la sugerencia: no hay
  // categoría/equipo/persona que elegir, sólo confirmar la sanción misma.
  const [prefillContext, setPrefillContext] = useState(null);

  const openImpose = () => {
    setForm(emptyImposeForm());
    setPrefillContext(null);
    setImposeError('');
    setImposeOpen(true);
  };

  // Una sugerencia nueva abre el diálogo sola -- el motivo de que esto viva
  // en un efecto y no en el onClick de un botón es que la sugerencia llega
  // por navegación (SanctionsPage lee el state de React Router), no por una
  // acción tomada en esta misma pantalla.
  useEffect(() => {
    if (!prefill) return;

    setForm({
      ...emptyImposeForm(),
      teamId: prefill.teamId,
      athleteId: prefill.athleteId,
      matchId: prefill.matchId || null,
      reason: prefill.reason || '',
    });
    setPrefillContext({ athleteName: prefill.athleteName, teamName: prefill.teamName });
    setImposeError('');
    setImposeOpen(true);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [prefill]);

  const saveImpose = async () => {
    setImposeSaving(true);
    setImposeError('');
    try {
      await apiPost(endpoints.sanctions(competitionId), {
        athleteId: form.athleteId,
        teamId: form.teamId,
        matchId: form.matchId,
        reason: form.reason,
        completionRule: form.completionRule,
        matchesRequired: form.completionRule === 'fine' ? null : Number(form.matchesRequired),
        fineAmount: form.completionRule === 'matches' ? null : Number(form.fineAmount),
      });
      setImposeOpen(false);
      mutate();
    } catch (err) { setImposeError(err.message); } finally { setImposeSaving(false); }
  };

  // Levantar / anular / condonar la multa -- un toggle con motivo, el mismo
  // patrón que TuitionAccountDialog ya usa para condonar/anular un cargo.

  const [prompt, setPrompt] = useState({ open: false, sanction: null, kind: null });
  const [reason, setReason] = useState('');
  const [promptSaving, setPromptSaving] = useState(false);
  const [promptError, setPromptError] = useState('');

  const clearToggle = async (kind, sanction) => {
    try {
      await apiPut(endpointFor(kind, competitionId, sanction.id), { reason: null });
      mutate();
    } catch (err) { toast.error(err.message); }
  };

  const openPrompt = (kind, sanction) => {
    setReason('');
    setPromptError('');
    setPrompt({ open: true, sanction, kind });
  };

  const savePrompt = async () => {
    setPromptSaving(true);
    setPromptError('');
    try {
      await apiPut(endpointFor(prompt.kind, competitionId, prompt.sanction.id), { reason });
      setPrompt({ open: false, sanction: null, kind: null });
      mutate();
    } catch (err) { setPromptError(err.message); } finally { setPromptSaving(false); }
  };

  // Retener / devolver la credencial -- no es una revocación, así que no
  // pide motivo: ver WithholdCredential del lado del servidor.

  const toggleWithholding = async (sanction, withheld) => {
    try {
      await apiPut(endpoints.sanctionCredentialWithholding(competitionId, sanction.id), { withheld });
      mutate();
    } catch (err) { toast.error(err.message); }
  };

  // Pagos de la multa -- vive en su propio diálogo (SanctionPaymentsDialog)
  // porque una sanción puede acumular varios, igual que una pensión.

  const [paymentsFor, setPaymentsFor] = useState(null);

  const columns = [
    { field: 'athleteLastName', headerName: 'Apellido', flex: 1, minWidth: 120, valueGetter: (_, row) => row.athleteLastName },
    { field: 'athleteFirstName', headerName: 'Nombre', flex: 1, minWidth: 120, valueGetter: (_, row) => row.athleteFirstName },
    { field: 'teamName', headerName: 'Equipo', flex: 1, minWidth: 140 },
    { field: 'reason', headerName: 'Motivo', flex: 1.5, minWidth: 160 },
    { field: 'completionRule', headerName: 'Se cumple con', width: 130, renderCell: ({ value }) => COMPLETION_RULE_LABEL[value] || value },
    { field: 'matches', headerName: 'Partidos', width: 100, align: 'center', headerAlign: 'center', renderCell: ({ row: s }) =>
      s.completionRule === 'fine' ? '—' : `${s.matchesServed ?? 0}/${s.matchesRequired ?? 0}`,
    },
    { field: 'fine', headerName: 'Multa (saldo)', width: 120, align: 'right', headerAlign: 'right', renderCell: ({ row: s }) =>
      s.completionRule === 'matches' ? '—' : `Bs ${s.fineBalance ?? s.fineAmount ?? 0}`,
    },
    { field: 'status', headerName: 'Estado', width: 110, renderCell: ({ value }) => {
      const estado = SANCTION_STATUS[value] || SANCTION_STATUS.active;
      return <Chip size="small" label={estado.label} color={estado.color} />;
    }},
    { field: 'credentialWithheldAt', headerName: 'Credencial', width: 130, renderCell: ({ value, row }) => (
      <EstadoChip
        activo={!value}
        onLabel="Habilitada" offLabel="Retenida" onColor="success" offColor="error"
        onTooltip="Retener" offTooltip="Devolver"
        onClick={() => toggleWithholding(row, !value)}
      />
    )},
    { field: 'actions', headerName: 'Acciones', width: 70, align: 'center', headerAlign: 'center', renderCell: ({ row: s }) => {
      const lifted = !!s.liftedAt;
      const voided = !!s.voidedAt;
      const fineWaived = !!s.fineWaivedAt;
      const hasFine = s.completionRule !== 'matches';

      return (
        <RowActionsMenu
          primary={{
            icon: lifted ? 'eva:undo-fill' : 'eva:shield-off-outline',
            label: lifted ? 'Quitar levantamiento' : 'Levantar (apelación)',
            onClick: () => (lifted ? clearToggle('lift', s) : openPrompt('lift', s)),
          }}
          actions={[
            {
              icon: voided ? 'eva:undo-fill' : 'eva:close-circle-outline',
              label: voided ? 'Quitar anulación' : 'Anular',
              color: voided ? 'info.main' : 'error.main',
              onClick: () => (voided ? clearToggle('void', s) : openPrompt('void', s)),
            },
            hasFine && {
              icon: fineWaived ? 'eva:undo-fill' : 'eva:gift-outline',
              label: fineWaived ? 'Quitar condonación' : 'Condonar multa',
              color: fineWaived ? 'info.main' : 'warning.main',
              onClick: () => (fineWaived ? clearToggle('fine', s) : openPrompt('fine', s)),
            },
            hasFine && {
              icon: 'mdi:cash-multiple',
              label: 'Pagos',
              onClick: () => setPaymentsFor(s),
            },
          ].filter(Boolean)}
        />
      );
    }},
  ];

  return (
    <Box>
      <Stack direction="row" justifyContent="flex-end" sx={{ mb: 2 }}>
        <Button variant="contained" startIcon={<Iconify icon="eva:alert-triangle-outline" />} onClick={openImpose} disabled={!competitionId}>
          Imponer sanción
        </Button>
      </Stack>

      {rows.length === 0 && !isLoading && (
        <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
          Todavía no hay sanciones registradas en esta competencia.
        </Typography>
      )}

      <DataGrid rows={rows} columns={columns} loading={isLoading} autoHeight disableRowSelectionOnClick getRowId={(row) => row.id} />

      {/* Imponer ------------------------------------------------------ */}
      <CrudDialog
        open={imposeOpen} editId={null} entityName="sanción" entityGender="f" error={imposeError} saving={imposeSaving}
        onClose={() => { setImposeOpen(false); setPrefillContext(null); }} onSave={saveImpose} maxWidth="sm"
      >
        {prefillContext ? (
          // Ya se sabe quién y en qué equipo -- sugerido desde una tarjeta
          // roja al finalizar el partido (ver matches-page.jsx). Nada que
          // elegir acá, sólo confirmar los términos de la sanción misma.
          <Alert severity="info">
            <strong>{prefillContext.athleteName}</strong> — {prefillContext.teamName}
          </Alert>
        ) : (
          <>
            <Box sx={{ display: 'flex', gap: 2, flexWrap: 'wrap' }}>
              <Box sx={{ flex: 1, minWidth: 180 }}>
                <SelectionCategory
                  competitionId={competitionId}
                  value={form.categoryId}
                  onChange={(e) => setForm({ ...form, categoryId: e.target.value, teamId: '', athleteId: '' })}
                  required
                />
              </Box>
              <Box sx={{ flex: 1, minWidth: 180 }}>
                <SelectionTeam
                  categoryId={form.categoryId}
                  value={form.teamId}
                  onChange={(e) => setForm({ ...form, teamId: e.target.value, athleteId: '' })}
                  required
                />
              </Box>
            </Box>
            <SelectionAthletes
              label="Persona sancionada"
              multiple={false}
              teamId={form.teamId}
              value={form.athleteId}
              onChange={(athleteId) => setForm({ ...form, athleteId })}
              helperText="Deportista o integrante del cuerpo técnico/dirigencia del equipo elegido -- se busca por nombre."
              required
            />
          </>
        )}
        <TextField
          label="Motivo" value={form.reason}
          onChange={(e) => setForm({ ...form, reason: e.target.value })}
          fullWidth required multiline minRows={2}
        />
        <TextField
          select label="Se cumple con" value={form.completionRule}
          onChange={(e) => setForm({ ...form, completionRule: e.target.value })}
          fullWidth required
        >
          {COMPLETION_RULES.map((r) => <MenuItem key={r.value} value={r.value}>{r.label}</MenuItem>)}
        </TextField>
        {form.completionRule !== 'fine' && (
          <TextField
            label="Cantidad de partidos" type="number" value={form.matchesRequired} fullWidth required
            onChange={(e) => setForm({ ...form, matchesRequired: soloDigitos(e.target.value) })}
            onKeyDown={bloquearNoEnteros}
            slotProps={{ htmlInput: { min: 1, step: 1 } }}
          />
        )}
        {form.completionRule !== 'matches' && (
          <TextField
            label="Multa (Bs)" type="number" value={form.fineAmount} fullWidth required
            onChange={(e) => setForm({ ...form, fineAmount: soloDecimales(e.target.value) })}
            onKeyDown={bloquearNegativos}
            slotProps={{ htmlInput: { min: 0.01, step: 0.01 } }}
          />
        )}
      </CrudDialog>

      {/* Levantar / anular / condonar ----------------------------------- */}
      <CrudDialog
        open={prompt.open}
        editId={null}
        entityName={prompt.kind ? PROMPT_COPY[prompt.kind].noun : ''}
        entityGender={prompt.kind ? PROMPT_COPY[prompt.kind].gender : 'm'}
        error={promptError}
        saving={promptSaving}
        onClose={() => setPrompt({ open: false, sanction: null, kind: null })}
        onSave={savePrompt}
      >
        {prompt.kind && (
          <Alert severity="info" sx={{ mb: 1 }}>{PROMPT_COPY[prompt.kind].helper}</Alert>
        )}
        <TextField
          label="Motivo" value={reason}
          onChange={(e) => setReason(e.target.value)}
          fullWidth required multiline minRows={2}
        />
      </CrudDialog>

      {/* Pagos de la multa ---------------------------------------------- */}
      <SanctionPaymentsDialog
        competitionId={competitionId}
        sanction={paymentsFor}
        onClose={() => setPaymentsFor(null)}
        onChanged={mutate}
      />
    </Box>
  );
}

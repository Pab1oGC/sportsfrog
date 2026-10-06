import { useState } from 'react';
import { useSearchParams } from 'react-router';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Chip from '@mui/material/Chip';
import TextField from '@mui/material/TextField';
import MenuItem from '@mui/material/MenuItem';
import Button from '@mui/material/Button';
import Dialog from '@mui/material/Dialog';
import DialogTitle from '@mui/material/DialogTitle';
import DialogContent from '@mui/material/DialogContent';
import DialogActions from '@mui/material/DialogActions';
import Alert from '@mui/material/Alert';
import Paper from '@mui/material/Paper';
import { DataGrid } from '@mui/x-data-grid';
import { toast } from 'sonner';
import { useApi, apiPut, apiPost, apiDelete } from 'src/hooks/use-api';
import { endpoints } from 'src/lib/axios';
import { useLastCompetition } from 'src/hooks/use-last-competition';
import { SelectionCompetition } from 'src/components/selectors';
import { RowActionsMenu } from 'src/components/row-actions-menu';
import { useConfirm } from 'src/components/confirm-dialog';
import { Iconify } from 'src/components/iconify';
import { OverridesDialog } from 'src/pages/accreditation/overrides-dialog';
import { CredentialPreviewDialog } from 'src/pages/accreditation/credential-preview-dialog';

/**
 * Quién tiene qué categoría de acreditación en esta competencia -- y a
 * quién todavía le falta.
 *
 * Parte del plantel, no de las acreditaciones ya hechas: una persona sin
 * categoría asignada sigue apareciendo en la lista, con el hueco a la
 * vista, en vez de ser invisible en la única pantalla hecha para cerrar
 * ese hueco -- mismo criterio que ya aplica ReadAthleteAccreditations del
 * lado del servidor.
 */
export default function AccreditationAssignmentPage() {
  const confirm = useConfirm();
  const [searchParams] = useSearchParams();
  const { data: comps } = useApi(endpoints.competitions);
  const [compId, setCompId] = useLastCompetition(comps, searchParams.get('competition'));

  const { data: roster, isLoading, mutate } = useApi(compId ? endpoints.athleteAccreditations(compId) : null);
  const { data: categories } = useApi(compId ? endpoints.accreditationCategories(compId) : null);

  const [selection, setSelection] = useState([]);
  const [bulkCategoryId, setBulkCategoryId] = useState('');
  const [bulkSaving, setBulkSaving] = useState(false);

  const [assignTarget, setAssignTarget] = useState(null);
  const [assignCategoryId, setAssignCategoryId] = useState('');
  const [assignSaving, setAssignSaving] = useState(false);

  const [overridesTarget, setOverridesTarget] = useState(null);
  const [previewTarget, setPreviewTarget] = useState(null);

  const categoriesById = Object.fromEntries((categories || []).map((c) => [c.id, c]));

  const openAssign = (row) => {
    setAssignTarget(row);
    setAssignCategoryId(row.categoryId || '');
  };

  const handleAssign = async () => {
    if (!assignTarget || !assignCategoryId) return;
    setAssignSaving(true);
    try {
      await apiPut(endpoints.athleteAccreditation(compId, assignTarget.athleteId), { categoryId: assignCategoryId });
      toast.success('Categoría asignada.');
      setAssignTarget(null);
      mutate();
    } catch (err) {
      toast.error(err.message || 'No se pudo asignar la categoría.');
    } finally {
      setAssignSaving(false);
    }
  };

  const handleRemove = async (row) => {
    const ok = await confirm(`¿Quitar la acreditación de ${row.firstName} ${row.lastName}?`, { confirmLabel: 'Quitar', danger: true });
    if (!ok) return;
    try {
      await apiDelete(endpoints.athleteAccreditation(compId, row.athleteId));
      toast.success('Acreditación quitada.');
      mutate();
    } catch (err) {
      toast.error(err.message || 'No se pudo quitar la acreditación.');
    }
  };

  const handleBulkAssign = async () => {
    if (!bulkCategoryId || selection.length === 0) return;
    setBulkSaving(true);
    try {
      await apiPost(endpoints.assignAccreditationCategoryBulk(compId, bulkCategoryId), { athleteIds: selection });
      toast.success(`Categoría asignada a ${selection.length} persona(s).`);
      setSelection([]);
      setBulkCategoryId('');
      mutate();
    } catch (err) {
      toast.error(err.message || 'No se pudo asignar la categoría al grupo.');
    } finally {
      setBulkSaving(false);
    }
  };

  const columns = [
    { field: 'lastName', headerName: 'Apellido', width: 150 },
    { field: 'firstName', headerName: 'Nombre', width: 150 },
    { field: 'teamName', headerName: 'Equipo', flex: 1, minWidth: 140 },
    {
      field: 'categoryCode', headerName: 'Categoría', width: 160,
      renderCell: ({ row }) => row.categoryId
        ? <Chip label={`${row.categoryCode} -- ${row.categoryName}`} size="small" sx={{ bgcolor: categoriesById[row.categoryId]?.colorHex, color: '#fff' }} />
        : <Chip label="Sin asignar" size="small" variant="outlined" color="warning" />,
    },
    {
      field: 'overrideCount', headerName: 'Excepciones', width: 100,
      renderCell: ({ value }) => value > 0 ? <Chip label={value} size="small" variant="outlined" /> : '--',
    },
    {
      field: 'actions', headerName: 'Acciones', width: 90, align: 'center', headerAlign: 'center', sortable: false,
      renderCell: ({ row }) => (
        <RowActionsMenu
          primary={{
            icon: 'eva:eye-outline',
            label: 'Vista previa',
            disabled: !row.categoryId,
            disabledLabel: 'Asigná una categoría primero',
            onClick: () => setPreviewTarget(row),
          }}
          actions={[
            { icon: 'eva:person-add-outline', label: row.categoryId ? 'Cambiar categoría' : 'Asignar categoría', onClick: () => openAssign(row) },
            { icon: 'eva:options-2-outline', label: 'Excepciones', disabled: !row.categoryId, onClick: () => setOverridesTarget(row) },
            row.categoryId && { icon: 'eva:close-circle-outline', label: 'Quitar acreditación', color: 'error.main', onClick: () => handleRemove(row) },
          ].filter(Boolean)}
        />
      ),
    },
  ];

  return (
    <Box>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 3, flexWrap: 'wrap', gap: 1 }}>
        <Typography variant="h4" fontWeight={700}>Acreditaciones</Typography>
      </Box>

      <Box sx={{ maxWidth: 400, mb: 3 }}>
        <SelectionCompetition value={compId} onChange={(e) => { setCompId(e.target.value); setSelection([]); }} required />
      </Box>

      {selection.length > 0 && (
        <Paper variant="outlined" sx={{ p: 2, mb: 2, display: 'flex', gap: 2, alignItems: 'center', flexWrap: 'wrap' }}>
          <Typography variant="body2">{selection.length} seleccionado(s)</Typography>
          <TextField
            select size="small" label="Asignar categoría" value={bulkCategoryId}
            onChange={(e) => setBulkCategoryId(e.target.value)} sx={{ minWidth: 220 }}
          >
            <MenuItem value="">Elegir...</MenuItem>
            {(categories || []).map((c) => <MenuItem key={c.id} value={c.id}>{c.code} -- {c.name}</MenuItem>)}
          </TextField>
          <Button variant="contained" size="small" startIcon={<Iconify icon="eva:checkmark-circle-2-outline" />} disabled={!bulkCategoryId || bulkSaving} onClick={handleBulkAssign}>
            {bulkSaving ? 'Asignando...' : 'Asignar a los seleccionados'}
          </Button>
        </Paper>
      )}

      <DataGrid
        rows={roster || []}
        columns={columns}
        loading={isLoading}
        autoHeight
        checkboxSelection
        disableRowSelectionOnClick
        getRowId={(r) => r.athleteId}
        // MUI X v9 cambió la forma de la selección: ya no es un arreglo de
        // ids sino { type, ids: Set }. Se convierte de ida y vuelta para
        // que el resto de la página siga trabajando con un arreglo simple.
        rowSelectionModel={{ type: 'include', ids: new Set(selection) }}
        onRowSelectionModelChange={(model) => setSelection([...model.ids])}
      />

      {/* Asignar / cambiar categoría */}
      <Dialog open={!!assignTarget} onClose={() => setAssignTarget(null)} maxWidth="xs" fullWidth>
        <DialogTitle>Categoría de acreditación</DialogTitle>
        <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: '16px !important' }}>
          {assignTarget && (
            <Typography variant="body2" color="text.secondary">
              {assignTarget.firstName} {assignTarget.lastName}
            </Typography>
          )}
          {(categories || []).length === 0 && (
            <Alert severity="warning">Esta competencia todavía no tiene categorías de acreditación cargadas.</Alert>
          )}
          <TextField select label="Categoría" value={assignCategoryId} onChange={(e) => setAssignCategoryId(e.target.value)} fullWidth required>
            {(categories || []).map((c) => <MenuItem key={c.id} value={c.id}>{c.code} -- {c.name}</MenuItem>)}
          </TextField>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setAssignTarget(null)} disabled={assignSaving}>Cancelar</Button>
          <Button variant="contained" onClick={handleAssign} disabled={assignSaving || !assignCategoryId}>
            {assignSaving ? 'Guardando...' : 'Guardar'}
          </Button>
        </DialogActions>
      </Dialog>

      <OverridesDialog
        competitionId={compId}
        athleteId={overridesTarget?.athleteId}
        athleteName={overridesTarget ? `${overridesTarget.firstName} ${overridesTarget.lastName}` : ''}
        open={!!overridesTarget}
        onClose={() => setOverridesTarget(null)}
        onSaved={() => { setOverridesTarget(null); mutate(); }}
      />

      <CredentialPreviewDialog
        competitionId={compId}
        athleteId={previewTarget?.athleteId}
        athleteName={previewTarget ? `${previewTarget.firstName} ${previewTarget.lastName}` : ''}
        open={!!previewTarget}
        onClose={() => setPreviewTarget(null)}
      />
    </Box>
  );
}

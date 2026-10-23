import { useState } from 'react';
import Box from '@mui/material/Box';
import Paper from '@mui/material/Paper';
import Stack from '@mui/material/Stack';
import Chip from '@mui/material/Chip';
import Button from '@mui/material/Button';
import Typography from '@mui/material/Typography';
import TextField from '@mui/material/TextField';
import { DataGrid } from '@mui/x-data-grid';
import { toast } from 'sonner';
import { Iconify } from 'src/components/iconify';
import { useApi, apiPut } from 'src/hooks/use-api';
import { useRememberedChild } from 'src/hooks/use-remembered-child';
import { useCrudDialog } from 'src/hooks/use-crud';
import { endpoints, default as axios } from 'src/lib/axios';
import { downloadBlob } from 'src/lib/download-blob';
import { esIndividual } from 'src/lib/sport-shape';
import { bloquearNoEnteros, soloDigitos } from 'src/lib/entero-sin-signo';
import { CrudDialog } from 'src/components/crud-dialog';
import { EditDeleteActions } from 'src/components/edit-delete-actions';
import { DelegationImportDialog } from 'src/components/delegation-import-dialog';
import { EstadoChip } from 'src/components/estado-chip';
import { useConfirm } from 'src/components/confirm-dialog';
import { SelectionClub, SelectionAthletes, SelectionCategory } from 'src/components/selectors';
import { MembersPanel } from 'src/pages/teams/members-panel';
import { derivarVocabulario } from 'src/pages/teams/roster-vocabulary';

const emptyForm = () => ({ clubId: '', name: '', groupLabel: '', seed: '', isActive: true, athleteIds: [] });

/**
 * Los equipos (o las inscripciones individuales) de UNA categoría de UNA
 * competencia, ya elegida por quien llama -- a diferencia de TeamsPage (la
 * pantalla de siempre, con su propio selector de competencia), esto solo
 * elige la categoría, igual que CategoriesPanel no elige ni eso.
 *
 * El id de la categoría sí se eligie acá adentro -- a diferencia de
 * CategoriesPanel, esta pantalla no tiene una sola grilla "de la
 * competencia entera", siempre es "los equipos de esta categoría" -- con
 * useRememberedChild en vez de la cascada completa (useCascade): no hace
 * falta la competencia, que ya llega fija, ni el nivel de equipo, que
 * Nómina (todavía no migrada) es quien de verdad usa.
 */
export function TeamsPanel({ competitionId }) {
  const confirm = useConfirm();
  const { data: comps } = useApi(endpoints.competitions);
  const { data: sports } = useApi(endpoints.sports);
  const { data: categorias } = useApi(competitionId ? endpoints.categories(competitionId) : null);
  const [catId, setCatId] = useRememberedChild(categorias, competitionId, 'sportfrog:lastCategoryId:');

  // De que deporte es esta categoria decide toda la forma del alta: un
  // deporte individual (Sport.IsIndividual, taekwondo hoy) inscribe
  // deportistas via EnrollIndividual, no clubes via CreateTeam -- el backend
  // ya lo rechaza con un 409 si se lo pide al reves.
  const comp = comps?.find((c) => c.id === competitionId);
  const sport = sports?.find((s) => comp && s.code === comp.sportCode);
  const individual = esIndividual(sport);

  // Cuantos compiten juntos no es un dato del deporte -- Sport no tiene
  // ningun campo que lo diga, solo IsIndividual. Lo que si existe es el cupo
  // de la categoria (Category.MaxRosterSize: "cuantos puede registrar aca"),
  // que RosterPolicy ya hace cumplir. Kyorugi no tiene duplas, asi que sus
  // categorias van con cupo 1; Poomsae tiene individual (1), pareja (2) y
  // equipo (3), cada una su propia categoria con su propio cupo.
  const categoria = (categorias || []).find((c) => c.id === catId);
  const { cupo, soloDeportista, entityName, entityGender } = derivarVocabulario({ individual, sport, categoria });

  const {
    rows: teams, isLoading, mutate, open, editId, form, setForm, error, openCreate, openEdit, close, save, remove,
  } = useCrudDialog({
    resourceUrl: catId ? endpoints.teams(catId) : null,
    createUrl: individual ? endpoints.categoryIndividuals(catId) : endpoints.teams(catId),
    emptyForm,
    entityName,
    entityGender,
    savedMessage: soloDeportista ? 'Deportista inscrito.' : (individual ? 'Inscripción guardada.' : 'Equipo guardado.'),
    buildUrl: (base, id) => endpoints.team(id),
    mapToForm: (row) => ({ clubId: row.clubId || '', name: row.name || '', groupLabel: row.groupLabel || '', seed: row.seed ?? '', isActive: row.isActive !== false, athleteIds: [] }),
    mapToSend: (f, wasEdit) => {
      if (wasEdit) {
        return { name: f.name || null, groupLabel: f.groupLabel || null, seed: f.seed !== '' ? Number(f.seed) : null, isActive: f.isActive };
      }
      const seed = f.seed !== '' ? Number(f.seed) : null;
      return individual
        ? { athleteIds: f.athleteIds, clubId: f.clubId || null, groupLabel: f.groupLabel || null, seed }
        : { clubId: f.clubId, name: f.name || null, groupLabel: f.groupLabel || null, seed, isActive: f.isActive };
    },
  });

  const [selectedId, setSelectedId] = useState(null);
  const selected = (teams || []).find((t) => t.id === selectedId) || null;

  const [importOpen, setImportOpen] = useState(false);

  // El club cuya delegacion se baja/sube: mismo rol que `teamId` en
  // RosterPanel (lo que habilita Plantilla e Importar Excel), salvo que acá
  // no cuelga de la categoría -- una planilla de delegación cubre toda la
  // competencia, cada fila elige su propia categoría (ver
  // DelegationRosterImportGate). Una sola fuente de verdad para el selector
  // de aca abajo y para el que vive dentro del dialogo, en vez de elegir el
  // club dos veces.
  const [delegationClubId, setDelegationClubId] = useState('');

  const downloadDelegationTemplate = async () => {
    if (!delegationClubId) return;
    try {
      const res = await axios.get(
        endpoints.delegationRosterTemplate(competitionId, delegationClubId),
        { responseType: 'blob' },
      );
      downloadBlob(res, 'planilla-delegacion.xlsx');
    } catch (err) { toast.error(err.message); }
  };

  const toggleTeamActive = async (row, event) => {
    event.stopPropagation();
    if (row.isActive) {
      const ok = await confirm(
        `Desactivar "${row.name}"? Deja de sortearse y programarse en partidos nuevos. Lo que ya jugó no se ve afectado.`,
        { confirmLabel: 'Desactivar', danger: true },
      );
      if (!ok) return;
    }
    try {
      await apiPut(endpoints.team(row.id), {
        name: row.name || null,
        groupLabel: row.groupLabel || null,
        seed: row.seed ?? null,
        isActive: !row.isActive,
      });
      mutate();
    } catch (err) { toast.error(err.message); }
  };

  const columns = [
    { field: 'name', headerName: soloDeportista ? 'Deportista' : (individual ? 'Deportista(s)' : 'Nombre'), flex: 1, minWidth: 200 },
    { field: 'clubName', headerName: 'Club', width: 160 },
    { field: 'groupLabel', headerName: 'Grupo', width: 100, renderCell: ({ value }) => value ? <Chip label={value} size="small" /> : '--' },
    { field: 'seed', headerName: 'Bombo', width: 90, renderCell: ({ value }) => value != null ? value : '--' },
    { field: 'isActive', headerName: 'Estado', width: 110, sortable: false, renderCell: ({ value, row }) => (
      <EstadoChip activo={value} femenino={entityGender === 'f'} onClick={(e) => toggleTeamActive(row, e)} />
    )},
    { field: 'actions', headerName: 'Acciones', width: 90, align: 'center', headerAlign: 'center', renderCell: ({ row }) => (
      <EditDeleteActions onEdit={() => openEdit(row)} onDelete={() => remove(row.id)} />
    )},
  ];

  const grid = (
    <DataGrid
      rows={teams || []}
      columns={columns}
      loading={isLoading}
      autoHeight
      disableRowSelectionOnClick
      getRowId={(r) => r.id}
      {...(individual && !soloDeportista ? { onRowClick: (p) => setSelectedId(p.row.id), sx: { cursor: 'pointer' } } : {})}
    />
  );

  return (
    <Box>
      {/* Botones arriba, selector abajo -- mismo orden que RosterPanel, en
          vez de una pantalla con el selector primero y la otra al revés.
          Un solo Stack vertical para las dos filas, en vez de un mb suelto
          en cada una: así el espacio de arriba y el de abajo de los
          botones sale del mismo mecanismo (spacing) en vez de dos
          márgenes independientes que podrían no coincidir. */}
      <Stack spacing={2} sx={{ mb: 2 }}>
        {/* Mismo orden y mismos tres botones que RosterPanel (Plantilla,
            Importar Excel, alta): Plantilla e Importar Excel ya no viven
            escondidos dentro de un dialogo combinado, son acciones propias
            de la barra, habilitadas por el club elegido abajo -- igual que
            en Nomina las habilita el equipo elegido. */}
        <Stack direction="row" justifyContent="flex-end" spacing={1} flexWrap="wrap">
          {individual && (
            <>
              <Button
                variant="outlined"
                startIcon={<Iconify icon="eva:download-outline" />}
                onClick={downloadDelegationTemplate}
                disabled={!delegationClubId}
              >
                Plantilla
              </Button>
              <Button
                variant="outlined"
                startIcon={<Iconify icon="eva:upload-outline" />}
                onClick={() => setImportOpen(true)}
                disabled={!delegationClubId}
              >
                Importar Excel
              </Button>
            </>
          )}
          <Button
            variant="contained"
            startIcon={<Iconify icon="eva:plus-fill" />}
            onClick={openCreate}
            disabled={!catId}
          >
            {soloDeportista ? 'Inscribir deportista' : (individual ? 'Nueva inscripción' : 'Nuevo equipo')}
          </Button>
        </Stack>
        <Stack direction="row" spacing={2} flexWrap="wrap">
          <Box sx={{ minWidth: 220, maxWidth: 320 }}>
            <SelectionCategory competitionId={competitionId} value={catId} onChange={(e) => setCatId(e.target.value)} required />
          </Box>
          {individual && (
            <Box sx={{ minWidth: 220, maxWidth: 320 }}>
              <SelectionClub
                value={delegationClubId}
                onChange={(e) => setDelegationClubId(e.target.value)}
                helperText="Para bajar la plantilla o importar la delegación."
              />
            </Box>
          )}
        </Stack>
      </Stack>

      {individual && !soloDeportista ? (
        // Dos paneles, igual que Sedes con sus espacios: la inscripcion a la
        // izquierda, quienes la componen a la derecha. Solo tiene sentido
        // donde una inscripcion puede tener mas de una persona (la pareja o
        // el trio de Poomsae); con cupo 1 seria un panel para mostrar
        // siempre una sola fila con el mismo nombre que ya esta a la
        // izquierda, que es exactamente la redundancia que estamos sacando.
        <Box sx={{ display: 'flex', gap: 3, flexDirection: { xs: 'column', md: 'row' } }}>
          <Paper variant="outlined" sx={{ flex: 1, p: 2 }}>
            <Typography variant="h6" fontWeight={600} sx={{ mb: 1.5 }}>Inscripciones</Typography>
            {grid}
          </Paper>
          <MembersPanel team={selected} max={cupo} onChanged={mutate} />
        </Box>
      ) : grid}

      <CrudDialog open={open} editId={editId} entityName={soloDeportista ? 'Deportista' : (individual ? 'Inscripción' : 'Equipo')} entityGender={entityGender} error={error} onClose={close} onSave={save}>
        {!editId && (
          <SelectionClub
            value={form.clubId}
            onChange={(e) => setForm({ ...form, clubId: e.target.value })}
            required={!individual}
            emptyLabel={individual ? 'Sin delegación' : undefined}
          />
        )}
        {individual && !editId && (soloDeportista ? (
          <SelectionAthletes
            label="Deportista"
            multiple={false}
            value={form.athleteIds[0] || ''}
            onChange={(athleteId) => setForm({ ...form, athleteIds: athleteId ? [athleteId] : [] })}
            required
          />
        ) : (
          <SelectionAthletes
            value={form.athleteIds}
            onChange={(athleteIds) => setForm({ ...form, athleteIds })}
            helperText="Uno para un deportista solo, dos para una pareja, mas para un grupo mas grande."
            required
          />
        ))}
        {(!individual || editId) && (
          <TextField
            label={soloDeportista ? 'Nombre en el cuadro' : (individual ? 'Nombre de la inscripción' : (editId ? 'Nombre del equipo' : 'Nombre del equipo (vacio = nombre del club)'))}
            value={form.name}
            onChange={(e) => setForm({ ...form, name: e.target.value })}
            fullWidth
            helperText={editId ? undefined : 'Dejar vacio para usar el nombre del club'}
          />
        )}
        <TextField
          label="Bombo"
          type="number"
          value={form.seed}
          onChange={(e) => setForm({ ...form, seed: soloDigitos(e.target.value) })}
          onKeyDown={bloquearNoEnteros}
          fullWidth
          helperText={`Opcional. Para un sorteo de grupos por bombos: ${soloDeportista ? 'deportistas' : (individual ? 'inscripciones' : 'equipos')} del mismo bombo nunca caen en el mismo grupo.`}
          slotProps={{ htmlInput: { min: 1, max: 26 } }}
        />
      </CrudDialog>

      <DelegationImportDialog
        open={importOpen}
        onClose={() => setImportOpen(false)}
        competitionId={competitionId}
        clubId={delegationClubId}
        onApplied={() => mutate()}
      />
    </Box>
  );
}

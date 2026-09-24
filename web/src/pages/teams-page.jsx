import { useState } from 'react';
import { useSearchParams } from 'react-router';
import Box from '@mui/material/Box';
import Paper from '@mui/material/Paper';
import Chip from '@mui/material/Chip';
import Button from '@mui/material/Button';
import Typography from '@mui/material/Typography';
import TextField from '@mui/material/TextField';
import { DataGrid } from '@mui/x-data-grid';
import { toast } from 'sonner';
import { Iconify } from 'src/components/iconify';
import { useApi, apiPut } from 'src/hooks/use-api';
import { useCascade } from 'src/hooks/use-cascade';
import { useCrudDialog } from 'src/hooks/use-crud';
import { endpoints } from 'src/lib/axios';
import { esIndividual } from 'src/lib/sport-shape';
import { bloquearNoEnteros, soloDigitos } from 'src/lib/entero-sin-signo';
import { PageHeader } from 'src/components/page-header';
import { CrudDialog } from 'src/components/crud-dialog';
import { EditDeleteActions } from 'src/components/edit-delete-actions';
import { CascadeFilters } from 'src/components/cascade-filters';
import { DelegationImportDialog } from 'src/components/delegation-import-dialog';
import { EstadoChip } from 'src/components/estado-chip';
import { useConfirm } from 'src/components/confirm-dialog';
import { SelectionClub, SelectionAthletes } from 'src/components/selectors';
import { MembersPanel } from 'src/pages/teams/members-panel';

const emptyForm = () => ({ clubId: '', name: '', groupLabel: '', seed: '', isActive: true, athleteIds: [] });

export default function TeamsPage() {
  // Un id que vino por link -- Nomina manda para aca cuando la competencia
  // elegida es de un deporte individual, porque ahi esta pantalla es la
  // unica que administra inscripciones. La categoria no viaja en la URL: la
  // cascada ya la recuerda por competencia (useRememberedChild).
  const [searchParams] = useSearchParams();
  const confirm = useConfirm();
  const cascade = useCascade(searchParams.get('competition'));
  const { data: sports } = useApi(endpoints.sports);

  // De que deporte es esta categoria decide toda la forma del alta: un
  // deporte individual (Sport.IsIndividual, taekwondo hoy) inscribe
  // deportistas via EnrollIndividual, no clubes via CreateTeam -- el backend
  // ya lo rechaza con un 409 si se lo pide al reves.
  const comp = cascade.competiciones.find((c) => c.id === cascade.compId);
  const sport = sports?.find((s) => comp && s.code === comp.sportCode);
  const individual = esIndividual(sport);

  // Cuantos compiten juntos no es un dato del deporte -- Sport no tiene
  // ningun campo que lo diga, solo IsIndividual. Lo que si existe es el cupo
  // de la categoria (Category.MaxRosterSize: "cuantos puede registrar aca"),
  // que RosterPolicy ya hace cumplir. Kyorugi no tiene duplas, asi que sus
  // categorias van con cupo 1; Poomsae tiene individual (1), pareja (2) y
  // equipo (3), cada una su propia categoria con su propio cupo.
  //
  // Se lee de ahi en vez de mirar el codigo del deporte a proposito: un
  // `sportCode === 'taekwondo_kyorugi'` escrito en el frontend queda viejo
  // apenas el catalogo suma otro deporte individual, y ademas duplicaria en
  // la pantalla una regla que el backend ya tiene.
  const categoria = cascade.categorias.find((c) => c.id === cascade.catId);

  // Cuantos pueden competir juntos sale de dos lugares, y manda el menor: el
  // techo del deporte (Sport.MaxEntrySize -- Kyorugi 1, Poomsae 3) y el cupo
  // de la categoria, que puede achicarlo (Poomsae individual dentro del techo
  // de trio) pero nunca ensancharlo. Es la misma cuenta que hace RosterPolicy
  // del lado del servidor, que es quien de verdad lo hace cumplir.
  //
  // Sin nada declarado se asume individual: en un deporte individual la
  // enorme mayoria de las categorias se compiten de a uno, y entre
  // equivocarse mostrando un panel de integrantes que sobra o escondiendo uno
  // que hace falta, lo segundo se nota y se corrige eligiendo la modalidad,
  // mientras que lo primero deja a la vista una capa del modelo que
  // justamente estamos tratando de no mostrar.
  const cupos = [sport?.maxEntrySize, categoria?.maxRosterSize].filter((v) => v != null);
  const cupo = cupos.length > 0 ? Math.min(...cupos) : 1;
  const soloDeportista = individual && cupo === 1;

  // "Equipo" es el nombre correcto del lado del backend (ver el comentario
  // de Team.cs: "la unidad que compite", no un sinonimo de club) pero desde
  // el frontend eso se lee raro en un deporte individual: para el usuario,
  // una inscripcion de una sola persona *es* el deportista, y llamarla
  // equipo -- o incluso "inscripcion" -- es hablarle de una capa interna que
  // no le importa. El backend no se entera de este cambio de vocabulario:
  // mismos endpoints, mismo contrato, mismo Team por debajo.
  const entityName = soloDeportista ? 'deportista' : (individual ? 'inscripción' : 'equipo');
  const entityGender = individual && !soloDeportista ? 'f' : 'm';

  const {
    rows: teams, isLoading, mutate, open, editId, form, setForm, error, openCreate, openEdit, close, save, remove,
  } = useCrudDialog({
    resourceUrl: cascade.catId ? endpoints.teams(cascade.catId) : null,
    createUrl: individual ? endpoints.categoryIndividuals(cascade.catId) : endpoints.teams(cascade.catId),
    emptyForm,
    entityName,
    entityGender,
    savedMessage: soloDeportista ? 'Deportista inscrito.' : (individual ? 'Inscripción guardada.' : 'Equipo guardado.'),
    buildUrl: (base, id) => endpoints.team(id),
    // La edicion no cambia con el deporte: UpdateTeam es el mismo contrato
    // para los dos casos, y ni club ni deportistas se tocan aca (el club no
    // se puede corregir una vez inscripto -- ver el comentario de UpdateTeam
    // en el backend -- y los integrantes se administran en el panel de la
    // derecha, no en este dialogo).
    mapToForm: (row) => ({ clubId: row.clubId || '', name: row.name || '', groupLabel: row.groupLabel || '', seed: row.seed ?? '', isActive: row.isActive !== false, athleteIds: [] }),
    mapToSend: (f, wasEdit) => {
      if (wasEdit) {
        return { name: f.name || null, groupLabel: f.groupLabel || null, seed: f.seed !== '' ? Number(f.seed) : null, isActive: f.isActive };
      }
      const seed = f.seed !== '' ? Number(f.seed) : null;
      // Sin club elegido, EnrollIndividual entra al deportista bajo el club
      // no afiliado de la organizacion -- una forma completamente ordinaria
      // de inscribirse en un deporte donde una inscripcion no tiene por que
      // pertenecer a ninguna delegacion.
      return individual
        ? { athleteIds: f.athleteIds, clubId: f.clubId || null, groupLabel: f.groupLabel || null, seed }
        : { clubId: f.clubId, name: f.name || null, groupLabel: f.groupLabel || null, seed, isActive: f.isActive };
    },
  });

  // Solo el id, no la fila entera: sumar o retirar un integrante recalcula el
  // nombre de la inscripcion (IndividualTeamName), asi que guardar el objeto
  // dejaria el titulo del panel mostrando el nombre viejo. Derivarlo de la
  // lista tambien lo limpia solo si la inscripcion se elimina o si se cambia
  // de categoria.
  const [selectedId, setSelectedId] = useState(null);
  const selected = (teams || []).find((t) => t.id === selectedId) || null;

  const [importOpen, setImportOpen] = useState(false);

  // Mismo patron que VenuesPage/AthletesPage: activar es inofensivo y
  // reversible con el mismo click, pero desactivar saca la inscripcion del
  // sorteo y de la programacion de partidos nuevos (ver UpdateTeam.IsActive
  // y FixturePolicy en el backend) -- vale la pena avisar antes, no solo
  // cambiar el estado en silencio. Lo que ya jugo no se ve afectado.
  // Reenvia name/groupLabel/seed junto con isActive porque UpdateTeam.Request
  // los exige todos juntos, igual que mapToSend en la edicion de arriba.
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
      // La fila solo tiene un detalle que mirar cuando la inscripcion puede
      // tener mas de una persona: ahi hay integrantes que administrar. Con
      // cupo 1 la fila *es* el deportista y no hay nada abajo que abrir; en
      // un deporte de equipo eso vive en Nomina, con su dorsal y su
      // posicion, y esta pantalla no cambia en nada.
      {...(individual && !soloDeportista ? { onRowClick: (p) => setSelectedId(p.row.id), sx: { cursor: 'pointer' } } : {})}
    />
  );

  return (
    <Box>
      <PageHeader
        title={soloDeportista ? 'Deportistas inscritos' : (individual ? 'Inscripciones' : 'Equipos')}
        actionLabel={soloDeportista ? 'Inscribir deportista' : (individual ? 'Nueva inscripción' : 'Nuevo equipo')}
        onAction={openCreate}
        actionDisabled={!cascade.catId}
      >
        {/* El Excel de delegacion se pide por competencia + club, no por
            equipo, y cada fila crea su propio ingreso: es un alta masiva de
            inscripciones, asi que vive donde viven las altas. */}
        {individual && (
          <Button variant="outlined" startIcon={<Iconify icon="eva:upload-outline" />} onClick={() => setImportOpen(true)} disabled={!cascade.compId}>
            Importar Excel (delegación)
          </Button>
        )}
      </PageHeader>

      <CascadeFilters cascade={cascade} />

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
        {/* El club no se puede corregir editando -- UpdateTeam no lo acepta,
            así que mostrarlo ahí sería un campo que parece editable y no lo
            es. Al crear, sigue siendo obligatorio para un club (CreateTeam
            lo exige); para un deporte individual es la delegación, y queda
            opcional -- sin elegir ninguno, EnrollIndividual entra al
            deportista bajo el club no afiliado. */}
        {!editId && (
          <SelectionClub
            value={form.clubId}
            onChange={(e) => setForm({ ...form, clubId: e.target.value })}
            required={!individual}
            emptyLabel={individual ? 'Sin delegación' : undefined}
          />
        )}
        {/* Con cupo 1 se elige una sola persona: ofrecer un multi-select
            ahi seria invitar a armar algo que RosterPolicy va a rechazar.
            El formulario guarda igual un arreglo en los dos casos, asi
            mapToSend no tiene que saber cual de los dos se dibujo --
            EnrollIndividual recibe athleteIds siempre. */}
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
        {/* El nombre lo arma el backend a partir de los deportistas elegidos
            (IndividualTeamName) mientras no hay ninguno inscripto todavia --
            mostrar un campo de texto aca no tendria nada que editar. Al
            corregir, en cambio, es el mismo campo de siempre. */}
        {(!individual || editId) && (
          <TextField
            label={soloDeportista ? 'Nombre en el cuadro' : (individual ? 'Nombre de la inscripción' : (editId ? 'Nombre del equipo' : 'Nombre del equipo (vacio = nombre del club)'))}
            value={form.name}
            onChange={(e) => setForm({ ...form, name: e.target.value })}
            fullWidth
            // UpdateTeam exige el nombre al corregir -- a diferencia de
            // CreateTeam, ahi no hay club al que recurrir si se deja vacio.
            helperText={editId ? undefined : 'Dejar vacio para usar el nombre del club'}
          />
        )}
        {/* El grupo ya no se tipea a mano: lo completa el sorteo de grupos
            (ver Fixtures/Partidos). El campo seguia en el formulario aunque
            eso ya lo resolvia solo — se saca de aca, pero el valor que ya
            tenga (asignado por un sorteo anterior) no se toca:
            form.groupLabel sigue viajando en el guardado, solo que ya no hay
            forma de escribirlo a mano. */}
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
        {/* El estado activo/inactivo ya se alterna desde la columna "Estado"
            de la grilla (EstadoChip) -- repetirlo acá era un segundo control
            para lo mismo. form.isActive sigue viajando igual en el guardado
            (mapToSend lo exige junto con name/groupLabel/seed), solo que ya
            no hay forma de tocarlo desde este diálogo. */}
      </CrudDialog>

      <DelegationImportDialog
        open={importOpen}
        onClose={() => setImportOpen(false)}
        competitionId={cascade.compId}
        // Sin la funcion-flecha: mutate(x) usa x como reemplazo de la cache
        // sin revalidar (asi es como SWR deja "optimistic update" un dato
        // que ya se tiene a mano), y DelegationImportDialog llama a
        // onApplied con la respuesta del import -- un resumen (rows,
        // registered, total...), no un array de equipos. Pasarla directo
        // dejaba `teams` apuntando a ese objeto en vez de a la lista.
        onApplied={() => mutate()}
      />
    </Box>
  );
}

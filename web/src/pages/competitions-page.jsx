import { useState, useCallback, useRef } from 'react';
import { useNavigate } from 'react-router';
import { useCrudDialog } from 'src/hooks/use-crud';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Chip from '@mui/material/Chip';
import IconButton from '@mui/material/IconButton';
import Tooltip from '@mui/material/Tooltip';
import TextField from '@mui/material/TextField';
import MenuItem from '@mui/material/MenuItem';
import Switch from '@mui/material/Switch';
import FormControlLabel from '@mui/material/FormControlLabel';
import Accordion from '@mui/material/Accordion';
import AccordionSummary from '@mui/material/AccordionSummary';
import AccordionDetails from '@mui/material/AccordionDetails';
import Typography from '@mui/material/Typography';
import Stack from '@mui/material/Stack';
import Divider from '@mui/material/Divider';
import Alert from '@mui/material/Alert';
import { DataGrid } from '@mui/x-data-grid';
import { Iconify } from 'src/components/iconify';
import { useApi, apiPost } from 'src/hooks/use-api';
import { endpoints } from 'src/lib/axios';
import { EMPTY_PORTAL_FORM, readPortalForm, buildPortalPayload } from 'src/pages/competitions/portal-payload';
import { aSlug, normalizarSlug, problemaDeSlug, slugDeOrganizacion, SLUG_MAX } from 'src/lib/slug';
import { bloquearNoEnteros, soloDigitos } from 'src/lib/entero-sin-signo';
import { PageHeader } from 'src/components/page-header';
import { CrudDialog } from 'src/components/crud-dialog';
import { RowActionsMenu } from 'src/components/row-actions-menu';
import { EstadoChip } from 'src/components/estado-chip';
import { SelectionSpace } from 'src/components/selectors';
import {
  STATUS_LABEL, STATUS_COLOR, STATUS_ICON, NEXT_STATUS, FORMAT_LABEL, motivoPublicacionInhabilitada,
} from 'src/pages/competitions/competition-lifecycle';
import { useCompetitionActions } from 'src/pages/competitions/use-competition-actions';
import { buildDefaultRulesetConfig, defaultRulesetName } from 'src/pages/rulesets/default-ruleset';

const FORMATS = ['league', 'knockout', 'groups'];
const CAPS = ['basic', 'detailed'];
const FORMATO_INFO = {
  league: 'Todos juegan contra todos.',
  knockout: 'El que pierde queda afuera.',
  groups: 'Primero zonas, despues llaves.',
};

// MUI resetea el borde del Accordion a 90° (border-radius: 0) salvo que sea
// el primero/último de su tipo entre hermanos del mismo tag -- que acá no lo
// es, porque comparte contenedor con los TextField del formulario. Sin este
// radio, el "outlined" queda un cuadrado de esquinas rectas que desentona
// con el resto de la app (Card, Button, DataGrid: todos redondeados).
// overflow: 'hidden' recorta el tinte de ACCORDION_SUMMARY_SX a esas mismas
// esquinas en vez de dejarlo asomar en punta por debajo.
//
// flexShrink: 0 es el que evita que el acordeón se recorte: el Accordion es
// un item flex dentro de la columna del DialogContent (CrudDialog), y la
// propia regla de CSS que resuelve el tamaño mínimo de un item flex dice que
// cuando tiene overflow distinto de "visible" -- que ahora tiene, por la
// línea de arriba -- ese mínimo pasa a ser 0 en vez de "lo que ocupa su
// contenido". Sin flexShrink:0, un Accordion expandido con más contenido del
// que entra en el modal se aprieta hasta lo que sobre en vez de empujar al
// diálogo a scrollear -- por eso se veía como si tuviera un alto fijo que
// recortaba lo de más abajo.
const ACCORDION_SX = { borderRadius: 1, overflow: 'hidden', flexShrink: 0, '&:before': { display: 'none' } };

// Un fondo propio en la cabecera del accordion: "outlined" a secas se
// confunde con un panel fijo (el borde es igual al de cualquier TextField de
// al lado), y la flecha sola no basta para que se note que es desplegable.
// Con un tinte y un hover más marcado, la cabecera se lee como una barra
// clickeable antes de que el usuario repare en el ícono.
const ACCORDION_SUMMARY_SX = {
  bgcolor: 'action.hover',
  borderRadius: 1,
  '&:hover': { bgcolor: 'action.selected' },
  '&.Mui-expanded': { bgcolor: 'action.selected', borderBottomLeftRadius: 0, borderBottomRightRadius: 0 },
};

// La personalizacion del portal (colores, tipografia, portada, redes,
// auspiciantes) se edita en su propia pantalla con vista previa -- el estudio
// de portal, /dashboard/competitions/:id/portal. Se llega ahi de dos formas:
// el icono de paleta suelto en la fila (directo, sin abrir el dialogo -- ver
// columna "actions" mas abajo), o el mismo atajo adentro del accordion
// "Portal publico" del dialogo de edicion (util si ya se esta ahi por otro
// motivo). Aca en el dialogo solo quedan los tres interruptores de
// secciones, que son de una linea. La lectura y el armado del cuerpo
// settings.public los comparte con el estudio en portal-payload.js para que
// los dos no se pisen.
const emptyForm = () => ({
  name: '', slug: '', season: '', format: 'league', sportCode: '', rulesetId: '', captureLevel: 'basic', credentialDesignId: '',
  bufferMinutes: '', scheduleSpaceIds: [],
  bulletinIntroduction: '', bulletinSanctions: '', bulletinGeneralProvisions: '', bulletinContactInfo: '',
  ...EMPTY_PORTAL_FORM,
});

export default function CompetitionsPage() {
  const navigate = useNavigate();
  const { data: rulesets } = useApi(endpoints.rulesets);
  const { data: sports } = useApi(endpoints.sports);
  const { data: designs } = useApi(endpoints.credentialDesigns);
  const nombreDeporte = (code) => sports?.find((s) => s.code === code)?.name || code;

  const [slugTouched, setSlugTouched] = useState(false);

  // 0-indexado, como lo pide DataGrid -- skip/take (lo que el backend
  // realmente entiende, ver PagedListing) se arman a partir de esto adentro
  // de useCrudDialog.
  const [paginationModel, setPaginationModel] = useState({ page: 0, pageSize: 25 });

  // Un ref y no un estado: lo escribe save() justo antes de guardar y lo lee
  // savedMessage() del otro lado de ese mismo guardado -- ninguno de los dos
  // necesita volver a renderizar nada por esto, así que un estado de React
  // sería puro ruido.
  const reglamentoCreadoRef = useRef(null);

  const {
    rows: data, rowCount, isLoading, mutate, open, editId, form, setForm, error, setError, saving, openCreate: openCreateBase,
    openEdit: openEditBase, close, save: saveBase, remove,
  } = useCrudDialog({
    resourceUrl: endpoints.competitions,
    pageParams: { skip: paginationModel.page * paginationModel.pageSize, take: paginationModel.pageSize },
    emptyForm,
    entityName: 'competencia',
    entityGender: 'f',
    buildUrl: (base, id) => endpoints.competition(id),
    savedMessage: (result, wasEdit) => {
      if (wasEdit) return 'Competencia guardada.';

      const nombreReglamento = reglamentoCreadoRef.current;
      reglamentoCreadoRef.current = null;

      if (!nombreReglamento) return 'Competencia creada. Ahora agregale al menos una categoria.';

      return {
        message: `Competencia creada junto con el reglamento "${nombreReglamento}". ` +
          'Ahora agregale al menos una categoria.',
        options: { action: { label: 'Ver reglamentos', onClick: () => navigate('/dashboard/rulesets') } },
      };
    },
    // El resumen de la competencia (overview-page.jsx) es lo que antes
    // faltaba entre crear una competencia y saber qué hacer con ella: su
    // propio checklist ya señala "agregá una categoría" como el primer paso
    // pendiente, así que no hace falta elegir de antemano a cuál de las
    // pantallas siguientes (Categorías, Portal) conviene mandar a alguien
    // que recién la creó.
    onSaved: (result, wasEdit) => {
      if (!wasEdit) navigate(`/dashboard/competitions/${result.id}`);
    },
    mapToForm: (row) => {
      const sched = (row.settings && row.settings.schedule) || {};
      const bulletin = (row.settings && row.settings.bulletin) || {};
      return {
        name: row.name, slug: row.slug, season: row.season, format: row.format, sportCode: row.sportCode,
        rulesetId: row.rulesetId || '', captureLevel: row.captureLevel, credentialDesignId: row.credentialDesignId || '',
        bufferMinutes: sched.bufferMinutes ?? '',
        scheduleSpaceIds: sched.spaceIds || [],
        bulletinIntroduction: bulletin.introduction || '',
        bulletinSanctions: bulletin.sanctions || '',
        bulletinGeneralProvisions: bulletin.generalProvisions || '',
        bulletinContactInfo: bulletin.contactInfo || '',
        // Colores, portada, redes y auspiciantes: se leen aca para reenviarlos
        // sin tocar (el PUT reemplaza settings.public entero), pero se editan
        // en el estudio de portal.
        ...readPortalForm(row),
      };
    },
    mapToSend: (f) => ({
      name: f.name, slug: normalizarSlug(f.slug), season: f.season,
      format: f.format, rulesetId: f.rulesetId, captureLevel: f.captureLevel, credentialDesignId: f.credentialDesignId || null,
      settings: {
        // Vacio de margen y de canchas es "no configurado todavia": se manda
        // null en vez de un objeto a medio llenar, para no perder el aviso
        // propio de ScheduleCalendar ("esta competencia no tiene canchas
        // habilitadas") por uno generico de validacion. La duracion del
        // partido en si ya no vive aca -- sale entera del reglamento, con o
        // sin reloj (ver rulesets-page.jsx).
        schedule: (f.bufferMinutes !== '' || f.scheduleSpaceIds.length) ? {
          bufferMinutes: f.bufferMinutes !== '' ? Number(f.bufferMinutes) : null,
          spaceIds: f.scheduleSpaceIds.length ? f.scheduleSpaceIds : null,
        } : null,
        // Igual criterio que schedule: nada cargado todavia es null, no un
        // objeto con las cuatro claves vacias.
        bulletin: (f.bulletinIntroduction || f.bulletinSanctions || f.bulletinGeneralProvisions || f.bulletinContactInfo) ? {
          introduction: f.bulletinIntroduction || null,
          sanctions: f.bulletinSanctions || null,
          generalProvisions: f.bulletinGeneralProvisions || null,
          contactInfo: f.bulletinContactInfo || null,
        } : null,
        public: buildPortalPayload(f),
      },
    }),
  });

  const slugError = form.slug || slugTouched ? problemaDeSlug(form.slug) : null;
  const slugHelp = `/${slugDeOrganizacion()}/${normalizarSlug(form.slug) || '...'}`;

  // El deporte elige el reglamento, nunca al revés (ver el comentario de
  // CompetitionContract del lado del servidor sobre por qué el sport no se
  // manda por separado): esta lista es la única razón de ser del select de
  // Deporte en el diálogo.
  const deporteElegido = (sports || []).find((s) => s.code === form.sportCode);
  const reglamentosDelDeporte = (rulesets || []).filter((r) => r.sportCode === form.sportCode);
  const deporteSinReglamentos = !!form.sportCode && reglamentosDelDeporte.length === 0;

  const openDialog = useCallback((row) => {
    setSlugTouched(!!row);
    (row ? openEditBase : openCreateBase)(row);
  }, [openEditBase, openCreateBase]);

  // El slug no es un campo mas: hay que validarlo antes de que el pedido
  // salga, y marcarlo "tocado" para que su error se muestre si todavia no lo
  // estaba (por ejemplo, guardando sin haber escrito nunca el nombre).
  //
  // Si la organización no tiene todavía ningún reglamento de este deporte
  // (deporteSinReglamentos, reflejado en el diálogo por el Alert en vez del
  // select de Reglamento), guardar crea uno estándar primero -- con los
  // mismos valores de partida que armaría el formulario de Reglamentos para
  // ese deporte, ver buildDefaultRulesetConfig -- y recién entonces crea la
  // competencia apuntando a él. `overrides` existe exactamente para este
  // caso: el id nuevo tiene que llegar al cuerpo de este guardado, no al del
  // próximo render (ver el comentario de useCrudDialog.save).
  const save = async () => {
    const problem = problemaDeSlug(form.slug);
    if (problem) { setSlugTouched(true); setError(problem); return; }

    let overrides;

    if (!form.rulesetId) {
      if (!deporteElegido) { setError('Elegí un deporte.'); return; }

      if (!deporteSinReglamentos) { setError('Elegí un reglamento.'); return; }

      try {
        const nombre = defaultRulesetName(deporteElegido);
        const creado = await apiPost(endpoints.rulesets, {
          sportCode: deporteElegido.code,
          name: nombre,
          config: buildDefaultRulesetConfig(deporteElegido),
        });
        reglamentoCreadoRef.current = nombre;
        overrides = { rulesetId: creado.id };
        setForm((f) => ({ ...f, rulesetId: creado.id }));
      } catch (err) {
        setError(err.message || 'No se pudo crear el reglamento por defecto.');
        return;
      }
    }

    await saveBase(overrides);
  };

  const agregarCancha = () => setForm((f) => ({ ...f, scheduleSpaceIds: [...f.scheduleSpaceIds, ''] }));

  const quitarCancha = (i) => setForm((f) => ({ ...f, scheduleSpaceIds: f.scheduleSpaceIds.filter((_, idx) => idx !== i) }));

  const cambiarCancha = (i, venueSpaceId) => setForm((f) => ({
    ...f, scheduleSpaceIds: f.scheduleSpaceIds.map((id, idx) => (idx === i ? venueSpaceId : id)),
  }));

  const handleNameChange = (e) => {
    const name = e.target.value;
    setForm((f) => ({ ...f, name, slug: slugTouched ? f.slug : aSlug(name) }));
  };

  const handleSlugChange = (e) => {
    setSlugTouched(true);
    setForm((f) => ({ ...f, slug: normalizarSlug(e.target.value) }));
  };

  const { changeStatus, togglePublish, descargarConvocatoria } = useCompetitionActions(mutate);

  const columns = [
    {
      field: 'name',
      headerName: 'Nombre',
      flex: 1,
      minWidth: 180,
      // Clickeable hacia el resumen de la competencia (overview-page.jsx):
      // es la puerta de entrada a Categorías, Partidos y Portal ya con esta
      // competencia elegida, sin ocupar un ícono más en "Acciones".
      renderCell: ({ value, row }) => (
        <Box
          onClick={() => navigate(`/dashboard/competitions/${row.id}`)}
          sx={{ cursor: 'pointer', color: 'primary.main', fontWeight: 600, '&:hover': { textDecoration: 'underline' } }}
        >
          {value}
        </Box>
      ),
    },
    { field: 'sportCode', headerName: 'Deporte', width: 110, renderCell: ({ value }) => nombreDeporte(value) },
    { field: 'season', headerName: 'Temporada', width: 120 },
    { field: 'format', headerName: 'Formato', width: 110, renderCell: ({ value }) => FORMAT_LABEL[value] || value },
    { field: 'status', headerName: 'Estado', width: 190, align: 'center', headerAlign: 'center', renderCell: ({ value, row }) => (
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'center', height: '100%', gap: 0.75 }}>
        <Chip label={STATUS_LABEL[value] || value} color={STATUS_COLOR[value] || 'default'} size="small" />
        {row.categoryCount === 0 && (
          <Tooltip title="Sin categorias todavia: no se puede sortear ni programar. Agregale una desde Categorias.">
            <Chip
              label="Sin categorias"
              color="warning"
              variant="outlined"
              size="small"
              icon={<Iconify icon="eva:alert-triangle-outline" width={14} />}
              onClick={() => navigate(`/dashboard/categories?competition=${row.id}`)}
              sx={{ cursor: 'pointer' }}
            />
          </Tooltip>
        )}
      </Box>
    )},
    { field: 'isPublic', headerName: 'Pública', width: 80, renderCell: ({ value, row }) => {
      const motivo = motivoPublicacionInhabilitada(row);

      return (
        <EstadoChip
          activo={value}
          onClick={() => togglePublish(row.id, value)}
          onLabel="Si" offLabel="No"
          onIcon="eva:globe-fill" offIcon="eva:eye-off-outline"
          onTooltip="Alternar publicación" offTooltip="Alternar publicación"
          disabled={!!motivo}
          disabledTooltip={motivo}
        />
      );
    }},
    { field: 'actions', headerName: 'Acciones', width: 150, align: 'center', headerAlign: 'center', renderCell: ({ row }) => {
      const next = NEXT_STATUS[row.status] || [];
      return (
        <RowActionsMenu
          primary={[
            { icon: 'eva:edit-fill', label: 'Editar', onClick: () => openDialog(row) },
            // Sacado del "..." y puesto suelto a pedido -- "muy oculta" seguia
            // siendo la queja aun a un clic del menu. El color propio (en vez
            // de heredar el gris de Editar/Eliminar) es lo que hace que la
            // etiqueta no haga falta leerla para notar que hay una tercera
            // accion nueva ahi.
            { icon: 'mdi:palette-outline', label: 'Personalizar portal', color: 'secondary.main', onClick: () => navigate(`/dashboard/competitions/${row.id}/portal`) },
            { icon: 'eva:trash-2-outline', label: 'Eliminar', color: 'error.main', onClick: () => remove(row.id) },
          ]}
          actions={[
            ...next.map((s) => ({
              icon: STATUS_ICON[s] || 'eva:arrow-right-fill',
              label: STATUS_LABEL[s],
              // "default" no es una paleta real de MUI fuera de Chip -- un
              // volver a Borrador con color: 'default.main' no pintaba nada.
              color: STATUS_COLOR[s] === 'default' ? 'text.secondary' : `${STATUS_COLOR[s]}.main`,
              onClick: () => changeStatus(row.id, s),
            })),
            { icon: 'mdi:file-pdf-box', label: 'Descargar convocatoria (PDF)', onClick: () => descargarConvocatoria(row.id, 'pdf') },
            { icon: 'mdi:file-word-box', label: 'Descargar convocatoria (Word)', onClick: () => descargarConvocatoria(row.id, 'docx') },
          ].filter(Boolean)}
        />
      );
    }},
  ];

  return (
    <Box>
      <PageHeader title="Competiciones" actionLabel="Nueva" onAction={() => openDialog(null)} />
      <DataGrid
        rows={data || []}
        columns={columns}
        loading={isLoading}
        autoHeight
        disableRowSelectionOnClick
        getRowId={(r) => r.id}
        paginationMode="server"
        rowCount={rowCount}
        paginationModel={paginationModel}
        onPaginationModelChange={setPaginationModel}
        pageSizeOptions={[25, 50, 100]}
      />
      <CrudDialog open={open} editId={editId} entityName="Competición" entityGender="f" error={error} saving={saving} onClose={close} onSave={save} maxWidth="md">
        <TextField label="Nombre" value={form.name} onChange={handleNameChange} fullWidth required />
        {/* La direccion queda fija desde que se crea: cambiarla romperia
            cualquier enlace ya compartido, y el backend la rechaza (ver
            UpdateCompetition). Editando, se muestra pero no se toca. */}
        <TextField
          label="Direccion pública (slug)"
          value={form.slug}
          onChange={handleSlugChange}
          fullWidth
          required
          disabled={!!editId}
          error={!editId && !!slugError}
          helperText={editId ? 'No se puede cambiar una vez creada la competencia.' : (slugError || slugHelp)}
          slotProps={{ htmlInput: { maxLength: SLUG_MAX, spellCheck: false, autoCapitalize: 'none' } }}
        />
        <TextField label="Temporada" value={form.season} onChange={(e) => setForm({ ...form, season: e.target.value })} fullWidth required />
        <TextField select label="Formato" value={form.format} onChange={(e) => setForm({ ...form, format: e.target.value })} fullWidth helperText={FORMATO_INFO[form.format]}>
          {FORMATS.map((f) => <MenuItem key={f} value={f}>{f === 'league' ? 'Todos vs todos' : f === 'knockout' ? 'Eliminacion' : 'Grupos'}</MenuItem>)}
        </TextField>
        <TextField
          select
          label="Deporte"
          value={form.sportCode}
          // Cambiar de deporte deja un reglamento elegido de otro deporte sin
          // sentido -- se limpia junto con el cambio en vez de esperar a que
          // el backend lo rechace.
          onChange={(e) => setForm((f) => ({ ...f, sportCode: e.target.value, rulesetId: '' }))}
          fullWidth
          required
        >
          <MenuItem value="">Seleccionar</MenuItem>
          {(sports || []).map((s) => <MenuItem key={s.code} value={s.code}>{s.name}</MenuItem>)}
        </TextField>
        {deporteSinReglamentos ? (
          <Alert severity="info">
            Esta organización todavía no tiene un reglamento de {deporteElegido.name}. Al guardar se
            va a crear uno estándar («{defaultRulesetName(deporteElegido)}») que vas a poder ajustar
            o reemplazar después desde Reglamentos.
          </Alert>
        ) : (
          <TextField
            select
            label="Reglamento"
            value={form.rulesetId}
            onChange={(e) => setForm({ ...form, rulesetId: e.target.value })}
            fullWidth
            required
            disabled={!form.sportCode}
            helperText={!form.sportCode ? 'Elegí un deporte primero.' : undefined}
          >
            <MenuItem value="">Seleccionar</MenuItem>
            {reglamentosDelDeporte.map((r) => <MenuItem key={r.id} value={r.id}>{r.name}</MenuItem>)}
          </TextField>
        )}
        <TextField
          select
          label="Captura"
          value={form.captureLevel}
          onChange={(e) => setForm({ ...form, captureLevel: e.target.value })}
          fullWidth
          // El reglamento elegido puede definir qué métricas se registran
          // (goles, tarjetas...), pero esa lista no sirve de nada si la
          // captura queda en Básica: ahí no se carga ningún evento, así que
          // tampoco hay tabla de goleadores ni línea de tiempo del partido
          // -- ver EventPolicy.RefuseMatch del lado del servidor, que
          // rechaza cualquier evento mientras esto no sea Detallado. Y a
          // diferencia del reglamento, que se puede ajustar más adelante,
          // esto queda fijo apenas la competencia sale de Borrador.
          helperText={
            form.captureLevel === 'basic'
              ? 'Solo se guarda el resultado final. Ningún evento del partido se registra -- sin goleadores, sin línea de tiempo -- aunque el reglamento elegido los tenga previstos. Fijo una vez que la competencia sale de Borrador.'
              : 'Permite cargar los eventos del partido (goles, tarjetas...) por jugador. Fijo una vez que la competencia sale de Borrador.'
          }
        >
          {CAPS.map((c) => <MenuItem key={c} value={c}>{c === 'basic' ? 'Basico' : 'Detallado'}</MenuItem>)}
        </TextField>
        <TextField
          select
          label="Diseño de credencial"
          value={form.credentialDesignId}
          onChange={(e) => setForm({ ...form, credentialDesignId: e.target.value })}
          fullWidth
          helperText="Vacío usa el diseño predeterminado de la organización."
        >
          <MenuItem value="">Predeterminado de la organización</MenuItem>
          {(designs || []).map((d) => <MenuItem key={d.id} value={d.id}>{d.name}{d.isDefault ? ' (predeterminado)' : ''}</MenuItem>)}
        </TextField>

        <Accordion disableGutters variant="outlined" sx={ACCORDION_SX}>
          <AccordionSummary sx={ACCORDION_SUMMARY_SX} expandIcon={<Iconify icon="eva:chevron-down-fill" />}>
            <Typography variant="subtitle2">Disponibilidad para programar el calendario</Typography>
          </AccordionSummary>
          <AccordionDetails sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
            {/* La duración del partido ya no se toca acá para ningún deporte
                -- sale entera del reglamento de cada categoría, con reloj
                (se calcula sola) o sin él (se declara ahí, ver
                rulesets-page.jsx). Acá solo queda el margen, que es
                logística de la organización y no del deporte. */}
            <TextField
              label="Minutos entre partidos"
              type="number"
              value={form.bufferMinutes}
              onChange={(e) => setForm({ ...form, bufferMinutes: soloDigitos(e.target.value) })}
              onKeyDown={bloquearNoEnteros}
              slotProps={{ htmlInput: { min: 0, step: 1 } }}
              fullWidth
              helperText="La duración del partido se calcula sola (o se declara, para un deporte sin reloj) desde el reglamento de cada categoría, esto es solo el margen entre el final de uno y el arranque del siguiente en la misma cancha (cambio de equipos, entrada en calor). Vacío usa un valor por defecto."
            />

            <Divider />

            <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <Typography variant="caption" color="text.secondary">Canchas habilitadas</Typography>
              <Button size="small" startIcon={<Iconify icon="eva:plus-fill" width={16} />} onClick={agregarCancha}>Agregar</Button>
            </Box>
            {form.scheduleSpaceIds.length === 0 && (
              <Typography variant="caption" color="text.secondary">
                Sin canchas cargadas, "Generar siguiente jornada" no va a poder colocar ningún partido.
              </Typography>
            )}
            {form.scheduleSpaceIds.map((venueSpaceId, i) => (
              <Box key={i} sx={{ display: 'flex', alignItems: 'flex-start', gap: 1 }}>
                <Box sx={{ flex: 1 }}>
                  <SelectionSpace value={venueSpaceId} onChange={(e) => cambiarCancha(i, e.target.value)} size="small" />
                </Box>
                <IconButton size="small" onClick={() => quitarCancha(i)} sx={{ mt: 0.5 }}>
                  <Iconify icon="eva:trash-2-outline" width={18} sx={{ color: 'error.main' }} />
                </IconButton>
              </Box>
            ))}
          </AccordionDetails>
        </Accordion>

        <Accordion disableGutters variant="outlined" sx={ACCORDION_SX}>
          <AccordionSummary sx={ACCORDION_SUMMARY_SX} expandIcon={<Iconify icon="eva:chevron-down-fill" />}>
            <Typography variant="subtitle2">Convocatoria</Typography>
          </AccordionSummary>
          <AccordionDetails sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
            <Typography variant="caption" color="text.secondary">
              Categorías, reglamento y puntaje salen solos de lo que ya cargaste en Categorías y Reglamento —
              esto es solo lo que el sistema no puede saber por su cuenta.
            </Typography>
            <TextField
              label="Presentación" value={form.bulletinIntroduction}
              onChange={(e) => setForm({ ...form, bulletinIntroduction: e.target.value })}
              fullWidth multiline minRows={2}
              helperText="Abre el documento: qué es la competencia, quién la organiza, por qué existe."
            />
            <TextField
              label="Sanciones" value={form.bulletinSanctions}
              onChange={(e) => setForm({ ...form, bulletinSanctions: e.target.value })}
              fullWidth multiline minRows={2}
              helperText="Qué amerita tarjeta, suspensión o descalificación. No hay nada cargado en el sistema para esto."
            />
            <TextField
              label="Disposiciones generales" value={form.bulletinGeneralProvisions}
              onChange={(e) => setForm({ ...form, bulletinGeneralProvisions: e.target.value })}
              fullWidth multiline minRows={2}
              helperText="Lo demás: sedes, plazos de inscripción, protestos, cualquier cosa propia de esta competencia."
            />
            <TextField
              label="Contacto" value={form.bulletinContactInfo}
              onChange={(e) => setForm({ ...form, bulletinContactInfo: e.target.value })}
              fullWidth multiline minRows={1}
              helperText="A quién escribirle o llamar con una consulta."
            />
          </AccordionDetails>
        </Accordion>

        <Accordion disableGutters variant="outlined" sx={ACCORDION_SX}>
          <AccordionSummary sx={ACCORDION_SUMMARY_SX} expandIcon={<Iconify icon="eva:chevron-down-fill" />}>
            <Typography variant="subtitle2">Portal público</Typography>
          </AccordionSummary>
          <AccordionDetails sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
            <Stack direction="row" spacing={1} sx={{ flexWrap: 'wrap' }}>
              <FormControlLabel control={<Switch checked={form.showStandings} onChange={(e) => setForm({ ...form, showStandings: e.target.checked })} />} label="Tabla de posiciones" />
              <FormControlLabel control={<Switch checked={form.showLeaders} onChange={(e) => setForm({ ...form, showLeaders: e.target.checked })} />} label="Lideres" />
              <FormControlLabel control={<Switch checked={form.showRosters} onChange={(e) => setForm({ ...form, showRosters: e.target.checked })} />} label="Nominas" />
            </Stack>

            <Divider />

            {/* Colores, tipografia, portada, redes y auspiciantes se editan en
                el estudio de portal, con vista previa en vivo. Solo tiene
                sentido sobre una competencia ya creada -- de ahi que en el
                alta se muestre el aviso y no el boton. */}
            {editId ? (
              <Box>
                <Button
                  variant="outlined"
                  startIcon={<Iconify icon="mdi:palette-outline" width={18} />}
                  onClick={() => { close(); navigate(`/dashboard/competitions/${editId}/portal`); }}
                >
                  Abrir estudio de portal
                </Button>
                <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 0.5 }}>
                  Colores, tipografia, portada, presentacion, redes y auspiciantes, con vista previa.
                </Typography>
              </Box>
            ) : (
              <Typography variant="caption" color="text.secondary">
                Guarda la competencia y despues personaliza su portal (colores, portada, redes) desde el estudio.
              </Typography>
            )}
          </AccordionDetails>
        </Accordion>
      </CrudDialog>
    </Box>
  );
}
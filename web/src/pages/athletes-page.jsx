import Box from '@mui/material/Box';
import { useState, useEffect, useRef } from 'react';
import TextField from '@mui/material/TextField';
import Alert from '@mui/material/Alert';
import Button from '@mui/material/Button';
import CircularProgress from '@mui/material/CircularProgress';
import Avatar from '@mui/material/Avatar';
import Typography from '@mui/material/Typography';
import Paper from '@mui/material/Paper';
import Slider from '@mui/material/Slider';
import ToggleButton from '@mui/material/ToggleButton';
import ToggleButtonGroup from '@mui/material/ToggleButtonGroup';
import { DataGrid } from '@mui/x-data-grid';
import { toast } from 'sonner';
import { Iconify } from 'src/components/iconify';
import { endpoints, default as axios } from 'src/lib/axios';
import { apiPut } from 'src/hooks/use-api';
import { useCrudDialog } from 'src/hooks/use-crud';
import { PageHeader } from 'src/components/page-header';
import { CrudDialog } from 'src/components/crud-dialog';
import { EditDeleteActions } from 'src/components/edit-delete-actions';
import { EstadoChip } from 'src/components/estado-chip';
import { SelectionField } from 'src/components/selectors';
import { useConfirm } from 'src/components/confirm-dialog';
import { DateField } from 'src/components/date-field';
import { readInlinePhoto, INLINE_PHOTO_REQUIREMENT } from 'src/lib/inline-photo';
import { bloquearNegativos, soloDecimales } from 'src/lib/entero-sin-signo';
import { withQueryParams } from 'src/lib/query-string';
import { edad } from 'src/lib/age';
import { evaluarFoto, detalleDeFoto } from 'src/lib/photo-validation';
import { PhotoValidationChip } from 'src/components/photo-validation-chip';

/**
 * Lo que se muestra mientras se decide la foto elegida en el formulario.
 * Vive fuera de la pagina para que React no lo trate como un componente nuevo
 * en cada tecla (ver EstadoChip).
 *
 * Dos niveles, los mismos que decide el validador: un fallo critico bloquea el
 * guardado y hay que elegir otra foto o quitarla; un defecto no critico solo
 * avisa, y la foto se guarda bajo la responsabilidad de quien la sube.
 */
function AvisoFotoElegida({ eleccion }) {
  if (eleccion.cargando) {
    return <Typography variant="caption" color="text.secondary">Validando la foto...</Typography>;
  }

  if (eleccion.error) {
    return (
      <Alert severity="info">
        {eleccion.error} Igual podés guardar: la foto quedará sin evaluar.
      </Alert>
    );
  }

  const { veredicto } = eleccion;
  const { motivos, advertencias } = detalleDeFoto(veredicto);

  if (veredicto.state === 'rejected') {
    return (
      <Alert severity="error">
        <Box component="span" sx={{ display: 'block' }}>Esta foto no se puede usar:</Box>
        <Box component="ul" sx={{ m: 0, pl: 2 }}>
          {motivos.map((texto, i) => <li key={`motivo-${i}`}>{texto}</li>)}
        </Box>
        <Box component="span" sx={{ display: 'block' }}>
          Elegí otra foto, o quitala, para poder guardar.
        </Box>
      </Alert>
    );
  }

  if (advertencias.length > 0) {
    return (
      <Alert severity="warning" action={<PhotoValidationChip validation={veredicto} />}>
        <Box component="span" sx={{ display: 'block' }}>Esta foto se puede usar, pero:</Box>
        <Box component="ul" sx={{ m: 0, pl: 2 }}>
          {advertencias.map((texto, i) => <li key={`advertencia-${i}`}>{texto}</li>)}
        </Box>
        <Box component="span" sx={{ display: 'block' }}>
          Si la guardás, queda bajo tu responsabilidad.
        </Box>
      </Alert>
    );
  }

  return <PhotoValidationChip validation={veredicto} />;
}

// La misma regla que el servidor (CreateAthlete / UpdateAthlete): nadie nace
// hoy ni despues, ni antes de 1900. Como fecha "AAAA-MM-DD", que se compara
// bien como texto.
const NACIMIENTO_MAS_ANTIGUO = '1900-01-02';
const ayer = () => new Date(Date.now() - 24 * 60 * 60 * 1000).toLocaleDateString('sv-SE');
const MENSAJE_NACIMIENTO = 'La fecha de nacimiento no puede ser de hoy ni futura.';

const emptyForm = () => ({ firstName: '', lastName: '', documentId: '', birthDate: '', gender: '', guardianName: '', guardianPhone: '', weightKg: '', isActive: true });

export default function AthletesPage() {
  const confirm = useConfirm();

  // Busca por nombre o documento -- ReadAthletes.ListAsync ya acepta este
  // parametro (ILIKE parcial contra las tres columnas por igual), asi que la
  // busqueda corre en el servidor. Mismo patron que ya usa el directorio
  // publico (public-portal.jsx).
  const [search, setSearch] = useState('');

  // Genero, edad y peso corrian en el cliente, sobre lo que ya habia
  // llegado -- correcto solo mientras la lista completa de la organizacion
  // cupiera en una sola respuesta. ReadAthletes.ListAsync ahora acepta los
  // tres directamente (ver el backend), asi que se arman como parametros de
  // consulta en vez de filtrar un arreglo ya completo.
  const [genderFilter, setGenderFilter] = useState('');

  // Limites fijos, no calculados de `data`: si dependieran del maximo/minimo
  // ya cargado, cada tecla escrita en "Buscar" cambiaria la lista y con ella
  // el rango del control mientras alguien todavia lo esta arrastrando. 0-80
  // y 0-150kg cubren cualquier categoria del catalogo (desde infantiles
  // hasta peso pesado); el rango completo es "sin filtrar", igual que antes
  // los campos vacios.
  const AGE_BOUNDS = [0, 80];
  const WEIGHT_BOUNDS = [0, 150];

  // Dos estados por control, no uno: `ageRange`/`weightRange` son el valor
  // visual del Slider (se mueven en cada pixel mientras se arrastra, para
  // que el control y la leyenda respondan al instante, igual que antes).
  // `ageQuery`/`weightQuery` son lo que realmente viaja al servidor, y solo
  // cambian al soltar (onChangeCommitted) -- antes filtrar en cada pixel era
  // gratis (un .filter en memoria); ahora cada cambio dispara una peticion
  // de red, y una por pixel arrastrado seria un disparo de peticiones que
  // nadie pidio.
  const [ageRange, setAgeRange] = useState(AGE_BOUNDS);
  const [weightRange, setWeightRange] = useState(WEIGHT_BOUNDS);
  const [ageQuery, setAgeQuery] = useState(AGE_BOUNDS);
  const [weightQuery, setWeightQuery] = useState(WEIGHT_BOUNDS);
  const ageFilterActive = ageRange[0] !== AGE_BOUNDS[0] || ageRange[1] !== AGE_BOUNDS[1];
  const weightFilterActive = weightRange[0] !== WEIGHT_BOUNDS[0] || weightRange[1] !== WEIGHT_BOUNDS[1];
  const ageQueryActive = ageQuery[0] !== AGE_BOUNDS[0] || ageQuery[1] !== AGE_BOUNDS[1];
  const weightQueryActive = weightQuery[0] !== WEIGHT_BOUNDS[0] || weightQuery[1] !== WEIGHT_BOUNDS[1];

  const clearFilters = () => {
    setGenderFilter('');
    setAgeRange(AGE_BOUNDS);
    setWeightRange(WEIGHT_BOUNDS);
    setAgeQuery(AGE_BOUNDS);
    setWeightQuery(WEIGHT_BOUNDS);
  };

  const athletesUrl = withQueryParams(endpoints.athletes, {
    search: search || undefined,
    gender: genderFilter || undefined,
    minAge: ageQueryActive ? ageQuery[0] : undefined,
    maxAge: ageQueryActive ? ageQuery[1] : undefined,
    minWeight: weightQueryActive ? weightQuery[0] : undefined,
    maxWeight: weightQueryActive ? weightQuery[1] : undefined,
  });

  // 0-indexado, como lo pide DataGrid -- skip/take (lo que el backend
  // realmente entiende, ver PagedListing) se arman a partir de esto adentro
  // de useCrudDialog.
  const [paginationModel, setPaginationModel] = useState({ page: 0, pageSize: 25 });

  // Volver a la primera pagina cuando cambia cualquier filtro que en verdad
  // le llega al servidor -- quedarse en la pagina 3 de un resultado que
  // ahora tiene una sola pagina mostraria una grilla vacia sin que se note
  // por que. Deliberadamente atado a los valores *comprometidos*
  // (ageQuery/weightQuery), no a los visuales: arrastrar el slider no
  // deberia reiniciar la pagina en cada pixel, solo cuando el filtro que de
  // verdad se manda cambia.
  useEffect(() => {
    setPaginationModel((current) => (current.page === 0 ? current : { ...current, page: 0 }));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [search, genderFilter, ageQuery, weightQuery]);

  const {
    rows: data, rowCount, isLoading, mutate, open, editId, form, setForm, error, setError, openCreate, openEdit, close, save, remove, saving,
  } = useCrudDialog({
    resourceUrl: athletesUrl,
    pageParams: { skip: paginationModel.page * paginationModel.pageSize, take: paginationModel.pageSize },
    // El alta no lleva el filtro de busqueda -- search solo pinta que se
    // lee, no cambia adonde se escribe. Sin esto, crear con un termino de
    // busqueda tipeado postearia a /athletes?search=... : el backend lo
    // aceptaria igual (la ruta no mira el query string), pero mezclar los
    // dos es innecesario y confunde a quien lea esto despues.
    createUrl: endpoints.athletes,
    emptyForm,
    entityName: 'deportista',
    savedMessage: 'Deportista guardado.',
    buildUrl: (base, id) => endpoints.athlete(id),
    mapToForm: (row) => ({ firstName: row.firstName, lastName: row.lastName, documentId: row.documentId, birthDate: row.birthDate || '', gender: row.gender || '', guardianName: row.guardianName || '', guardianPhone: row.guardianPhone || '', weightKg: row.weightKg ?? '', isActive: row.isActive !== false }),
    // Los opcionales viajan en null, no en '': el backend acepta "sin
    // definir" pero no una cadena vacia, que para el validador es un valor
    // invalido en vez de una ausencia. isActive tambien viaja siempre: al
    // editar es obligatorio para el backend y sin valor por defecto — si no
    // se manda, el deportista queda inactivo en silencio con cualquier
    // edicion, y un deportista inactivo no se puede inscribir en ningun equipo.
    // photoUrl no viaja aca a proposito, a diferencia del resto de los
    // campos: es un tercer estado, no un valor mas (ver UpdateAthlete.Request
    // en el backend) -- ausente deja la foto que ya tenia, '' la quita, una
    // data URL la reemplaza. Spreadear siempre `f` alcanza porque
    // form.photoUrl no existe hasta que la persona elige una foto nueva o
    // toca "Quitar foto" (ver onPickAthletePhoto/removeAthletePhoto mas
    // abajo); si nunca lo toca, la clave ni aparece en el objeto y JSON la
    // omite del todo -- exactamente "ausente". Meter aca el link firmado que
    // se usa solo para la vista previa (photoPreview) mandaria un valor que
    // ni siquiera empieza con "data:image/" y el validador lo rechazaria en
    // cualquier edicion que no tocara la foto.
    mapToSend: (f) => ({
      ...f,
      gender: f.gender || null,
      guardianName: f.guardianName || null,
      guardianPhone: f.guardianPhone || null,
      weightKg: f.weightKg !== '' ? Number(f.weightKg) : null,
    }),
  });

  // Separado de `form`: es la miniatura a mostrar (el link firmado que ya
  // tenia el deportista, o la foto recien elegida), nunca lo que se manda.
  // Mezclarlo con form.photoUrl mandaria ese link firmado como si fuera una
  // foto nueva -- ver el comentario de mapToSend arriba.
  const [photoPreview, setPhotoPreview] = useState(null);

  // Veredicto de la foto elegida en el formulario, antes de guardarla. Forma:
  // { cargando: true } mientras se evalua, { veredicto } al terminar, o
  // { error } si la API no respondio. Null si no se eligio ninguna.
  const [fotoElegida, setFotoElegida] = useState(null);
  // Veredicto ya guardado del deportista que se edita, para mostrarlo
  // mientras no se elija una foto nueva.
  const [fotoGuardada, setFotoGuardada] = useState(null);
  // Cada eleccion lleva su numero: si la persona elige otra foto antes de que
  // responda la anterior, esa respuesta vieja se descarta.
  const eleccionRef = useRef(0);

  const openCreateForm = () => {
    setPhotoPreview(null); setFotoElegida(null); setFotoGuardada(null); openCreate();
  };
  const openEditForm = (row) => {
    setPhotoPreview(row.photoUrl || null); setFotoElegida(null);
    setFotoGuardada(row.photoValidation ?? null); openEdit(row);
  };

  // La foto rechazada se deja a la vista: no se vuelve sola a la anterior,
  // porque guardar queda bloqueado hasta que la persona elija otra o la quite.
  const evaluarEleccion = async (archivo) => {
    const eleccion = ++eleccionRef.current;
    setFotoElegida({ cargando: true });
    try {
      const veredicto = await evaluarFoto(archivo);
      if (eleccion === eleccionRef.current) setFotoElegida({ veredicto });
    } catch (err) {
      if (eleccion === eleccionRef.current) setFotoElegida({ error: err.message });
    }
  };

  const onPickAthletePhoto = async (e) => {
    const file = e.target.files[0];
    e.target.value = ''; // permite elegir el mismo archivo dos veces seguidas
    if (!file) return;
    try {
      const dataUrl = await readInlinePhoto(file);
      setForm({ ...form, photoUrl: dataUrl });
      setPhotoPreview(dataUrl);
      setFotoGuardada(null);
      evaluarEleccion(file);
    } catch (err) {
      toast.error(err.message);
    }
  };

  const removeAthletePhoto = () => {
    eleccionRef.current++; // descarta cualquier evaluacion que siga en vuelo
    setForm({ ...form, photoUrl: '' });
    setPhotoPreview(null);
    setFotoElegida(null);
    setFotoGuardada(null);
  };

  const [photoOpen, setPhotoOpen] = useState(false);
  const [photoFile, setPhotoFile] = useState(null);
  const [photoLoading, setPhotoLoading] = useState(false);
  const [photoResult, setPhotoResult] = useState(null);

  // Mismo patron que VenuesPage: activar es inofensivo y reversible con el
  // mismo click, pero desactivar saca a la persona de cualquier inscripcion
  // futura (ver RosterPolicy.InspectAthlete en el backend, que rechaza a un
  // deportista inactivo) — vale la pena avisar antes, no solo cambiar el
  // estado en silencio. Reenvia el registro completo porque
  // UpdateAthlete.Request exige IsActive como parte de una correccion
  // entera, no de un parche de un solo campo (ver el comentario de
  // mapToSend mas arriba).
  const toggleAthleteActive = async (row, event) => {
    event.stopPropagation();
    if (row.isActive) {
      const ok = await confirm(
        `Desactivar a ${row.firstName} ${row.lastName}? No va a poder inscribirse en ningún equipo hasta reactivarlo. Las inscripciones que ya tiene no se ven afectadas.`,
        { confirmLabel: 'Desactivar', danger: true },
      );
      if (!ok) return;
    }
    try {
      await apiPut(endpoints.athlete(row.id), {
        firstName: row.firstName,
        lastName: row.lastName,
        documentId: row.documentId,
        birthDate: row.birthDate,
        gender: row.gender || null,
        guardianName: row.guardianName || null,
        guardianPhone: row.guardianPhone || null,
        weightKg: row.weightKg ?? null,
        isActive: !row.isActive,
      });
      mutate();
    } catch (err) { toast.error(err.message); }
  };

  const uploadPhotos = async () => {
    if (!photoFile) return;
    setPhotoLoading(true); setPhotoResult(null);
    try {
      const fd = new FormData(); fd.append('file', photoFile);
      const token = sessionStorage.getItem('jwt_access_token');
      const res = await axios.post(endpoints.athletePhotos, fd, { headers: { 'Content-Type': 'multipart/form-data', Authorization: `Bearer ${token}` } });
      setPhotoResult(res.data);
    } catch (err) { setPhotoResult({ error: err.message }); }
    finally { setPhotoLoading(false); }
  };

  // El selector ya no deja elegir esas fechas, pero una tipeada a mano si
  // llega al formulario: se avisa en el campo y no se manda. El servidor la
  // rechazaria igual, solo que despues de un viaje de ida y vuelta.
  const nacimientoInvalido = Boolean(form.birthDate)
    && (form.birthDate > ayer() || form.birthDate < NACIMIENTO_MAS_ANTIGUO);
  // Dos situaciones impiden guardar. Mientras se valida, porque el veredicto
  // todavia no existe. Y con una foto rechazada, porque fallo algo critico:
  // hay que elegir otra o quitarla, no guardar y seguir.
  const evaluandoFoto = fotoElegida?.cargando === true;
  const fotoRechazada = fotoElegida?.veredicto?.state === 'rejected';
  const fotoBloquea = evaluandoFoto || fotoRechazada;
  const guardar = () => {
    if (fotoBloquea) return;
    if (nacimientoInvalido) { setError(MENSAJE_NACIMIENTO); return; }
    save();
  };

  const columns = [
    { field: 'firstName', headerName: 'Nombres', flex: 1, minWidth: 150 },
    { field: 'lastName', headerName: 'Apellidos', flex: 1, minWidth: 150 },
    { field: 'documentId', headerName: 'Documento', width: 140 },
    { field: 'birthDate', headerName: 'Fecha nac.', width: 120 },
    { field: 'edad', headerName: 'Edad', width: 70, valueGetter: (_, row) => edad(row.birthDate), renderCell: ({ value }) => (
      value != null ? <Box component="span" sx={{ color: value < 18 ? 'warning.dark' : undefined, fontWeight: value < 18 ? 700 : 400 }}>{value}</Box> : '--'
    ) },
    { field: 'gender', headerName: 'Genero', width: 100, renderCell: ({ value }) => value === 'M' ? 'Masculino' : value === 'F' ? 'Femenino' : '--' },
    { field: 'weightKg', headerName: 'Peso (kg)', width: 90, renderCell: ({ value }) => value ?? '--' },
    { field: 'guardianName', headerName: 'Apoderado', width: 150, renderCell: ({ value }) => value || '--' },
    { field: 'photoValidation', headerName: 'Foto', width: 120, sortable: false, renderCell: ({ value }) => (
      <PhotoValidationChip validation={value} />
    )},
    { field: 'isActive', headerName: 'Estado', width: 110, sortable: false, renderCell: ({ value, row }) => (
      <EstadoChip activo={value} onClick={(e) => toggleAthleteActive(row, e)} />
    )},
    { field: 'actions', headerName: 'Acciones', width: 90, align: 'center', headerAlign: 'center', renderCell: ({ row }) => (
      <EditDeleteActions onEdit={() => openEditForm(row)} onDelete={() => remove(row.id)} />
    )},
  ];

  return (
    <div>
      <PageHeader title="Deportistas" actionLabel="Nuevo" onAction={openCreateForm}>
        <Button variant="outlined" startIcon={<Iconify icon="eva:image-outline" />} onClick={() => { setPhotoFile(null); setPhotoResult(null); setPhotoOpen(true); }}>Importar fotos</Button>
      </PageHeader>
      <Paper variant="outlined" sx={{ p: 2, mb: 3 }}>
        {/*
          Las cuatro columnas comparten la misma estructura a proposito
          (leyenda afuera y arriba, del mismo alto, control abajo): "Buscar"
          antes tenia el label flotante de MUI -- adentro del propio campo,
          sin ocupar una fila propia -- mientras las otras tres ya llevaban
          una leyenda aparte. Esa diferencia de estructura era lo que
          corria todo de nivel, no un ajuste de gap o de padding.
        */}
        <Box sx={{ display: 'flex', gap: 3, flexWrap: 'wrap', alignItems: 'flex-start' }}>
          <Box sx={{ flex: '1 1 220px' }}>
            <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 0.5 }}>Buscar</Typography>
            <TextField
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Nombre, apellido o documento"
              size="small"
              fullWidth
              slotProps={{ input: { startAdornment: <Iconify icon="eva:search-outline" width={18} sx={{ mr: 1, color: 'text.disabled' }} /> } }}
            />
          </Box>

          <Box>
            <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 0.5 }}>Genero</Typography>
            <ToggleButtonGroup value={genderFilter} exclusive onChange={(_, v) => setGenderFilter(v ?? '')} size="small">
              <ToggleButton value="">Todos</ToggleButton>
              <ToggleButton value="M">Masculino</ToggleButton>
              <ToggleButton value="F">Femenino</ToggleButton>
            </ToggleButtonGroup>
          </Box>

          <Box sx={{ flex: '1 1 200px', px: 1 }}>
            <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 0.5 }}>
              Edad{ageFilterActive ? `: ${ageRange[0]} – ${ageRange[1]} años` : ''}
            </Typography>
            <Slider
              value={ageRange}
              onChange={(_, v) => setAgeRange(v)}
              onChangeCommitted={(_, v) => setAgeQuery(v)}
              min={AGE_BOUNDS[0]}
              max={AGE_BOUNDS[1]}
              size="small"
              valueLabelDisplay="auto"
              valueLabelFormat={(v) => `${v} años`}
            />
          </Box>

          <Box sx={{ flex: '1 1 200px', px: 1 }}>
            <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 0.5 }}>
              Peso{weightFilterActive ? `: ${weightRange[0]} – ${weightRange[1]} kg` : ''}
            </Typography>
            <Slider
              value={weightRange}
              onChange={(_, v) => setWeightRange(v)}
              onChangeCommitted={(_, v) => setWeightQuery(v)}
              min={WEIGHT_BOUNDS[0]}
              max={WEIGHT_BOUNDS[1]}
              size="small"
              valueLabelDisplay="auto"
              valueLabelFormat={(v) => `${v} kg`}
            />
          </Box>
        </Box>

        {(genderFilter || ageFilterActive || weightFilterActive) && (
          <Button size="small" onClick={clearFilters} startIcon={<Iconify icon="eva:close-circle-outline" width={16} />} sx={{ mt: 1 }}>
            Limpiar filtros
          </Button>
        )}
      </Paper>
      <DataGrid
        rows={data || []}
        columns={columns}
        loading={isLoading}
        autoHeight
        disableRowSelectionOnClick
        paginationMode="server"
        rowCount={rowCount}
        paginationModel={paginationModel}
        onPaginationModelChange={setPaginationModel}
        pageSizeOptions={[25, 50, 100]}
      />

      <CrudDialog open={open} editId={editId} entityName="Deportista" error={error} saving={saving} disabled={fotoBloquea} onClose={close} onSave={guardar}>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 2 }}>
          <Avatar src={photoPreview || undefined} sx={{ width: 64, height: 64 }}>
            <Iconify icon="eva:person-fill" width={32} />
          </Avatar>
          <Box sx={{ display: 'flex', flexDirection: 'column', alignItems: 'flex-start', gap: 0.5 }}>
            <Button variant="outlined" size="small" component="label" startIcon={<Iconify icon="eva:image-outline" width={16} />}>
              {photoPreview ? 'Cambiar foto' : 'Agregar foto'}
              <input type="file" accept="image/png,image/jpeg,image/webp" hidden onChange={onPickAthletePhoto} />
            </Button>
            {photoPreview && <Button size="small" color="error" onClick={removeAthletePhoto}>Quitar foto</Button>}
            <Typography variant="caption" color="text.secondary">{INLINE_PHOTO_REQUIREMENT}</Typography>
          </Box>
        </Box>
        {fotoElegida && <AvisoFotoElegida eleccion={fotoElegida} />}
        {!fotoElegida && fotoGuardada && photoPreview && <PhotoValidationChip validation={fotoGuardada} />}
        <TextField label="Nombres" value={form.firstName} onChange={(e) => setForm({ ...form, firstName: e.target.value })} fullWidth />
        <TextField label="Apellidos" value={form.lastName} onChange={(e) => setForm({ ...form, lastName: e.target.value })} fullWidth />
        <TextField label="Documento" value={form.documentId} onChange={(e) => setForm({ ...form, documentId: e.target.value })} fullWidth />
        <DateField
          label="Fecha nacimiento" value={form.birthDate} fullWidth
          onChange={(e) => setForm({ ...form, birthDate: e.target.value })}
          minDate={NACIMIENTO_MAS_ANTIGUO} maxDate={ayer()}
          error={nacimientoInvalido}
          helperText={nacimientoInvalido ? MENSAJE_NACIMIENTO : undefined}
        />
        {/* Solo M o F, o sin definir -- Sex.IsAcceptable en el backend no
            admite nada mas, porque una categoria con restriccion de sexo
            compara este valor contra el suyo (ver RosterPolicy.InspectAthlete):
            texto libre aca es un deportista que ninguna categoria con
            restriccion podria admitir nunca, un rechazo que recien se
            explica al intentar inscribirlo. */}
        <SelectionField
          label="Genero"
          value={form.gender}
          onChange={(e) => setForm({ ...form, gender: e.target.value })}
          emptyLabel="Sin definir"
          options={[{ value: 'M', label: 'Masculino' }, { value: 'F', label: 'Femenino' }]}
        />
        <TextField
          label="Peso (kg)"
          type="number"
          value={form.weightKg}
          onChange={(e) => setForm({ ...form, weightKg: soloDecimales(e.target.value) })}
          onKeyDown={bloquearNegativos}
          fullWidth
          helperText="Ultimo pesaje registrado. Lo lee la categoria con limite de peso, si la hay."
          slotProps={{ htmlInput: { min: 0, step: 0.1 } }}
        />
        {edad(form.birthDate) != null && edad(form.birthDate) < 18 && (
          <Alert severity="warning">
            Es menor de edad ({edad(form.birthDate)} años): conviene completar los datos del apoderado.
          </Alert>
        )}
        <TextField label="Nombre del apoderado" value={form.guardianName} onChange={(e) => setForm({ ...form, guardianName: e.target.value })} fullWidth helperText="Padre, madre o tutor. Solo se usa dentro de la organizacion, nunca se publica." />
        <TextField label="Telefono del apoderado" value={form.guardianPhone} onChange={(e) => setForm({ ...form, guardianPhone: e.target.value })} fullWidth />
        {/* El estado activo/inactivo no se edita aca -- mismo patron que
            Sedes: se alterna con un click en el chip de la grilla, que avisa
            antes de desactivar. form.isActive sigue viajando en cada
            guardado (ver mapToSend) para no pisarlo en silencio con
            cualquier otra correccion. */}
      </CrudDialog>

      {photoOpen && (
        <Box component="div" sx={{ position: 'fixed', inset: 0, bgcolor: 'overlay.scrim', zIndex: 1300, display: 'flex', alignItems: 'center', justifyContent: 'center', p: 2, boxSizing: 'border-box' }} onClick={() => setPhotoOpen(false)}>
          <Box sx={{ bgcolor: 'background.paper', borderRadius: 2, p: 3, width: { xs: '100%', sm: 400 }, maxHeight: '90vh', overflowY: 'auto' }} onClick={(e) => e.stopPropagation()}>
            <h3 style={{ margin: '0 0 12px' }}>Importar fotos (ZIP)</h3>
            <Alert severity="info" sx={{ mb: 2 }}>Cada imagen debe llamarse igual al documento del deportista.</Alert>
            <Button variant="outlined" component="label" startIcon={<Iconify icon="eva:upload-outline" />}>
              Seleccionar ZIP
              <input type="file" accept=".zip" hidden onChange={(e) => setPhotoFile(e.target.files[0])} />
            </Button>
            {photoFile && <p style={{ marginTop: 8 }}>{photoFile.name}</p>}
            {photoResult && !photoResult.error && <Alert severity="success" sx={{ mt: 2 }}>Lote enviado. Estado: {photoResult.status}.</Alert>}
            {photoResult?.error && <Alert severity="error" sx={{ mt: 2 }}>{photoResult.error}</Alert>}
            <Box sx={{ display: 'flex', justifyContent: 'flex-end', gap: 1, mt: 2 }}>
              <Button onClick={() => setPhotoOpen(false)} disabled={photoLoading}>Cerrar</Button>
              <Button
                variant="contained"
                onClick={uploadPhotos}
                disabled={!photoFile || photoLoading}
                startIcon={photoLoading ? <CircularProgress size={16} color="inherit" /> : undefined}
              >
                {photoLoading ? 'Subiendo...' : 'Subir'}
              </Button>
            </Box>
          </Box>
        </Box>
      )}
    </div>
  );
}
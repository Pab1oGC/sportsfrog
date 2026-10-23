import React from 'react';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Select from '@mui/material/Select';
import MenuItem from '@mui/material/MenuItem';
import CircularProgress from '@mui/material/CircularProgress';
import InputLabel from '@mui/material/InputLabel';
import FormControl from '@mui/material/FormControl';
import FormHelperText from '@mui/material/FormHelperText';
import Autocomplete from '@mui/material/Autocomplete';
import TextField from '@mui/material/TextField';
import { useApi } from 'src/hooks/use-api';
import { endpoints } from 'src/lib/axios';

function SelectionField({ label, value, onChange, options, isLoading, disabled, required, fullWidth = true, size = 'medium', emptyLabel, helperText }) {
  return (
    <FormControl fullWidth={fullWidth} disabled={disabled || isLoading} required={required} size={size} sx={{ mb: 2 }}>
      <InputLabel>{label}</InputLabel>
      <Select label={label} value={value} onChange={onChange} required={required}
        endAdornment={isLoading ? <CircularProgress size={20} sx={{ mr: 2 }} /> : null}>
        <MenuItem value=""><em>{isLoading ? 'Cargando...' : (emptyLabel || 'Seleccionar...')}</em></MenuItem>
        {(options || []).map((o) => <MenuItem key={o.value} value={o.value}>{o.label}</MenuItem>)}
      </Select>
      {helperText && <FormHelperText>{helperText}</FormHelperText>}
    </FormControl>
  );
}

function SelectionCompetition({ value, onChange, ...props }) {
  const { data, isLoading } = useApi(endpoints.competitions);
  const options = (data || []).map((c) => ({ value: c.id, label: c.name }));
  return <SelectionField label="Competicion" value={value} onChange={onChange} options={options} isLoading={isLoading} {...props} />;
}

function SelectionCategory({ competitionId, value, onChange, ...props }) {
  const { data, isLoading } = useApi(competitionId ? endpoints.categories(competitionId) : null);
  const options = (data || []).map((c) => ({ value: c.id, label: c.name }));
  return <SelectionField label="Categoria" value={value} onChange={onChange} options={options} isLoading={isLoading} disabled={!competitionId} {...props} />;
}

function SelectionTeam({ categoryId, value, onChange, ...props }) {
  const { data, isLoading } = useApi(categoryId ? endpoints.teams(categoryId) : null);
  const options = (data || []).map((t) => ({ value: t.id, label: t.name }));
  return <SelectionField label="Equipo" value={value} onChange={onChange} options={options} isLoading={isLoading} disabled={!categoryId} {...props} />;
}

// Un curso no depende de ninguna competicion ni categoria -- a diferencia
// de SelectionTeam, este desplegable no tiene nada que esperar antes de
// pedir su propia lista.
function SelectionCourse({ value, onChange, ...props }) {
  const { data, isLoading } = useApi(endpoints.courses);
  const options = (data || []).map((c) => ({ value: c.id, label: c.name }));
  return <SelectionField label="Curso" value={value} onChange={onChange} options={options} isLoading={isLoading} {...props} />;
}

// Nomina (RosterEntry) y cuerpo tecnico/dirigencia (TeamStaff) de un equipo,
// combinados y achatados a la misma forma que /athletes (un `id` de
// deportista, no un id de inscripcion) -- una sancion puede recaer sobre
// cualquiera de los dos por igual, ver las remarks de Sanction.cs. Solo
// quienes estan hoy en el equipo (`available`/`active` en true): elegir a
// quien sancionar es una pregunta sobre el plantel actual, no sobre todo
// el historico.
const athletesOfTeam = (roster, staff) => {
  const porId = new Map();
  for (const entry of [...(roster || []), ...(staff || [])]) {
    porId.set(entry.athleteId, { id: entry.athleteId, firstName: entry.firstName, lastName: entry.lastName });
  }
  return [...porId.values()];
};

// Un Autocomplete, no un SelectionField mas: elegir una persona entre una
// lista que puede ser larga se hace buscando por nombre a medida que se
// tipea, no scrolleando un <Select> -- mismo problema que ya resuelve el
// buscador de jugador de EventsDialog. Reinventar esa busqueda dentro de un
// <Select> hubiera sido peor UX y una segunda implementacion del mismo
// patron.
//
// `multiple` (default true, el uso original: entrar a alguien a un deporte
// individual es elegir uno, una pareja o un trio) cambia la forma de
// value/onChange -- un array de ids yendo y viniendo, o un id suelto
// (string, como cualquier otro campo de esta pagina) cuando es false. La
// version simple existe para NominaPage: registrar un jugador es elegir una
// sola persona, y antes de esto lo hacia con un <TextField select> comun sin
// buscador -- localizar a alguien en una lista larga escribiendo el nombre
// no era posible ahi, a diferencia de aca.
//
// `teamId` (opcional) acota la lista a un equipo en vez de toda la
// organizacion -- Sanciones lo usa asi: una vez elegido el equipo, solo
// tiene sentido ofrecer a quien ya esta en el (ver athletesOfTeam arriba).
// Sin este prop el comportamiento es el de siempre, toda la organizacion --
// NominaPage lo sigue usando para eso, porque inscribir a alguien es elegir
// a alguien que *todavia no* esta en el equipo. Pasar `teamId=""` (el
// equipo aun no elegido) deshabilita el campo en vez de mostrar la lista
// completa como reemplazo, para que nunca se vea una persona ajena al
// equipo mientras se espera que se elija uno.
//
// Igual que SelectionClub, no filtra por activo cuando no hay `teamId`: ver
// la nota de mas abajo para lo que pasa cuando si lo hay.
function SelectionAthletes({ teamId, value, onChange, label = 'Deportistas', helperText, disabled, multiple = true, required }) {
  const porEquipo = teamId !== undefined;
  // RosterPolicy / StaffPolicy ya rechazan a alguien inactivo con su propio
  // mensaje (ver EnrollIndividual / RegisterPlayer / RegisterStaff), y
  // esconderlo aca duplicaria esa regla en dos lugares que podrian
  // desalinearse -- por eso la lista de toda la organizacion no filtra por
  // activo. Acotada a un equipo si filtra a quien esta hoy en el (ver el
  // comentario de arriba): ahi la regla no es "esta inactivo", es "no esta
  // en este equipo", y esa la pone esta misma lista, no una policy aparte.
  const { data: todos, isLoading: cargandoTodos } = useApi(porEquipo ? null : endpoints.athletes);
  const { data: roster, isLoading: cargandoRoster } = useApi(porEquipo && teamId ? `${endpoints.roster(teamId)}?available=true` : null);
  const { data: staff, isLoading: cargandoStaff } = useApi(porEquipo && teamId ? `${endpoints.teamStaff(teamId)}?active=true` : null);

  const athletes = porEquipo ? athletesOfTeam(roster, staff) : (todos || []);
  const isLoading = porEquipo ? (cargandoRoster || cargandoStaff) : cargandoTodos;
  const ids = multiple ? value : (value ? [value] : []);
  const seleccionados = ids.map((id) => athletes.find((a) => a.id === id)).filter(Boolean);

  return (
    <Autocomplete
      multiple={multiple}
      options={athletes}
      loading={isLoading}
      disabled={disabled || (porEquipo && !teamId)}
      getOptionLabel={(a) => `${a.lastName} ${a.firstName}`}
      // Sin esto MUI usa la etiqueta como key de React, y dos deportistas con
      // el mismo nombre repiten key: al filtrar, React deja filas viejas en
      // la lista (opciones que ya no coinciden con lo tipeado).
      getOptionKey={(a) => a.id}
      // Cada palabra tipeada tiene que ser el comienzo de alguna palabra del
      // nombre completo, en cualquier orden y sin importar tildes ni
      // mayusculas: "juan perez" y "perez juan" encuentran lo mismo. Al
      // comienzo y no en cualquier parte, para que "pe" no traiga a "Lopez".
      filterOptions={(opciones, { inputValue }) => {
        const normalizar = (texto) => texto.normalize('NFD').replace(/\p{M}/gu, '').toLowerCase();
        const buscadas = normalizar(inputValue).split(/\s+/).filter(Boolean);
        return opciones.filter((a) => {
          const nombre = normalizar(`${a.lastName} ${a.firstName}`).split(/\s+/);
          return buscadas.every((buscada) => nombre.some((palabra) => palabra.startsWith(buscada)));
        });
      }}
      isOptionEqualToValue={(a, b) => a.id === b.id}
      value={multiple ? seleccionados : (seleccionados[0] || null)}
      onChange={(_, seleccion) => onChange(multiple ? seleccion.map((a) => a.id) : (seleccion?.id || ''))}
      sx={{ mb: 2 }}
      renderInput={(params) => (
        <TextField
          {...params}
          label={label}
          placeholder="Nombre o apellido"
          required={required}
          helperText={helperText}
          slotProps={{
            ...params.slotProps,
            // MUI v9: renderInput ya no manda `params.InputProps` (v5) --
            // las props del input viven en `params.slotProps.input`. Se
            // conserva el resto de params.slotProps (inputLabel, htmlInput)
            // y solo se pisa endAdornment para agregarle el spinner.
            input: { ...params.slotProps.input, endAdornment: (
              <>
                {isLoading ? <CircularProgress size={18} sx={{ mr: 1 }} /> : null}
                {params.slotProps.input.endAdornment}
              </>
            ) },
          }}
        />
      )}
    />
  );
}

// El catalogo de deportes es compartido por toda la plataforma (no tiene
// org_id, ver Sport.cs) y corto: no necesita busqueda, un desplegable comun
// alcanza.
function SelectionSport({ value, onChange, ...props }) {
  const { data, isLoading } = useApi(endpoints.sports);
  const options = (data || []).map((s) => ({ value: s.code, label: s.name }));
  return <SelectionField label="Deporte" value={value} onChange={onChange} options={options} isLoading={isLoading} {...props} />;
}

function SelectionClub({ value, onChange, ...props }) {
  const { data, isLoading } = useApi(endpoints.clubs);
  const options = (data || []).map((c) => ({ value: c.id, label: c.name }));
  return <SelectionField label="Club" value={value} onChange={onChange} options={options} isLoading={isLoading} {...props} />;
}

function SelectionVenue({ value, onChange, ...props }) {
  const { data, isLoading } = useApi(endpoints.venues);
  const options = (data || []).map((v) => ({ value: v.id, label: v.address ? `${v.name} - ${v.address}` : v.name }));
  return <SelectionField label="Sede" value={value} onChange={onChange} options={options} isLoading={isLoading} {...props} />;
}

// La cancha/pista concreta donde se juega un partido — no la sede entera,
// que puede tener varias. Un solo desplegable en vez de sede-y-luego-cancha
// porque la lista completa de espacios de la organizacion suele ser corta.
// Solo ofrece los que se pueden usar ahora: el espacio activo y su sede
// tambien activa, que es exactamente lo que IsAvailable ya responde.
function SelectionSpace({ value, onChange, ...props }) {
  const { data, isLoading } = useApi(endpoints.spaces);
  const options = (data || [])
    .filter((s) => s.isAvailable)
    .map((s) => ({ value: s.id, label: `${s.venueName} — ${s.name}` }));
  // Un desplegable vacio y uno sin datos todavia se ven identicos si no se
  // dice la diferencia: el primero significa "no hay nada creado", no "el
  // campo esta roto".
  const helperText = !isLoading && options.length === 0
    ? 'No hay canchas activas todavia. Se crean desde Sedes.'
    : undefined;
  return <SelectionField label="Cancha / espacio" value={value} onChange={onChange} options={options} isLoading={isLoading} helperText={helperText} {...props} />;
}

export { SelectionField, SelectionCompetition, SelectionCategory, SelectionTeam, SelectionCourse, SelectionAthletes, SelectionClub, SelectionVenue, SelectionSpace, SelectionSport };
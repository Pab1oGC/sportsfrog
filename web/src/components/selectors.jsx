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
// Igual que SelectionClub, no filtra por activo: RosterPolicy ya rechaza un
// deportista inactivo con su propio mensaje (ver EnrollIndividual /
// RegisterPlayer), y esconderlo aca duplicaria esa regla en dos lugares que
// podrian desalinearse.
function SelectionAthletes({ value, onChange, label = 'Deportistas', helperText, disabled, multiple = true, required }) {
  const { data, isLoading } = useApi(endpoints.athletes);
  const athletes = data || [];
  const ids = multiple ? value : (value ? [value] : []);
  const seleccionados = ids.map((id) => athletes.find((a) => a.id === id)).filter(Boolean);

  return (
    <Autocomplete
      multiple={multiple}
      options={athletes}
      loading={isLoading}
      disabled={disabled}
      getOptionLabel={(a) => `${a.lastName}, ${a.firstName}`}
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

export { SelectionField, SelectionCompetition, SelectionCategory, SelectionTeam, SelectionAthletes, SelectionClub, SelectionVenue, SelectionSpace };
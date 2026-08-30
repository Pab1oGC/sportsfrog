import React from 'react';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Select from '@mui/material/Select';
import MenuItem from '@mui/material/MenuItem';
import CircularProgress from '@mui/material/CircularProgress';
import InputLabel from '@mui/material/InputLabel';
import FormControl from '@mui/material/FormControl';
import FormHelperText from '@mui/material/FormHelperText';
import { useApi } from 'src/hooks/use-api';
import { endpoints } from 'src/lib/axios';

function SelectionField({ label, value, onChange, options, isLoading, disabled, fullWidth = true, size = 'medium', emptyLabel, helperText }) {
  return (
    <FormControl fullWidth={fullWidth} disabled={disabled || isLoading} size={size} sx={{ mb: 2 }}>
      <InputLabel>{label}</InputLabel>
      <Select label={label} value={value} onChange={onChange}
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

export { SelectionField, SelectionCompetition, SelectionCategory, SelectionTeam, SelectionClub, SelectionVenue, SelectionSpace };
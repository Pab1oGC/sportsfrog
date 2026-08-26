import React from 'react';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Select from '@mui/material/Select';
import MenuItem from '@mui/material/MenuItem';
import CircularProgress from '@mui/material/CircularProgress';
import InputLabel from '@mui/material/InputLabel';
import FormControl from '@mui/material/FormControl';
import { useApi } from 'src/hooks/use-api';
import { endpoints } from 'src/lib/axios';

function SelectionField({ label, value, onChange, options, isLoading, disabled, fullWidth = true, size = 'medium', emptyLabel }) {
  return (
    <FormControl fullWidth={fullWidth} disabled={disabled || isLoading} size={size} sx={{ mb: 2 }}>
      <InputLabel>{label}</InputLabel>
      <Select label={label} value={value} onChange={onChange}
        endAdornment={isLoading ? <CircularProgress size={20} sx={{ mr: 2 }} /> : null}>
        <MenuItem value=""><em>{isLoading ? 'Cargando...' : (emptyLabel || 'Seleccionar...')}</em></MenuItem>
        {(options || []).map((o) => <MenuItem key={o.value} value={o.value}>{o.label}</MenuItem>)}
      </Select>
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

export { SelectionField, SelectionCompetition, SelectionCategory, SelectionTeam, SelectionClub, SelectionVenue };
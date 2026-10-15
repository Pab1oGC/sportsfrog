import React from 'react';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Grid from '@mui/material/Grid';
import { useApi } from 'src/hooks/use-api';
import { endpoints } from 'src/lib/axios';
import { StatCard } from 'src/components/stat-card';

export default function DashboardPage() {
  const { data: comps } = useApi(endpoints.competitions);
  const { data: clubs } = useApi(endpoints.clubs);
  const { data: athletes } = useApi(endpoints.athletes);
  return (
    <Box>
      <Typography variant='h4' fontWeight={700} sx={{ mb: 3 }}>Dashboard</Typography>
      <Grid container spacing={3}>
        <Grid size={{ xs: 12, sm: 6, md: 3 }}><StatCard title='Competiciones' value={comps ? comps.length : 0} icon='mdi:trophy-outline' color='primary' /></Grid>
        <Grid size={{ xs: 12, sm: 6, md: 3 }}><StatCard title='Clubes' value={clubs ? clubs.length : 0} icon='mdi:office-building-outline' color='info' /></Grid>
        <Grid size={{ xs: 12, sm: 6, md: 3 }}><StatCard title='Deportistas' value={athletes ? athletes.length : 0} icon='mdi:run' color='success' /></Grid>
        <Grid size={{ xs: 12, sm: 6, md: 3 }}><StatCard title='Reglamentos' value='--' icon='mdi:book-open-outline' color='warning' /></Grid>
      </Grid>
    </Box>
  );
}
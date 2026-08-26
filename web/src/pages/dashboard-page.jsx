import React from 'react';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Grid from '@mui/material/Grid';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import { Iconify } from 'src/components/iconify';
import { useApi } from 'src/hooks/use-api';
import { endpoints } from 'src/lib/axios';

function StatCard({ title, value, icon, color }) {
  return (
    <Card>
      <CardContent sx={{ display: 'flex', alignItems: 'center', gap: 2 }}>
        <Box sx={{ width: 48, height: 48, borderRadius: 2, bgcolor: color + '.lighter', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
          <Iconify icon={icon} width={24} sx={{ color: color + '.main' }} />
        </Box>
        <Box>
          <Typography variant='h4' fontWeight={700}>{value || '--'}</Typography>
          <Typography variant='body2' color='text.secondary'>{title}</Typography>
        </Box>
      </CardContent>
    </Card>
  );
}

export default function DashboardPage() {
  const { data: comps } = useApi(endpoints.competitions);
  const { data: clubs } = useApi(endpoints.clubs);
  const { data: athletes } = useApi(endpoints.athletes);
  return (
    <Box>
      <Typography variant='h4' fontWeight={700} sx={{ mb: 3 }}>Dashboard</Typography>
      <Grid container spacing={3}>
        <Grid size={{ xs: 12, sm: 6, md: 3 }}><StatCard title='Competiciones' value={comps ? comps.length : 0} icon='mdi:trophy-outline' color='primary' /></Grid>
        <Grid size={{ xs: 12, sm: 6, md: 3 }}><StatCard title='Clubes' value={clubs ? clubs.length : 0} icon='mdi:domain-outline' color='info' /></Grid>
        <Grid size={{ xs: 12, sm: 6, md: 3 }}><StatCard title='Deportistas' value={athletes ? athletes.length : 0} icon='mdi:run-outline' color='success' /></Grid>
        <Grid size={{ xs: 12, sm: 6, md: 3 }}><StatCard title='Reglamentos' value='--' icon='mdi:book-open-outline' color='warning' /></Grid>
      </Grid>
    </Box>
  );
}
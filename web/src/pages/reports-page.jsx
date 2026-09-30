import { useState } from 'react';
import Box from '@mui/material/Box';
import Avatar from '@mui/material/Avatar';
import Paper from '@mui/material/Paper';
import Tabs from '@mui/material/Tabs';
import Tab from '@mui/material/Tab';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import Chip from '@mui/material/Chip';
import Divider from '@mui/material/Divider';
import { useTheme } from '@mui/material/styles';
import { DataGrid } from '@mui/x-data-grid';
import { PieChart } from '@mui/x-charts/PieChart';
import { BarChart } from '@mui/x-charts/BarChart';
import { Iconify } from 'src/components/iconify';
import { useApi } from 'src/hooks/use-api';
import { useCascade } from 'src/hooks/use-cascade';
import { endpoints, default as axios } from 'src/lib/axios';
import { downloadBlob } from 'src/lib/download-blob';
import { fechaHora } from 'src/lib/format-date';
import { PageHeader } from 'src/components/page-header';
import { CascadeFilters } from 'src/components/cascade-filters';
import { SelectionTeam, SelectionField } from 'src/components/selectors';
import { edad as ageFromBirthDate } from 'src/lib/age';
import { toast } from 'sonner';

const SC = { scheduled: 'info', in_progress: 'warning', finished: 'success', cancelled: 'error', walkover: 'warning', postponed: 'default' };
const SL = { scheduled: 'Programado', in_progress: 'En curso', finished: 'Finalizado', cancelled: 'Cancelado', walkover: 'Walkover', postponed: 'Aplazado' };
const RESULT_COLOR = { Ganado: 'success', Empatado: 'default', Perdido: 'error' };

// Cebra en la grilla: sin esto una tabla larga de puro texto se pierde de
// vista renglon a renglon, el mismo problema que tenia el PDF antes de
// arreglarlo (ver TeamReportPdf/AthleteReportPdf).
const zebraSx = {
  '& .fila-par': { backgroundColor: 'action.hover' },
};
const zebraRowClassName = (params) => (params.indexRelativeToCurrentPage % 2 === 1 ? 'fila-par' : '');

const matchColumns = [
  { field: 'scheduledAt', headerName: 'Fecha', width: 180, renderCell: ({ value }) => value ? fechaHora(value) : 'Sin fecha' },
  { field: 'opponentName', headerName: 'Rival', flex: 1, minWidth: 140 },
  { field: 'home', headerName: '', width: 90, renderCell: ({ value }) => <Chip label={value ? 'Local' : 'Visitante'} size="small" variant="outlined" /> },
  { field: 'ownTotal', headerName: 'Marcador', width: 100, renderCell: ({ row }) =>
    row.ownTotal != null && row.opponentTotal != null ? `${row.ownTotal} - ${row.opponentTotal}` : 'vs' },
  { field: 'outcome', headerName: 'Resultado', width: 130, renderCell: ({ row }) =>
    row.outcome
      ? <Chip label={row.outcome} color={RESULT_COLOR[row.outcome] || 'default'} size="small" />
      : <Chip label={SL[row.status] || row.status} color={SC[row.status] || 'default'} size="small" /> },
];

/**
 * Reportes nuevos, sacados de los datos que ya genera una competencia: por
 * equipo (su lugar en la tabla o su clasificación, sus partidos, sus propias
 * figuras) y por deportista (su ficha y su participación en cada inscripción
 * que tiene). Nada de esto exportaba a PDF ni tenia pantalla propia antes de
 * esto -- ver TeamReportQuery/AthleteReportQuery en el backend.
 */
export default function ReportsPage() {
  const [modo, setModo] = useState('equipo');
  const cascade = useCascade();
  const [athleteId, setAthleteId] = useState('');

  const { data: roster } = useApi(modo === 'deportista' && cascade.teamId ? endpoints.roster(cascade.teamId) : null);

  const { data: teamReport, isLoading: loadingTeam } = useApi(
    modo === 'equipo' && cascade.teamId ? endpoints.teamReport(cascade.teamId) : null,
  );
  const { data: athleteReport, isLoading: loadingAthlete } = useApi(
    modo === 'deportista' && athleteId ? endpoints.athleteReport(athleteId) : null,
  );

  const opcionesDeportista = (roster || []).map((r) => ({ value: r.athleteId, label: `${r.firstName} ${r.lastName}` }));

  const descargar = async (url, fallback) => {
    try {
      const res = await axios.get(url, { responseType: 'blob' });
      downloadBlob(res, fallback);
    } catch (err) { toast.error(err.message); }
  };

  return (
    <Box>
      <PageHeader title="Reportes" />
      <Tabs
        value={modo}
        onChange={(e, v) => { setModo(v); setAthleteId(''); }}
        sx={{ mb: 2, borderBottom: 1, borderColor: 'divider' }}
      >
        <Tab label="Por equipo" value="equipo" />
        <Tab label="Por deportista" value="deportista" />
      </Tabs>

      <CascadeFilters cascade={cascade} />
      <Box sx={{ display: 'flex', gap: 2, mb: 3, maxWidth: 700, flexWrap: 'wrap' }}>
        <Box sx={{ flex: 1, minWidth: 200 }}>
          <SelectionTeam categoryId={cascade.catId} value={cascade.teamId} onChange={(e) => cascade.setTeamId(e.target.value)} required />
        </Box>
        {modo === 'deportista' && (
          <Box sx={{ flex: 1, minWidth: 200 }}>
            <SelectionField
              label="Deportista" value={athleteId} onChange={(e) => setAthleteId(e.target.value)}
              options={opcionesDeportista} disabled={!cascade.teamId}
              emptyLabel={cascade.teamId ? 'Elegir...' : 'Elegí un equipo primero'}
            />
          </Box>
        )}
      </Box>

      {modo === 'equipo' && !cascade.teamId && <Typography color="text.secondary">Elegí un equipo.</Typography>}
      {modo === 'equipo' && cascade.teamId && !loadingTeam && teamReport && (
        <TeamReportView report={teamReport} onDownload={() => descargar(endpoints.teamReportPdf(teamReport.teamId), 'reporte-equipo.pdf')} />
      )}

      {modo === 'deportista' && !athleteId && <Typography color="text.secondary">Elegí un equipo y despues un deportista de su nomina.</Typography>}
      {modo === 'deportista' && athleteId && !loadingAthlete && athleteReport && (
        <AthleteReportView report={athleteReport} onDownload={() => descargar(endpoints.athleteReportPdf(athleteReport.athleteId), 'reporte-deportista.pdf')} />
      )}
    </Box>
  );
}

function TeamReportView({ report, onDownload }) {
  return (
    <Box>
      <Paper variant="outlined" sx={{ borderLeft: 4, borderLeftColor: 'primary.main', p: 2, mb: 3 }}>
        <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: 1 }}>
          <Box>
            <Typography variant="h6">{report.teamName}{report.clubName ? ` — ${report.clubName}` : ''}</Typography>
            <Typography variant="body2" color="text.secondary">
              {report.competitionName} · {report.categoryName} · {report.sportName}
            </Typography>
          </Box>
          <Button variant="outlined" startIcon={<Iconify icon="mdi:file-pdf-box" />} onClick={onDownload}>Descargar PDF</Button>
        </Box>
      </Paper>

      {report.isJudged ? <ClassificationSummary report={report} /> : <StandingSummary standing={report.standing} />}

      {!report.isJudged && (
        <>
          <Divider sx={{ my: 2 }} />
          <Typography variant="subtitle1" sx={{ mb: 1 }}>Partidos</Typography>
          <DataGrid
            rows={report.matches} columns={matchColumns} autoHeight disableRowSelectionOnClick
            getRowId={(r) => r.matchId} rowHeight={52} getRowClassName={zebraRowClassName} sx={zebraSx}
            slots={{ noRowsOverlay: () => <Box sx={{ p: 2 }}><Typography color="text.secondary">Todavía no tiene partidos cargados.</Typography></Box> }}
          />

          {report.leaders.length > 0 && (
            <>
              <Divider sx={{ my: 2 }} />
              <Typography variant="subtitle1" sx={{ mb: 1 }}>Figuras del equipo</Typography>
              <LeadersChart leaders={report.leaders} />
            </>
          )}
        </>
      )}
    </Box>
  );
}

function StandingSummary({ standing }) {
  const theme = useTheme();

  if (!standing) {
    return <Typography color="text.secondary">Todavía no tiene partidos que cuenten para la tabla.</Typography>;
  }

  const record = [
    { id: 0, label: 'Ganados', value: standing.won, color: theme.palette.success.main },
    { id: 1, label: 'Empatados', value: standing.drawn, color: theme.palette.grey[500] },
    { id: 2, label: 'Perdidos', value: standing.lost, color: theme.palette.error.main },
  ].filter((slice) => slice.value > 0);

  return (
    <Box sx={{ display: 'flex', gap: 4, flexWrap: 'wrap', alignItems: 'center' }}>
      <Box sx={{ display: 'flex', gap: 1.5, flexWrap: 'wrap' }}>
        <StatCard label={`Posición${standing.groupLabel ? ` · Grupo ${standing.groupLabel}` : ''}`} value={`${standing.position}º`} />
        <StatCard label="PJ" value={standing.played} />
        <StatCard label="Ganados" value={standing.won} color={theme.palette.success.main} />
        <StatCard label="Empatados" value={standing.drawn} color={theme.palette.grey[600]} />
        <StatCard label="Perdidos" value={standing.lost} color={theme.palette.error.main} />
        <StatCard label="GF-GC" value={`${standing.scoreFor}-${standing.scoreAgainst} (${standing.scoreDifference >= 0 ? '+' : ''}${standing.scoreDifference})`} />
        <StatCard label="Puntos" value={standing.points} color={theme.palette.primary.main} />
      </Box>

      {record.length > 0 && (
        <PieChart
          series={[{
            data: record,
            innerRadius: 30,
            outerRadius: 58,
            paddingAngle: 3,
            cornerRadius: 4,
            highlightScope: { fade: 'global', highlight: 'item' },
          }]}
          width={230}
          height={140}
          slotProps={{ legend: { direction: 'column' } }}
        />
      )}
    </Box>
  );
}

function ClassificationSummary({ report }) {
  const theme = useTheme();

  if (report.score == null) {
    return <Typography color="text.secondary">Todavía no tiene puntaje cargado en esta clasificación.</Typography>;
  }

  return (
    <Box sx={{ display: 'flex', gap: 1.5, flexWrap: 'wrap' }}>
      <StatCard label="Puntaje" value={(report.score / 100).toFixed(2)} color={theme.palette.primary.main} />
      <StatCard label="Clasificación" value={report.classificationPosition ? `${report.classificationPosition}º` : 'Sin posición aún'} />
      <StatCard label="Estado" value={report.performanceStatus === 'scored' ? 'Puntuado' : 'Pendiente'} />
    </Box>
  );
}

/// Una tarjeta chica por numero: reemplaza la fila de texto plano que tenia
/// cada resumen antes -- un vistazo alcanza para leerla, no hace falta parsear
/// una linea densa como "PJ 5 · G 3 · E 1 · P 1".
function StatCard({ label, value, color }) {
  return (
    <Paper
      variant="outlined"
      sx={{ px: 2, py: 1.25, minWidth: 96, textAlign: 'center', borderTop: 3, borderTopColor: color || 'primary.main' }}
    >
      <Typography variant="h5" fontWeight={700} sx={{ color: color || 'primary.main', lineHeight: 1.2 }}>{value}</Typography>
      <Typography variant="caption" color="text.secondary">{label}</Typography>
    </Paper>
  );
}

function LeadersChart({ leaders }) {
  const theme = useTheme();
  const dataset = leaders.map((leader) => ({
    nombre: `${leader.athleteName}${leader.jerseyNumber ? ` #${leader.jerseyNumber}` : ''} · ${leader.metricLabel}`,
    total: leader.total,
  }));

  return (
    <BarChart
      dataset={dataset}
      layout="horizontal"
      yAxis={[{ scaleType: 'band', dataKey: 'nombre' }]}
      series={[{ dataKey: 'total', color: theme.palette.primary.main, label: 'Total' }]}
      height={Math.max(120, leaders.length * 46)}
      margin={{ left: 190, right: 24 }}
      grid={{ vertical: true }}
      hideLegend
    />
  );
}

function AthleteReportView({ report, onDownload }) {
  const age = ageFromBirthDate(report.birthDate);

  return (
    <Box>
      <Paper variant="outlined" sx={{ borderLeft: 4, borderLeftColor: 'primary.main', p: 2, mb: 3 }}>
        <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: 1 }}>
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5 }}>
            {/* Sin link a un silueta generica cuando no hay foto real -- el
                propio Avatar ya trae su icono de respaldo, igual que en
                clubs-page/competitions-page para logos. La silueta sólo se
                dibuja como pixeles dentro del PDF (ver AthleteReportPdf). */}
            <Avatar src={report.photoUrl || undefined} sx={{ width: 56, height: 56, bgcolor: 'action.hover' }}>
              {!report.photoUrl && <Iconify icon="mdi:account" width={32} sx={{ color: 'text.disabled' }} />}
            </Avatar>
            <Box>
              <Typography variant="h6">{report.firstName} {report.lastName}</Typography>
              <Typography variant="body2" color="text.secondary">
                Doc. {report.documentId} · {report.birthDate} ({age} años){report.gender ? ` · ${report.gender}` : ''}
                {report.weightKg != null ? ` · ${report.weightKg} kg` : ''}
              </Typography>
            </Box>
          </Box>
          <Button variant="outlined" startIcon={<Iconify icon="mdi:file-pdf-box" />} onClick={onDownload}>Descargar PDF</Button>
        </Box>
      </Paper>

      {report.entries.length === 0 && <Typography color="text.secondary">Sin inscripciones registradas.</Typography>}

      {report.entries.map((entry) => (
        <Paper key={entry.rosterEntryId} variant="outlined" sx={{ mb: 3, p: 2, borderTop: 3, borderTopColor: 'primary.main' }}>
          <Typography variant="subtitle1">
            {entry.teamName} — {entry.categoryName}
            {entry.withdrawn && <Chip label="Retirado" size="small" color="default" variant="outlined" sx={{ ml: 1 }} />}
          </Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 1.5 }}>
            {entry.competitionName} · {entry.sportName}
          </Typography>

          {entry.isJudged ? (
            <ClassificationSummary report={entry} />
          ) : (
            <>
              {entry.metrics.length > 0 && (
                <Box sx={{ display: 'flex', gap: 3, flexWrap: 'wrap', alignItems: 'center', mb: 1.5 }}>
                  <Box sx={{ display: 'flex', gap: 1.5, flexWrap: 'wrap' }}>
                    {entry.metrics.map((m) => <StatCard key={m.metricLabel} label={m.metricLabel} value={m.total} />)}
                  </Box>
                  {entry.metrics.length > 1 && <MetricsChart metrics={entry.metrics} />}
                </Box>
              )}
              {entry.metrics.length === 0 && (
                <Typography variant="body2" color="text.secondary" sx={{ mb: 1.5 }}>Sin estadísticas propias registradas todavía.</Typography>
              )}
              <DataGrid
                rows={entry.matches} columns={matchColumns} autoHeight disableRowSelectionOnClick
                getRowId={(r) => r.matchId} rowHeight={52} getRowClassName={zebraRowClassName} sx={zebraSx}
                slots={{ noRowsOverlay: () => <Box sx={{ p: 2 }}><Typography color="text.secondary">Su equipo todavía no tiene partidos cargados.</Typography></Box> }}
              />
            </>
          )}
        </Paper>
      ))}
    </Box>
  );
}

function MetricsChart({ metrics }) {
  const theme = useTheme();

  return (
    <BarChart
      dataset={metrics.map((m) => ({ etiqueta: m.metricLabel, total: m.total }))}
      xAxis={[{ scaleType: 'band', dataKey: 'etiqueta' }]}
      series={[{ dataKey: 'total', color: theme.palette.primary.main, label: 'Total' }]}
      width={Math.max(180, metrics.length * 90)}
      height={140}
      margin={{ top: 10, bottom: 30 }}
      hideLegend
    />
  );
}

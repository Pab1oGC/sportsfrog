import { useState } from 'react';
import Box from '@mui/material/Box';
import Paper from '@mui/material/Paper';
import Typography from '@mui/material/Typography';
import TextField from '@mui/material/TextField';
import MenuItem from '@mui/material/MenuItem';
import Button from '@mui/material/Button';
import IconButton from '@mui/material/IconButton';
import Tabs from '@mui/material/Tabs';
import Tab from '@mui/material/Tab';
import { DataGrid } from '@mui/x-data-grid';
import { toast } from 'sonner';
import { Iconify } from 'src/components/iconify';
import { useCrudDialog } from 'src/hooks/use-crud';
import { apiPut, apiPost } from 'src/hooks/use-api';
import { endpoints } from 'src/lib/axios';
import { PageHeader } from 'src/components/page-header';
import { CrudDialog } from 'src/components/crud-dialog';
import { RowActionsMenu } from 'src/components/row-actions-menu';
import { EstadoChip } from 'src/components/estado-chip';
import { useConfirm } from 'src/components/confirm-dialog';
import { SelectionSport, SelectionSpace } from 'src/components/selectors';
import { DateField, TimeField } from 'src/components/date-field';
import { bloquearNoEnteros, soloDigitos, bloquearNegativos, soloDecimales } from 'src/lib/entero-sin-signo';
import { EnrollmentsTab } from 'src/pages/courses/enrollments-tab';
import { SessionsTab } from 'src/pages/courses/sessions-tab';
import { AttendanceSummaryTab } from 'src/pages/courses/attendance-summary-tab';

// Domingo primero porque asi enumera .NET DayOfWeek (y Date#getDay de JS):
// el indice de este arreglo es exactamente el numero que viaja en schedules.
const DIAS_SEMANA = ['Domingo', 'Lunes', 'Martes', 'Miercoles', 'Jueves', 'Viernes', 'Sabado'];
const DIAS_ABREVIADOS = ['Dom', 'Lun', 'Mar', 'Mie', 'Jue', 'Vie', 'Sab'];

const nuevaFranja = () => ({ day: 1, startsAt: '18:00', endsAt: '19:00' });

const emptyForm = () => ({
  name: '',
  sportCode: '',
  venueSpaceId: '',
  capacity: '',
  monthlyFeeBob: '',
  startsOn: '',
  endsOn: '',
  isActive: true,
  schedules: [nuevaFranja()],
});

// Franjas ya ordenadas por dia y hora -- ver ReadCourses.Project del lado
// del servidor, que entrega el mismo orden -- asi que alcanza con
// recorrerlas tal cual llegan.
function resumenHorario(schedules) {
  if (!schedules || schedules.length === 0) return '—';
  return schedules.map((s) => `${DIAS_ABREVIADOS[s.day]} ${s.startsAt}-${s.endsAt}`).join(', ');
}

export default function CoursesPage() {
  const confirm = useConfirm();
  const [selected, setSelected] = useState(null);
  const [tab, setTab] = useState('enrollments');

  const {
    rows: courses, isLoading, mutate, open, editId, form, setForm, error, saving,
    openCreate, openEdit, close, save, remove,
  } = useCrudDialog({
    resourceUrl: endpoints.courses,
    emptyForm,
    entityName: 'curso',
    savedMessage: 'Curso guardado.',
    buildUrl: (base, id) => endpoints.course(id),
    mapToForm: (row) => ({
      name: row.name || '',
      sportCode: row.sportCode || '',
      venueSpaceId: row.venueSpaceId || '',
      capacity: row.capacity ?? '',
      monthlyFeeBob: row.monthlyFeeBob ?? '',
      startsOn: row.startsOn || '',
      endsOn: row.endsOn || '',
      isActive: row.isActive !== false,
      schedules: row.schedules && row.schedules.length > 0 ? row.schedules : [nuevaFranja()],
    }),
    mapToSend: (f) => ({
      name: f.name,
      sportCode: f.sportCode,
      // coachMembershipId: no hay todavia una pantalla que lea la nomina de
      // miembros de la organizacion (GET /members no existe aun), asi que
      // un curso no se le asigna un profesor hasta que ese picker exista.
      coachMembershipId: null,
      venueSpaceId: f.venueSpaceId || null,
      capacity: f.capacity !== '' ? Number(f.capacity) : null,
      monthlyFeeBob: f.monthlyFeeBob !== '' ? Number(f.monthlyFeeBob) : 0,
      startsOn: f.startsOn,
      endsOn: f.endsOn || null,
      isActive: !!f.isActive,
      schedules: f.schedules.map((s) => ({ day: Number(s.day), startsAt: s.startsAt, endsAt: s.endsAt })),
    }),
  });

  // Igual razonamiento que VenuesPage.toggleVenueActive: desactivar saca el
  // curso de la lista para inscripciones nuevas, asi que vale la pena
  // avisar antes en vez de solo cambiar el estado en silencio.
  const toggleActive = async (row) => {
    if (row.isActive) {
      const ok = await confirm(
        `Desactivar "${row.name}"? Deja de ofrecerse para inscripciones nuevas. Lo que ya esta inscrito no se ve afectado.`,
        { confirmLabel: 'Desactivar', danger: true },
      );
      if (!ok) return;
    }
    try {
      await apiPut(endpoints.course(row.id), {
        name: row.name,
        sportCode: row.sportCode,
        coachMembershipId: row.coachMembershipId || null,
        venueSpaceId: row.venueSpaceId || null,
        capacity: row.capacity || null,
        monthlyFeeBob: row.monthlyFeeBob,
        startsOn: row.startsOn,
        endsOn: row.endsOn || null,
        isActive: !row.isActive,
        schedules: row.schedules,
      });
      mutate();
    } catch (err) { toast.error(err.message); }
  };

  // Borrar un curso deja sin sentido el panel de al lado si era el
  // seleccionado -- mismo motivo que VenuesPage.deleteVenue cierra el panel
  // de espacios.
  const deleteCourse = async (id) => {
    if (await remove(id) && selected?.id === id) setSelected(null);
  };

  const selectCourse = (row) => {
    setSelected(row);
    setTab('enrollments');
  };

  // Devengo de pensiones --------------------------------------------------
  // Org-wide a propósito: AccrueTuition no recibe un curso puntual, carga
  // el mes de todas las inscripciones vigentes de la organización -- ver
  // TuitionAccrual.cs del lado del servidor.
  const [accruing, setAccruing] = useState(false);

  const accrueTuition = async () => {
    const hoy = new Date();
    const ok = await confirm(
      `Generar la pensión de ${hoy.getMonth() + 1}/${hoy.getFullYear()} para todas las inscripciones vigentes de la organización? Lo que ya se generó para este mes no se duplica.`,
    );
    if (!ok) return;
    setAccruing(true);
    try {
      const result = await apiPost(endpoints.tuitionAccruals, { year: hoy.getFullYear(), month: hoy.getMonth() + 1 });
      toast.success(
        result.alreadyCharged > 0
          ? `${result.charged} pensión(es) nueva(s), ${result.alreadyCharged} ya estaban generadas.`
          : `${result.charged} pensión(es) generada(s).`,
      );
    } catch (err) { toast.error(err.message); } finally { setAccruing(false); }
  };

  const agregarFranja = () => setForm({ ...form, schedules: [...form.schedules, nuevaFranja()] });
  const quitarFranja = (i) => setForm({ ...form, schedules: form.schedules.filter((_, idx) => idx !== i) });
  const editarFranja = (i, cambios) =>
    setForm({ ...form, schedules: form.schedules.map((s, idx) => (idx === i ? { ...s, ...cambios } : s)) });

  const columns = [
    { field: 'name', headerName: 'Nombre', flex: 1, minWidth: 160 },
    { field: 'sportName', headerName: 'Deporte', width: 120 },
    { field: 'schedules', headerName: 'Horario', flex: 1.4, minWidth: 220, sortable: false,
      renderCell: ({ value }) => <Box sx={{ fontSize: 13 }}>{resumenHorario(value)}</Box> },
    { field: 'monthlyFeeBob', headerName: 'Pension', width: 100, renderCell: ({ value }) => `Bs ${value}` },
    { field: 'capacity', headerName: 'Cupo', width: 80, renderCell: ({ value }) => value || '—' },
    { field: 'isActive', headerName: 'Estado', width: 110, sortable: false,
      renderCell: ({ value, row }) => <EstadoChip activo={value} onClick={() => toggleActive(row)} /> },
    { field: 'actions', headerName: 'Acciones', width: 110, align: 'center', headerAlign: 'center',
      renderCell: ({ row }) => (
        <RowActionsMenu
          primary={[
            { icon: 'eva:edit-fill', label: 'Editar', onClick: () => openEdit(row) },
            { icon: 'eva:trash-2-outline', label: 'Eliminar', color: 'error.main', onClick: () => deleteCourse(row.id) },
          ]}
          actions={[
            { icon: 'eva:people-outline', label: 'Administrar', color: selected?.id === row.id ? 'primary.main' : 'text.secondary', onClick: () => selectCourse(row) },
          ]}
        />
      ) },
  ];

  return (
    <Box>
      <PageHeader title="Cursos" actionLabel="Nuevo curso" onAction={openCreate}>
        <Button
          variant="outlined" size="small" disabled={accruing}
          startIcon={<Iconify icon="mdi:cash-sync" width={16} />}
          onClick={accrueTuition}
        >
          Generar pensión del mes
        </Button>
      </PageHeader>

      <Paper variant="outlined" sx={{ p: 2, mb: selected ? 3 : 0 }}>
        <Typography variant="h6" fontWeight={600} sx={{ mb: 1.5 }}>Cursos</Typography>
        <DataGrid rows={courses || []} columns={columns} loading={isLoading} autoHeight disableRowSelectionOnClick getRowId={(r) => r.id}
          onRowClick={(p) => selectCourse(p.row)} sx={{ cursor: 'pointer' }} />
        {!selected && (
          <Typography color="text.secondary" variant="body2" sx={{ mt: 1.5 }}>
            Hacé clic en un curso para administrar sus inscripciones, su calendario y su asistencia.
          </Typography>
        )}
      </Paper>

      {selected && (
        <Paper variant="outlined" sx={{ p: 2 }}>
          <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 1.5 }}>
            <Typography variant="h6" fontWeight={600}>{selected.name}</Typography>
            <IconButton size="small" onClick={() => setSelected(null)}>
              <Iconify icon="eva:close-fill" width={20} />
            </IconButton>
          </Box>
          <Tabs value={tab} onChange={(_, v) => setTab(v)} sx={{ mb: 2 }}>
            <Tab value="enrollments" label="Inscripciones" />
            <Tab value="sessions" label="Calendario" />
            <Tab value="attendance" label="Asistencia" />
          </Tabs>
          {tab === 'enrollments' && <EnrollmentsTab course={selected} />}
          {tab === 'sessions' && <SessionsTab course={selected} />}
          {tab === 'attendance' && <AttendanceSummaryTab course={selected} />}
        </Paper>
      )}

      <CrudDialog open={open} editId={editId} entityName="Curso" error={error} saving={saving} onClose={close} onSave={save} maxWidth="md">
        <TextField label="Nombre" value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} fullWidth required />
        <SelectionSport value={form.sportCode} onChange={(e) => setForm({ ...form, sportCode: e.target.value })} required />
        <SelectionSpace value={form.venueSpaceId} onChange={(e) => setForm({ ...form, venueSpaceId: e.target.value })} required={false} />

        <Box sx={{ display: 'flex', gap: 2, flexWrap: 'wrap' }}>
          <TextField
            label="Cupo" type="number" value={form.capacity} sx={{ flex: 1, minWidth: 120 }}
            onChange={(e) => setForm({ ...form, capacity: soloDigitos(e.target.value) })}
            onKeyDown={bloquearNoEnteros}
            slotProps={{ htmlInput: { min: 1, step: 1 } }}
            helperText="Vacio para no ponerle limite."
          />
          <TextField
            label="Pension mensual (Bs)" type="number" value={form.monthlyFeeBob} sx={{ flex: 1, minWidth: 160 }} required
            onChange={(e) => setForm({ ...form, monthlyFeeBob: soloDecimales(e.target.value) })}
            onKeyDown={bloquearNegativos}
            slotProps={{ htmlInput: { min: 0, step: 0.01 } }}
          />
        </Box>

        <Box sx={{ display: 'flex', gap: 2, flexWrap: 'wrap' }}>
          <DateField label="Fecha de inicio" value={form.startsOn} onChange={(e) => setForm({ ...form, startsOn: e.target.value })} fullWidth required sx={{ flex: 1, minWidth: 160 }} />
          <DateField label="Fecha de fin" value={form.endsOn} onChange={(e) => setForm({ ...form, endsOn: e.target.value })} fullWidth sx={{ flex: 1, minWidth: 160 }} helperText="Vacio para un curso sin fin definido." />
        </Box>

        <Box>
          <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 1 }}>
            <Typography variant="subtitle2">Horario semanal</Typography>
            <Button size="small" startIcon={<Iconify icon="eva:plus-fill" width={16} />} onClick={agregarFranja}>Agregar franja</Button>
          </Box>
          {form.schedules.length === 0 && (
            <Typography variant="body2" color="text.secondary">Sin franjas: el curso no se podria dictar.</Typography>
          )}
          {form.schedules.map((s, i) => (
            <Box key={i} sx={{ display: 'flex', alignItems: 'center', gap: 1.5, p: 1, mb: 1, border: '1px solid', borderColor: 'divider', borderRadius: 1 }}>
              <TextField
                select label="Dia" size="small" value={s.day} sx={{ flex: 1, minWidth: 110 }}
                onChange={(e) => editarFranja(i, { day: Number(e.target.value) })}
              >
                {DIAS_SEMANA.map((nombre, dia) => <MenuItem key={dia} value={dia}>{nombre}</MenuItem>)}
              </TextField>
              <TimeField label="Desde" value={s.startsAt} onChange={(e) => editarFranja(i, { startsAt: e.target.value })} size="small" sx={{ flex: 1, minWidth: 110 }} />
              <TimeField label="Hasta" value={s.endsAt} onChange={(e) => editarFranja(i, { endsAt: e.target.value })} size="small" sx={{ flex: 1, minWidth: 110 }} />
              <IconButton size="small" onClick={() => quitarFranja(i)} disabled={form.schedules.length === 1}>
                <Iconify icon="eva:trash-2-outline" width={18} sx={{ color: 'error.main' }} />
              </IconButton>
            </Box>
          ))}
        </Box>
      </CrudDialog>
    </Box>
  );
}

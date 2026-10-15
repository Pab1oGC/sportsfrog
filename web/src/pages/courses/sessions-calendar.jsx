import { useState } from 'react';
import Box from '@mui/material/Box';
import Menu from '@mui/material/Menu';
import MenuItem from '@mui/material/MenuItem';
import ListItemIcon from '@mui/material/ListItemIcon';
import ListItemText from '@mui/material/ListItemText';
import Divider from '@mui/material/Divider';
import Typography from '@mui/material/Typography';
import Chip from '@mui/material/Chip';
import { useTheme } from '@mui/material/styles';
import dayjs from 'dayjs';
import FullCalendar from '@fullcalendar/react';
import dayGridPlugin from '@fullcalendar/daygrid';
import timeGridPlugin from '@fullcalendar/timegrid';
import interactionPlugin from '@fullcalendar/interaction';
import esLocale from '@fullcalendar/core/locales/es';
import { Iconify } from 'src/components/iconify';

// Mismos tres valores de CourseSessionState que ESTADOS en SessionsTab,
// resueltos a un color real: FullCalendar pinta cada evento con
// backgroundColor/borderColor/textColor directo, no con el `color` de un
// <Chip> de MUI.
function colorsFor(theme, status) {
  if (status === 'held') {
    return { bg: theme.palette.success.main, border: theme.palette.success.dark, text: theme.palette.success.contrastText };
  }
  if (status === 'cancelled') {
    return { bg: theme.palette.grey[300], border: theme.palette.grey[400], text: theme.palette.text.disabled };
  }
  return { bg: theme.palette.primary.main, border: theme.palette.primary.dark, text: theme.palette.primary.contrastText };
}

const ESTADOS_LABEL = { scheduled: 'Programada', held: 'Dictada', cancelled: 'Cancelada' };

/**
 * El calendario de un curso, en forma de calendario real (FullCalendar) en
 * vez de una tabla: cada sesión es un evento en su fecha, con su horario y
 * el color de su estado -- ver el historial de esta página para el porqué.
 *
 * Arrastrar un evento a otro día u otra hora reprograma la sesión:
 * RescheduleSession.cs ya acepta exactamente eso (fecha y horario nuevos)
 * para cualquier sesión que no esté cancelada, así que `onDragReschedule`
 * es el mismo PUT que ya hacía el diálogo de reprogramar, sin el diálogo.
 * Una sesión cancelada no se puede arrastrar (`editable: false` por
 * evento) por la misma razón que RescheduleSession la rechaza del lado del
 * servidor: reprogramar algo tachado del calendario no es corregirle la
 * fecha, es reintegrarlo primero.
 *
 * Todas las acciones (asistencia, reprogramar a mano, cancelar, reintegrar)
 * siguen viviendo en los diálogos que ya tenía SessionsTab -- este
 * componente solo decide qué mostrar y abre un menú al hacer clic en una
 * sesión; quién hace cada cosa sigue siendo SessionsTab, por las props que
 * recibe.
 */
export function SessionsCalendar({ sessions, onAttendance, onReschedule, onCancel, onReinstate, onDragReschedule }) {
  const theme = useTheme();
  const [menu, setMenu] = useState({ anchorEl: null, session: null });

  const closeMenu = () => setMenu({ anchorEl: null, session: null });

  const events = (sessions || []).map((session) => {
    const colors = colorsFor(theme, session.status);
    return {
      id: session.id,
      // No se ve -- eventContent la reemplaza entera -- pero FullCalendar
      // la usa igual para el aria-label del evento.
      title: `${session.startsAt}–${session.endsAt} ${ESTADOS_LABEL[session.status] || session.status}`,
      start: `${session.sessionDate}T${session.startsAt}`,
      end: `${session.sessionDate}T${session.endsAt}`,
      backgroundColor: colors.bg,
      borderColor: colors.border,
      textColor: colors.text,
      editable: session.status !== 'cancelled',
      extendedProps: { session },
    };
  });

  // Dos lineas en vez del titulo de una sola que ponia FullCalendar por
  // defecto -- horario en negrita, estado abajo -- para que el bloque de
  // color (eventDisplay: 'block', mas abajo) tenga algo que llenarlo en vez
  // de quedar con una sola linea de texto chico flotando en el medio.
  const renderEventContent = (arg) => {
    const { session: s } = arg.event.extendedProps;
    return (
      <Box sx={{ overflow: 'hidden', lineHeight: 1.3, width: '100%' }}>
        <Box sx={{ fontWeight: 700, fontSize: '0.78rem', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
          {s.startsAt}–{s.endsAt}
        </Box>
        <Box sx={{ fontSize: '0.7rem', opacity: 0.9 }}>{ESTADOS_LABEL[s.status] || s.status}</Box>
      </Box>
    );
  };

  const handleEventDrop = (info) => {
    const { session } = info.event.extendedProps;
    onDragReschedule(
      session,
      {
        sessionDate: dayjs(info.event.start).format('YYYY-MM-DD'),
        startsAt: dayjs(info.event.start).format('HH:mm'),
        endsAt: dayjs(info.event.end).format('HH:mm'),
      },
      info.revert,
    );
  };

  const { session } = menu;

  return (
    <Box
      sx={{
        '.fc': { fontFamily: 'inherit' },
        '.fc .fc-toolbar-title': { fontSize: '1.15rem', fontWeight: 700 },
        '.fc .fc-button': {
          backgroundColor: 'transparent',
          borderColor: theme.palette.divider,
          color: theme.palette.text.primary,
          textTransform: 'capitalize',
          boxShadow: 'none',
        },
        '.fc .fc-button:hover': { backgroundColor: theme.palette.action.hover },
        '.fc .fc-button-primary:not(:disabled).fc-button-active': {
          backgroundColor: `${theme.palette.action.selected} !important`,
          borderColor: theme.palette.divider,
        },
        // Celdas de dia mas altas: con eventDisplay: 'block' el bloque de
        // dos lineas de arriba necesita mas lugar del que FullCalendar le
        // da por defecto, o se ve tan apretado como el punto que remplaza.
        '.fc .fc-daygrid-day-frame': { minHeight: 112 },
        '.fc .fc-event': {
          cursor: 'pointer',
          borderRadius: 0,
          padding: '3px 6px',
          marginBottom: 2,
        },
        '.fc .fc-daygrid-day.fc-day-today, .fc .fc-timegrid-col.fc-day-today': {
          backgroundColor: `${theme.palette.primary.main}2E`,
        },
        '.fc .fc-day-today .fc-daygrid-day-number': {
          color: theme.palette.primary.main,
          fontWeight: 700,
        },
      }}
    >
      <FullCalendar
        plugins={[dayGridPlugin, timeGridPlugin, interactionPlugin]}
        initialView="dayGridMonth"
        headerToolbar={{ left: 'prev,next today', center: 'title', right: 'dayGridMonth,timeGridWeek,timeGridDay' }}
        locale={esLocale}
        height="auto"
        events={events}
        eventDisplay="block"
        eventContent={renderEventContent}
        editable
        eventClick={(info) => setMenu({ anchorEl: info.el, session: info.event.extendedProps.session })}
        eventDrop={handleEventDrop}
        eventResize={handleEventDrop}
        dayMaxEventRows={4}
      />

      <Menu anchorEl={menu.anchorEl} open={!!menu.anchorEl} onClose={closeMenu}>
        {session && (
          <Box sx={{ px: 2, py: 1 }}>
            <Typography variant="subtitle2" sx={{ textTransform: 'capitalize' }}>
              {dayjs(session.sessionDate).format('dddd D [de] MMMM')}
            </Typography>
            <Typography variant="body2" color="text.secondary" sx={{ mb: 0.75 }}>
              {session.startsAt}–{session.endsAt}
            </Typography>
            <Chip size="small" label={ESTADOS_LABEL[session.status] || session.status} />
            {session.status === 'cancelled' && session.cancellationReason && (
              <Typography variant="body2" color="text.secondary" sx={{ mt: 0.75, maxWidth: 260 }}>
                {session.cancellationReason}
              </Typography>
            )}
          </Box>
        )}
        <Divider />
        {session && session.status !== 'cancelled' && (
          <MenuItem onClick={() => { onAttendance(session); closeMenu(); }}>
            <ListItemIcon><Iconify icon="eva:people-outline" width={18} /></ListItemIcon>
            <ListItemText>Tomar asistencia</ListItemText>
          </MenuItem>
        )}
        {session && session.status !== 'cancelled' && (
          <MenuItem onClick={() => { onReschedule(session); closeMenu(); }}>
            <ListItemIcon><Iconify icon="eva:calendar-outline" width={18} /></ListItemIcon>
            <ListItemText>Reprogramar</ListItemText>
          </MenuItem>
        )}
        {session && session.status !== 'cancelled' && (
          <MenuItem onClick={() => { onCancel(session); closeMenu(); }}>
            <ListItemIcon><Iconify icon="eva:close-circle-outline" width={18} sx={{ color: 'warning.main' }} /></ListItemIcon>
            <ListItemText>Cancelar</ListItemText>
          </MenuItem>
        )}
        {session && session.status === 'cancelled' && (
          <MenuItem onClick={() => { onReinstate(session); closeMenu(); }}>
            <ListItemIcon><Iconify icon="eva:undo-fill" width={18} sx={{ color: 'info.main' }} /></ListItemIcon>
            <ListItemText>Reintegrar</ListItemText>
          </MenuItem>
        )}
      </Menu>
    </Box>
  );
}

import { alpha, useTheme } from '@mui/material/styles';
import { JETBRAINS_MONO_STACK } from 'src/theme/fonts';
import { useScrollReveal } from 'src/pages/landing/use-scroll-reveal';
import { VisualFrame } from './visual-frame';

const VENUES = ['Cancha 1', 'Cancha 2', 'Cancha 3', 'Cancha 4'];
const TIME_SLOTS = ['09:00', '11:00', '14:00', '16:00'];
// Qué celdas (fila de horario x columna de sede) tienen un partido cargado
// esta jornada -- a propósito no todas, para que la grilla se vea real y no
// un tablero completo e irreal.
const MATCHES = [
  { row: 0, col: 0 },
  { row: 0, col: 2 },
  { row: 1, col: 1 },
  { row: 1, col: 3 },
  { row: 2, col: 0 },
  { row: 2, col: 2 },
  { row: 3, col: 1 },
];

const GRID_LEFT = 200;
const GRID_TOP = 180;
const COL_WIDTH = 290;
const ROW_HEIGHT = 200;

/**
 * El gráfico de la sección "Calendario": una jornada repartida entre sedes
 * (columnas) y horarios (filas), como describe content.js ("El sistema
 * reparte los cruces de cada jornada entre sedes y horarios"). No todas las
 * celdas tienen partido -- una jornada real no llena la grilla entera.
 */
export function CalendarVisual() {
  const theme = useTheme();
  const scopeRef = useScrollReveal({ selector: '.visual-item', from: { stagger: 0.06, duration: 0.5 } });

  return (
    <VisualFrame ref={scopeRef}>
      <g className="visual-item">
        <rect x={60} y={50} width={220} height={64} rx={999} fill={alpha(theme.palette.primary.main, 0.1)} />
        <text x={88} y={91} fontSize={26} fontWeight={700} fill={theme.palette.primary.main} fontFamily={JETBRAINS_MONO_STACK}>
          Jornada 3
        </text>
      </g>

      {VENUES.map((venue, col) => (
        <text
          key={venue}
          className="visual-item"
          x={GRID_LEFT + col * COL_WIDTH + COL_WIDTH / 2 - 20}
          y={GRID_TOP - 24}
          fontSize={24}
          fontWeight={700}
          fill={theme.palette.text.primary}
        >
          {venue}
        </text>
      ))}

      {TIME_SLOTS.map((time, row) => (
        <text
          key={time}
          className="visual-item"
          x={60}
          y={GRID_TOP + row * ROW_HEIGHT + ROW_HEIGHT / 2 + 8}
          fontSize={24}
          fill={theme.palette.text.secondary}
          fontFamily={JETBRAINS_MONO_STACK}
        >
          {time}
        </text>
      ))}

      {/* la grilla en sí: solo líneas, sin animación propia -- son el fondo contra el que los partidos aparecen */}
      {TIME_SLOTS.map((_, row) => (
        <line
          key={`h-${row}`}
          x1={GRID_LEFT - 20}
          y1={GRID_TOP + row * ROW_HEIGHT}
          x2={GRID_LEFT + VENUES.length * COL_WIDTH}
          y2={GRID_TOP + row * ROW_HEIGHT}
          stroke={theme.palette.divider}
        />
      ))}
      {VENUES.map((_, col) => (
        <line
          key={`v-${col}`}
          x1={GRID_LEFT + col * COL_WIDTH}
          y1={GRID_TOP - 20}
          x2={GRID_LEFT + col * COL_WIDTH}
          y2={GRID_TOP + TIME_SLOTS.length * ROW_HEIGHT}
          stroke={theme.palette.divider}
        />
      ))}

      {MATCHES.map((match) => {
        const x = GRID_LEFT + match.col * COL_WIDTH + 18;
        const y = GRID_TOP + match.row * ROW_HEIGHT + 18;
        const width = COL_WIDTH - 36;
        const height = ROW_HEIGHT - 36;
        return (
          <g key={`${match.row}-${match.col}`} className="visual-item">
            <rect x={x} y={y} width={width} height={height} rx={12} fill={theme.palette.background.paper} stroke={theme.palette.divider} strokeWidth={1.5} />
            <rect x={x + 18} y={y + 24} width={width - 36} height={18} rx={9} fill={alpha(theme.palette.text.primary, 0.6)} />
            <text x={x + 18} y={y + height / 2 + 6} fontSize={18} fill={theme.palette.text.secondary} fontFamily={JETBRAINS_MONO_STACK}>
              vs
            </text>
            <rect x={x + 18} y={y + height - 42} width={width - 36} height={18} rx={9} fill={alpha(theme.palette.text.primary, 0.3)} />
          </g>
        );
      })}

      <g className="visual-item">
        <path
          d="M 350 1010 L 430 1010"
          stroke={theme.palette.primary.main}
          strokeWidth={2.5}
          markerEnd="url(#calendar-visual-arrow)"
        />
        <defs>
          <marker id="calendar-visual-arrow" markerWidth={10} markerHeight={10} refX={6} refY={3} orient="auto">
            <path d="M0,0 L6,3 L0,6 Z" fill={theme.palette.primary.main} />
          </marker>
        </defs>
        <text x={450} y={1016} fontSize={22} fill={theme.palette.text.secondary}>
          avanza a cuartos de final
        </text>
      </g>
    </VisualFrame>
  );
}

import { alpha, useTheme } from '@mui/material/styles';
import { JETBRAINS_MONO_STACK } from 'src/theme/fonts';
import { useScrollReveal } from 'src/pages/landing/use-scroll-reveal';
import { VisualFrame } from './visual-frame';

// Nombres ilustrativos, no datos reales de ninguna organización -- el punto
// es mostrar la forma de la tabla, no afirmar un resultado.
const STANDINGS = [
  { team: 'Atlético Sur', played: 9, diff: '+11', points: 22 },
  { team: 'Real Andina', played: 9, diff: '+7', points: 20 },
  { team: 'Unión Central', played: 9, diff: '+5', points: 18, justMoved: true },
  { team: 'Cóndor FC', played: 9, diff: '+2', points: 17 },
  { team: 'Deportivo Norte', played: 9, diff: '-3', points: 14 },
];

const TABLE_LEFT = 60;
const TABLE_TOP = 150;
const ROW_HEIGHT = 128;
const POS_X = TABLE_LEFT + 24;
const TEAM_X = TABLE_LEFT + 90;
const PLAYED_X = 740;
const DIFF_X = 870;
const POINTS_X = 1030;

/**
 * El gráfico de la sección "Resultados": la tabla recalculándose -- una
 * fila sube de posición (ver content.js, "nunca queda desincronizada") -- y
 * un panel de líder al costado (sub-característica "Líderes y estadísticas").
 */
export function ResultsVisual() {
  const theme = useTheme();
  const scopeRef = useScrollReveal({ selector: '.visual-item', from: { stagger: 0.07, duration: 0.5 } });

  return (
    <VisualFrame ref={scopeRef}>
      <g className="visual-item">
        <text x={TABLE_LEFT} y={110} fontSize={28} fontWeight={700} fill={theme.palette.text.primary}>
          Tabla de posiciones
        </text>
      </g>

      <g className="visual-item">
        <text x={POS_X} y={TABLE_TOP - 16} fontSize={20} fill={theme.palette.text.secondary}>
          #
        </text>
        <text x={TEAM_X} y={TABLE_TOP - 16} fontSize={20} fill={theme.palette.text.secondary}>
          Equipo
        </text>
        <text x={PLAYED_X} y={TABLE_TOP - 16} fontSize={20} fill={theme.palette.text.secondary} fontFamily={JETBRAINS_MONO_STACK}>
          PJ
        </text>
        <text x={DIFF_X} y={TABLE_TOP - 16} fontSize={20} fill={theme.palette.text.secondary} fontFamily={JETBRAINS_MONO_STACK}>
          DG
        </text>
        <text x={POINTS_X} y={TABLE_TOP - 16} fontSize={20} fill={theme.palette.text.secondary} fontFamily={JETBRAINS_MONO_STACK}>
          Pts
        </text>
        <line x1={TABLE_LEFT} y1={TABLE_TOP} x2={1382} y2={TABLE_TOP} stroke={theme.palette.divider} strokeWidth={1.5} />
      </g>

      {STANDINGS.map((row, index) => {
        const y = TABLE_TOP + index * ROW_HEIGHT;
        return (
          <g key={row.team} className="visual-item">
            {row.justMoved && <rect x={TABLE_LEFT - 20} y={y} width={1402 - TABLE_LEFT} height={ROW_HEIGHT} fill={alpha(theme.palette.primary.main, 0.06)} />}
            <text x={POS_X} y={y + ROW_HEIGHT / 2 + 10} fontSize={26} fontWeight={700} fill={theme.palette.text.primary} fontFamily={JETBRAINS_MONO_STACK}>
              {index + 1}
            </text>
            <text x={TEAM_X} y={y + ROW_HEIGHT / 2 + 10} fontSize={26} fill={theme.palette.text.primary}>
              {row.team}
            </text>
            <text x={PLAYED_X} y={y + ROW_HEIGHT / 2 + 10} fontSize={24} fill={theme.palette.text.secondary} fontFamily={JETBRAINS_MONO_STACK}>
              {row.played}
            </text>
            <text x={DIFF_X} y={y + ROW_HEIGHT / 2 + 10} fontSize={24} fill={theme.palette.text.secondary} fontFamily={JETBRAINS_MONO_STACK}>
              {row.diff}
            </text>
            <text x={POINTS_X} y={y + ROW_HEIGHT / 2 + 10} fontSize={26} fontWeight={700} fill={theme.palette.primary.main} fontFamily={JETBRAINS_MONO_STACK}>
              {row.points}
            </text>
            {row.justMoved && (
              <path
                d={`M ${POS_X - 32} ${y + ROW_HEIGHT / 2 + 10} l 10 -14 l 10 14 Z`}
                fill={theme.palette.primary.main}
              />
            )}
            <line x1={TABLE_LEFT - 20} y1={y + ROW_HEIGHT} x2={1382} y2={y + ROW_HEIGHT} stroke={theme.palette.divider} strokeWidth={1} />
          </g>
        );
      })}

      <g className="visual-item">
        <rect x={TABLE_LEFT - 20} y={850} width={1342} height={126} rx={16} fill={alpha(theme.palette.primary.main, 0.06)} stroke={theme.palette.divider} strokeWidth={1.5} />
        <text x={TABLE_LEFT + 16} y={904} fontSize={22} fill={theme.palette.text.secondary}>
          Goleador
        </text>
        <text x={TABLE_LEFT + 16} y={940} fontSize={28} fontWeight={700} fill={theme.palette.text.primary}>
          J. Pérez
        </text>
        <text x={1240} y={930} fontSize={44} fontWeight={700} fill={theme.palette.primary.main} fontFamily={JETBRAINS_MONO_STACK}>
          8
        </text>
        <text x={1240} y={960} fontSize={20} fill={theme.palette.text.secondary}>
          goles
        </text>
      </g>
    </VisualFrame>
  );
}

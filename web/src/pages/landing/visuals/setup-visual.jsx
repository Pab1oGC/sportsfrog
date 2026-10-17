import { alpha, useTheme } from '@mui/material/styles';
import { useScrollReveal } from 'src/pages/landing/use-scroll-reveal';
import { VisualFrame } from './visual-frame';

const SPORT_SHAPES = [
  { label: 'Por puntos', detail: 'Gana el marcador más alto', selected: true },
  { label: 'Mejor de sets', detail: 'El que gana más sets' },
  { label: 'Contra el reloj', detail: 'El que corta antes el tiempo' },
  { label: 'Nota del juez', detail: 'Califica un juez, no un marcador' },
];

const FORM_ROWS = ['Nombre del reglamento', 'Categoría', 'Criterio de desempate'];

/**
 * El gráfico de la sección "Reglamento": a la izquierda, las formas de
 * resultado que el sistema ya distingue (ver content.js, sub-característica
 * "Cualquier forma de resultado"); a la derecha, el formulario de un
 * reglamento nuevo validándose. No es una captura de pantalla -- es la idea
 * del formulario, abstracta, igual que los mocks de Railway.
 */
export function SetupVisual() {
  const theme = useTheme();
  const scopeRef = useScrollReveal({ selector: '.visual-item', from: { stagger: 0.07, duration: 0.5 } });

  return (
    <VisualFrame ref={scopeRef}>
      {SPORT_SHAPES.map((shape, index) => {
        const y = 90 + index * 116;
        return (
          <g key={shape.label} className="visual-item">
            <rect
              x={60}
              y={y}
              width={520}
              height={96}
              rx={14}
              fill={shape.selected ? alpha(theme.palette.primary.main, 0.08) : theme.palette.background.paper}
              stroke={shape.selected ? theme.palette.primary.main : theme.palette.divider}
              strokeWidth={shape.selected ? 2.5 : 1.5}
            />
            <text x={92} y={y + 42} fontSize={28} fontWeight={700} fill={theme.palette.text.primary}>
              {shape.label}
            </text>
            <text x={92} y={y + 72} fontSize={22} fill={theme.palette.text.secondary}>
              {shape.detail}
            </text>
          </g>
        );
      })}

      <g className="visual-item">
        <rect x={640} y={90} width={742} height={792} rx={18} fill={theme.palette.background.paper} stroke={theme.palette.divider} strokeWidth={1.5} />
        <text x={680} y={150} fontSize={30} fontWeight={700} fill={theme.palette.text.primary}>
          Nuevo reglamento
        </text>
      </g>

      {FORM_ROWS.map((label, index) => {
        const y = 210 + index * 130;
        return (
          <g key={label} className="visual-item">
            <text x={680} y={y} fontSize={22} fill={theme.palette.text.secondary}>
              {label}
            </text>
            <rect x={680} y={y + 18} width={662} height={64} rx={10} fill={theme.palette.action.hover} />
          </g>
        );
      })}

      <g className="visual-item">
        <rect x={680} y={700} width={662} height={130} rx={14} fill={alpha(theme.palette.brand.accent, 0.14)} stroke={alpha(theme.palette.brand.accent, 0.6)} strokeWidth={2} />
        <circle cx={730} cy={765} r={26} fill={theme.palette.primary.main} />
        <path d="M 718 765 L 727 775 L 744 753" stroke={theme.palette.primary.contrastText} strokeWidth={4} fill="none" strokeLinecap="round" strokeLinejoin="round" />
        <text x={776} y={758} fontSize={24} fontWeight={700} fill={theme.palette.text.primary}>
          Reglas consistentes
        </text>
        <text x={776} y={788} fontSize={20} fill={theme.palette.text.secondary}>
          Validado contra la forma del deporte
        </text>
      </g>
    </VisualFrame>
  );
}

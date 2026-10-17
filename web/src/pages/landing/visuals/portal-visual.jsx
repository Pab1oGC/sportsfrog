import { alpha, useTheme } from '@mui/material/styles';
import { JETBRAINS_MONO_STACK } from 'src/theme/fonts';
import { useScrollReveal } from 'src/pages/landing/use-scroll-reveal';
import { VisualFrame } from './visual-frame';

const STANDINGS = [
  { team: 'Atlético Sur', points: 22 },
  { team: 'Real Andina', points: 20 },
  { team: 'Unión Central', points: 18 },
];

/**
 * El gráfico de la sección "Portal público": una ventana de navegador
 * (cromado neutro, sin los semáforos de colores de siempre -- el registro
 * formal pide monocromo) mostrando el portal con el color que eligió la
 * organización, no el de SportFrog -- por eso el banner usa
 * `secondary.main`, no `primary.main`: el punto es que es un color distinto
 * al de la propia marca.
 */
export function PortalVisual() {
  const theme = useTheme();
  const scopeRef = useScrollReveal({ selector: '.visual-item', from: { stagger: 0.08, duration: 0.5 } });

  return (
    <VisualFrame ref={scopeRef}>
      <g className="visual-item">
        <rect x={0} y={0} width={1442} height={90} fill={theme.palette.action.hover} />
        <line x1={0} y1={90} x2={1442} y2={90} stroke={theme.palette.divider} />
        {[40, 70, 100].map((cx) => (
          <circle key={cx} cx={cx} cy={45} r={9} fill={alpha(theme.palette.text.primary, 0.25)} />
        ))}
        <rect x={140} y={22} width={760} height={46} rx={23} fill={theme.palette.background.paper} />
        <text x={168} y={52} fontSize={20} fill={theme.palette.text.secondary} fontFamily={JETBRAINS_MONO_STACK}>
          sportfrog.app/liga-test/apertura-2026
        </text>
        <rect x={1150} y={22} width={222} height={46} rx={23} fill={alpha(theme.palette.primary.main, 0.1)} />
        <text x={1180} y={52} fontSize={20} fontWeight={700} fill={theme.palette.primary.main}>
          Sin cuenta
        </text>
      </g>

      <g className="visual-item">
        <rect x={60} y={130} width={1322} height={220} rx={16} fill={theme.palette.secondary.main} />
        <text x={100} y={230} fontSize={38} fontWeight={700} fill={theme.palette.secondary.contrastText}>
          Copa Apertura 2026
        </text>
        <text x={100} y={272} fontSize={22} fill={alpha(theme.palette.secondary.contrastText, 0.85)}>
          Liga Test · Fútbol
        </text>
      </g>

      <g className="visual-item">
        <text x={60} y={400} fontSize={24} fontWeight={700} fill={theme.palette.text.primary}>
          Tabla de posiciones
        </text>
      </g>

      {STANDINGS.map((row, index) => {
        const y = 430 + index * 76;
        return (
          <g key={row.team} className="visual-item">
            <text x={60} y={y} fontSize={24} fill={theme.palette.text.primary}>
              {index + 1}. {row.team}
            </text>
            <text x={1322} y={y} fontSize={24} fontWeight={700} textAnchor="end" fill={theme.palette.secondary.main} fontFamily={JETBRAINS_MONO_STACK}>
              {row.points}
            </text>
            <line x1={60} y1={y + 20} x2={1382} y2={y + 20} stroke={theme.palette.divider} />
          </g>
        );
      })}

      <g className="visual-item">
        <text x={60} y={770} fontSize={22} fill={theme.palette.text.secondary}>
          Personalización
        </text>
        {[theme.palette.secondary.main, theme.palette.primary.main, theme.palette.brand.edge, theme.palette.brand.accent].map((color, index) => (
          <circle
            // El índice es key a propósito, no el color: dos roles de la
            // paleta pueden compartir el mismo hex (secondary y brand.bright
            // lo hacían), y una key repetida rompe la identidad de React.
            key={index}
            cx={90 + index * 80}
            cy={830}
            r={28}
            fill={color}
            stroke={index === 0 ? theme.palette.text.primary : 'none'}
            strokeWidth={3}
          />
        ))}
      </g>

      <g className="visual-item">
        <rect x={460} y={800} width={320} height={60} rx={12} fill={alpha(theme.palette.success.main, 0.08)} stroke={alpha(theme.palette.success.main, 0.5)} strokeWidth={2} />
        <circle cx={496} cy={830} r={16} fill={theme.palette.success.main} />
        <path d="M 488 830 L 494 836 L 506 822" stroke={theme.palette.background.paper} strokeWidth={3.5} fill="none" strokeLinecap="round" strokeLinejoin="round" />
        <text x={524} y={837} fontSize={20} fontWeight={700} fill={theme.palette.text.primary}>
          Contraste verificado
        </text>
      </g>
    </VisualFrame>
  );
}

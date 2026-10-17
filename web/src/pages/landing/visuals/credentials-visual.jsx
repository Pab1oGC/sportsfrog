import { useId } from 'react';
import { alpha, useTheme } from '@mui/material/styles';
import { JETBRAINS_MONO_STACK } from 'src/theme/fonts';
import { useLoopReveal } from 'src/pages/landing/use-loop-reveal';
import { VisualFrame } from './visual-frame';

const CANVAS_X = 60;
const CANVAS_Y = 170;
const CANVAS_W = 760;
const CANVAS_H = 800;
const CARD_W = 400;
const CARD_H = 600;
const CARD_X = CANVAS_X + (CANVAS_W - CARD_W) / 2;
const CARD_Y = CANVAS_Y + (CANVAS_H - CARD_H) / 2;

// Grilla cualquiera, no un código que escanee a nada -- el punto es que el
// campo QR existe en el diseño, no demostrar el algoritmo.
const QR_PATTERN = [
  [1, 1, 1, 0, 1, 0],
  [1, 0, 1, 1, 0, 1],
  [1, 1, 0, 1, 1, 0],
  [0, 1, 1, 0, 1, 1],
  [1, 0, 1, 1, 0, 1],
  [1, 1, 0, 1, 1, 0],
];

// Los tres campos que se van "seleccionando" en el loop -- mismo orden en el
// lienzo y en la lista de la derecha, como en el editor real.
const FIELDS = [
  { key: 'photo', label: 'Foto' },
  { key: 'name', label: 'Nombre' },
  { key: 'qr', label: 'QR' },
];

/**
 * Un glifo mínimo dibujado a mano para cada tipo de campo, centrado en
 * (cx, cy) -- ningún otro gráfico de sección usa <Iconify> dentro de su
 * <svg>, y mezclar un componente React/SVG con posicionamiento x/y crudo
 * adentro de otro <svg> es terreno no probado. Formas simples en vez.
 */
function FieldGlyph({ type, cx, cy, color }) {
  if (type === 'photo') {
    return (
      <g stroke={color} strokeWidth={1.8} fill="none">
        <rect x={cx - 11} y={cy - 9} width={22} height={18} rx={2} />
        <circle cx={cx - 4} cy={cy - 3} r={2.4} fill={color} stroke="none" />
        <path d={`M ${cx - 9} ${cy + 7} L ${cx - 1} ${cy - 1} L ${cx + 5} ${cy + 3} L ${cx + 9} ${cy - 1} L ${cx + 9} ${cy + 7} Z`} fill={color} opacity={0.5} stroke="none" />
      </g>
    );
  }
  if (type === 'name') {
    return (
      <g stroke={color} strokeWidth={1.8} fill="none">
        <circle cx={cx} cy={cy - 5} r={5} />
        <path d={`M ${cx - 9} ${cy + 10} Q ${cx} ${cy - 2} ${cx + 9} ${cy + 10}`} />
      </g>
    );
  }
  return (
    <g fill={color}>
      {[0, 1].flatMap((row) =>
        [0, 1].map((col) => (row === 0 && col === 1 ? null : <rect key={`${row}-${col}`} x={cx - 10 + col * 11} y={cy - 10 + row * 11} width={9} height={9} />)),
      )}
    </g>
  );
}

/**
 * El gráfico de la sección "Credenciales" -- el editor visual de plantillas
 * real (ver api/Features/Documents + web/pages/template-designer.jsx): un
 * lienzo tipo tablero de ajedrez (simula transparencia, igual que cualquier
 * editor de diseño), con los campos que trae la credencial -- foto, nombre,
 * QR -- cada uno con su borde punteado de siempre y un borde sólido con
 * asas que aparece cuando "está seleccionado". La lista de campos a la
 * derecha es la misma del panel real.
 *
 * El loop (useLoopReveal, no useScrollReveal) selecciona un campo a la vez
 * una y otra vez, en el lienzo y en la lista a la vez -- no es una sola
 * revelación al entrar en pantalla, es la idea de "así se arma esto" en
 * bucle.
 */
export function CredentialsVisual() {
  const theme = useTheme();
  const checkerId = useId();
  const line = theme.palette.primary.main;

  const scopeRef = useLoopReveal((tl) => {
    FIELDS.forEach((field) => {
      tl.to(`.handles-${field.key}, .highlight-${field.key}`, { opacity: 1, duration: 0.3 })
        .to(`.handles-${field.key}, .highlight-${field.key}`, { opacity: 0, duration: 0.3 }, '+=0.7');
    });
    tl.to({}, { duration: 0.6 }); // respiro antes de volver a empezar
  });

  return (
    <VisualFrame ref={scopeRef}>
      <defs>
        <pattern id={checkerId} width={32} height={32} patternUnits="userSpaceOnUse">
          <rect width={32} height={32} fill={theme.palette.background.paper} />
          <rect width={16} height={16} fill={alpha(theme.palette.text.primary, 0.05)} />
          <rect x={16} y={16} width={16} height={16} fill={alpha(theme.palette.text.primary, 0.05)} />
        </pattern>
      </defs>

      {/* barra superior: título, versión, acciones -- cromo fijo, sin animar */}
      <text x={60} y={66} fontSize={30} fontWeight={700} fill={theme.palette.text.primary}>
        Diseño: Credencial Sub-14
      </text>
      <text x={60} y={98} fontSize={20} fill={theme.palette.text.secondary} fontFamily={JETBRAINS_MONO_STACK}>
        Credencial · v3
      </text>
      <rect x={1108} y={38} width={110} height={44} rx={8} fill="none" stroke={theme.palette.divider} strokeWidth={1.5} />
      <text x={1133} y={66} fontSize={20} fill={theme.palette.text.secondary}>
        Volver
      </text>
      <rect x={1234} y={38} width={150} height={44} rx={8} fill={line} />
      <text x={1256} y={66} fontSize={20} fontWeight={700} fill={theme.palette.primary.contrastText}>
        Guardar
      </text>

      {/* tabs anverso/reverso */}
      <text x={60} y={150} fontSize={22} fontWeight={700} fill={line}>
        Anverso
      </text>
      <line x1={60} y1={162} x2={160} y2={162} stroke={line} strokeWidth={2.5} />
      <text x={190} y={150} fontSize={22} fill={theme.palette.text.secondary}>
        Reverso
      </text>

      {/* el lienzo: tablero de ajedrez simulando transparencia */}
      <rect x={CANVAS_X} y={CANVAS_Y} width={CANVAS_W} height={CANVAS_H} rx={12} fill={`url(#${checkerId})`} stroke={theme.palette.divider} strokeWidth={1.5} />
      <rect x={CARD_X} y={CARD_Y} width={CARD_W} height={CARD_H} rx={10} fill="none" stroke={alpha(theme.palette.text.primary, 0.25)} strokeWidth={1.5} strokeDasharray="6 6" />

      {/* campo: foto */}
      <g>
        <rect x={CARD_X + 40} y={CARD_Y + 40} width={160} height={160} rx={10} fill={alpha(line, 0.06)} stroke={line} strokeWidth={2} strokeDasharray="5 5" />
        <circle cx={CARD_X + 120} cy={CARD_Y + 120} r={46} fill={alpha(line, 0.12)} stroke={line} strokeWidth={1.5} />
        <g className="handles-photo" opacity={0}>
          <rect x={CARD_X + 40} y={CARD_Y + 40} width={160} height={160} rx={10} fill="none" stroke={line} strokeWidth={2.5} />
          {[
            [CARD_X + 40, CARD_Y + 40],
            [CARD_X + 200, CARD_Y + 40],
            [CARD_X + 40, CARD_Y + 200],
            [CARD_X + 200, CARD_Y + 200],
          ].map(([hx, hy]) => (
            <circle key={`${hx}-${hy}`} cx={hx} cy={hy} r={7} fill={theme.palette.background.paper} stroke={line} strokeWidth={2.5} />
          ))}
        </g>
      </g>

      {/* campo: nombre */}
      <g>
        <rect x={CARD_X + 40} y={CARD_Y + 230} width={320} height={56} rx={8} fill={alpha(line, 0.06)} stroke={line} strokeWidth={2} strokeDasharray="5 5" />
        <circle cx={CARD_X + 68} cy={CARD_Y + 258} r={10} fill={alpha(theme.palette.text.primary, 0.3)} />
        <rect x={CARD_X + 92} y={CARD_Y + 248} width={230} height={20} rx={4} fill={alpha(theme.palette.text.primary, 0.3)} />
        <g className="handles-name" opacity={0}>
          <rect x={CARD_X + 40} y={CARD_Y + 230} width={320} height={56} rx={8} fill="none" stroke={line} strokeWidth={2.5} />
          {[
            [CARD_X + 40, CARD_Y + 230],
            [CARD_X + 360, CARD_Y + 230],
            [CARD_X + 40, CARD_Y + 286],
            [CARD_X + 360, CARD_Y + 286],
          ].map(([hx, hy]) => (
            <circle key={`${hx}-${hy}`} cx={hx} cy={hy} r={7} fill={theme.palette.background.paper} stroke={line} strokeWidth={2.5} />
          ))}
        </g>
      </g>

      {/* campo: QR */}
      <g>
        <rect x={CARD_X + CARD_W - 150} y={CARD_Y + CARD_H - 150} width={110} height={110} rx={8} fill={alpha(line, 0.06)} stroke={line} strokeWidth={2} strokeDasharray="5 5" />
        {QR_PATTERN.map((row, rowIndex) =>
          row.map(
            (cell, colIndex) =>
              Boolean(cell) && (
                <rect
                  key={`${rowIndex}-${colIndex}`}
                  x={CARD_X + CARD_W - 134 + colIndex * 13}
                  y={CARD_Y + CARD_H - 134 + rowIndex * 13}
                  width={11}
                  height={11}
                  fill={theme.palette.text.primary}
                />
              ),
          ),
        )}
        <g className="handles-qr" opacity={0}>
          <rect x={CARD_X + CARD_W - 150} y={CARD_Y + CARD_H - 150} width={110} height={110} rx={8} fill="none" stroke={line} strokeWidth={2.5} />
          {[
            [CARD_X + CARD_W - 150, CARD_Y + CARD_H - 150],
            [CARD_X + CARD_W - 40, CARD_Y + CARD_H - 150],
            [CARD_X + CARD_W - 150, CARD_Y + CARD_H - 40],
            [CARD_X + CARD_W - 40, CARD_Y + CARD_H - 40],
          ].map(([hx, hy]) => (
            <circle key={`${hx}-${hy}`} cx={hx} cy={hy} r={7} fill={theme.palette.background.paper} stroke={line} strokeWidth={2.5} />
          ))}
        </g>
      </g>

      {/* panel lateral: la misma lista de campos que el editor real */}
      <text x={900} y={210} fontSize={26} fontWeight={700} fill={theme.palette.text.primary}>
        Campos
      </text>

      {FIELDS.map((field, index) => {
        const y = 250 + index * 120;
        return (
          <g key={field.key}>
            <rect x={900} y={y} width={482} height={96} rx={10} fill={theme.palette.background.paper} stroke={theme.palette.divider} strokeWidth={1.5} />
            <rect className={`highlight-${field.key}`} opacity={0} x={900} y={y} width={6} height={96} fill={line} />
            <FieldGlyph type={field.key} cx={946} cy={y + 48} color={line} />
            <text x={980} y={y + 56} fontSize={24} fontWeight={600} fill={theme.palette.text.primary}>
              {field.label}
            </text>
          </g>
        );
      })}
    </VisualFrame>
  );
}

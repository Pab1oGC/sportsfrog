import { nombreFase } from 'src/lib/phase-labels';

export const FLYER_FORMATS = [
  { value: 'story', label: 'Historia', detail: '1080 × 1920', width: 1080, height: 1920 },
  { value: 'portrait', label: 'Feed 4:5', detail: '1080 × 1350', width: 1080, height: 1350 },
  { value: 'square', label: 'Cuadrado', detail: '1080 × 1080', width: 1080, height: 1080 },
];

const DISPLAY = 'Inter, Arial, sans-serif';
const SANS = 'Inter, Arial, sans-serif';
const WHITE = '#F8F7EE';
const LAYOUTS = {
  story: { top: 108, title: 330, board: 570, boardHeight: 500, details: 1320, footer: 1720 },
  portrait: { top: 65, title: 230, board: 388, boardHeight: 390, details: 970, footer: 1250 },
  square: { top: 28, title: 185, board: 286, boardHeight: 310, details: 760, footer: 1000 },
};

export function flyerFormat(value) {
  return FLYER_FORMATS.find((format) => format.value === value) || FLYER_FORMATS[0];
}
export function teamName(match, side) {
  return match[`${side}TeamName`] || match[`${side}Placeholder`] || 'Por definir';
}
export function fixtureTitle(match) {
  return `${teamName(match, 'home')} vs ${teamName(match, 'away')}`;
}
export function flyerFileName(match, format = 'story', type = 'fixture') {
  const safe = fixtureTitle(match).toLowerCase().normalize('NFD').replace(/[\u0300-\u036f]/g, '')
    .replace(/[^a-z0-9]+/g, '-').replace(/(^-|-$)/g, '');
  return `${type}-${safe || 'partido'}-${format}.png`;
}

function hex(value, fallback) {
  return /^#[0-9a-f]{6}$/i.test(value?.trim() || '') ? value.trim() : fallback;
}
function mix(color, target, amount) {
  return '#' + [1, 3, 5].map((offset) => Math.round(
    parseInt(color.slice(offset, offset + 2), 16) * (1 - amount)
    + parseInt(target.slice(offset, offset + 2), 16) * amount,
  ).toString(16).padStart(2, '0')).join('');
}
function luminance(color) {
  const c = [1, 3, 5].map((offset) => {
    const v = parseInt(color.slice(offset, offset + 2), 16) / 255;
    return v <= 0.04045 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4;
  });
  return c[0] * 0.2126 + c[1] * 0.7152 + c[2] * 0.0722;
}
export function flyerColors(competition) {
  const pub = competition?.settings?.public || {};
  const primary = hex(pub.theme?.primary, hex(pub.accentColor, '#08786F'));
  const secondary = hex(pub.theme?.secondary, mix(primary, '#FFFFFF', 0.78));
  const background = mix(primary, '#000000', 0.42);
  let accent = secondary;
  // Preserve the brand hue, adjusting heading brightness for legibility.
  while ((luminance(accent) + 0.05) / (luminance(background) + 0.05) < 4.5) {
    accent = mix(accent, '#FFFFFF', 0.2);
  }
  return { primary, secondary, background, accent,
    score: luminance(secondary) > 0.179 ? '#101722' : '#FFFFFF' };
}
function loadImage(url) {
  if (!url) return Promise.resolve(null);
  return new Promise((resolve) => {
    const image = new Image();
    const timer = setTimeout(() => resolve(null), 8000);
    image.crossOrigin = 'anonymous';
    image.onload = () => { clearTimeout(timer); resolve(image); };
    image.onerror = () => { clearTimeout(timer); resolve(null); };
    image.src = url;
  });
}
function contain(ctx, image, x, y, width, height) {
  if (!image) return;
  const scale = Math.min(width / image.width, height / image.height);
  const w = image.width * scale;
  const h = image.height * scale;
  ctx.drawImage(image, x + (width - w) / 2, y + (height - h) / 2, w, h);
}

// All copy is measured, including long team names and bracket placeholders.
function label(ctx, value, x, y, size, { color = WHITE, width = 940, family = SANS, tracking = 0, align = 'center' } = {}) {
  ctx.save();
  ctx.font = `800 ${size}px ${family}`;
  ctx.fillStyle = color;
  ctx.textAlign = align;
  ctx.textBaseline = 'middle';
  ctx.letterSpacing = `${tracking}px`;
  ctx.fillText(String(value), x, y, width);
  ctx.restore();
}
function rule(ctx, x, y, width, color = '#FFFFFF40') {
  ctx.fillStyle = color;
  ctx.fillRect(x, y, width, 1);
}
function background(ctx, format, image, colors) {
  const { width, height } = format;
  ctx.fillStyle = colors.background;
  ctx.fillRect(0, 0, width, height);
  if (image) {
    const scale = Math.max(width / image.width, height / image.height);
    const w = image.width * scale;
    const h = image.height * scale;
    ctx.drawImage(image, (width - w) / 2, (height - h) / 2, w, h);
  }
  const shade = ctx.createLinearGradient(0, 0, 0, height);
  shade.addColorStop(0, colors.background + (image ? 'CC' : 'FF'));
  shade.addColorStop(1, mix(colors.background, '#000000', 0.4) + (image ? 'E6' : 'FF'));
  ctx.fillStyle = shade;
  ctx.fillRect(0, 0, width, height);
  if (!image) {
    ctx.strokeStyle = '#FFFFFF09';
    ctx.lineWidth = 3;
    for (let x = -80; x < width + 120; x += 115) {
      ctx.beginPath();
      ctx.moveTo(x, height * 0.30);
      ctx.lineTo(x + 58, height * 0.18);
      ctx.lineTo(x + 112, height * 0.30);
      ctx.stroke();
    }
    ctx.fillStyle = '#FFFFFF0A';
    for (let y = height * 0.34; y < height * 0.70; y += 26) {
      for (let x = (y % 50); x < width; x += 31) ctx.fillRect(x, y, 3, 3);
    }
    rule(ctx, 0, height * 0.72, width, '#FFFFFF15');
  }
}
function badge(ctx, image, name, x, y, size, colors) {
  if (image) return contain(ctx, image, x, y, size, size);
  // A restrained shield, rather than a generic app avatar, for clubs without a logo.
  ctx.save();
  ctx.translate(x, y);
  ctx.fillStyle = colors.background;
  ctx.beginPath();
  ctx.moveTo(size * 0.12, size * 0.12);
  ctx.lineTo(size * 0.88, size * 0.12);
  ctx.lineTo(size * 0.84, size * 0.66);
  ctx.quadraticCurveTo(size * 0.76, size * 0.86, size * 0.5, size * 0.98);
  ctx.quadraticCurveTo(size * 0.24, size * 0.86, size * 0.16, size * 0.66);
  ctx.closePath();
  ctx.fill();
  rule(ctx, size * 0.28, size * 0.27, size * 0.44, colors.accent);
  const letters = name.split(/\s+/).filter(Boolean).slice(0, 2).map((word) => word[0]).join('').toUpperCase();
  label(ctx, letters, size / 2, size * 0.55, size * 0.40, { family: DISPLAY, width: size * 0.62 });
  ctx.restore();
}
function draw(ctx, format, match, competition, type, images) {
  const colors = flyerColors(competition);
  const l = LAYOUTS[format.value];
  const square = format.value === 'square';
  const result = type === 'result';
  const postponed = match.status === 'postponed';
  background(ctx, format, images.banner, colors);

  if (images.logo) {
    contain(ctx, images.logo, 490, l.top - 30, 100, 100);
  } else {
    // Small tournament insignia: three stars and a simple cup outline.
    label(ctx, '★  ★  ★', 540, l.top - 8, 20, { color: colors.accent });
    ctx.strokeStyle = WHITE;
    ctx.lineWidth = 4;
    ctx.beginPath();
    ctx.moveTo(518, l.top + 15);
    ctx.lineTo(522, l.top + 40);
    ctx.quadraticCurveTo(540, l.top + 62, 558, l.top + 40);
    ctx.lineTo(562, l.top + 15);
    ctx.closePath();
    ctx.moveTo(540, l.top + 54); ctx.lineTo(540, l.top + 68);
    ctx.moveTo(526, l.top + 68); ctx.lineTo(554, l.top + 68);
    ctx.stroke();
  }
  label(ctx, (competition?.name || 'Competencia deportiva').toUpperCase(), 540, l.top + 103, square ? 24 : 28, { width: 860, tracking: 2 });

  const heading = result ? 'RESULTADO FINAL' : postponed ? 'PARTIDO APLAZADO' : 'DÍA DE PARTIDO';
  label(ctx, heading, 540, l.title, square ? 40 : 57, { family: DISPLAY, color: colors.accent, width: 950 });
  const phase = match.phase ? nombreFase(match.phase) : match.roundNumber ? `JORNADA ${match.roundNumber}` : '';
  const metadata = [match.categoryName, phase].filter(Boolean).join('  /  ').toUpperCase();
  label(ctx, result ? (match.status === 'walkover' ? 'RESOLUCIÓN POR WALKOVER' : 'TIEMPO COMPLETO') : metadata || 'PRÓXIMO ENCUENTRO', 540, l.title + (square ? 43 : 58), square ? 19 : 23, { tracking: 3 });

  const boardW = square ? 300 : format.value === 'portrait' ? 350 : 430;
  const boardX = (1080 - boardW) / 2;
  const middle = l.board + l.boardHeight / 2;
  const bandH = square ? 194 : 248;
  ctx.fillStyle = WHITE;
  ctx.fillRect(60, middle - bandH / 2, 960, bandH);
  const crestSize = square ? 142 : 174;
  badge(ctx, images.home, teamName(match, 'home'), 182 - crestSize / 2, middle - crestSize / 2, crestSize, colors);
  badge(ctx, images.away, teamName(match, 'away'), 898 - crestSize / 2, middle - crestSize / 2, crestSize, colors);
  ctx.save();
  ctx.shadowColor = '#001E3055';
  ctx.shadowBlur = 38;
  ctx.shadowOffsetY = 16;
  ctx.fillStyle = colors.secondary;
  ctx.fillRect(boardX, l.board, boardW, l.boardHeight);
  ctx.restore();
  // Draw the score using actual glyph bounds, so it fills the block vertically.
  let score = result ? `${match.homeTotal ?? '—'}-${match.awayTotal ?? '—'}` : 'VS';
  if (result && match.status === 'walkover') score = 'W.O.';
  ctx.save();
  ctx.font = `800 ${l.boardHeight}px ${DISPLAY}`;
  const metrics = ctx.measureText(score);
  const glyphH = metrics.actualBoundingBoxAscent + metrics.actualBoundingBoxDescent;
  const scale = Math.min((boardW - 54) / metrics.width, (l.boardHeight - 78) / glyphH);
  ctx.translate(540, middle);
  ctx.scale(scale, scale);
  ctx.fillStyle = colors.score;
  ctx.textAlign = 'center';
  ctx.fillText(score, 0, (metrics.actualBoundingBoxAscent - metrics.actualBoundingBoxDescent) / 2);
  ctx.restore();

  const namesY = l.board + l.boardHeight + 50;
  label(ctx, teamName(match, 'home').toUpperCase(), 300, namesY, square ? 22 : 28, { family: DISPLAY, width: 425 });
  label(ctx, teamName(match, 'away').toUpperCase(), 780, namesY, square ? 32 : 39, { family: DISPLAY, width: 425 });
  if (result && match.penaltyHomeScore != null && match.penaltyAwayScore != null) {
    label(ctx, `PENALES  ${match.penaltyHomeScore} – ${match.penaltyAwayScore}`, 540, namesY + 43, 22, { color: colors.accent });
  }

  rule(ctx, 480, l.details - 37, 120, colors.accent);
  const date = match.scheduledAt ? new Date(match.scheduledAt) : null;
  const validDate = date && !Number.isNaN(date.getTime());
  const dateText = validDate ? new Intl.DateTimeFormat('es-BO', { day: 'numeric', month: 'long' }).format(date).toUpperCase() : 'FECHA POR CONFIRMAR';
  const time = validDate ? new Intl.DateTimeFormat('es-BO', { hour: '2-digit', minute: '2-digit', hour12: false }).format(date) : 'HORA POR CONFIRMAR';
  label(ctx, postponed ? 'NUEVA FECHA POR CONFIRMAR' : result ? metadata || 'RESULTADO OFICIAL' : `${dateText}  ·  ${time}`, 540, l.details, square ? 26 : 32, { family: DISPLAY, width: 930, tracking: 1 });
  const venue = [match.venueName, match.spaceName].filter(Boolean).join(' · ');
  label(ctx, postponed ? 'Consultá la próxima programación' : venue || (result ? dateText : 'Sede por confirmar'), 540, l.details + 45, square ? 19 : 24, { width: 890, color: '#DBE9E4' });
  rule(ctx, 60, l.footer - 30, 960);
  label(ctx, 'SPORTFROG', 60, l.footer, 21, { align: 'left', tracking: 2 });
  label(ctx, result ? 'EL JUEGO EN NÚMEROS.' : 'TODO EMPIEZA EN LA CANCHA.', 1020, l.footer, 16, { align: 'right', tracking: 1, color: '#CCDAD5' });
}

export async function createFixtureFlyer(match, competition, formatValue = 'story', type = 'fixture') {
  const format = flyerFormat(formatValue);
  const preview = competition?.publicPreview || {};
  const [home, away, logo, banner] = await Promise.all([
    loadImage(match.homeClubLogoUrl), loadImage(match.awayClubLogoUrl),
    loadImage(preview.logoUrl),
    loadImage(preview.bannerUrl),
  ]);
  const canvas = document.createElement('canvas');
  canvas.width = format.width;
  canvas.height = format.height;
  draw(canvas.getContext('2d'), format, match, competition, type, { home, away, logo, banner });
  return canvas;
}
export function downloadCanvas(canvas, name) {
  const link = document.createElement('a');
  link.download = name;
  link.href = canvas.toDataURL('image/png');
  link.click();
}

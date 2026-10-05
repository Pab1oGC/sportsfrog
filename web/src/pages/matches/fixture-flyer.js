import { nombreFase } from 'src/lib/phase-labels';

/*
 * Las piezas se dibujan en un canvas en el navegador: no dependen de una
 * plantilla remota ni suben los escudos a un servicio de terceros. Eso deja
 * el archivo listo para publicar y mantiene los enlaces temporales de los
 * logos dentro de la sesión que ya autorizó el organizador.
 */

export const FLYER_FORMATS = [
  { value: 'story', label: 'Historia', detail: '1080 × 1920', width: 1080, height: 1920 },
  { value: 'portrait', label: 'Feed vertical', detail: '1080 × 1350', width: 1080, height: 1350 },
  { value: 'square', label: 'Post cuadrado', detail: '1080 × 1080', width: 1080, height: 1080 },
];

const BRAND = '#F50057';
const INK = '#101722';
const PAPER = '#F7F4EF';

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

function initials(name) {
  return (name || '?').split(/\s+/).filter(Boolean).slice(0, 2).map((word) => word[0]).join('').toUpperCase();
}

function safeHex(value, fallback) {
  return /^#[0-9a-f]{6}$/i.test(value || '') ? value : fallback;
}

function rgba(hex, alpha) {
  const normalized = safeHex(hex, '#000000').slice(1);
  const red = parseInt(normalized.slice(0, 2), 16);
  const green = parseInt(normalized.slice(2, 4), 16);
  const blue = parseInt(normalized.slice(4, 6), 16);
  return `rgba(${red}, ${green}, ${blue}, ${alpha})`;
}

function branding(competition) {
  const theme = competition?.settings?.public?.theme || {};
  return {
    primary: safeHex(theme.primary || competition?.settings?.public?.accentColor, BRAND),
    secondary: safeHex(theme.secondary, '#F6C453'),
    logoUrl: competition?.publicPreview?.logoUrl || null,
  };
}

function dateLabel(value) {
  if (!value) return 'FECHA POR CONFIRMAR';
  const date = new Date(value);
  return new Intl.DateTimeFormat('es-BO', { weekday: 'long', day: 'numeric', month: 'long' })
    .format(date).replace(/^./, (letter) => letter.toUpperCase());
}

function timeLabel(value) {
  if (!value) return 'HORARIO POR CONFIRMAR';
  return `${new Intl.DateTimeFormat('es-BO', { hour: '2-digit', minute: '2-digit', hour12: false }).format(new Date(value))} HRS`;
}

function matchLabel(match) {
  const phase = match.phase ? nombreFase(match.phase) : null;
  return [match.categoryName, phase || (match.roundNumber ? `Jornada ${match.roundNumber}` : null)]
    .filter(Boolean).join(' · ').toUpperCase();
}

function loadImage(url) {
  if (!url) return Promise.resolve(null);
  return new Promise((resolve) => {
    const image = new Image();
    image.crossOrigin = 'anonymous';
    image.decoding = 'async';
    image.onload = () => resolve(image);
    image.onerror = () => resolve(null);
    image.src = url;
  });
}

function fit(image, x, y, width, height) {
  const scale = Math.min(width / image.width, height / image.height);
  const nextWidth = image.width * scale;
  const nextHeight = image.height * scale;
  return [x + (width - nextWidth) / 2, y + (height - nextHeight) / 2, nextWidth, nextHeight];
}

function rounded(ctx, x, y, width, height, radius) {
  ctx.beginPath();
  ctx.roundRect(x, y, width, height, radius);
}

function drawImageContain(ctx, image, x, y, width, height) {
  if (!image) return;
  ctx.drawImage(image, ...fit(image, x, y, width, height));
}

function drawText(ctx, text, { x, y, width, font, color, align = 'left', lineHeight, maxLines = 2 }) {
  ctx.save();
  ctx.font = font;
  ctx.fillStyle = color;
  ctx.textAlign = align;
  ctx.textBaseline = 'top';
  const words = String(text || '').trim().split(/\s+/).filter(Boolean);
  const lines = [];
  let line = '';
  words.forEach((word) => {
    const next = line ? `${line} ${word}` : word;
    if (line && ctx.measureText(next).width > width) {
      lines.push(line);
      line = word;
    } else line = next;
  });
  if (line) lines.push(line);
  const visible = lines.slice(0, maxLines);
  const left = align === 'center' ? x : align === 'right' ? x - width : x;
  visible.forEach((part, index) => ctx.fillText(part, left, y + (lineHeight * index)));
  ctx.restore();
  return visible.length * lineHeight;
}

function crest(ctx, image, name, x, y, size, { primary, secondary }) {
  const pad = Math.round(size * 0.15);
  ctx.save();
  ctx.shadowColor = 'rgba(4, 13, 24, 0.20)';
  ctx.shadowBlur = 34;
  ctx.shadowOffsetY = 16;
  ctx.fillStyle = '#FFFFFF';
  rounded(ctx, x, y, size, size, Math.round(size * 0.28));
  ctx.fill();
  ctx.restore();

  ctx.save();
  rounded(ctx, x, y, size, size, Math.round(size * 0.28));
  ctx.clip();
  if (image) {
    drawImageContain(ctx, image, x + pad, y + pad, size - pad * 2, size - pad * 2);
  } else {
    const fallback = ctx.createLinearGradient(x, y, x + size, y + size);
    fallback.addColorStop(0, primary);
    fallback.addColorStop(1, secondary);
    ctx.fillStyle = fallback;
    ctx.fillRect(x, y, size, size);
    ctx.fillStyle = '#FFFFFF';
    ctx.font = `800 ${Math.round(size * 0.30)}px Inter, Arial, sans-serif`;
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    ctx.fillText(initials(name), x + size / 2, y + size / 2 + 4);
  }
  ctx.restore();

  ctx.strokeStyle = rgba(primary, 0.2);
  ctx.lineWidth = 3;
  rounded(ctx, x + 1.5, y + 1.5, size - 3, size - 3, Math.round(size * 0.28));
  ctx.stroke();
}

function drawBackdrop(ctx, format, theme) {
  const { width, height } = format;
  ctx.fillStyle = PAPER;
  ctx.fillRect(0, 0, width, height);

  const ink = ctx.createLinearGradient(0, 0, width, height * 0.72);
  ink.addColorStop(0, '#0C1524');
  ink.addColorStop(0.72, '#182A40');
  ink.addColorStop(1, '#20364B');
  ctx.fillStyle = ink;
  ctx.fillRect(0, 0, width, Math.round(height * 0.68));

  // Formas discretas, con aire editorial. No son una textura aleatoria: se
  // ven igual en cada exportación y por lo tanto forman parte de la marca.
  ctx.save();
  ctx.translate(width * 0.76, -height * 0.07);
  ctx.rotate(-0.34);
  ctx.fillStyle = rgba(theme.primary, 0.76);
  ctx.fillRect(0, 0, width * 0.22, height * 0.92);
  ctx.fillStyle = rgba(theme.secondary, 0.16);
  ctx.fillRect(width * 0.28, 0, width * 0.07, height * 0.92);
  ctx.restore();

  ctx.strokeStyle = 'rgba(255,255,255,0.08)';
  ctx.lineWidth = 2;
  for (let offset = -height; offset < width; offset += 92) {
    ctx.beginPath();
    ctx.moveTo(offset, 0);
    ctx.lineTo(offset + height, height);
    ctx.stroke();
  }
}

function drawHeader(ctx, competitionName, competitionLogo, theme) {
  const gutter = 76;
  const logoSize = 76;
  if (competitionLogo) {
    ctx.fillStyle = '#FFFFFF';
    rounded(ctx, gutter, 64, logoSize, logoSize, 18);
    ctx.fill();
    drawImageContain(ctx, competitionLogo, gutter + 10, 74, logoSize - 20, logoSize - 20);
  } else {
    ctx.fillStyle = theme.primary;
    rounded(ctx, gutter, 64, logoSize, logoSize, 18);
    ctx.fill();
    ctx.fillStyle = '#FFFFFF';
    ctx.font = '900 25px Inter, Arial, sans-serif';
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    ctx.fillText('SF', gutter + logoSize / 2, 64 + logoSize / 2 + 1);
  }

  ctx.fillStyle = rgba('#FFFFFF', 0.72);
  ctx.font = '700 22px Inter, Arial, sans-serif';
  ctx.textAlign = 'left';
  ctx.textBaseline = 'top';
  ctx.fillText('FIXTURE OFICIAL', gutter + logoSize + 20, 70);
  drawText(ctx, competitionName || 'Competencia deportiva', {
    x: gutter + logoSize + 20, y: 101, width: 650, font: '800 29px Inter, Arial, sans-serif',
    color: '#FFFFFF', lineHeight: 34, maxLines: 1,
  });
  ctx.fillStyle = theme.secondary;
  ctx.fillRect(gutter, 172, 118, 6);
}

function drawMatchup(ctx, format, match, logos, theme, compact, type) {
  const { width } = format;
  const cardX = 54;
  const cardWidth = width - cardX * 2;
  const cardTop = compact ? 282 : 366;
  const cardHeight = compact ? 378 : 494;
  const crestSize = compact ? 154 : 206;
  const crestY = cardTop + (compact ? 86 : 118);
  const homeX = compact ? 122 : 116;
  const awayX = width - homeX - crestSize;
  const center = width / 2;

  ctx.save();
  ctx.shadowColor = 'rgba(3, 9, 17, 0.20)';
  ctx.shadowBlur = 44;
  ctx.shadowOffsetY = 18;
  ctx.fillStyle = '#FFFFFF';
  rounded(ctx, cardX, cardTop, cardWidth, cardHeight, 34);
  ctx.fill();
  ctx.restore();

  const isResult = type === 'result';
  const badge = isResult ? 'RESULTADO FINAL' : match.status === 'postponed' ? 'PARTIDO APLAZADO' : 'PRÓXIMO PARTIDO';
  ctx.fillStyle = isResult || match.status === 'postponed' ? '#9B2C2C' : theme.primary;
  rounded(ctx, cardX + 28, cardTop + 26, 202, 43, 21);
  ctx.fill();
  ctx.fillStyle = '#FFFFFF';
  ctx.font = '800 19px Inter, Arial, sans-serif';
  ctx.textAlign = 'center';
  ctx.textBaseline = 'middle';
  ctx.fillText(badge, cardX + 129, cardTop + 48);

  drawText(ctx, matchLabel(match), {
    x: center, y: cardTop + 92, width: 740, font: '700 22px Inter, Arial, sans-serif',
    color: '#667085', align: 'center', lineHeight: 27, maxLines: 1,
  });
  crest(ctx, logos.home, teamName(match, 'home'), homeX, crestY, crestSize, theme);
  crest(ctx, logos.away, teamName(match, 'away'), awayX, crestY, crestSize, theme);

  ctx.fillStyle = INK;
  ctx.font = `900 ${isResult ? (compact ? 58 : 72) : (compact ? 48 : 60)}px Inter, Arial, sans-serif`;
  ctx.textAlign = 'center';
  ctx.textBaseline = 'middle';
  ctx.fillText(isResult ? `${match.homeTotal ?? 0} – ${match.awayTotal ?? 0}` : 'VS', center, crestY + crestSize / 2 + 1);
  if (isResult && match.penaltyHomeScore != null) {
    ctx.fillStyle = '#667085';
    ctx.font = `700 ${compact ? 16 : 19}px Inter, Arial, sans-serif`;
    ctx.fillText(`Penales ${match.penaltyHomeScore} – ${match.penaltyAwayScore}`, center, crestY + crestSize / 2 + (compact ? 42 : 50));
  }

  const nameY = crestY + crestSize + (compact ? 28 : 35);
  drawText(ctx, teamName(match, 'home').toUpperCase(), {
    x: homeX + crestSize / 2, y: nameY, width: compact ? 270 : 310,
    font: `800 ${compact ? 24 : 29}px Inter, Arial, sans-serif`, color: INK, align: 'center', lineHeight: compact ? 30 : 35,
  });
  drawText(ctx, teamName(match, 'away').toUpperCase(), {
    x: awayX + crestSize / 2, y: nameY, width: compact ? 270 : 310,
    font: `800 ${compact ? 24 : 29}px Inter, Arial, sans-serif`, color: INK, align: 'center', lineHeight: compact ? 30 : 35,
  });
  return cardTop + cardHeight;
}

function drawDetails(ctx, format, match, top, theme, compact, type) {
  const { width } = format;
  const side = 78;
  const contentTop = top + (compact ? 46 : 64);
  const isResult = type === 'result';
  const postponed = match.status === 'postponed';
  const venue = postponed
    ? 'Esperá la nueva programación oficial'
    : [match.venueName, match.spaceName].filter(Boolean).join(' · ') || 'Sede por confirmar';
  ctx.fillStyle = theme.primary;
  ctx.font = '800 20px Inter, Arial, sans-serif';
  ctx.textAlign = 'left';
  ctx.textBaseline = 'top';
  ctx.fillText(isResult ? 'PARTIDO FINALIZADO' : 'CUÁNDO Y DÓNDE', side, contentTop);
  drawText(ctx, postponed ? 'NUEVA FECHA POR CONFIRMAR' : dateLabel(match.scheduledAt), {
    x: side, y: contentTop + 38, width: width - side * 2, font: `900 ${compact ? 38 : 46}px Inter, Arial, sans-serif`,
    color: INK, lineHeight: compact ? 44 : 53, maxLines: 1,
  });
  ctx.fillStyle = '#56606D';
  ctx.font = `700 ${compact ? 28 : 32}px Inter, Arial, sans-serif`;
  ctx.fillText(postponed ? 'REPROGRAMACIÓN PENDIENTE' : isResult ? 'RESULTADO OFICIAL' : timeLabel(match.scheduledAt), side, contentTop + (compact ? 92 : 104));
  ctx.strokeStyle = rgba(INK, 0.14);
  ctx.lineWidth = 2;
  ctx.beginPath();
  ctx.moveTo(side, contentTop + (compact ? 145 : 161));
  ctx.lineTo(width - side, contentTop + (compact ? 145 : 161));
  ctx.stroke();
  drawText(ctx, venue, {
    x: side, y: contentTop + (compact ? 174 : 194), width: width - side * 2,
    font: `700 ${compact ? 27 : 31}px Inter, Arial, sans-serif`, color: INK, lineHeight: compact ? 34 : 38, maxLines: 2,
  });
}

function drawFooter(ctx, format, theme, type) {
  const { width, height } = format;
  const footerY = height - 120;
  ctx.fillStyle = INK;
  ctx.fillRect(0, footerY, width, 120);
  ctx.fillStyle = theme.primary;
  ctx.fillRect(0, footerY, 14, 120);
  ctx.fillStyle = '#FFFFFF';
  ctx.font = '700 22px Inter, Arial, sans-serif';
  ctx.textAlign = 'left';
  ctx.textBaseline = 'middle';
  ctx.fillText(type === 'result' ? 'RESULTADOS EN' : 'SEGUÍ EL TORNEO EN', 72, footerY + 60);
  ctx.fillStyle = theme.secondary;
  ctx.font = '900 28px Inter, Arial, sans-serif';
  ctx.textAlign = 'right';
  ctx.fillText('SPORTFROG', width - 72, footerY + 60);
}

/** Creates a publication-ready fixture graphic. */
export async function createFixtureFlyer(match, competition, formatValue = 'story', type = 'fixture') {
  const format = flyerFormat(formatValue);
  const canvas = document.createElement('canvas');
  canvas.width = format.width;
  canvas.height = format.height;
  const ctx = canvas.getContext('2d');
  const theme = branding(competition);
  const [home, away, competitionLogo] = await Promise.all([
    loadImage(match.homeClubLogoUrl),
    loadImage(match.awayClubLogoUrl),
    loadImage(theme.logoUrl),
  ]);
  const compact = format.value !== 'story';
  drawBackdrop(ctx, format, theme);
  drawHeader(ctx, competition?.name, competitionLogo, theme);
  const matchupBottom = drawMatchup(ctx, format, match, { home, away }, theme, compact, type);
  drawDetails(ctx, format, match, matchupBottom, theme, compact, type);
  drawFooter(ctx, format, theme, type);
  return canvas;
}

export function downloadCanvas(canvas, name) {
  const link = document.createElement('a');
  link.download = name;
  link.href = canvas.toDataURL('image/png');
  link.click();
}

/**
 * Motor de exportación PNG y PDF de alta resolución.
 * Usa html2canvas-pro para la captura y jsPDF para los PDF.
 */

import html2canvas from 'html2canvas-pro';
import { jsPDF } from 'jspdf';

const SCALE = 3; // 3× = ~288 DPI sobre pantalla de 96 DPI

/**
 * Captura un elemento DOM y lo descarga como imagen PNG.
 * @param {HTMLElement} element
 * @param {string} filename
 */
export async function exportToPng(element, filename = 'exportacion.png') {
  const canvas = await html2canvas(element, {
    scale: SCALE,
    useCORS: true,
    allowTaint: false,
    backgroundColor: '#ffffff',
    logging: false,
  });
  const url = canvas.toDataURL('image/png', 1.0);
  triggerDownload(url, filename);
}

/**
 * Captura un elemento DOM y lo descarga como PDF.
 * @param {HTMLElement} element
 * @param {string} filename
 * @param {'A4'|'A3'|'letter'} format
 * @param {'portrait'|'landscape'} orientation
 */
export async function exportToPdf(
  element,
  filename = 'exportacion.pdf',
  format = 'A4',
  orientation = 'portrait'
) {
  const canvas = await html2canvas(element, {
    scale: SCALE,
    useCORS: true,
    allowTaint: false,
    backgroundColor: '#ffffff',
    logging: false,
  });

  const imgData = canvas.toDataURL('image/jpeg', 0.95);

  const pdf = new jsPDF({ orientation, format, unit: 'mm' });
  const pageWidth = pdf.internal.pageSize.getWidth();
  const pageHeight = pdf.internal.pageSize.getHeight();

  // Mantiene proporciones y ajusta a la página
  const ratio = canvas.width / canvas.height;
  let imgW = pageWidth;
  let imgH = imgW / ratio;

  if (imgH > pageHeight) {
    imgH = pageHeight;
    imgW = imgH * ratio;
  }

  const offsetX = (pageWidth - imgW) / 2;
  const offsetY = (pageHeight - imgH) / 2;

  pdf.addImage(imgData, 'JPEG', offsetX, offsetY, imgW, imgH);
  pdf.save(filename);
}

/**
 * Imprime múltiples elementos DOM en un solo lote (batch).
 * Abre la ventana de impresión del navegador con todos los elementos.
 * @param {HTMLElement[]} elements
 */
export async function batchPrint(elements) {
  const printWindow = window.open('', '_blank');
  if (!printWindow) return;

  const canvases = await Promise.all(
    elements.map((el) =>
      html2canvas(el, { scale: 2, useCORS: true, backgroundColor: '#ffffff' })
    )
  );

  const imgTags = canvases
    .map(
      (c) =>
        `<div style="page-break-after:always;margin:0;padding:0;text-align:center;">
           <img src="${c.toDataURL('image/png')}" style="max-width:100%;height:auto;" />
         </div>`
    )
    .join('');

  printWindow.document.write(`
    <!DOCTYPE html>
    <html>
      <head>
        <title>Impresión por lote — SportFrog</title>
        <style>
          @page { margin: 10mm; }
          body { margin: 0; padding: 0; }
          img { display: block; }
        </style>
      </head>
      <body>${imgTags}</body>
    </html>
  `);
  printWindow.document.close();
  printWindow.focus();
  printWindow.print();
}

// ── helpers ──────────────────────────────────────────────────────────────────

function triggerDownload(dataUrl, filename) {
  const a = document.createElement('a');
  a.href = dataUrl;
  a.download = filename;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
}

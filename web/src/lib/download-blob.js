// Dispara la descarga de una respuesta pedida con `responseType: 'blob'`.
// El nombre de archivo sale del Content-Disposition que mande el backend
// cuando lo manda; si no, se usa el que decida quien llama.
export function downloadBlob(res, fallbackName) {
  const url = window.URL.createObjectURL(res.data);
  const a = document.createElement('a');
  a.href = url;
  const disp = res.headers?.['content-disposition'] || '';
  const match = /filename\*=UTF-8''([^;]+)/i.exec(disp) || /filename="?([^";]+)"?/i.exec(disp);
  a.download = match ? decodeURIComponent(match[1] || match[0]) : fallbackName;
  document.body.appendChild(a); a.click(); document.body.removeChild(a);
  setTimeout(() => window.URL.revokeObjectURL(url), 1000);
}

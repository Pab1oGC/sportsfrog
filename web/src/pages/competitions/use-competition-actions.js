import { apiPut } from 'src/hooks/use-api';
import { endpoints, default as axios } from 'src/lib/axios';
import { downloadBlob } from 'src/lib/download-blob';
import { useConfirm } from 'src/components/confirm-dialog';
import { toast } from 'sonner';
import { STATUS_LABEL } from 'src/pages/competitions/competition-lifecycle';

/**
 * Las tres acciones de ciclo de vida de una competencia que CompetitionsPage
 * y su resumen (overview-page.jsx) necesitan por igual: cambiar de estado,
 * publicar/despublicar y descargar la convocatoria. Centralizadas acá para
 * que las dos pantallas pidan lo mismo de la misma forma -- antes solo
 * vivían, a mano, dentro de la primera.
 *
 * `mutate` es de quien llama: cada pantalla trae su propio `useApi` (la
 * grilla lee la lista entera, el resumen lee una sola competencia), así que
 * no hay un único refresco que este hook podría disparar por su cuenta.
 */
export function useCompetitionActions(mutate) {
  const confirm = useConfirm();

  const changeStatus = async (id, status) => {
    const ok = await confirm(`Cambiar estado a "${STATUS_LABEL[status] || status}"?`, { confirmLabel: 'Cambiar' });
    if (!ok) return;
    try { await apiPut(endpoints.competitionStatus(id), { status }); mutate(); }
    catch (err) { toast.error(err.message); }
  };

  // Nunca pide confirmación: a diferencia de un cambio de estado, publicar o
  // despublicar es un interruptor que hay que poder revertir sin un segundo
  // click -- "bajar un resultado equivocado" tiene que ser inmediato (ver
  // PublishCompetition del lado del servidor).
  const togglePublish = async (id, current) => {
    try { await apiPut(endpoints.competitionPublication(id), { isPublic: !current }); mutate(); }
    catch (err) { toast.error(err.message); }
  };

  const descargarConvocatoria = async (id, formato) => {
    const url = formato === 'pdf' ? endpoints.competitionBulletinPdf(id) : endpoints.competitionBulletinWord(id);
    const fallback = formato === 'pdf' ? 'convocatoria.pdf' : 'convocatoria.docx';
    try {
      const res = await axios.get(url, { responseType: 'blob' });
      downloadBlob(res, fallback);
    } catch (err) { toast.error(err.message); }
  };

  return { changeStatus, togglePublish, descargarConvocatoria };
}

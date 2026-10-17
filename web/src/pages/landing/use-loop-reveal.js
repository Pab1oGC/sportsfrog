import { useEffect, useRef } from 'react';
import gsap from 'gsap';
import { ScrollTrigger } from 'gsap/ScrollTrigger';
import { sinMovimiento } from 'src/lib/motion';

// Mismo motivo que use-scroll-reveal.js: cada hook se basta a sí mismo en
// vez de depender del orden de import de un módulo hermano para este efecto
// de borde. registerPlugin es idempotente -- llamarlo dos veces no hace nada raro.
gsap.registerPlugin(ScrollTrigger);

/**
 * Arranca una timeline de GSAP en loop (`repeat: -1`) la primera vez que el
 * elemento con el ref devuelto entra en pantalla -- distinto de
 * useScrollReveal (una revelación y termina): esto es para una secuencia que
 * se repite, como un gráfico de producto armándose solo una y otra vez.
 *
 * Sin movimiento (`prefers-reduced-motion`), la timeline nunca se crea ni se
 * arranca: los elementos quedan en su estado natural de CSS, que por eso
 * tiene que ser el estado "completo" (todo visible) y no uno a mitad de
 * animar -- la timeline los esconde recién cuando de verdad arranca, con
 * `gsap.from()`/`fromTo()`, igual que useScrollReveal.
 *
 * @param {(timeline: gsap.core.Timeline) => void} buildTimeline - Arma la
 *   timeline contra la instancia que recibe. Corre dentro de un gsap.context
 *   scopeado al ref devuelto, así que sus selectores de texto solo ven los
 *   elementos propios de este componente -- aunque haya cinco instancias
 *   montadas a la vez con las mismas clases, como las SectionPanel.
 */
export function useLoopReveal(buildTimeline) {
  const scopeRef = useRef(null);

  useEffect(() => {
    if (sinMovimiento() || !scopeRef.current) return undefined;

    const ctx = gsap.context(() => {
      const timeline = gsap.timeline({ repeat: -1, paused: true });
      buildTimeline(timeline);

      ScrollTrigger.create({
        trigger: scopeRef.current,
        start: 'top 85%',
        once: true,
        onEnter: () => timeline.play(),
      });
    }, scopeRef);

    return () => ctx.revert();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return scopeRef;
}

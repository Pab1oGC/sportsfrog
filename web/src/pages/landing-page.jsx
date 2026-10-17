import Box from '@mui/material/Box';
import { LandingNavbar } from 'src/pages/landing/navbar';
import { Hero } from 'src/pages/landing/hero';
import { LiveCompetitions } from 'src/pages/landing/live-competitions';
import { SectionNav } from 'src/pages/landing/section-nav';
import { SectionPanel } from 'src/pages/landing/section-panel';
import { ClosingCta } from 'src/pages/landing/closing-cta';
import { SiteFooter } from 'src/pages/landing/site-footer';
import { LANDING_SECTIONS } from 'src/pages/landing/content';
import { CredentialsVisual } from 'src/pages/landing/visuals/credentials-visual';
import { PortalVisual } from 'src/pages/landing/visuals/portal-visual';

// Los colores de esta portada salen del tema (theme/palette.js): acá no hay
// ningun hex. El rediseño a su registro formal (ver plan) terminó acá: el
// navbar, el hero, las SectionPanel y el cierre (ClosingCta + SiteFooter) ya
// comparten el mismo lenguaje visual -- este archivo solo compone, no tiene
// estilo propio.

// Qué gráfico le toca a cada sección de content.js -- content.js es solo
// datos (ver su propio comentario: no importa componentes), así que esta
// correspondencia vive acá, en el compositor, y no ahí.
//
// "competencias" y "escuelas" todavía no tienen gráfico propio -- content.js
// se reestructuró de 5 secciones a 4 (Reglamento+Calendario+Resultados se
// fusionaron en "Competencias"; "Escuelas" es nueva) y los gráficos viejos de
// esas tres (visuals/setup-visual.jsx, calendar-visual.jsx,
// results-visual.jsx) quedaron sin usar acá -- no se borraron todavía porque
// no está decidido si alguno se adapta para "Competencias" o si se rehacen
// desde cero. Hasta que se decida, SectionPanel ya sabe mostrar un
// marcador honesto ("Vista previa en construcción") en vez de nada.
const VISUALS_BY_SECTION_ID = {
  credenciales: CredentialsVisual,
  portal: PortalVisual,
};

export default function LandingPage() {
  return (
    <Box sx={{ minHeight: '100vh', bgcolor: 'background.default' }}>
      <LandingNavbar />

      {/* -------------------------------------------------------------- hero */}
      <Hero />

      {/* ------------------------------------------------ competencias reales */}
      <LiveCompetitions />

      {/* --------------------------------------------- secciones (registro formal) */}
      <SectionNav sections={LANDING_SECTIONS} />

      {LANDING_SECTIONS.map(function(section, index) {
        var Visual = VISUALS_BY_SECTION_ID[section.id];
        return (
          <SectionPanel key={section.id} section={section} index={index}>
            {Visual && <Visual />}
          </SectionPanel>
        );
      })}

      <ClosingCta />
      <SiteFooter />
    </Box>
  );
}

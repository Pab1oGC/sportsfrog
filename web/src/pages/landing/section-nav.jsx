import { useEffect, useRef, useState } from 'react';
import Box from '@mui/material/Box';
import Container from '@mui/material/Container';
import Typography from '@mui/material/Typography';
import { Iconify } from 'src/components/iconify';
import { scrollToSection } from './scroll-to-section';

/**
 * Fila de pills sticky, una por sección de la landing (ver content.js), que
 * resalta la que está en pantalla y hace scroll a la sección al hacer click.
 * Solo lee `id`/`navLabel`/`navIcon` de cada sección -- nunca su copy -- así
 * que agregar o reordenar secciones en content.js no toca este archivo.
 */
export function SectionNav({ sections }) {
  const ids = sections.map((section) => section.id);
  const activeId = useActiveSection(ids);

  function handleClick(id) {
    return function (event) {
      event.preventDefault();
      scrollToSection(id);
    };
  }

  return (
    <Box
      component="nav"
      aria-label="Secciones de la landing"
      sx={{
        position: 'sticky',
        top: { xs: 56, md: 64 },
        zIndex: 5,
        bgcolor: 'background.paper',
        borderBottom: 1,
        borderColor: 'divider',
      }}
    >
      <Container maxWidth="lg" sx={{ display: 'flex', gap: 1, overflowX: 'auto', py: 1.5 }}>
        {sections.map((section) => {
          const isActive = section.id === activeId;
          return (
            <Box
              key={section.id}
              component="a"
              href={`#${section.id}`}
              onClick={handleClick(section.id)}
              sx={{
                display: 'flex',
                alignItems: 'center',
                gap: 0.75,
                flexShrink: 0,
                px: 1.75,
                py: 0.75,
                borderRadius: 999,
                textDecoration: 'none',
                color: isActive ? 'primary.contrastText' : 'text.secondary',
                bgcolor: isActive ? 'primary.main' : 'transparent',
                transition: 'background-color 0.15s ease, color 0.15s ease',
                '&:hover': { bgcolor: isActive ? 'primary.main' : 'action.hover' },
              }}
            >
              <Iconify icon={section.navIcon} width={16} />
              <Typography variant="body2" fontWeight={600} noWrap>
                {section.navLabel}
              </Typography>
            </Box>
          );
        })}
      </Container>
    </Box>
  );
}

/**
 * Qué sección está en pantalla ahora, por intersección -- sin librerías
 * nuevas. La franja central angosta (`rootMargin` negativo arriba y abajo)
 * evita que dos secciones largas se disputen "activa" mientras ambas tienen
 * un trozo visible: solo importa cuál cruza el medio de la pantalla.
 */
function useActiveSection(ids) {
  const [activeId, setActiveId] = useState(ids[0]);
  const idsKey = ids.join(',');
  const idsRef = useRef(ids);
  idsRef.current = ids;

  useEffect(() => {
    const elements = idsRef.current.map((id) => document.getElementById(id)).filter(Boolean);
    if (elements.length === 0) return undefined;

    const observer = new IntersectionObserver(
      (entries) => {
        const visible = entries.filter((entry) => entry.isIntersecting);
        if (visible.length === 0) return;
        const topmost = visible.reduce((a, b) => (a.boundingClientRect.top <= b.boundingClientRect.top ? a : b));
        setActiveId(topmost.target.id);
      },
      { rootMargin: '-45% 0px -45% 0px' },
    );

    elements.forEach((element) => observer.observe(element));
    return () => observer.disconnect();
  }, [idsKey]);

  return activeId;
}

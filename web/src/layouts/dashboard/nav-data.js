export function getNavData(isPlatformAdmin = false) {
  return [
    { subheader: "Principal", items: [
      { title: "Dashboard", path: "/dashboard", icon: "mdi:view-dashboard-outline" },
    ]},
    { subheader: "Competencia", items: [
      // Categorias, Equipos y Nomina ya viven como pestañas dentro de una
      // competencia (ver CompetitionOverviewPage): colgarlas acá es que el
      // menú diga lo mismo que ya dice la pantalla, en vez de sugerir que
      // son secciones sueltas al mismo nivel que Competiciones. Siguen
      // siendo rutas propias -- NavVertical navega a "Competiciones" igual
      // que a cualquier otra, el desplegable es además, no en cambio.
      { title: "Competiciones", path: "/dashboard/competitions", icon: "mdi:trophy-outline", children: [
        { title: "Categorias", path: "/dashboard/categories" },
        { title: "Equipos / Inscripciones", path: "/dashboard/teams" },
        { title: "Nomina", path: "/dashboard/roster" },
      ]},
      { title: "Sanciones", path: "/dashboard/sanctions", icon: "mdi:gavel" },
      { title: "Clasificación", path: "/dashboard/performances", icon: "mdi:podium-gold" },
      { title: "Fixtures/Partidos", path: "/dashboard/matches", icon: "mdi:calendar-clock-outline" },
      { title: "Tabla posiciones", path: "/dashboard/standings", icon: "mdi:format-list-numbered" },
      { title: "Lideres", path: "/dashboard/leaders", icon: "mdi:star-outline" },
      { title: "Reportes", path: "/dashboard/reports", icon: "mdi:chart-box-outline" },
      { title: "Listas", path: "/dashboard/lists", icon: "mdi:format-list-bulleted" },
    ]},
    { subheader: "Gestion", items: [
      { title: "Clubes", path: "/dashboard/clubs", icon: "mdi:office-building-outline" },
      { title: "Deportistas", path: "/dashboard/athletes", icon: "mdi:run" },
      { title: "Sedes", path: "/dashboard/venues", icon: "mdi:map-marker-outline" },
      { title: "Reglamentos", path: "/dashboard/rulesets", icon: "mdi:book-open-outline" },
    ]},
    { subheader: "Escuela", items: [
      { title: "Cursos", path: "/dashboard/courses", icon: "mdi:school-outline" },
      { title: "Cobranza", path: "/dashboard/tuition", icon: "mdi:cash-multiple" },
    ]},
    { subheader: "Documentos", items: [
      { title: "Plantillas", path: "/dashboard/templates", icon: "mdi:file-document-outline" },
      { title: "Diseños de credencial", path: "/dashboard/credential-designs", icon: "mdi:card-account-details-outline" },
      { title: "Catalogo de acreditacion", path: "/dashboard/accreditation-catalog", icon: "mdi:badge-account-outline" },
      { title: "Acreditaciones", path: "/dashboard/accreditation", icon: "mdi:account-check-outline" },
      { title: "Documentos emitidos", path: "/dashboard/documents", icon: "mdi:certificate-outline" },
    ]},
    { subheader: "Sistema", items: [
      { title: "Miembros", path: "/dashboard/members", icon: "mdi:account-multiple-outline" },
      { title: "Organizacion", path: "/dashboard/organization", icon: "mdi:domain" },
      ...(isPlatformAdmin
        ? [{ title: "Crear Organización", path: "/dashboard/register-organization", icon: "mdi:domain-plus" }]
        : []),
    ]},
  ];
}

export var navData = getNavData(false);
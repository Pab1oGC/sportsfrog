export var navData = [
  { subheader: "Principal", items: [
    { title: "Dashboard", path: "/dashboard", icon: "mdi:view-dashboard-outline" },
  ]},
  { subheader: "Competencia", items: [
    { title: "Competiciones", path: "/dashboard/competitions", icon: "mdi:trophy-outline" },
    { title: "Categorias", path: "/dashboard/categories", icon: "mdi:tag-outline" },
    // Dos nombres porque la pantalla es dos cosas segun el deporte de la
    // competencia que se elija adentro, y el menu no puede saberlo de
    // antemano: equipos para un deporte de conjunto, inscripciones (o
    // deportistas sueltos) para uno individual.
    { title: "Equipos / Inscripciones", path: "/dashboard/teams", icon: "mdi:account-group-outline" },
    { title: "Nomina", path: "/dashboard/roster", icon: "mdi:account-multiple-check-outline" },
    { title: "Clasificación", path: "/dashboard/performances", icon: "mdi:podium-gold" },
    { title: "Fixtures/Partidos", path: "/dashboard/matches", icon: "mdi:calendar-clock-outline" },
    { title: "Tabla posiciones", path: "/dashboard/standings", icon: "mdi:format-list-numbered" },
    { title: "Lideres", path: "/dashboard/leaders", icon: "mdi:star-outline" },
    { title: "Reportes", path: "/dashboard/reports", icon: "mdi:chart-box-outline" },
  ]},
  { subheader: "Gestion", items: [
    { title: "Clubes", path: "/dashboard/clubs", icon: "mdi:office-building-outline" },
    { title: "Deportistas", path: "/dashboard/athletes", icon: "mdi:run" },
    { title: "Sedes", path: "/dashboard/venues", icon: "mdi:map-marker-outline" },
    { title: "Reglamentos", path: "/dashboard/rulesets", icon: "mdi:book-open-outline" },
  ]},
  { subheader: "Documentos", items: [
    { title: "Plantillas", path: "/dashboard/templates", icon: "mdi:file-document-outline" },
    { title: "Documentos emitidos", path: "/dashboard/documents", icon: "mdi:certificate-outline" },
  ]},
  { subheader: "Sistema", items: [
    { title: "Miembros", path: "/dashboard/members", icon: "mdi:account-multiple-outline" },
    { title: "Organizacion", path: "/dashboard/organization", icon: "mdi:domain" },
  ]},
];
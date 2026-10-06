import { lazy } from "react";
import { Navigate, Outlet } from "react-router";
import { AuthGuard } from "src/auth/guard";
import { GuestGuard } from "src/auth/guard";
import { DashboardLayout } from "src/layouts/dashboard";
import { PortalThemeBoundary } from "src/theme/portal-base";
import React from "react";
import Box from "@mui/material/Box";
import CircularProgress from "@mui/material/CircularProgress";

var LandingPage = lazy(function() { return import("src/pages/landing-page"); });
var SignInPage = lazy(function() { return import("src/pages/sign-in"); });
var DashboardPage = lazy(function() { return import("src/pages/dashboard-page"); });
var CompetitionsPage = lazy(function() { return import("src/pages/competitions-page"); });
var PortalStudioPage = lazy(function() { return import("src/pages/competitions/portal-studio"); });
var ClubsPage = lazy(function() { return import("src/pages/clubs-page"); });
var AthletesPage = lazy(function() { return import("src/pages/athletes-page"); });
var RulesetsPage = lazy(function() { return import("src/pages/rulesets-page"); });
var CategoriesPage = lazy(function() { return import("src/pages/categories-page"); });
var TeamsPage = lazy(function() { return import("src/pages/teams-page"); });
var RosterPage = lazy(function() { return import("src/pages/roster-page"); });
var PerformancesPage = lazy(function() { return import("src/pages/performances-page"); });
var MatchesPage = lazy(function() { return import("src/pages/matches-page"); });
var StandingsPage = lazy(function() { return import("src/pages/standings-page"); });
var LeadersPage = lazy(function() { return import("src/pages/leaders-page"); });
var ReportsPage = lazy(function() { return import("src/pages/reports-page"); });
var ListsPage = lazy(function() { return import("src/pages/lists-page"); });
var VenuesPage = lazy(function() { return import("src/pages/venues-page"); });
var TemplatesPage = lazy(function() { return import("src/pages/templates-page"); });
var TemplateDesignerPage = lazy(function() { return import("src/pages/template-designer"); });
var CredentialDesignsPage = lazy(function() { return import("src/pages/credential-designs-page"); });
var DocumentsPage = lazy(function() { return import("src/pages/documents-page"); });
var AccreditationCatalogPage = lazy(function() { return import("src/pages/accreditation-catalog-page"); });
var AccreditationAssignmentPage = lazy(function() { return import("src/pages/accreditation-assignment-page"); });
var MembersPage = lazy(function() { return import("src/pages/members-page"); });
var OrganizationPage = lazy(function() { return import("src/pages/organization-page"); });
var RegisterOrganizationPage = lazy(function() { return import("src/pages/register-organization-page"); });
var PublicPortalPage = lazy(function() { return import("src/pages/public/public-portal"); });
var PublicCompetitionPage = lazy(function() { return import("src/pages/public/public-competition"); });

function LazyPage(props) {
  return (
    <React.Suspense fallback={<Box sx={{ display: "flex", justifyContent: "center", mt: 10 }}><CircularProgress /></Box>}>
      <props.Component />
    </React.Suspense>
  );
}

// Las páginas de una competencia (/public/:org/:comp) conservan el tema base
// propio del portal (ver theme/portal-base.jsx): cada organizador lo
// personaliza y lo que no elige no debe seguir la paleta de la marca. El
// directorio /public NO entra acá: es una página de listado con el estilo de
// la portada, no un portal personalizable, y usa la paleta de la marca.
function PortalLazyPage(props) {
  return <PortalThemeBoundary><LazyPage Component={props.Component} /></PortalThemeBoundary>;
}

function LayoutOutlet() {
  return <DashboardLayout><Outlet /></DashboardLayout>;
}

export var routesSection = [
  // Landing & Public
  { path: "/", element: <LazyPage Component={LandingPage} /> },
  { path: "/public", element: <LazyPage Component={PublicPortalPage} /> },
  { path: "/public/:orgSlug/:compSlug", element: <PortalLazyPage Component={PublicCompetitionPage} /> },
  // Desconectada a propósito (2026-09-27): la verificación pública de
  // credenciales no hace falta todavía. La API ya no mapea
  // /public/verify/{org}/{serie} (ver Program.cs); esta ruta tampoco apunta
  // a nada del lado del cliente (PublicCompetitionPage no lee :serial).

  // Auth
  { path: "/auth/jwt/sign-in", element: <GuestGuard><SignInPage /></GuestGuard> },
  { path: "/auth", element: <Navigate to="/auth/jwt/sign-in" replace /> },

  // Dashboard
  {
    path: "/dashboard",
    element: <AuthGuard><LayoutOutlet /></AuthGuard>,
    children: [
      { index: true, element: <LazyPage Component={DashboardPage} /> },
      { path: "competitions", element: <LazyPage Component={CompetitionsPage} /> },
      { path: "competitions/:id/portal", element: <LazyPage Component={PortalStudioPage} /> },
      { path: "clubs", element: <LazyPage Component={ClubsPage} /> },
      { path: "athletes", element: <LazyPage Component={AthletesPage} /> },
      { path: "rulesets", element: <LazyPage Component={RulesetsPage} /> },
      { path: "categories", element: <LazyPage Component={CategoriesPage} /> },
      { path: "teams", element: <LazyPage Component={TeamsPage} /> },
      { path: "roster", element: <LazyPage Component={RosterPage} /> },
      { path: "performances", element: <LazyPage Component={PerformancesPage} /> },
      { path: "matches", element: <LazyPage Component={MatchesPage} /> },
      { path: "standings", element: <LazyPage Component={StandingsPage} /> },
      { path: "leaders", element: <LazyPage Component={LeadersPage} /> },
      { path: "reports", element: <LazyPage Component={ReportsPage} /> },
      { path: "lists", element: <LazyPage Component={ListsPage} /> },
      { path: "venues", element: <LazyPage Component={VenuesPage} /> },
      { path: "templates", element: <LazyPage Component={TemplatesPage} /> },
      { path: "templates/:id/design", element: <LazyPage Component={TemplateDesignerPage} /> },
      { path: "credential-designs", element: <LazyPage Component={CredentialDesignsPage} /> },
      { path: "documents", element: <LazyPage Component={DocumentsPage} /> },
      { path: "accreditation-catalog", element: <LazyPage Component={AccreditationCatalogPage} /> },
      { path: "accreditation", element: <LazyPage Component={AccreditationAssignmentPage} /> },
      { path: "members", element: <LazyPage Component={MembersPage} /> },
      { path: "organization", element: <LazyPage Component={OrganizationPage} /> },
      { path: "register-organization", element: <LazyPage Component={RegisterOrganizationPage} /> },
    ],
  },

  { path: "*", element: <Navigate to="/" replace /> },
];
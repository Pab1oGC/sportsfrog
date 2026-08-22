# SportFrog

SaaS multi-tenant para administrar competencias deportivas, clubes, atletas,
planteles, fixture, resultados, credenciales y portal público.

## Estado actual

### Funcional

- Frontend React 19 + TypeScript + Vite.
- API ASP.NET Core 10 con JWT y aislamiento por organización.
- PostgreSQL 16 y MinIO mediante Docker Compose.
- Registro de organización y login con renovación de sesión.
- CRUD de clubes.
- CRUD de atletas.
- Registro de fotografía del atleta como `data:image/...` PNG, JPEG o WebP,
  con límite de 5 MB.
- Modal de alta de atletas con preview 3x4, extensión departamental boliviana,
  cálculo de edad y tutor para menores.
- CORS para el frontend local.

### Pendiente de implementación backend

El esquema SQL ya contiene parte del dominio futuro, pero todavía falta exponerlo
como modelo EF y endpoints:

1. Competencias y categorías.
2. Equipos por club y categoría.
3. Nóminas: asignar atleta, dorsal y posición.
4. Sedes, canchas y disponibilidad.
5. Fixture por fases y generación de partidos.
6. Resultados, períodos, clima y eventos de partido.
7. Administradores específicos de equipo.
8. Pagos y estados de pago.
9. Plantillas, emisión, revocación y descarga de credenciales PDF.

No se debe guardar `team_id`, `jersey_number` o `position` en `athletes`: esos
valores pertenecen a una inscripción de nómina y requieren las tablas
`teams`/`roster_entries` que ya están declaradas en el esquema SQL.

## Arranque con Docker

Desde `api/`:

```powershell
$env:Path = "$env:LOCALAPPDATA\Programs\DockerDesktop\resources\bin;$env:Path"
docker compose --profile full up -d
```

Servicios:

- API: http://localhost:8080
- Health: http://localhost:8080/health
- PostgreSQL: localhost:5432
- MinIO: http://localhost:9000
- MinIO Console: http://localhost:9001

Ver estado:

```powershell
docker compose --profile full ps
```

La migración debe quedar como `Exited (0)`. La API debe aparecer como `Up`.

## Arranque local de la API

Requiere SDK .NET `10.0.301`. Los contenedores de PostgreSQL y MinIO deben
estar activos.

```powershell
cd api
$env:DOTNET_ROOT = "$env:USERPROFILE\.dotnet"
$env:Path = "$env:USERPROFILE\.dotnet;$env:Path"
$env:Authentication__Jwt__SigningKey = "dev-local-signing-key-32-bytes-min"
$env:ConnectionStrings__Default = "Host=localhost;Port=5432;Database=sportfrog;Username=sportfrog_app;Password=dev-local-2026"
$env:ConnectionStrings__Public = "Host=localhost;Port=5432;Database=sportfrog;Username=sportfrog_public;Password=dev-local-2026"
dotnet run --project .\src\SportFrog.Api\SportFrog.Api.csproj --no-restore
```

## Arranque del frontend

```powershell
cd frontend
$env:VITE_API_URL = "http://localhost:8080"
npm install
npm run dev
```

En este workspace el frontend está en la raíz, por lo que el comando equivalente
es:

```powershell
cd C:\Users\roberto.clavijo\Downloads\sportfrog
npm run dev
```

URL: http://localhost:5173

## Cuenta demo

La cuenta local creada durante la integración es:

- Email: `admin@rc.com`
- Password: `Password123!`
- Organización: `SportFrog Demo`

## Verificaciones

```powershell
cd api
dotnet build .\src\SportFrog.Api\SportFrog.Api.csproj --no-restore
cd ..
npm run build
```

## Git

Antes de hacer push, revisar siempre los cambios y no subir `.env` ni tokens:

```powershell
git status --short
git diff --stat
git remote -v
git add README.md api/src frontend/src
 git commit -m "Integrate SportFrog API and athlete registration"
git push origin master
```

El remoto objetivo es `https://github.com/Pab1oGC/sportsfrog.git`. El push
requiere que GitHub esté autenticado en esta máquina.

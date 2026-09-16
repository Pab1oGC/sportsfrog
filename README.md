# SportFrog

Plataforma para organizar competencias deportivas: inscribir jugadores, sortear
el fixture, cargar resultados, emitir credenciales y publicar resultados.

Es multiorganización. Cada liga o federación vive en la misma base de datos que
las demás y no puede ver ni una fila de las otras — eso no lo garantizan los
filtros de las consultas, lo garantiza PostgreSQL, y es la decisión de la que
cuelga casi todo lo demás.

```
D:\frogTech\sportsFrog
├── api/     Backend .NET, y los contenedores de datos que necesita
└── web/     Frontend React
```

Las dos mitades son independientes: se ejecutan por separado, se despliegan por
separado y hablan solo por HTTP. Cada una tiene su propio `.env`, y ninguno de
los dos está en el repositorio.

---

## Requisitos

| Herramienta | Versión | Comprobación |
|---|---|---|
| SDK de .NET | 10.0 o superior | `dotnet --version` |
| Node.js | 20.19+ o 22.12+ | `node --version` |
| Docker Desktop | 27 o superior, con Compose v2 | `docker compose version` |
| Herramientas de EF Core | 10.0 o superior | `dotnet ef --version` |

La versión del SDK está fijada en `api/global.json`: si la instalada no sirve,
cualquier comando de `dotnet` falla de entrada diciendo cuál hace falta, en vez
de compilar con una distinta a la del resto del equipo.

---

## Arrancar de cero

Cuatro pasos, en este orden. Los tres primeros son dentro de `api/`.

### 1. Los contenedores de datos

```bash
cd api
cp .env.example .env       # y reemplazar TODAS las contraseñas
docker compose up -d
docker compose ps          # sportfrog-postgres y sportfrog-minio en healthy
```

| Servicio | Puerto | Para qué |
|---|---|---|
| PostgreSQL 16 | 5432 | Base de datos |
| MinIO | 9000 | Almacenamiento de objetos compatible con S3 |
| Consola de MinIO | 9001 | Interfaz web, con las credenciales `MINIO_ROOT_*` |

El script que crea los tres roles de base de datos corre **una sola vez**, con
el volumen vacío. Para volver a ejecutarlo hay que destruirlo:
`docker compose down -v`.

### 2. Las migraciones

```bash
dotnet ef database update --project src/SportFrog.Api
```

Corren con `sportfrog_owner`, no con el usuario de la aplicación. El detalle de
por qué, y cómo escribir una migración nueva, está en
[`api/README.md`](api/README.md).

### 3. La API

```bash
dotnet run --project src/SportFrog.Api
```

Queda en **http://localhost:5293**. `GET /health` responde 200 cuando está lista.

### 4. El frontend

```bash
cd ../web
cp .env.example .env       # se deja vacío: es el valor correcto en desarrollo
pnpm install
pnpm dev
```

Queda en **http://localhost:5173**.

`VITE_API_URL` vacío hace que axios pida rutas relativas (`/api/...`), que el
proxy de `vite.config.js` reenvía a `:5293` quitando el prefijo. Todo queda en
el mismo origen: sin CORS, sin preflight, y con las cabeceras de descarga
legibles.

> `web/.env` **no es un lugar para secretos.** Vite hornea cada variable
> `VITE_*` dentro del bundle JavaScript al compilar: queda en texto plano en el
> `.js` que descarga cualquier visitante. Las contraseñas, la firma del JWT y
> las claves de MinIO van en `api/.env`, que nunca sale del servidor.

### Probarlo sin instalar nada

Hay un perfil de Compose que levanta la API en un contenedor, migraciones
incluidas, para quien solo quiere verla andar sin SDK ni IDE. Está documentado
en [`api/README.md`](api/README.md).

### Desplegarlo, todo dockerizado

Otro perfil de Compose levanta el sitio completo — Postgres, MinIO, la API y
el frontend detrás de nginx — para un despliegue real en vez de una prueba
local. También está en [`api/README.md`](api/README.md), con el paso que hay
que hacer antes de exponerlo con un túnel o un dominio propio.

---

## Qué hace el sistema

| Módulo | Qué resuelve |
|---|---|
| **Organizaciones y miembros** | Alta de la liga, invitaciones, roles |
| **Reglamentos** | Cómo se puntúa, cuántos períodos, qué desempata, qué métricas se registran |
| **Competencias** | Liga, eliminación directa o fase de grupos; borrador → programada → en curso → finalizada |
| **Categorías** | Divisiones por edad y género, con reglamento propio si hace falta |
| **Clubes, equipos y deportistas** | El padrón, y la nómina de cada equipo por categoría |
| **Inscripción por planilla** | Plantilla de Excel que conoce sus propias reglas, se previsualiza y se aplica; las fotos se suben aparte en lote |
| **Sorteo** | Fixture de liga a una o dos vueltas, llaves ronda por ronda, o una liga dentro de cada zona |
| **Calendario** | Sedes, canchas y horarios; reparto automático de los partidos entre los espacios disponibles |
| **Resultados y eventos** | Marcador por período, goles, tarjetas, walkover, aplazamiento |
| **Tablas y líderes** | Posiciones y goleadores, derivados en cada lectura — nunca almacenados |
| **Credenciales y certificados** | Se acomodan los datos sobre el arte con el mouse, se emiten en lote en segundo plano, y salen en una hoja lista para imprimir |
| **Verificación pública** | Cada documento lleva un QR que abre una página diciendo si sigue vigente |
| **Portal público** | Directorio de competencias, tabla, calendario y líderes, sin iniciar sesión |

Deportes en el catálogo: fútbol, futsal, vóleibol, básquetbol y wally.

### Roles

| Rol | Alcance |
|---|---|
| `owner` | Creó la organización. No se puede quitar ni degradar |
| `admin` | Administración completa |
| `operator` | Opera la competencia: fixture, equipos, nóminas |
| `recorder` | Solo carga resultados |
| `viewer` | Solo lectura del panel |

### Direcciones públicas

Ninguna necesita credenciales.

```
GET  /public/competitions                              directorio, cruza organizaciones
GET  /public/{organización}/{competencia}              la competencia y sus categorías
GET  /public/{organización}/{competencia}/standings    tabla de posiciones
GET  /public/{organización}/{competencia}/leaders      goleadores y tarjetas
GET  /public/{organización}/{competencia}/matches      calendario y resultados
GET  /public/{organización}/{competencia}/teams/{id}/roster    nómina de un equipo
GET  /public/verify/{organización}/{serie}             ¿esta credencial es válida?
```

Una competencia aparece acá solo si su organización la publicó. Qué secciones se
muestran lo decide cada competencia.

---

## Estructura

```
api/
├── src/SportFrog.Api/        Endpoints, infraestructura y migraciones
├── src/SportFrog.Domain/     Las reglas del deporte. Sin dependencias
├── tests/                    Dominio (sin Docker) y extremo a extremo (con Docker)
├── docker/                   Inicialización de PostgreSQL: crea los tres roles
├── scripts/new-migration.sh  Genera el par de archivos .sql de una migración
└── README.md                 Migraciones, esquema, configuración, estructura

web/
├── src/pages/                Una página por módulo del panel
├── src/pages/public/         Portal público: directorio y detalle
├── src/auth/                 Sesión, guardas de ruta y almacenamiento del token
├── src/lib/axios.js          Instancia, interceptores y catálogo de endpoints
├── src/components/           Piezas compartidas: selectores, iconos
└── vite.config.js            Proxy de /api hacia la API
```

El panel vive bajo `/dashboard` y el portal público bajo `/public`. La portada
en `/` muestra las competencias publicadas leyendo el directorio real.

---

## Convenciones

- **Ningún secreto en el repositorio.** `.env` está en `.gitignore`; los
  `.env.example` llevan marcadores, nunca valores reales.
- **Los enums viajan como texto** en los contratos y se interpretan dentro de la
  funcionalidad. Un enum atado directamente falla en el lector de JSON y nunca
  llega al validador, que es la diferencia entre un mensaje útil y un 400 vacío.
- **Nada de `dotnet ef migrations add`.** Ver [`api/README.md`](api/README.md).
- **Las tablas derivadas se derivan.** Posiciones y goleadores se calculan en
  cada lectura: una copia almacenada queda vieja apenas se corrige un evento, y
  nada lo diría.

---

## Dónde seguir

[**`api/README.md`**](api/README.md) — el documento de fondo: cómo aplicar y
revertir migraciones, cómo escribir una nueva, qué hacer al crear una tabla de
negocio, cómo se configuran las cadenas de conexión y el almacenamiento, y el
mapa completo de carpetas del backend.

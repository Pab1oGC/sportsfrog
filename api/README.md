# SportFrog — API

Backend de SportFrog: ASP.NET Core 10, minimal APIs, organizado en cortes
verticales por funcionalidad.

Todo lo que necesita esta API vive en esta carpeta, incluidos sus contenedores
de datos. El frontend, en `../web/`, es independiente.

El aislamiento entre organizaciones lo garantiza Row Level Security de
PostgreSQL, no los filtros de las consultas: si el contexto de organización no
se estableció, ninguna fila satisface la política y el sistema falla de forma
cerrada. La vista pública opera sin autenticación y con un usuario de base de
datos de solo lectura.

Todos los comandos de este documento se ejecutan **desde esta carpeta**
(`api/`).

---

## Requisitos previos

| Herramienta | Versión | Comprobación |
|---|---|---|
| SDK de .NET | 10.0 o superior | `dotnet --version` |
| Docker Desktop | 27 o superior, con Compose v2 | `docker compose version` |
| Herramientas de EF Core | 10.0 o superior | `dotnet ef --version` |

Si `dotnet ef` no está instalado:

```bash
dotnet tool install --global dotnet-ef
```

La versión del SDK está fijada en `global.json`. Si la instalada no sirve,
cualquier comando de `dotnet` falla de entrada indicando cuál hace falta, en
lugar de compilar con una versión distinta a la del resto del equipo.

---

## Levantar los contenedores

Las credenciales viven en `.env`, que está ignorado por Git. Copiar la
plantilla y **reemplazar todas las contraseñas** antes de arrancar:

```bash
cp .env.example .env
# editar .env
docker compose up -d
```

Comprobar que ambos servicios quedaron en verde:

```bash
docker compose ps
```

Se esperan `sportfrog-postgres` y `sportfrog-minio` con estado `healthy`.

| Servicio | Puerto | Para qué |
|---|---|---|
| PostgreSQL | 5432 | Base de datos |
| MinIO | 9000 | Almacenamiento de objetos compatible con S3 |
| Consola de MinIO | 9001 | Interfaz web, con las credenciales `MINIO_ROOT_*` |

El script `docker/postgres/init/01-roles.sh` corre **una sola vez**, cuando el
volumen de datos está vacío. Para volver a ejecutarlo hay que destruir el
volumen:

```bash
docker compose down -v
docker compose up -d
```

### Probar la aplicación sin instalar el SDK

En el flujo normal de desarrollo la API **no** corre en un contenedor: se
ejecuta desde el IDE contra los contenedores de datos, con depurador y recarga
en caliente.

Para quien solo quiere probarla —sin .NET, sin IDE— hay un perfil que levanta
todo, con el esquema ya aplicado:

```bash
cp .env.example .env    # y cambiar las contraseñas
docker compose --profile full up -d
# la API queda en http://localhost:8080/health
```

El arranque encadena tres pasos: Postgres queda sano, el contenedor `migrate`
aplica las migraciones y termina, y recién entonces arranca la API. Si las
migraciones fallan, la API no arranca.

Sin `--profile full` esos dos servicios se ignoran, así que el flujo de
desarrollo no cambia.

El contenedor de la API **nunca aplica migraciones**: eso lo hace `migrate`,
que se conecta como propietario del esquema. La separación de usuarios se
mantiene también acá.

Para ver qué hizo:

```bash
docker compose logs migrate
```

---

## Aplicar las migraciones

### Con qué usuario, y por qué no con el de la aplicación

El script de inicialización crea tres roles con responsabilidades separadas:

| Rol | Quién lo usa | Qué puede hacer |
|---|---|---|
| `sportfrog_owner` | Las migraciones, a mano | Es el propietario del esquema: crea, altera y destruye objetos |
| `sportfrog_app` | La API | Lee y escribe datos, siempre bajo un contexto de organización establecido con `SET LOCAL` |
| `sportfrog_public` | La vista pública anónima | Solo lectura, y únicamente sobre las tablas que esa vista necesita |

Las migraciones **no** se aplican con `sportfrog_app` porque ese rol no es
propietario de las tablas y no debe poder modificar el esquema: si pudiera
hacerlo, también podría alterar o eliminar las políticas de aislamiento que lo
contienen. Esas políticas están declaradas con `FORCE ROW LEVEL SECURITY`, de
modo que alcanzan al propietario tanto como a cualquier otro rol.

Por la misma razón la aplicación no aplica migraciones al arrancar: el
despliegue del esquema es un paso deliberado y con otro usuario.

### Comandos, desde cero

La cadena de conexión se pasa por variable de entorno, que es de donde la lee
la fábrica de tiempo de diseño; así la contraseña del propietario no queda en
ningún archivo del repositorio.

```bash
export SPORTFROG_MIGRATIONS_CONNECTION="Host=localhost;Port=5432;Database=sportfrog;Username=sportfrog_owner;Password=LA-CLAVE-DE-.ENV"

dotnet ef database update --project src/SportFrog.Api
```

Equivalente en PowerShell:

```powershell
$env:SPORTFROG_MIGRATIONS_CONNECTION = "Host=localhost;Port=5432;Database=sportfrog;Username=sportfrog_owner;Password=LA-CLAVE-DE-.ENV"
dotnet ef database update --project src/SportFrog.Api
```

Verificar el resultado:

```bash
dotnet ef migrations list --project src/SportFrog.Api
```

Se esperan dos, ambas aplicadas:

- `20260813120000_InitialSchema` — extensiones, tipos enumerados, tablas,
  índices, restricciones, políticas de aislamiento, `current_org_id()`, los dos
  roles de aplicación con sus concesiones, `resolve_public_competition` y los
  disparadores.
- `20260813120100_SeedCatalog` — los cinco deportes y sus métricas.

### Revertir

```bash
# Deshace solo el catálogo
dotnet ef database update 20260813120000_InitialSchema --project src/SportFrog.Api

# Deshace todo
dotnet ef database update 0 --project src/SportFrog.Api
```

La reversión del esquema inicial revoca las concesiones pero **no elimina los
roles**: los provisiona el entorno, no la migración.

### Generar el script para producción

```bash
dotnet ef migrations script --idempotent --output deploy.sql --project src/SportFrog.Api
```

No toca ninguna base: deja un `.sql` revisable que aplica quien administra el
servidor. Así la aplicación nunca necesita permisos de DDL.

---

## Cambiar el esquema

El esquema lo define SQL, no el modelo de EF. No hay entidades ni `DbSet` que
generen las tablas, así que **`dotnet ef migrations add` no se usa en este
proyecto**: no hay nada que generar a partir del modelo.

Cada migración son tres archivos: una clase en `src/SportFrog.Api/Migrations/`
que solo apunta a dos archivos `.sql` en `Migrations/Sql/`, donde está el
trabajo real. Se eligió esa forma sobre las cadenas dentro de C# porque el SQL
así se revisa, se compara entre versiones y se puede ejecutar a mano contra la
base sin intermediarios.

### Aplicar una nueva migración

> Una migración que ya se aplicó en otra máquina **no se edita nunca**. EF la
> tiene registrada y no la vuelve a ejecutar, así que editarla hace que las
> bases diverjan en silencio. Todo cambio posterior es una migración nueva.

Ejemplo completo: agregar un teléfono a los clubes.

**1. Crear los archivos**

```bash
./scripts/new-migration.sh AddClubPhone
```

Genera tres archivos con la marca de tiempo ya sincronizada entre ellos:

```
src/SportFrog.Api/Migrations/20260814041320_AddClubPhone.cs         ← no se toca
src/SportFrog.Api/Migrations/Sql/20260814041320_AddClubPhone.Up.sql
src/SportFrog.Api/Migrations/Sql/20260814041320_AddClubPhone.Down.sql
```

El `.cs` sale completo: solo apunta a los otros dos. Esa marca de tiempo
aparece en el nombre de la clase, en el atributo `[Migration]` y en dos cadenas
dentro del `.cs`; si no coinciden, el compilador no lo detecta y la migración
falla al aplicarse. Por eso conviene el script y no crear los archivos a mano.

**2. Escribir el cambio en el `.Up.sql`**

```sql
ALTER TABLE clubs ADD COLUMN phone text;
```

**3. Escribir el reverso en el `.Down.sql`**

Tiene que deshacer exactamente lo del paso anterior, en orden inverso.

```sql
ALTER TABLE clubs DROP COLUMN phone;
```

No es opcional. Es lo que permite deshacer un despliegue fallido sin recrear la
base, y se ejecuta de verdad: probalo con `dotnet ef database update` apuntando
a la migración anterior, y volviendo a subir.

**4. Aplicar**

```bash
export SPORTFROG_MIGRATIONS_CONNECTION="Host=localhost;Port=5432;Database=sportfrog;Username=sportfrog_owner;Password=LA-CLAVE-DE-.ENV"

dotnet ef database update --project src/SportFrog.Api
```

**5. Verificar**

```bash
dotnet ef migrations list --project src/SportFrog.Api
```

La nueva tiene que aparecer en la lista y **sin** `(Pending)`. Si dice
`(Pending)`, no se aplicó.

**6. Actualizar la entidad**

Si la tabla ya tiene una entidad mapeada en `SportFrog.Domain/Entities/`, hay
que agregarle la propiedad y declararla en su `IEntityTypeConfiguration`. El
SQL y el modelo son dos verdades separadas: nada las sincroniza solo.

### Al crear una tabla de negocio

Una columna nueva no necesita nada especial. Una **tabla** nueva con `org_id`
sí: los bucles de la migración inicial recorren una lista literal de tablas y
no alcanzan a las que se agreguen después. En la misma migración van el
aislamiento, los permisos y el disparador de `updated_at`:

```sql
ALTER TABLE nueva_tabla ENABLE ROW LEVEL SECURITY;
ALTER TABLE nueva_tabla FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON nueva_tabla
    USING (org_id = current_org_id())
    WITH CHECK (org_id = current_org_id());

GRANT SELECT, INSERT, UPDATE, DELETE ON nueva_tabla TO sportfrog_app;

CREATE TRIGGER trg_nueva_tabla_touch BEFORE UPDATE ON nueva_tabla
    FOR EACH ROW EXECUTE FUNCTION touch_updated_at();
```

Omitir los permisos falla ruidosamente. **Omitir el aislamiento no falla
nunca**, hasta que una organización ve datos de otra. La plantilla que genera
el script lleva este bloque como recordatorio.

---

## Compilar y probar

```bash
dotnet build
dotnet test
```

Los proyectos de prueba usan Testcontainers, que necesita Docker en marcha.

Para correr la API:

```bash
dotnet run --project src/SportFrog.Api
# GET http://localhost:5xxx/health  ->  200
```

Las cadenas de conexión salen de `appsettings.Development.json`, que no lleva
contraseñas: cada una se completa desde el entorno, que tiene precedencia sobre
el archivo.

| Variable | Usuario |
|---|---|
| `ConnectionStrings__Default` | `sportfrog_app` |
| `ConnectionStrings__Public` | `sportfrog_public` |
| `ConnectionStrings__Migrations` | `sportfrog_owner` |

Para el trabajo diario, `dotnet user-secrets` mantiene las credenciales fuera
del árbol del repositorio.

---

## Estructura

| Carpeta | Qué contiene |
|---|---|
| `src/SportFrog.Api/Features/` | Un corte vertical por funcionalidad: endpoint, validación y consulta juntos. Sin capa de servicios intermedia. |
| `src/SportFrog.Api/Infrastructure/Persistence/` | `DbContext`, fábrica de tiempo de diseño y acceso a datos. |
| `src/SportFrog.Api/Infrastructure/Tenancy/` | Resolución de la organización y establecimiento del contexto de aislamiento por transacción. |
| `src/SportFrog.Api/Infrastructure/Auth/` | Emisión y validación de credenciales, resolución de permisos por rol. |
| `src/SportFrog.Api/Infrastructure/Storage/` | Carga y descarga de objetos contra S3/MinIO, y normalización de imágenes. |
| `src/SportFrog.Api/Infrastructure/Jobs/` | Trabajos en segundo plano: generación de documentos y tareas diferidas. |
| `src/SportFrog.Api/Infrastructure/Documents/` | Composición de credenciales y certificados en PDF a partir de las plantillas. |
| `src/SportFrog.Api/Infrastructure/Caching/` | Almacenamiento temporal de las consultas de la vista pública. |
| `src/SportFrog.Api/Migrations/` | Migraciones de EF Core y el SQL incrustado que ejecutan. |
| `src/SportFrog.Domain/Entities/` | Entidades del dominio, con sus invariantes. |
| `src/SportFrog.Domain/ValueObjects/` | Tipos sin identidad propia: marcadores, rangos de fechas, documentos. |
| `src/SportFrog.Domain/Rules/` | Reglamentos: puntuación, desempates y validación de la configuración. |
| `src/SportFrog.Domain/Scheduling/` | Generación de fixtures y asignación de encuentros a espacios y horarios. |
| `src/SportFrog.Domain/Abstractions/` | Contratos que el dominio declara y la infraestructura implementa. |
| `tests/SportFrog.Domain.Tests/` | Pruebas del dominio, sin base de datos ni contenedores. |
| `tests/SportFrog.Api.Tests/` | Pruebas de extremo a extremo sobre la API, con PostgreSQL en Testcontainers. |
| `docker/` | Script de inicialización de PostgreSQL: crea los tres roles. |
| `scripts/` | Utilidades de desarrollo: `new-migration.sh`. |

`SportFrog.Domain` no declara ninguna dependencia externa a propósito: compila
contra la biblioteca base y nada más.

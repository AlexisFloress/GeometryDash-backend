# GeometryDash Backend

API independiente en **ASP.NET Core 10 + EF Core + PostgreSQL**. Este repositorio se puede clonar y ejecutar sin tener el frontend en la misma computadora.

## Estructura

```text
GeometryDash.Backend.sln
GeometryDash.Api/
  Domain/                 entidades y reglas del juego/niveles
  Data/                   EF Core y migraciones SQL versionadas
  Endpoints/              identidad, niveles, interacciones, administración
  Infrastructure/         sesión, validaciones HTTP y errores
  wwwroot/                documentación y probador básico de la API
tests/
  GeometryDash.DomainChecks/
  api.integration.mjs
scripts/
  configure-local.ps1
  start-backend.ps1
  create-database.mjs
docs/
compose.yaml              PostgreSQL opcional para desarrollo
```

## Ejecutar

Requisitos: SDK .NET 10 y PostgreSQL 18. Node es opcional para la prueba HTTP y el asistente de creación de base.

1. Si es una instalación nueva, crea la base y un usuario propios. Configura `GeometryDash.Api/appsettings.Local.json` siguiendo `appsettings.Local.example.json`. El archivo local está excluido de Git. También puedes ejecutar `scripts/configure-local.ps1` si tu terminal permite scripts PowerShell, o usar variables `ConnectionStrings__Database` y `Jwt__Key`.
2. En esta computadora **ya existen la conexión local y la base `geometrydash` migrada**; no ejecutes el creador de base para reemplazarla.
3. Desde la raíz de este repositorio:

```powershell
dotnet restore GeometryDash.Backend.sln
dotnet build GeometryDash.Backend.sln
dotnet run --project GeometryDash.Api -- --migrate
dotnet run --project GeometryDash.Api --launch-profile http
```

API: http://localhost:5080. Contrato: `/openapi/v1.json`. Documentación básica: `/docs/backend`. Proceso: `/health/live`. Conexión y roles: `/health/ready`.

Las migraciones se aplican mediante `--migrate`, no al iniciar el servidor. Se usa SQL versionado en `GeometryDash.Api/Data/Migrations` y un registro con checksum en `__GeometryMigrations`. **No uses EnsureCreated ni dotnet ef database update sobre este esquema.**

Si la API está ejecutándose en Visual Studio, detén esa instancia antes de compilar sobre `bin/`, para evitar archivos bloqueados. No hacen falta cambios en PostgreSQL por mover esta carpeta.

### Base nueva opcional

```powershell
node scripts/create-database.mjs
```

Pide la contraseña administrativa de PostgreSQL en la terminal, crea base y rol nuevos, aplica el esquema y genera configuración local. Rechaza nombres/configuraciones existentes. Opciones de entorno: `GD_DB_NAME`, `GD_DB_USER`, `GD_PG_HOST`, `GD_PG_PORT`, `GD_PG_ADMIN`, `GD_PSQL`. No envíes contraseñas por el chat ni las escribas en archivos de ejemplo.

Otra opción es copiar `.env.example` a `.env`, establecer `POSTGRES_PASSWORD` y ejecutar `docker compose up -d db`. Usa el puerto 5433 de ese contenedor en tu conexión. El PostgreSQL existente utiliza 5432; el contenedor es una base distinta.

## Archivos locales de desarrollo

`.local/`, dentro de este repositorio, conserva cachés de .NET/NuGet, herramientas de comprobación y material temporal del desarrollo. No es código de la aplicación ni contiene la base PostgreSQL activa. Está excluida de Git. Se conserva para poder restaurar paquetes sin Internet en este equipo; si se elimina o se mueve, hay que restaurar las dependencias de nuevo antes de compilar. No debe publicarse, porque puede incluir copias de configuración local.

## Funciones

Consulta [qué hace cada endpoint](docs/endpoints.md) para leer una explicación sencilla de cada operación y saber quién puede usarla. Estas explicaciones también aparecen en la documentación web de `/docs/backend`.

La [referencia de solicitudes](docs/solicitudes-api.md) contiene los JSON de ejemplo, campos, encabezados y filtros para integrar la API. Los ejemplos se mantienen en `GeometryDash.Api/Documentation/requests.json`, se incluyen en OpenAPI y se cargan automáticamente en los probadores del frontend y del backend. Los endpoints sin cuerpo lo indican expresamente. Con la API ejecutándose, puedes comprobar la documentación sin cambiar datos con `node tests/api.documentation.mjs .local/docs-openapi.json` y regenerar la referencia con `node scripts/generate-endpoint-reference.mjs .local/docs-openapi.json`. Usa `GD_DOCS_URL` para comprobar otra dirección del contrato.

La documentación y el probador al final de la página están disponibles sin iniciar sesión, tanto en desarrollo como en producción. Después de desplegar esta versión, se podrán abrir en https://geometrydash-api.onrender.com/docs/backend. El probador usa el servicio desde el que se abre la página, sin depender de localhost. Para probar inicio de sesión, renovación y cierre desde el dominio del backend, incluye también `https://geometrydash-api.onrender.com` en `AllowedOrigins`; conserva los orígenes del frontend que ya uses. Configura `AllowedHosts` con el nombre público del backend. Si sirves el frontend bajo el mismo dominio, conserva sus reglas para reenviar `/api/*` y `/openapi/*` al backend, antes del fallback de React.

Identity, JWT y sesiones renovables; perfiles; borradores privados y concurrencia If-Match; validación de geometría; publicación por revisiones; catálogo; favoritos; votos; intentos y progreso; administración y auditoría. Los resultados de partidas son **ClientReported**, sin verificación de replay.

El frontend llama a `/api/v1` y descarga `/openapi/v1.json`; no hay dependencia hacia su carpeta ni su repositorio. El backend admite los orígenes locales 5080/5173. En producción configura los dominios reales y HTTPS.

## Pruebas y publicación

```powershell
dotnet run --project tests/GeometryDash.DomainChecks
# Solo contra una base desechable con la API iniciada:
node tests/api.integration.mjs
dotnet publish GeometryDash.Api --configuration Release --output artifacts/api
```

La suite de dominio contiene 31 comprobaciones. La prueba HTTP crea cuentas y niveles: no la ejecutes contra datos reales. El workflow `.github/workflows/checks.yml` comprueba compilación y dominio cuando subas el repositorio a GitHub; no despliega ni accede a tu base local.

`appsettings.Local.json` está excluido tanto de Git como del artefacto de publicación. Los secretos de producción se configuran en el servidor, no se copian desde tu computadora.

Guía completa: [arquitectura y publicación](docs/arquitectura-y-publicacion.md), también [en Word](docs/arquitectura-y-publicacion.docx). Referencia funcional: [especificación](docs/especificacion.txt). Puedes regenerar el documento Word desde el Markdown con `node scripts/generate-guide.mjs`.

## Subir como repositorio independiente

Git está inicializado con la rama `main`; no hay commit ni remoto configurados. Crea un repositorio remoto vacío, por ejemplo `geometrydash-backend`, y desde esta carpeta:

```powershell
git status
git add .
git diff --cached --name-only
git commit -m "Proyecto inicial del backend"
git remote add origin https://github.com/TU_USUARIO/geometrydash-backend.git
git push -u origin main
```

Comprueba la lista antes del commit: no debe contener contraseñas, `appsettings.Local.json`, `.env`, `bin/` ni `obj/`. Si ya configuraste un remoto, consulta `git remote -v` antes de agregar otro. Necesitas tu propia autenticación en GitHub para el push.

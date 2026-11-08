# GeometryDash: cómo está construido y cómo publicarlo

Guía del código existente, revisada el 8 de noviembre de 2026. Explica la separación en dos repositorios y una forma de publicar la aplicación. Los dominios y rutas de servidor que aparecen son ejemplos; no representan un despliegue ya realizado.

## 1. Qué aplicación estás construyendo

GeometryDash permite construir recorridos 2D, probarlos, publicarlos y jugar los de otras personas. El personaje avanza solo; el jugador decide cuándo saltar. Cubos, triángulos y plataformas componen cada recorrido.

Una persona sin cuenta puede explorar y jugar localmente. Una cuenta permite guardar creaciones, resultados, favoritos y valoraciones. El administrador puede retirar contenido o suspender cuentas. No se implementaron pagos, chat, multijugador ni clasificación competitiva.

## 2. Dos proyectos y tres partes de ejecución

El código se organiza en dos repositorios independientes:

| Repositorio sugerido | Carpeta local actual | Responsabilidad |
| --- | --- | --- |
| geometrydash-frontend | GeometryDash-web | Interfaz React, editor y juego Canvas |
| geometrydash-backend | GeometryDash-backend | API ASP.NET Core, permisos, persistencia y migraciones |

PostgreSQL es el tercer componente que debes ejecutar o contratar. Su servicio y sus datos no son un tercer repositorio de código fuente: el backend contiene el esquema, pero no se suben los archivos internos de la base a Git.

```text
Navegador
  └─ React + TypeScript
       ├─ Editor: produce un documento JSON
       ├─ Canvas: ejecuta las físicas del recorrido
       └─ HTTP /api/v1
             └─ ASP.NET Core
                  ├─ Autenticación y autorización
                  ├─ Validación y transacciones
                  └─ EF Core + Npgsql → PostgreSQL
```

No hay referencias de archivos entre ambos repositorios. El acuerdo compartido es el contrato HTTP: rutas, nombres de campos, estados y formato JSON. Puedes alojarlos o clonarlos por separado manteniendo ese contrato.

## 3. Cómo funciona el frontend

El frontend utiliza React para pantallas y formularios, TypeScript para describir datos y React Router para navegación. Vite ejecuta el entorno de desarrollo y construye los archivos estáticos. CSS propio define una interfaz sencilla, sin un framework visual adicional.

`src/pages` contiene catálogo, acceso, cuenta, ficha, biblioteca, documentación y administración. `src/lib/api.ts` concentra solicitudes HTTP, Bearer, renovación de sesión y errores. `auth.tsx` distribuye la sesión a los componentes y protege rutas. `types.ts` describe los DTO recibidos o enviados.

El editor trabaja con una copia local del nivel. Permite crear objetos, cambiar posiciones, duplicarlos y eliminarlos; también ofrece deshacer/rehacer, desplazamiento, zoom, prueba y publicación. Tras dos segundos de inactividad intenta guardar, sin superponer solicitudes.

La copia recuperable del editor se identifica por usuario, nivel y revisión. Un conflicto de versión no sobrescribe silenciosamente el servidor: conserva la copia y ofrece descargarla o recuperar la remota. El almacenamiento del navegador puede ser borrado por el usuario; no reemplaza el guardado remoto.

El juego vive en `src/game`. La simulación usa pasos fijos de 1/120 de segundo, independientemente de la frecuencia de dibujo. El Canvas representa el mundo; React muestra botones, pausa, avance y resultados. Se admiten Espacio, W, flecha arriba, clic derecho y toque como entradas según configuración.

La física v1 usa velocidad base 6, gravedad 30, impulso 10.5 y jugador de 0.8 × 0.8 unidades. No son parámetros libres del creador. La prueba de borrador se ejecuta sin registrar progreso público.

## 4. Cómo funciona el backend

El backend es un monolito modular en ASP.NET Core 10. No requiere microservicios ni una cola para esta entrega. `Program.cs` configura servicios, autenticación, errores, límites, CORS y rutas.

| Carpeta | Qué contiene |
| --- | --- |
| Domain | Entidades, documento de nivel y reglas de validación |
| Data | Modelo EF Core, relaciones y ejecutor de migraciones SQL |
| Endpoints | Operaciones de identidad, niveles, interacciones y administración |
| Infrastructure | Tokens/sesiones, errores y validaciones HTTP comunes |
| wwwroot | Documentación básica y probador independiente de la API |

EF Core convierte consultas y cambios del modelo a operaciones de base de datos. Npgsql conecta con PostgreSQL. Las consultas de usuario se parametrizan; los identificadores del autor/jugador salen de la sesión autenticada.

La API valida estructura, tipos, campos desconocidos, tamaño y geometría. No basta con deshabilitar un botón en React: el permiso y la validación definitiva están en el servidor.

## 5. Base de datos y significado de las revisiones

La base local `geometrydash` tiene la migración inicial aplicada: 15 tablas del modelo y `__GeometryMigrations`, 16 tablas en total en la verificación realizada. El registro de migraciones guarda el nombre, fecha y checksum. Los roles de aplicación iniciales son User y Administrator.

Las tablas de Identity conservan usuarios, roles, claims y relaciones. Sessions representa sesiones renovables. Levels conserva la identidad estable de una creación; Revisions contiene sus versiones y el JSON del recorrido. Favorites enlaza usuario con nivel; Ratings enlaza usuario con revisión. Attempts describe ejecuciones; Progress agrega avance por usuario/revisión. Audit conserva acciones de seguridad y administración.

Separar nivel y revisión resuelve un problema importante: editar un nivel no debe cambiar el contenido que otra persona ya está jugando. La publicación sella una revisión. La próxima edición utiliza otro borrador, mientras la publicación anterior permanece disponible.

Los favoritos pertenecen al nivel y sobreviven a nuevas publicaciones. Los votos y completados pertenecen a la revisión: terminar una versión anterior no afirma que ya terminaste el contenido nuevo.

Cada documento JSON contiene esquema, versión física, longitud, velocidad, inicio, meta y lista de objetos. No contiene scripts ejecutables. Se permite guardar un borrador vacío; publicar requiere entre 1 y 2000 objetos y cumplir las zonas libres y demás límites.

## 6. Autenticación y seguridad implementadas

ASP.NET Core Identity administra las contraseñas mediante hashes; no se almacenan ni devuelven como texto. El registro asigna User y no permite solicitar Administrator. El primer administrador se provisiona con una orden de despliegue sobre una cuenta existente.

Login devuelve un access token Bearer de hasta 15 minutos. El frontend lo mantiene en memoria. El refresh viaja en cookie HttpOnly, usa rotación y pertenece a una familia con máximo absoluto de siete días. Reutilizar un refresh consumido revoca su familia. Las solicitudes paralelas del cliente comparten una renovación.

La API comprueba estado de cuenta y sesión en cada solicitud autenticada. Logout y suspensión invalidan nuevas solicitudes aunque el Bearer conserve tiempo de vigencia. Las operaciones de cookie verifican el origen; en producción las cookies son Secure y la sesión actual usa SameSite Strict.

Los borradores solo los gestiona su propietario. Un UUID conocido no concede permiso. Las lecturas privadas filtran por usuario y no exponen correos ni hashes en el catálogo. Los errores incluyen un código funcional y un identificador de traza.

Los límites de peticiones están en memoria por proceso. Para varias réplicas se necesita una política coordinada o límites adicionales en el proxy. No se registran tokens ni contraseñas. `appsettings.Local.json` y `.env` quedan fuera de Git; la configuración local tampoco se incluye en el artefacto de publicación.

## 7. Tres flujos importantes

### Crear y publicar

1. El usuario crea con POST /api/v1/levels y una Idempotency-Key estable.
2. El editor obtiene el borrador y conserva su versión.
3. Guarda mediante PUT /levels/:id/draft con If-Match.
4. Si otra pestaña ya guardó, recibe 412 y conserva su copia local.
5. Publicar guarda primero y envía identificador de borrador, versión del nivel e If-Match.
6. El servidor valida, sella la revisión y cambia la publicación dentro de una transacción.

### Jugar y guardar un resultado

1. El navegador descarga la revisión pública.
2. Si hay sesión, crea un intento indicando expectedRevisionId.
3. El motor ejecuta esa copia sin enviar una petición por frame.
4. Al terminar envía desenlace, ticks, avance y un clientResultId estable.
5. Reenviar el mismo cierre devuelve el resultado existente; cambiarlo produce conflicto.
6. El intento y el agregado de progreso se actualizan juntos.

Los resultados son ClientReported: se contrasta su coherencia, pero no se reproduce una secuencia de entradas para demostrar el recorrido. No deben presentarse como Verified ni como evidencia de una competición.

### Eliminar un nivel

La API marca el nivel como eliminado, borra sus favoritos y abandona intentos activos dentro de la operación transaccional. Las operaciones de favorito y eliminación bloquean la misma fila del nivel para evitar que quede un favorito insertado después de la limpieza. El historial interno puede conservarse sin exponer el contenido eliminado.

## 8. Ejecutarlo en tu computadora

Desde la raíz del repositorio backend:

```powershell
dotnet restore GeometryDash.Backend.sln
dotnet build GeometryDash.Backend.sln
dotnet run --project GeometryDash.Api -- --migrate
dotnet run --project GeometryDash.Api --launch-profile http
```

La conexión debe estar en GeometryDash.Api/appsettings.Local.json o en variables de entorno. En tu computadora ya existe la configuración y la base migrada; no necesitas crear otra. Si Visual Studio mantiene la API ejecutándose, detenla antes de compilar sobre sus mismos archivos.

Desde la raíz del repositorio frontend, en otra terminal:

```powershell
npm ci
npm run dev
```

Abre http://localhost:5173; la API está en http://localhost:5080. El frontend utiliza Node 24+ y el backend SDK .NET 10. El proxy local de Vite hace que el navegador use rutas /api sin conocer el directorio del backend.

## 9. Subir a dos repositorios

Cada proyecto tiene su propia carpeta .git, .gitignore, README y workflow de comprobación. No hay repositorio Git en la carpeta contenedora GeometryDash. Puedes llamar a los remotos geometrydash-backend y geometrydash-frontend, aunque el directorio local del segundo conserve otro nombre.

Crea dos repositorios vacíos en tu cuenta. Desde cada carpeta ejecuta git add, revisa git diff --cached --name-only, crea el commit y configura su propio origin. Los comandos completos están en cada README. No agregues la carpeta contenedora ni mezcles los remotos. No se han realizado commits, pushes ni despliegues durante esta reorganización.

Los archivos node_modules, dist, bin, obj, cachés y secretos no se suben. El package-lock.json sí se sube para que npm ci use las versiones registradas. La conexión de tu computadora no viajará con el repositorio: cada ambiente necesita su configuración.

## 10. Publicar el sistema: estrategia que encaja con este código

Dos repositorios pueden tener despliegues separados y aun así compartir el origen visible del navegador. Para este código, una opción sencilla es servir el frontend estático en https://juego.example.com y enviar /api, /openapi y /health a la API mediante un reverse proxy. Así las rutas relativas y cookies actuales siguen trabajando con un mismo origen.

```text
Internet → HTTPS / dominio público → servidor o gateway
   /                         → archivos del frontend: dist/
   /api/*                    → ASP.NET Core
   /openapi/*, /health/*      → ASP.NET Core
                                  └─ PostgreSQL privado
```

El servidor/gateway puede vivir en una máquina virtual o en un servicio de alojamiento que permita rutas de proxy. Elegir proveedor, plan, dominio y región sigue pendiente; no se ha contratado ni desplegado infraestructura.

Un hosting exclusivamente estático puede servir la interfaz, pero no ejecutar la API ni PostgreSQL. Dos dominios sin proxy requieren adaptar URLs del cliente, cookies y CORS; no funcionan únicamente por subir ambos repositorios.

### Paso 1: comprobar las dos partes

En el frontend ejecuta npm ci, npm test y npm run build. El resultado es dist/. Vite indica que ese directorio es el artefacto estático y que preview es una herramienta de revisión local, no un servidor de producción. [Documentación de despliegue de Vite](https://vite.dev/guide/static-deploy.html).

En el backend ejecuta dotnet restore, dotnet build y las pruebas del dominio. La suite HTTP necesita una base desechable: crea cuentas y contenido. No la ejecutes contra datos reales. Los workflows comprueban build/pruebas, pero no equivalen a pruebas de navegador ni a un despliegue.

### Paso 2: preparar PostgreSQL de producción

Provisiona una base privada con acceso limitado a la API. Utiliza un usuario de aplicación propio; no lleves el superusuario postgres del desarrollo como identidad habitual de producción. Configura el cifrado y la verificación de certificados de la conexión según el proveedor. Define copias de seguridad y comprueba una restauración.

Para migrar necesitas una identidad con permisos de cambio de esquema. Aplica las migraciones una vez por despliegue, después de revisar el SQL y conservar un respaldo si ya hay datos. No edites 001_initial.sql después de aplicarla: agrega un archivo numerado nuevo. Las migraciones de esta entrega son SQL propio, no dotnet ef database update.

### Paso 3: construir y configurar la API

Desde GeometryDash-backend:

```powershell
dotnet publish GeometryDash.Api -c Release -o artifacts/api
```

En el servidor instala el runtime ASP.NET Core 10 si utilizas esta publicación dependiente del framework. Copia el contenido publicado y ejecútalo mediante un administrador de procesos. Microsoft documenta dotnet publish, el proceso persistente y el proxy como piezas del alojamiento de ASP.NET Core. [Guía oficial de alojamiento](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/?view=aspnetcore-10.0).

Configura estos valores como secretos o variables del servidor:

| Variable | Uso |
| --- | --- |
| ASPNETCORE_ENVIRONMENT | Production |
| ConnectionStrings__Database | Conexión privada a PostgreSQL |
| Jwt__Key | Clave aleatoria de al menos 32 bytes, estable y protegida |
| Jwt__Issuer / Jwt__Audience | Identidades esperadas de los tokens; coherentes con la API |
| AllowedOrigins__0 | https://juego.example.com |
| AllowedHosts | juego.example.com y los hosts internos estrictamente necesarios |
| ASPNETCORE_URLS | http://127.0.0.1:5080 en el ejemplo de proxy en la misma máquina |

No introduzcas secretos de producción en Git, el bundle de React ni una variable VITE. La clave JWT no debe regenerarse en cada reinicio. Los orígenes y hosts son valores diferentes: el origen incluye esquema; AllowedHosts utiliza nombres de host.

Desde la carpeta publicada, con la configuración del servidor cargada:

```bash
dotnet GeometryDash.Api.dll --migrate
dotnet GeometryDash.Api.dll
```

El primer comando termina tras migrar. El segundo inicia el servidor. Para mantenerlo activo configura systemd, IIS o el administrador de procesos del proveedor; dejar una terminal abierta no ofrece reinicio automático.

### Paso 4: configurar el proxy y HTTPS

En este proyecto todavía debes configurar los encabezados reenviados para el proxy elegido antes de publicarlo detrás de terminación TLS. ASP.NET necesita conocer el esquema e IP originales para redirecciones, autenticación y límites por IP. Confía solo en proxies conocidos y procesa los encabezados antes de HTTPS y del limitador. [Encabezados reenviados en ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0).

Para un único Nginx en la misma máquina que la API, la adaptación de Program.cs debe usar ForwardedHeadersOptions y permitir el proxy loopback. No uses una opción que confíe en cualquier IP de Internet. Si el proxy está en otra máquina, debes indicar su dirección real. Este ajuste no se implementó en la reorganización: forma parte de preparar el alojamiento escogido.

El siguiente fragmento ilustra las rutas dentro de un servidor Nginx ya configurado con dominio y certificados HTTPS válidos:

```nginx
root /srv/geometrydash/frontend;
index index.html;
client_max_body_size 600k;

location ~ ^/(api|openapi|health)(/|$) {
    proxy_pass http://127.0.0.1:5080;
    proxy_set_header Host $host;
    proxy_set_header X-Forwarded-Proto $scheme;
    proxy_set_header X-Forwarded-For $remote_addr;
}

location / {
    try_files $uri $uri/ /index.html;
}
```

Reemplaza las rutas con las de tu servidor. El fragmento no incluye emisión de certificados, instalación de Nginx ni configuración completa del bloque server. No lo uses como configuración completa para Internet sin esos pasos. El fallback a index.html permite abrir rutas React directamente; el proxy se aplica a la API antes de ese fallback para no convertir errores JSON en páginas HTML.

### Paso 5: publicar el frontend

Copia el contenido de dist/ a la raíz estática configurada. No subas node_modules ni ejecutes npm run dev o preview como servicio público. El proxy de vite.config.ts solo sirve para ejecución local; no se copia como infraestructura de producción.

Prueba abriendo una ruta interna directamente, por ejemplo /levels o /my/levels. Una página 404 al refrescar una ruta React suele indicar que falta el fallback. Un HTML recibido donde esperas JSON suele indicar que /api está llegando al fallback en lugar de al backend.

### Paso 6: verificar y operar

Comprueba /health/live y /health/ready; después prueba registro, login, renovación, creación, guardado, publicación, juego y favoritos desde el dominio HTTPS. Verifica cookies Secure/HttpOnly y que el origen real esté permitido. Una prueba desde localhost no demuestra que la sesión funcione en producción.

Provisiona el primer administrador sobre una cuenta existente mediante GD_ADMIN_EMAIL y --grant-admin, con acceso controlado al servidor. La orden queda auditada. Conserva los logs operativos sin contraseñas/tokens y define cómo reiniciar, actualizar, respaldar y restaurar el servicio.

Si despliegas varias instancias de API, revisa la coordinación de límites y sesiones. Si una actualización cambia el contrato de nivel o las físicas, conserva compatibilidad con revisiones anteriores: no cambies las reglas históricas sin una versión explícita.

## 11. Qué está verificado y qué falta

- Migración local aplicada en geometrydash: 16 tablas, registro/checksum y roles verificados; repetir la migración no duplicó objetos.
- Backend reorganizado: compila con rutas nuevas, con 29 pruebas del dominio aprobadas. Se usó una salida aislada porque una API abierta bloqueaba su ejecutable normal.
- Frontend: comprobación completa de TypeScript y build de Vite aprobados, con 25 pruebas del motor/editor aprobadas.
- Repositorios Git locales inicializados; secretos y artefactos excluidos. No se hicieron commits ni pushes.
- Pendiente: suite HTTP completa sobre base desechable, pruebas de navegador, carga y validación de la configuración del alojamiento.
- Antes de abrir registro público: implementar recuperación de acceso/correo, concretar conservación de datos, revisar abuso y moderación, probar respaldos y configurar correctamente el proxy/HTTPS. La documentación original los exige como preparación operativa.

La aplicación está organizada para evolucionar y subirse a dos repositorios. Eso no significa que ya esté desplegada ni que todos los controles necesarios para operación pública estén terminados.

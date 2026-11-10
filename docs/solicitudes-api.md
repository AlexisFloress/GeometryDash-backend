# Cómo enviar solicitudes a la API

Base pública: `https://geometrydash-api.onrender.com`. Añade a esa dirección la ruta del endpoint. Esta referencia se genera a partir de OpenAPI, igual que la documentación web.

Los JSON son ejemplos: cambia correos, contraseñas e identificadores por tus datos. Los UUID de ejemplo no apuntan a recursos existentes. Envía solamente los campos indicados; los campos desconocidos se rechazan.

Para usar tu cuenta, registra un usuario e inicia sesión. Copia el `accessToken` de login en `Authorization: Bearer TU_ACCESS_TOKEN`. Conserva las cookies para renovar o cerrar sesión. Login, refresh y logout requieren un `Origin` permitido por la configuración del servidor; los navegadores lo envían automáticamente. Los clientes externos deben enviarlo expresamente.

No envíes `{}` en los endpoints sin cuerpo. Los filtros de los listados se envían en la URL; por ejemplo: `/api/v1/levels?page=1&pageSize=20&difficulty=Normal&sort=recent`. Los parámetros de consulta son opcionales.

## GET /api/v1/me

Muestra los datos de tu propia cuenta. Necesitas haber iniciado sesión.

Requiere iniciar sesión.

Encabezados:

```http
Authorization: Bearer TU_ACCESS_TOKEN
```


No lleva cuerpo JSON. Deja vacío el campo de datos.

Esta operación no recibe un cuerpo JSON. Deja vacío el campo de datos; no envíes {}.

## PATCH /api/v1/me

Cambia el nombre que muestras en tu perfil. Necesitas haber iniciado sesión.

Requiere iniciar sesión.

Encabezados:

```http
Authorization: Bearer TU_ACCESS_TOKEN
Content-Type: application/json
```


JSON de la solicitud:

```json
{
  "displayName": "Mi nuevo nombre"
}
```

- `displayName`: Obligatorio. Nuevo nombre visible de 1 a 60 caracteres. Este endpoint no cambia el correo, usuario ni contraseña.

## GET /docs/backend

Abre la documentación pública del backend, donde cualquier persona puede leer qué hace cada operación y usar el probador al final de la página.

No requiere una clave de acceso.


No lleva cuerpo JSON. Deja vacío el campo de datos.

Esta operación no recibe un cuerpo JSON. Deja vacío el campo de datos; no envíes {}.

## POST /api/v1/auth/register

Crea una cuenta para guardar tus niveles, partidas y favoritos. Después debes iniciar sesión.

No requiere una clave de acceso.

Encabezados:

```http
Content-Type: application/json
```


JSON de la solicitud:

```json
{
  "userName": "jugador_demo",
  "displayName": "Jugador Demo",
  "email": "jugador@example.com",
  "password": "MiClaveSegura123"
}
```

- `userName`: Obligatorio. Nombre de usuario de 3 a 30 caracteres; debe estar disponible.
- `displayName`: Obligatorio. Nombre visible de 1 a 60 caracteres.
- `email`: Obligatorio. Correo válido de hasta 254 caracteres; no puede estar registrado.
- `password`: Obligatorio. De 10 a 128 caracteres, con mayúscula, minúscula y número. Usa una contraseña propia, distinta de este ejemplo.

Crear la cuenta no inicia sesión. Después utiliza el endpoint de login.

## POST /api/v1/auth/login

Inicia sesión con tu correo y contraseña para acceder a tu cuenta.

No requiere una clave de acceso.

Encabezados:

```http
Content-Type: application/json
Origin: https://TU-DOMINIO-PERMITIDO
```


JSON de la solicitud:

```json
{
  "email": "jugador@example.com",
  "password": "MiClaveSegura123"
}
```

- `email`: Obligatorio. Correo de una cuenta registrada y activa.
- `password`: Obligatorio. Contraseña de esa cuenta.

La respuesta incluye accessToken. Úsalo en Authorization: Bearer TU_ACCESS_TOKEN para las operaciones privadas. La cookie de renovación se recibe automáticamente.

Origin debe coincidir con un dominio permitido por el servidor. El navegador lo envía automáticamente. En clientes externos debes enviarlo tú.

## POST /api/v1/auth/refresh

Renueva tu sesión para seguir usando tu cuenta sin volver a escribir la contraseña, mientras la sesión siga siendo válida.

No requiere una clave de acceso.

Encabezados:

```http
Origin: https://TU-DOMINIO-PERMITIDO
```


No lleva cuerpo JSON. Deja vacío el campo de datos.

Esta operación no recibe un cuerpo JSON. Deja vacío el campo de datos; no envíes {}.

Origin debe coincidir con un dominio permitido por el servidor. El navegador lo envía automáticamente. En clientes externos debes enviarlo tú.

Envía la cookie gd_refresh recibida al iniciar sesión. En el navegador se envía automáticamente; otros clientes deben conservar las cookies entre solicitudes.

## POST /api/v1/auth/logout

Cierra la sesión que estás usando en este navegador.

No requiere una clave de acceso.

Encabezados:

```http
Origin: https://TU-DOMINIO-PERMITIDO
```


No lleva cuerpo JSON. Deja vacío el campo de datos.

Esta operación no recibe un cuerpo JSON. Deja vacío el campo de datos; no envíes {}.

Origin debe coincidir con un dominio permitido por el servidor. El navegador lo envía automáticamente. En clientes externos debes enviarlo tú.

Envía la cookie gd_refresh recibida al iniciar sesión. En el navegador se envía automáticamente; otros clientes deben conservar las cookies entre solicitudes.

## GET /api/v1/levels

Muestra los niveles publicados. Puedes buscar por título, filtrar por dificultad, autor o velocidad y elegir el orden de la lista, sin iniciar sesión.

No requiere una clave de acceso.

- `page` (consulta, opcional): Página a consultar: de 1 a 1000000. Si se omite, 1.
- `pageSize` (consulta, opcional): Elementos por página: de 1 a 100. Si se omite, 20.
- `search` (consulta, opcional): Parte del título del nivel, hasta 80 caracteres.
- `difficulty` (consulta, opcional): Easy, Normal o Hard.
- `authorId` (consulta, opcional): UUID del creador para mostrar solo sus niveles.
- `sort` (consulta, opcional): recent, rating o completed. Si se omite, recent.
- `speed` (consulta, opcional): Velocidad: 0.75, 1, 1.25 o 1.5.

No lleva cuerpo JSON. Deja vacío el campo de datos.

Esta operación no recibe un cuerpo JSON. Deja vacío el campo de datos; no envíes {}.

## POST /api/v1/levels

Crea un nivel nuevo con un borrador privado para empezar a editarlo. Necesitas iniciar sesión; todavía no será visible para otras personas.

Requiere iniciar sesión.

Encabezados:

```http
Authorization: Bearer TU_ACCESS_TOKEN
Content-Type: application/json
Idempotency-Key: 33333333-3333-4333-8333-333333333333
```


JSON de la solicitud:

```json
{
  "title": "Mi primer recorrido",
  "description": "Un nivel para practicar saltos."
}
```

- `title`: Obligatorio. Título de 3 a 80 caracteres.
- `description`: Opcional en esta operación. Hasta 2000 caracteres; si se omite, queda vacía.

Conserva la misma Idempotency-Key y el mismo JSON si reintentas la creación. Usa una clave nueva para cada nivel nuevo.

La respuesta incluye levelId, revisionId, levelVersion y version. Guarda esos valores para editar y publicar el nivel.

## GET /api/v1/levels/{id}

Muestra la ficha de un nivel publicado: título, descripción, autor, dificultad y valoraciones. No necesitas iniciar sesión.

No requiere una clave de acceso.

- `id` (ruta, obligatorio): levelId del nivel; puedes obtenerlo del catálogo o de tus niveles.

No lleva cuerpo JSON. Deja vacío el campo de datos.

Esta operación no recibe un cuerpo JSON. Deja vacío el campo de datos; no envíes {}.

## DELETE /api/v1/levels/{id}

Elimina uno de tus niveles para que deje de estar disponible. También lo quita de los favoritos y da por abandonadas las partidas que seguían abiertas.

Requiere iniciar sesión.

Encabezados:

```http
Authorization: Bearer TU_ACCESS_TOKEN
```

- `id` (ruta, obligatorio): levelId del nivel; puedes obtenerlo del catálogo o de tus niveles.

No lleva cuerpo JSON. Deja vacío el campo de datos.

Esta operación no recibe un cuerpo JSON. Deja vacío el campo de datos; no envíes {}.

## GET /api/v1/levels/{id}/content

Entrega el recorrido de la versión publicada de un nivel, con sus obstáculos y posiciones, para poder jugarlo. No necesitas iniciar sesión.

No requiere una clave de acceso.

- `id` (ruta, obligatorio): levelId del nivel; puedes obtenerlo del catálogo o de tus niveles.

No lleva cuerpo JSON. Deja vacío el campo de datos.

Esta operación no recibe un cuerpo JSON. Deja vacío el campo de datos; no envíes {}.

## GET /api/v1/levels/{id}/draft

Recupera el borrador de un nivel para continuar editándolo. Solo su creador puede consultarlo.

Requiere iniciar sesión.

Encabezados:

```http
Authorization: Bearer TU_ACCESS_TOKEN
```

- `id` (ruta, obligatorio): levelId del nivel; puedes obtenerlo del catálogo o de tus niveles.

No lleva cuerpo JSON. Deja vacío el campo de datos.

Esta operación no recibe un cuerpo JSON. Deja vacío el campo de datos; no envíes {}.

## POST /api/v1/levels/{id}/draft

Prepara un borrador a partir de la versión publicada de tu nivel. Si ya tienes uno, lo recupera; la versión pública sigue disponible mientras editas.

Requiere iniciar sesión.

Encabezados:

```http
Authorization: Bearer TU_ACCESS_TOKEN
```

- `id` (ruta, obligatorio): levelId del nivel; puedes obtenerlo del catálogo o de tus niveles.

No lleva cuerpo JSON. Deja vacío el campo de datos.

Esta operación no recibe un cuerpo JSON. Deja vacío el campo de datos; no envíes {}.

## PUT /api/v1/levels/{id}/draft

Guarda los cambios del título, descripción, dificultad y recorrido de tu borrador. Comprueba la versión para evitar que sobrescribas cambios más recientes.

Requiere iniciar sesión.

Encabezados:

```http
Authorization: Bearer TU_ACCESS_TOKEN
Content-Type: application/json
If-Match: "1"
```

- `id` (ruta, obligatorio): levelId del nivel; puedes obtenerlo del catálogo o de tus niveles.

JSON de la solicitud:

```json
{
  "title": "Mi primer recorrido",
  "description": "Un nivel para practicar saltos.",
  "difficulty": "Normal",
  "content": {
    "schemaVersion": 1,
    "physicsVersion": 1,
    "length": 300,
    "speedMultiplier": 1,
    "spawn": {
      "x": 2,
      "y": 0
    },
    "finishX": 298,
    "objects": [
      {
        "id": "obj_01",
        "type": "triangle",
        "x": 20,
        "y": 0,
        "width": 1,
        "height": 1,
        "rotation": 0
      }
    ]
  }
}
```

- `title`: Obligatorio. Título de 3 a 80 caracteres.
- `description`: Obligatorio. Hasta 2000 caracteres; puede ser una cadena vacía.
- `difficulty`: Obligatorio. Uno de estos valores exactos: Easy, Normal o Hard.
- `content`: Obligatorio. Recorrido completo que quieres guardar. Se reemplaza el contenido anterior del borrador.
- `content.schemaVersion / content.physicsVersion`: Obligatorios. Ambos deben valer 1.
- `content.length`: Obligatorio. Número entero entre 100 y 5000.
- `content.speedMultiplier`: Obligatorio. Solo 0.75, 1, 1.25 o 1.5.
- `content.spawn`: Obligatorio. Punto inicial con x entre 0 y 10, en pasos de 0.5, e y igual a 0.
- `content.finishX`: Obligatorio. Meta en pasos de 0.5, al menos 10 unidades después del inicio y dentro de la longitud del nivel.
- `content.objects`: Obligatorio. Lista de hasta 2000 objetos; puede estar vacía al guardar, pero publicar requiere al menos uno.
- `content.objects[].id / type`: Obligatorios. Identificador único dentro del recorrido, de hasta 64 caracteres. Tipo: cube, triangle o platform.
- `content.objects[].x / y`: Obligatorios. Posiciones no negativas en pasos de 0.5. Reserva 4 unidades tras el inicio y 2 antes de la meta. El objeto debe quedar dentro del recorrido y no superar la altura 30.
- `content.objects[].width / height / rotation`: Obligatorios. cube y triangle miden 1 × 1. platform tiene ancho de 1 a 10, en pasos de 0.5, y altura 0.5. La rotación siempre es 0.

Usa el ETag de la lectura o guardado más reciente en If-Match, incluidas las comillas. El valor de ejemplo no reemplaza la versión real de tu borrador.

No se permiten objetos superpuestos, salvo entre dos triángulos. El recorrido no puede superar 512 KiB.

## POST /api/v1/levels/{id}/publish

Publica el borrador de tu nivel para que otras personas puedan encontrarlo y jugarlo. Antes comprueba que el recorrido cumpla las reglas del juego.

Requiere iniciar sesión.

Encabezados:

```http
Authorization: Bearer TU_ACCESS_TOKEN
Content-Type: application/json
If-Match: "1"
```

- `id` (ruta, obligatorio): levelId del nivel; puedes obtenerlo del catálogo o de tus niveles.

JSON de la solicitud:

```json
{
  "draftRevisionId": "11111111-1111-4111-8111-111111111111",
  "expectedLevelVersion": 1
}
```

- `draftRevisionId`: Obligatorio. Copia el revisionId del borrador guardado que quieres publicar.
- `expectedLevelVersion`: Obligatorio. Copia levelVersion del borrador; no es el mismo dato que version ni el número de revisión.

Primero guarda el recorrido. Envía el ETag del último guardado en If-Match. Si el nivel cambió, consulta el borrador otra vez antes de publicar.

Los identificadores y versiones del ejemplo son ilustrativos: reemplázalos por los que devolvió tu nivel.

## GET /api/v1/me/levels

Muestra tus niveles, incluidos los borradores y los publicados, para que puedas administrarlos. Necesitas haber iniciado sesión.

Requiere iniciar sesión.

Encabezados:

```http
Authorization: Bearer TU_ACCESS_TOKEN
```

- `page` (consulta, opcional): Página a consultar: de 1 a 1000000. Si se omite, 1.
- `pageSize` (consulta, opcional): Elementos por página: de 1 a 100. Si se omite, 20.

No lleva cuerpo JSON. Deja vacío el campo de datos.

Esta operación no recibe un cuerpo JSON. Deja vacío el campo de datos; no envíes {}.

## PUT /api/v1/levels/{id}/favorite

Añade un nivel publicado a tus favoritos para encontrarlo después. Necesitas haber iniciado sesión.

Requiere iniciar sesión.

Encabezados:

```http
Authorization: Bearer TU_ACCESS_TOKEN
```

- `id` (ruta, obligatorio): levelId del nivel; puedes obtenerlo del catálogo o de tus niveles.

No lleva cuerpo JSON. Deja vacío el campo de datos.

Esta operación no recibe un cuerpo JSON. Deja vacío el campo de datos; no envíes {}.

## DELETE /api/v1/levels/{id}/favorite

Quita un nivel de tus favoritos sin eliminar el nivel. Necesitas haber iniciado sesión.

Requiere iniciar sesión.

Encabezados:

```http
Authorization: Bearer TU_ACCESS_TOKEN
```

- `id` (ruta, obligatorio): levelId del nivel; puedes obtenerlo del catálogo o de tus niveles.

No lleva cuerpo JSON. Deja vacío el campo de datos.

Esta operación no recibe un cuerpo JSON. Deja vacío el campo de datos; no envíes {}.

## GET /api/v1/me/favorites

Muestra los niveles que guardaste como favoritos y que siguen publicados. Necesitas haber iniciado sesión.

Requiere iniciar sesión.

Encabezados:

```http
Authorization: Bearer TU_ACCESS_TOKEN
```

- `page` (consulta, opcional): Página a consultar: de 1 a 1000000. Si se omite, 1.
- `pageSize` (consulta, opcional): Elementos por página: de 1 a 100. Si se omite, 20.

No lleva cuerpo JSON. Deja vacío el campo de datos.

Esta operación no recibe un cuerpo JSON. Deja vacío el campo de datos; no envíes {}.

## PUT /api/v1/revisions/{id}/rating

Guarda o cambia tu valoración de una versión de un nivel, de 1 a 5 estrellas. Debes iniciar sesión y haber terminado una partida en esa versión; no puedes valorar tus propios niveles.

Requiere iniciar sesión.

Encabezados:

```http
Authorization: Bearer TU_ACCESS_TOKEN
Content-Type: application/json
```

- `id` (ruta, obligatorio): revisionId de la versión del nivel que quieres valorar.

JSON de la solicitud:

```json
{
  "score": 5
}
```

- `score`: Obligatorio. Número entero de 1 a 5 estrellas.

El id de la ruta es el revisionId de la versión que jugaste, no el levelId. Debes haber terminado una partida en esa versión y no puedes ser su creador. No es obligatorio haberla completado al 100%.

## DELETE /api/v1/revisions/{id}/rating

Borra la valoración que diste a una versión de un nivel. Necesitas haber iniciado sesión.

Requiere iniciar sesión.

Encabezados:

```http
Authorization: Bearer TU_ACCESS_TOKEN
```

- `id` (ruta, obligatorio): revisionId de la versión del nivel que quieres valorar.

No lleva cuerpo JSON. Deja vacío el campo de datos.

Esta operación no recibe un cuerpo JSON. Deja vacío el campo de datos; no envíes {}.

## POST /api/v1/levels/{id}/attempts

Registra el inicio de una partida en la versión publicada de un nivel para luego guardar su resultado. Necesitas haber iniciado sesión.

Requiere iniciar sesión.

Encabezados:

```http
Authorization: Bearer TU_ACCESS_TOKEN
Content-Type: application/json
```

- `id` (ruta, obligatorio): levelId del nivel; puedes obtenerlo del catálogo o de tus niveles.

JSON de la solicitud:

```json
{
  "expectedRevisionId": "11111111-1111-4111-8111-111111111111"
}
```

- `expectedRevisionId`: Obligatorio. Copia revisionId de GET /api/v1/levels/{id}/content. Debe corresponder a la publicación actual.

El id de la ruta es el levelId. La respuesta devuelve el id de la partida; úsalo para enviar el resultado. Máximo cinco partidas activas por cuenta; cada una vence a los 30 minutos.

## PUT /api/v1/attempts/{id}/result

Guarda cómo terminó tu partida y cuánto avanzaste. Reenviar el mismo resultado no lo cuenta dos veces. El resultado lo informa el juego y no se comprueba repitiendo la partida.

Requiere iniciar sesión.

Encabezados:

```http
Authorization: Bearer TU_ACCESS_TOKEN
Content-Type: application/json
```

- `id` (ruta, obligatorio): id de la partida que devolvió su creación.

JSON de la solicitud:

```json
{
  "clientResultId": "22222222-2222-4222-8222-222222222222",
  "outcome": "Quit",
  "elapsedTicks": 0,
  "pauseTicks": 0,
  "progressPercent": 0
}
```

- `clientResultId`: Obligatorio. UUID nuevo para este resultado. Consérvalo al reintentar el mismo envío.
- `outcome`: Obligatorio. Completed si llegaste a la meta, Dead si perdiste o Quit si abandonaste.
- `elapsedTicks`: Obligatorio. Entero entre 0 y 216000. Tiempo jugado sin pausas, en pasos de 1/120 de segundo.
- `pauseTicks`: Obligatorio. Entero entre 0 y 216000. Tiempo en pausa, en pasos de 1/120 de segundo.
- `progressPercent`: Obligatorio. Avance de 0 a 100, coherente con el tiempo jugado y la velocidad del recorrido. Solo Completed permite 100.

El ejemplo representa abandonar al inicio. Para una partida real usa los datos medidos por el juego; no inventes tiempos ni avances.

El id de la ruta es el de tu partida, obtenido al iniciarla. Reenviar el mismo clientResultId y los mismos datos no duplica el resultado; cambiar un resultado ya guardado produce un conflicto.

## GET /api/v1/attempts/{id}

Muestra el estado y, si ya terminó, el resultado de una de tus partidas. Solo puedes consultar tus propias partidas.

Requiere iniciar sesión.

Encabezados:

```http
Authorization: Bearer TU_ACCESS_TOKEN
```

- `id` (ruta, obligatorio): id de la partida que devolvió su creación.

No lleva cuerpo JSON. Deja vacío el campo de datos.

Esta operación no recibe un cuerpo JSON. Deja vacío el campo de datos; no envíes {}.

## GET /api/v1/me/attempts

Muestra tu historial de partidas, empezando por las más recientes. Necesitas haber iniciado sesión.

Requiere iniciar sesión.

Encabezados:

```http
Authorization: Bearer TU_ACCESS_TOKEN
```

- `page` (consulta, opcional): Página a consultar: de 1 a 1000000. Si se omite, 1.
- `pageSize` (consulta, opcional): Elementos por página: de 1 a 100. Si se omite, 20.

No lleva cuerpo JSON. Deja vacío el campo de datos.

Esta operación no recibe un cuerpo JSON. Deja vacío el campo de datos; no envíes {}.

## GET /api/v1/me/completed

Muestra los niveles que has completado y las versiones que terminaste. También indica si el nivel sigue disponible y si ya completaste su versión actual. Necesitas haber iniciado sesión.

Requiere iniciar sesión.

Encabezados:

```http
Authorization: Bearer TU_ACCESS_TOKEN
```

- `page` (consulta, opcional): Página a consultar: de 1 a 1000000. Si se omite, 1.
- `pageSize` (consulta, opcional): Elementos por página: de 1 a 100. Si se omite, 20.

No lleva cuerpo JSON. Deja vacío el campo de datos.

Esta operación no recibe un cuerpo JSON. Deja vacío el campo de datos; no envíes {}.

## POST /api/v1/admin/levels/{id}/withdraw

Oculta un nivel publicado para que deje de estar disponible al público. Solo un administrador puede hacerlo y debe indicar el motivo.

Solo administradores.

Encabezados:

```http
Authorization: Bearer TU_ACCESS_TOKEN
Content-Type: application/json
```

- `id` (ruta, obligatorio): levelId del nivel; puedes obtenerlo del catálogo o de tus niveles.

JSON de la solicitud:

```json
{
  "reason": "El nivel contiene contenido que incumple las reglas."
}
```

- `reason`: Obligatorio. Motivo de 1 a 1000 caracteres. Queda registrado en el historial de administración.

## POST /api/v1/admin/levels/{id}/restore

Vuelve a mostrar un nivel retirado por administración. Solo un administrador puede hacerlo y debe indicar el motivo.

Solo administradores.

Encabezados:

```http
Authorization: Bearer TU_ACCESS_TOKEN
Content-Type: application/json
```

- `id` (ruta, obligatorio): levelId del nivel; puedes obtenerlo del catálogo o de tus niveles.

JSON de la solicitud:

```json
{
  "reason": "Se revisó el contenido y el nivel puede volver a publicarse."
}
```

- `reason`: Obligatorio. Motivo de 1 a 1000 caracteres. Queda registrado en el historial de administración.

Solo permite restaurar un nivel retirado por administración que conserve una versión publicada; no recupera niveles eliminados.

## POST /api/v1/admin/users/{id}/suspend

Suspende el acceso de una cuenta y cierra sus sesiones. Solo un administrador puede hacerlo y debe indicar el motivo.

Solo administradores.

Encabezados:

```http
Authorization: Bearer TU_ACCESS_TOKEN
Content-Type: application/json
```

- `id` (ruta, obligatorio): UUID de la cuenta que vas a moderar.

JSON de la solicitud:

```json
{
  "reason": "La cuenta incumplió las reglas de la comunidad."
}
```

- `reason`: Obligatorio. Motivo de 1 a 1000 caracteres. Queda registrado en el historial de administración.

No puedes suspender tu propia cuenta.

## POST /api/v1/admin/users/{id}/activate

Habilita de nuevo una cuenta suspendida; la persona deberá iniciar sesión otra vez. Solo un administrador puede hacerlo y debe indicar el motivo.

Solo administradores.

Encabezados:

```http
Authorization: Bearer TU_ACCESS_TOKEN
Content-Type: application/json
```

- `id` (ruta, obligatorio): UUID de la cuenta que vas a moderar.

JSON de la solicitud:

```json
{
  "reason": "Se revisó la suspensión y se permite el acceso nuevamente."
}
```

- `reason`: Obligatorio. Motivo de 1 a 1000 caracteres. Queda registrado en el historial de administración.

La persona tendrá que iniciar sesión otra vez. No puedes moderar tu propia cuenta ni reactivar una cuenta eliminada.

## GET /api/v1/admin/audit

Muestra el historial de acciones de seguridad y administración para revisar qué ocurrió y cuándo. Solo está disponible para administradores.

Solo administradores.

Encabezados:

```http
Authorization: Bearer TU_ACCESS_TOKEN
```

- `page` (consulta, opcional): Página a consultar: de 1 a 1000000. Si se omite, 1.
- `pageSize` (consulta, opcional): Elementos por página: de 1 a 100. Si se omite, 20.

No lleva cuerpo JSON. Deja vacío el campo de datos.

Esta operación no recibe un cuerpo JSON. Deja vacío el campo de datos; no envíes {}.


# Qué hace cada endpoint

Un endpoint es una dirección de la API que permite consultar información o realizar una acción. Aquí se explica cada uno con palabras sencillas.

Para integrar la API en tu aplicación, consulta [cómo enviar solicitudes](solicitudes-api.md): incluye JSON completos, campos obligatorios, encabezados, filtros y notas para cada operación. Los mismos ejemplos aparecen en la documentación web y se cargan al seleccionar un endpoint en el probador.

La documentación web y el probador son públicos: cualquier persona puede abrir `/docs/backend` sin una cuenta. En el backend publicado, la dirección es https://geometrydash-api.onrender.com/docs/backend. El probador aparece al final de la página y envía las solicitudes al servicio donde se abrió. Consultar niveles publicados no requiere sesión; guardar cambios o consultar datos privados sí requiere la cuenta y los permisos correspondientes.

En las rutas, `{id}` se reemplaza por el identificador del nivel, la versión, la partida o la cuenta correspondiente. Una versión es una publicación concreta de un nivel: cuando el creador lo modifica y vuelve a publicarlo, aparece una versión nueva.

Los listados se pueden consultar por páginas. Las operaciones sobre tu cuenta, tus creaciones y tus partidas requieren iniciar sesión. Los borradores son privados y solo los puede consultar o modificar su creador.

## Cuenta y acceso

| Método | Ruta | Para qué sirve |
| --- | --- | --- |
| POST | `/api/v1/auth/register` | Crea una cuenta para guardar tus niveles, partidas y favoritos. Después debes iniciar sesión. |
| POST | `/api/v1/auth/login` | Inicia sesión con tu correo y contraseña para acceder a tu cuenta. |
| POST | `/api/v1/auth/refresh` | Renueva tu sesión para seguir usando tu cuenta sin volver a escribir la contraseña, mientras la sesión siga siendo válida. |
| POST | `/api/v1/auth/logout` | Cierra la sesión que estás usando en este navegador. |
| GET | `/api/v1/me` | Muestra los datos de tu propia cuenta. Necesitas haber iniciado sesión. |
| PATCH | `/api/v1/me` | Cambia el nombre que muestras en tu perfil. Necesitas haber iniciado sesión. |

## Niveles y borradores

| Método | Ruta | Para qué sirve |
| --- | --- | --- |
| GET | `/api/v1/levels` | Muestra los niveles publicados. Puedes buscar por título, filtrar por dificultad, autor o velocidad y elegir el orden de la lista, sin iniciar sesión. |
| GET | `/api/v1/levels/{id}` | Muestra la ficha de un nivel publicado: título, descripción, autor, dificultad y valoraciones. No necesitas iniciar sesión. |
| GET | `/api/v1/levels/{id}/content` | Entrega el recorrido de la versión publicada de un nivel, con sus obstáculos y posiciones, para poder jugarlo. No necesitas iniciar sesión. |
| POST | `/api/v1/levels` | Crea un nivel nuevo con un borrador privado para empezar a editarlo. Necesitas iniciar sesión; todavía no será visible para otras personas. |
| GET | `/api/v1/levels/{id}/draft` | Recupera el borrador de un nivel para continuar editándolo. Solo su creador puede consultarlo. |
| POST | `/api/v1/levels/{id}/draft` | Prepara un borrador a partir de la versión publicada de tu nivel. Si ya tienes uno, lo recupera; la versión pública sigue disponible mientras editas. |
| PUT | `/api/v1/levels/{id}/draft` | Guarda los cambios del título, descripción, dificultad y recorrido de tu borrador. Comprueba la versión para evitar que sobrescribas cambios más recientes. |
| POST | `/api/v1/levels/{id}/publish` | Publica el borrador de tu nivel para que otras personas puedan encontrarlo y jugarlo. Antes comprueba que el recorrido cumpla las reglas del juego. |
| DELETE | `/api/v1/levels/{id}` | Elimina uno de tus niveles para que deje de estar disponible. También lo quita de los favoritos y da por abandonadas las partidas que seguían abiertas. |
| GET | `/api/v1/me/levels` | Muestra tus niveles, incluidos los borradores y los publicados, para que puedas administrarlos. Necesitas haber iniciado sesión. |

## Favoritos, valoraciones y partidas

| Método | Ruta | Para qué sirve |
| --- | --- | --- |
| PUT | `/api/v1/levels/{id}/favorite` | Añade un nivel publicado a tus favoritos para encontrarlo después. Necesitas haber iniciado sesión. |
| DELETE | `/api/v1/levels/{id}/favorite` | Quita un nivel de tus favoritos sin eliminar el nivel. Necesitas haber iniciado sesión. |
| GET | `/api/v1/me/favorites` | Muestra los niveles que guardaste como favoritos y que siguen publicados. Necesitas haber iniciado sesión. |
| PUT | `/api/v1/revisions/{id}/rating` | Guarda o cambia tu valoración de una versión de un nivel, de 1 a 5 estrellas. Debes iniciar sesión y haber terminado una partida en esa versión; no puedes valorar tus propios niveles. |
| DELETE | `/api/v1/revisions/{id}/rating` | Borra la valoración que diste a una versión de un nivel. Necesitas haber iniciado sesión. |
| POST | `/api/v1/levels/{id}/attempts` | Registra el inicio de una partida en la versión publicada de un nivel para luego guardar su resultado. Necesitas haber iniciado sesión. |
| PUT | `/api/v1/attempts/{id}/result` | Guarda cómo terminó tu partida y cuánto avanzaste. Reenviar el mismo resultado no lo cuenta dos veces. El resultado lo informa el juego y no se comprueba repitiendo la partida. |
| GET | `/api/v1/attempts/{id}` | Muestra el estado y, si ya terminó, el resultado de una de tus partidas. Solo puedes consultar tus propias partidas. |
| GET | `/api/v1/me/attempts` | Muestra tu historial de partidas, empezando por las más recientes. Necesitas haber iniciado sesión. |
| GET | `/api/v1/me/completed` | Muestra los niveles que has completado y las versiones que terminaste. También indica si el nivel sigue disponible y si ya completaste su versión actual. Necesitas haber iniciado sesión. |

## Administración

Todas estas operaciones requieren una cuenta de administrador. Para retirar o restaurar niveles y suspender o reactivar cuentas hay que indicar el motivo.

| Método | Ruta | Para qué sirve |
| --- | --- | --- |
| POST | `/api/v1/admin/levels/{id}/withdraw` | Oculta un nivel publicado para que deje de estar disponible al público. Solo un administrador puede hacerlo y debe indicar el motivo. |
| POST | `/api/v1/admin/levels/{id}/restore` | Vuelve a mostrar un nivel retirado por administración. Solo un administrador puede hacerlo y debe indicar el motivo. |
| POST | `/api/v1/admin/users/{id}/suspend` | Suspende el acceso de una cuenta y cierra sus sesiones. Solo un administrador puede hacerlo y debe indicar el motivo. |
| POST | `/api/v1/admin/users/{id}/activate` | Habilita de nuevo una cuenta suspendida; la persona deberá iniciar sesión otra vez. Solo un administrador puede hacerlo y debe indicar el motivo. |
| GET | `/api/v1/admin/audit` | Muestra el historial de acciones de seguridad y administración para revisar qué ocurrió y cuándo. Solo está disponible para administradores. |

## Documentación y estado del servicio

Estas rutas se pueden abrir sin iniciar sesión.

| Método | Ruta | Para qué sirve |
| --- | --- | --- |
| GET | `/docs/backend` | Abre la documentación del backend, con las explicaciones de las operaciones y un formulario para probarlas. |
| GET | `/openapi/v1.json` | Entrega la lista de operaciones y los datos que utiliza cada una, para que otras herramientas puedan trabajar con la API. |
| GET | `/health/live` | Comprueba que el servidor está encendido y responde. No comprueba la conexión con la base de datos. |
| GET | `/health/ready` | Comprueba que el servidor puede conectarse a la base de datos y que existen los roles de acceso. Si algo falla, indica que el servicio todavía no está listo. |

# Identity dev por ngrok y Gateway

Importar `Identity-dev.postman_collection.json` en Postman Desktop, en un espacio local sin sincronización de datos sensibles. Contiene 28 solicitudes que cubren los 18 endpoints Identity y salud. La cobertura del artefacto no significa que se haya ejecutado ni que el cierre E2E esté verificado.

## Ejecución

1. Comprobar que PostgreSQL dev tiene las migraciones Identity aplicadas y API/Gateway arrancados. El túnel debe apuntar al Gateway, nunca directamente a la API.
2. Consultar localmente `http://127.0.0.1:4040/api/tunnels` y copiar `public_url` HTTPS del túnel actual a `baseUrl`, sin barra final. No versionar el dominio temporal. Ngrok debe tener la inspección desactivada (`--inspect=false`).
3. Configurar `email` de prueba y `password` fuerte solo en valores locales no compartidos. No reutilizar contraseñas reales. El registro crea un tenant/propietario persistentes; los datos fiscales y teléfono incluidos son sintéticos y pueden editarse antes del primer envío. La dirección fiscal se omite intencionadamente porque es opcional.
4. Ejecutar manualmente por bloques, no lanzar toda la colección sin preparar los correos. Los scripts mantienen sesiones, ETags e identificadores en `pm.variables`, temporales al runner. Para continuar entre ejecuciones hay que volver a iniciar sesión o transferir los valores a almacenamiento privado local de Postman, nunca a valores compartidos, al repositorio ni al chat. No guardar/exportar cuerpos ni sincronizar resultados; evitar consola y grabaciones. El artefacto no proporciona una pausa interactiva dentro de un runner: si falta un token, lo detiene y hay que reanudar manualmente desde la solicitud pertinente.
5. Alta: registro 202 sin tokens; login pendiente 401; reenvío 202 (cooldown de un minuto); leer verificación en el buzón y copiar `verificationToken` únicamente al valor privado local. Confirmar 200 y después login 200. No repetir el alta para recuperar un token perdido: solicitar reenvío con clave nueva después del cooldown.
6. Perfiles: capturar ETags con las lecturas antes de cada mutación. La solicitud sin If-Match espera 428; las válidas esperan 204. Releer tenant entre actualizar perfil y ajustes. Recuperación: forgot 202; introducir `resetToken` del buzón y `newPassword` local; reset 200, JWT anterior 401 y nuevo login 200. Refresh rota el token; logout 204 revoca refresh, no acredita revocación inmediata del JWT por logout.
7. Invitaciones: configurar un `inviteeEmail` distinto y expresamente autorizado por su titular. No usar direcciones de terceros sin permiso. Invitar como Viewer (4), leer el token en ese buzón e introducir `invitationToken`/`inviteePassword` localmente. Aceptar 200; el propietario mantiene su sesión. Listar usuarios para obtener ETag de la membresía; cambiar a Commercial (3), releer y desactivar solo ese invitado de prueba. El JWT del invitado debe ser rechazado. No ejecutar estas mutaciones contra el Owner.

Las claves se generan temporalmente como `key:<nombre de solicitud>`. Conservar clave y cuerpo para reintentar la misma operación; otro cuerpo con esa clave corresponde 409. Reiniciar un runner genera claves nuevas: no equivale a un replay. Tras refresh, no repetir con la clave anterior y el cuerpo que ahora contiene el token nuevo. Para un nuevo login o rotación intencional, generar otra clave manteniendo la sesión actual en privado. No borrar indiscriminadamente claves ni registros de BD.

## Alcance verificado y pendiente

Tras aplicar la tercera migración Identity en dev y arrancar API/Gateway, se confirmó `/health/ready` 200 por ngrok con inspección desactivada. El titular ejecutó manualmente los bloques en Postman y confirmó los resultados siguientes; no se trata de una ejecución automatizada integral del runner ni se conservan respuestas con credenciales.

| Recorrido manual confirmado por el titular | Resultado |
| --- | --- |
| Alta y correo de verificación | Correo recibido, confirmación 200 y login 200 |
| Recuperación | Solicitud 202, correo recibido, reset 200, JWT anterior 401 y login nuevo 200 |
| Invitación | Creación 201, correo recibido y aceptación 200 |
| Membresía | Cambio de rol 204, desactivación confirmada y JWT previamente vigente rechazado tras desactivar |
| Perfil y tenant | Actualizaciones 204, lecturas de tenant y listado de usuarios 200 |
| Precondición HTTP | Actualización sin If-Match 428 |
| Sesiones | Refresh 200, logout 204, nuevo login 200 y refresh revocado 401 con clave nueva |
| Idempotencia de login sin scripts | Misma clave/cuerpo: 200 y mismos tokens; cambio de channel con la misma clave: 409 |
| Reenvío de verificación | Segundo buzón controlado, segundo correo recibido, confirmación y login correctos |
| Aislamiento entre dos tenants | Lecturas separadas correctas y modificación con ETag ajeno rechazada según el procedimiento indicado (412) |

Las pruebas automatizadas HTTP/PostgreSQL y sus límites se documentan en `../Cierre-Identity-verificacion.md`. La confirmación manual no acredita todas las carreras ni todos los casos negativos por HTTP real.

Compose dev utiliza el cluster YARP real `api-cluster`. La caché OAuth está montada en la API y se verificó lectura del archivo y escritura del directorio con la identidad efectiva del contenedor SDK dev (root). Los correos de cuenta e invitación recibidos durante los recorridos aportan evidencia del envío real en dev; no prueban renovación OAuth tras expiración ni permisos de la imagen de producción.

Para copiar un `membershipETag` desde JSON a If-Match, conservar sus comillas dobles pero no las barras de escape del JSON. Usar el userId del mismo elemento, sin comillas en la URL. El ETag del perfil se obtiene de la cabecera ETag de GET users/me; el del tenant, de GET tenants/me. Releer antes de cada nueva mutación.

Para probar replay mediante envíos individuales, usar una solicitud independiente sin scripts con una Idempotency-Key literal fija: el script de esta colección puede regenerar su clave temporal entre ejecuciones. No usar una variable aleatoria por envío. Mantener el mismo cuerpo y comparar los tokens solo localmente; después cambiar channel de web a telegram con la misma clave para comprobar 409.

Pendientes fuera de estas comprobaciones manuales: renovación OAuth tras expiración, frontend de verificación/recuperación/aceptación y validación de producción. Las carreras están cubiertas solo en el alcance automatizado documentado. Ejecutar solicitudes de invitación o desactivación de esta colección sí produce esos efectos; no lanzar el runner completo sin preparar y autorizar los datos de prueba.

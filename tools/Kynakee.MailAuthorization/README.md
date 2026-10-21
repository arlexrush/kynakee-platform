# Autorización administrativa del correo

Utilidad independiente .NET 10 para ejecutar el consentimiento delegado inicial de la cuenta de invitaciones. Reutiliza `MailOAuthSession`; no inicia la API ni procesa trabajos. `authorize` no envía correo; `verify-smtp` envía un único diagnóstico al buzón OAuth o a un destinatario explícitamente autorizado.

## Preparación

La URL canónica planificada de invitación es `https://kynakee.com/invitations/accept?token=...`; el dominio está adquirido pero la página frontend no existe todavía. No enviar invitaciones reales hasta desplegar esa UI y verificar que consuma el endpoint de aceptación mediante Gateway. Detalles en [guía de despliegue](../../docs/Deployment/Correo-OAuth-personal.md).

- Registrar una aplicación compatible con cuentas personales para Outlook personal. Habilitar los flujos de cliente público y el permiso delegado de Exchange Online `SMTP.Send`; no usar un secreto de cliente ni permisos de Microsoft Graph como sustituto del scope SMTP.
- Para Outlook/Hotmail y Microsoft 365 Personal, usar `TenantId=consumers`. Solo para Microsoft 365 empresarial usar el GUID corporativo y evaluar consentimiento/políticas, SMTP AUTH y disponibilidad del flujo de dispositivo. Véase la [guía de despliegue](../../docs/Deployment/Correo-OAuth-personal.md).
- Configurar por un mecanismo seguro las variables `Smtp__OAuth__ClientId`, `Smtp__OAuth__TenantId`, `Smtp__OAuth__Username`, `Smtp__OAuth__CachePath` y `Smtp__OAuth__CacheKey`. La clave debe ser Base64 de 32 bytes aleatorios, independiente de JWT, idempotencia y payloads de correo. No se incluye ningún valor real en el repositorio.
- `CachePath` debe ser absoluto, en un directorio privado y escribible por la identidad administrativa y por la API según su despliegue. Compartir el mismo archivo físico y clave con la API; las rutas absolutas pueden diferir entre host y contenedor. No usar carpetas del repositorio ni volúmenes públicos. En Windows, restringir ACL explícitamente; en Linux, restringir directorio y montaje al usuario del servicio.
- Reservar también acceso al archivo `.lock` y al temporal `.tmp` asociados. La exclusión por archivo solo es aplicable si el sistema de archivos compartido respeta esos bloqueos; no es coordinación distribuida entre hosts independientes.

## Uso

Desde la raíz, `dotnet run --project tools/Kynakee.MailAuthorization -- --help` muestra ayuda sin conexiones ni lectura de secretos.

### Lanzador local para Compose dev

Si la configuración ya está en `docker/.env.dev`, ejecutar en una terminal PowerShell privada desde la raíz: `& .\tools\Kynakee.MailAuthorization\Authorize-DevMail.ps1`. Mantener API detenida y desactivar transcripciones/grabación. El script carga únicamente los cinco ajustes OAuth, traduce los alias Compose a variables de proceso y utiliza `mail.cache` dentro del directorio host configurado. Restaura las variables previas al terminar; no cambia claves, el archivo privado, ACL ni el contenedor. El directorio debe existir y tener permisos privados previamente revisados.

Acepta valores literales en una línea, opcionalmente entre comillas. Admite comentarios después de valores entre comillas o separados del valor sin comillas por espacios; conserva `#` dentro de comillas o pegado a un valor sin comillas. Las comillas simples conservan caracteres literales; fuera de ellas se rechazan `$` y backticks para evitar interpretar interpolación de forma distinta a Compose. Rechaza ajustes duplicados, valores multilínea y comillas escapadas; no es un parser dotenv general. Los errores identifican el nombre del ajuste, no su valor. `-EnvFile <ruta>` selecciona otro archivo privado; `-CheckOnly` comprueba presencia, formato de ClientId/clave y existencia del directorio sin contactar Microsoft ni mostrar valores. Esa comprobación no acredita permisos, consentimiento o SMTP. No pasar secretos como argumentos ni ejecutar con trazas de PowerShell.

Solo con autorización explícita del administrador, ejecutar `dotnet run --project tools/Kynakee.MailAuthorization -- authorize` en terminal interactiva privada. No redirigir salida ni usar grabación/transcripción. Introducir el código únicamente en la dirección mostrada por la utilidad y elegir el buzón configurado. No compartir códigos, tokens ni capturas de pantalla.

El comando tiene un límite de diez minutos y permite cancelar con Ctrl+C. Salidas: 0 éxito/ayuda, 1 fallo de autorización o caché, 2 uso incorrecto/no interactivo, 130 cancelación/timeout. No se aceptan secretos ni configuración por argumentos. La utilidad no imprime tokens ni excepciones del proveedor. Los comandos sin argumentos solo muestran ayuda.

### Diagnóstico SMTP autorizado

`& .\tools\Kynakee.MailAuthorization\Authorize-DevMail.ps1 -VerifySmtp` carga también `SMTP_HOST` y `SMTP_PORT` (defaults `smtp-mail.outlook.com` y `587`). Requiere caché previamente autorizada; no inicia consentimiento. Utiliza adquisición silenciosa, STARTTLS obligatorio y XOAUTH2, límite de 60 segundos, sin protocolo de logging ni reintentos automáticos. Envía por defecto al mismo buzón configurado un mensaje sin enlaces, tokens ni invitaciones. Para un único destinatario diferente previamente autorizado, añadir `-Recipient <direccion>`; se valida una dirección sin nombre visible y se transmite mediante `Smtp__VerificationRecipient` temporal, restaurada al terminar. No se guarda en `.env` ni se imprime en la salida. No repetir automáticamente tras timeout/error: podría haberse aceptado el mensaje antes del fallo.

El éxito confirma aceptación SMTP, no recepción, renovación de un token expirado ni ejecución del transporte de la API o worker. Confirmar recepción por separado. No usar este comando como endpoint ni como sustituto del flujo real de invitaciones.

## Recuperación y límites

- La API solo intenta adquisición silenciosa; nunca abre un flujo interactivo. Revocación de consentimiento, políticas o caducidad no recuperable exigen ejecutar nuevamente la utilidad.
- Caché cifrada y clave deben conservarse juntas de forma segura pero separadas en almacenamiento de secretos. La caché está ligada a aplicación, directorio y buzón; cambiar cualquiera de ellos requiere otra caché/consentimiento.
- Si la caché está dañada o cambia su clave, detener consumidores, conservarla en un respaldo protegido y retirarla de la ruta configurada antes de volver a autorizar. No eliminar `.lock` mientras haya procesos activos. No hay comando automático de borrado/reset.
- Autorizar no prueba el envío SMTP, STARTTLS, renovación real ni aceptación de destinatarios. Un cambio de buzón/tenant no es una migración automática a Microsoft 365.
- Nunca introducir secretos en Git. Backups pueden contener ciphertext histórico; aplicar retención y protección acordes. Los tres Compose montan un directorio externo preexistente configurado por `SMTP_OAUTH_CACHE_DIRECTORY`; la utilidad usa su ruta host al archivo, no la ruta interna del contenedor. Permisos y operación real siguen pendientes de comprobar.

## Evidencia

Validación inicial: compilación, ayuda, argumentos inválidos y rechazo de autorización con salida redirigida. Posteriormente el titular confirmó éxito de `authorize` y guardado de caché cifrada. Con autorización explícita se ejecutó una sola prueba `-VerifySmtp` contra la configuración privada existente: adquisición silenciosa, STARTTLS, XOAUTH2 y aceptación SMTP correctos. El titular confirmó recepción en la bandeja de entrada del buzón remitente. Sin invitaciones, ejecución desde API/contenedor, prueba de refresh tras expiración ni recorrido Gateway E2E. No se registran valores privados ni códigos de dispositivo.

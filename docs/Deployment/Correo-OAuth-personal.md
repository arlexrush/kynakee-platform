# Correo de invitaciones: Hotmail y Microsoft 365 Personal

## Estado y alcance

Microsoft 365 Personal es una suscripción de consumo: un buzón Hotmail/Outlook personal sigue utilizando `consumers`, no un GUID de directorio empresarial. El transporte implementado usa MailKit, STARTTLS obligatorio y SASL XOAUTH2. MSAL realiza consentimiento delegado mediante una utilidad administrativa y adquisición silenciosa desde la API. No hay autenticación por contraseña ni fallback sin TLS.

Implementación compilada; evidencia previa: 508/508 pruebas unitarias y 8/8 de durabilidad PostgreSQL efímero con sender sustituido. La consola pasó cuatro comprobaciones locales (ayuda/sin argumentos=0, argumentos inválidos/no interactivo=2). Esto **no acredita** consentimiento, renovación, permisos del montaje, SMTP ni entrega reales. Migraciones, Gateway/JWT E2E, Outbox e integración Billing siguen pendientes en su alcance respectivo.

## 1. Preparar el registro Microsoft

1. Entrar personalmente en el centro de administración Microsoft Entra y abrir Registros de aplicaciones. Si la cuenta personal no dispone de un directorio donde crear registros, será necesario obtener acceso a uno o pedir a su administrador un registro compatible; Microsoft 365 Personal no concede por sí solo un directorio empresarial ni permisos administrativos. No crear una cuenta corporativa ficticia ni cambiar `consumers` por un tenant arbitrario como solución.
2. Crear una aplicación cuyo tipo de cuentas admita cuentas Microsoft personales (solo personales o multitenant y personales, según las opciones disponibles). Anotar el **Id. de aplicación (cliente)**: ese es `SMTP_OAUTH_CLIENT_ID`, no el Id. de objeto.
3. Habilitar los flujos de cliente público para usar código de dispositivo. No crear ni configurar un secreto de cliente para este flujo.
4. Configurar el permiso **delegado** `SMTP.Send` de Office 365 Exchange Online. No sustituirlo por `Mail.Send` de Microsoft Graph ni por permisos de aplicación. La implementación solicita `https://outlook.office.com/SMTP.Send`; MSAL gestiona los scopes reservados del flujo, incluido el acceso sin conexión. Si el portal no ofrece el permiso o el tipo de cuenta, detener la preparación y revisar el registro antes de consentir.
5. El titular del buzón aceptará el consentimiento en Microsoft al ejecutar la utilidad. No compartir contraseñas, MFA, códigos de dispositivo, tokens ni capturas en chats o incidencias.

## 2. Configuración por entorno

Rellenar de forma privada el archivo de entorno correspondiente o el mecanismo de secretos del despliegue, usando los `.env*.example` solo como plantilla. No se han modificado archivos `.env` reales.

| Variable Compose | Valor o significado |
| --- | --- |
| `SMTP_HOST` | `smtp-mail.outlook.com` para Hotmail/Outlook personal |
| `SMTP_PORT` | `587`; se exige STARTTLS, no TLS implícito en 465 |
| `SMTP_OAUTH_TENANT_ID` | `consumers` |
| `SMTP_OAUTH_CLIENT_ID` | Id. de aplicación compatible con cuentas personales |
| `SMTP_OAUTH_USERNAME` | Dirección del buzón autorizado; también se utiliza como `Smtp:From` |
| `SMTP_OAUTH_CACHE_KEY` | Base64 de 32 bytes aleatorios independientes de JWT, idempotencia y payloads de correo |
| `SMTP_OAUTH_CACHE_DIRECTORY` | Ruta absoluta de un directorio privado existente del host, fuera del repositorio |
| `INVITATION_BASE_URL` | En desarrollo local: `http://localhost:3000/invitations/accept`; en producción: URL HTTPS de la página web desplegada |
| `EMAIL_VERIFICATION_BASE_URL` | En desarrollo local: `http://localhost:3000/auth/email/verify`; en producción: URL HTTPS de la página de verificación desplegada, sin consulta ni fragmento |
| `PASSWORD_RESET_BASE_URL` | En desarrollo local: `http://localhost:3000/auth/password/reset`; en producción: URL HTTPS de la página de recuperación desplegada, sin consulta ni fragmento |

El servidor de correo añade el token a `INVITATION_BASE_URL` como query string. Solo en `Development` admite HTTP con host exactamente `localhost`; en los demás entornos exige HTTPS. La URL local abre la interfaz que se ejecuta en el mismo dispositivo que el navegador y no sirve para probar desde otro dispositivo. Para pruebas externas se necesita una URL HTTPS que exponga la web; el ngrok de este Compose expone el Gateway de la API, no `kynakee-web`. El frontend y su Compose se mantienen en otro repositorio y deben iniciarse por separado.

La URL de producción prevista es `https://kynakee.com/invitations/accept?token=...`; configurarla no crea DNS, hosting ni una página funcional. No enviar invitaciones reales a usuarios hasta implementar y probar en HTTPS una página que lea el token, lo trate como secreto y complete el flujo de aceptación.

La API tiene el endpoint backend anónimo `POST /api/v1/identity/auth/invitations/accept`, que consume el token y acepta los datos requeridos por su contrato. No es una página navegable y no se debe poner como enlace directo de correo: la web debe llamar al endpoint a través del Gateway conforme al contrato API. La configuración local no demuestra que la página, el flujo completo ni el envío real hayan sido verificados.

El correo de registro y reenvío de verificación incluye `EMAIL_VERIFICATION_BASE_URL?token=...`; el de recuperación incluye `PASSWORD_RESET_BASE_URL?token=...`. Ambas URL deben ser absolutas, sin credenciales, consulta ni fragmento; solo se admite HTTP con host exactamente `localhost` en `Development`. Si falta o es inválida la URL correspondiente, el trabajo se reintenta como error de configuración. Las páginas web independientes deben tratar el token como secreto, evitar logs, analíticas y filtraciones mediante `Referer`, eliminarlo de la URL visible y enviar un POST explícito al endpoint correspondiente; una visita GET no debe consumirlo. El token de recuperación caduca a los 30 minutos y es de un solo uso; el POST `/api/v1/identity/auth/password/reset` recibe el token y la nueva contraseña. Las rutas web locales están planificadas y no se han verificado en este repositorio.

Los tres Compose montan ese directorio en `/var/lib/kynakee/mail-oauth` y fijan `Smtp:OAuth:CachePath=/var/lib/kynakee/mail-oauth/mail.cache`. El bind requiere que la carpeta exista (`create_host_path: false`); no se crea automáticamente con propietario root ni se cambia el usuario de la API. La API sigue siendo interna y Gateway la única entrada HTTP publicada.

Las claves `IDENTITY_IDEMPOTENCY_HASH_KEY`, `IDENTITY_IDEMPOTENCY_OUTCOME_KEY_V1` e `IDENTITY_INVITATION_EMAIL_KEY_V1` ya son necesarias para sus propios propósitos; no reutilizarlas como clave de caché OAuth. Cada entorno debe tener directorio, caché y secretos separados. Evitar el buzón de producción para pruebas.

## 3. Preparar permisos antes de autorizar

- Crear la carpeta fuera del repositorio. En Windows, aplicar ACL privadas al administrador y la identidad que vaya a ejecutarla; revisar acceso desde Docker Desktop. No usar una carpeta compartida públicamente ni sincronizada sin protección.
- En Linux, identificar el UID/GID efectivo de la API de la imagen; el Dockerfile utiliza `$APP_UID`. Dar al usuario del servicio acceso de lectura/escritura y búsqueda al directorio y sus archivos, mediante propietario/grupo o ACL controlados. No usar `chmod 777` ni ejecutar producción como root. La utilidad puede ejecutarse con esa misma identidad o con ACL específicas.
- La imagen SDK de desarrollo no tiene necesariamente la misma identidad que la imagen final: verificar por entorno. Debe poder crear/abrir `mail.cache.lock`, leer `mail.cache`, escribir `mail.cache.tmp` y reemplazar el archivo mediante rename en la misma carpeta.
- Inicializar el consentimiento con la API detenida. El primer propietario de `.lock` importa para siguientes accesos. No eliminar archivos lock de un proceso en uso. La exclusión se basa en bloqueos del sistema de archivos, no es un protocolo distribuido: no compartir por NFS/varios hosts sin validar sus garantías.
- La caché se cifra con AES-GCM y queda ligada a aplicación/directorio/buzón. En Linux el archivo temporal termina con permisos de lectura/escritura del propietario; el directorio también debe ser privado. Cifrado no sustituye permisos ni una política de backups.

## 4. Autorizar desde una terminal administrativa privada

La utilidad se ejecuta en el **host** con SDK .NET 10; no se añade al contenedor público ni a un endpoint HTTP. Preparar sus variables de entorno mediante un mecanismo seguro. Los nombres de la utilidad son distintos de los alias Compose:

| Alias Compose | Variable de la utilidad |
| --- | --- |
| `SMTP_OAUTH_CLIENT_ID` | `Smtp__OAuth__ClientId` |
| `SMTP_OAUTH_TENANT_ID` | `Smtp__OAuth__TenantId` |
| `SMTP_OAUTH_USERNAME` | `Smtp__OAuth__Username` |
| `SMTP_OAUTH_CACHE_KEY` | `Smtp__OAuth__CacheKey` |
| `SMTP_OAUTH_CACHE_DIRECTORY` | `Smtp__OAuth__CachePath`: ruta absoluta **host** al archivo `mail.cache` dentro del directorio |

Debe ser el mismo archivo físico y la misma clave que usará la API; no copiar la ruta Linux del contenedor a Windows. La utilidad no carga automáticamente archivos dotenv. No pasar secretos como argumentos, pegarlos en el historial ni imprimir todas las variables para diagnosticarlos.

Desde la raíz, consultar `dotnet run --project tools/Kynakee.MailAuthorization -- --help`. Solo con permiso del titular, ejecutar `dotnet run --project tools/Kynakee.MailAuthorization -- authorize` en una terminal sin redirección ni grabación. Abrir la página oficial indicada por Microsoft, introducir el código y elegir exactamente el buzón configurado. Revisar el permiso antes de aceptarlo. La utilidad no envía correo y no revela tokens. Ctrl+C cancela; límite de diez minutos.

El éxito indica consentimiento y guardado de caché, no envío SMTP comprobado. Cerrar la terminal o retirar las variables de esa sesión sin imprimirlas. El worker no muestra códigos ni inicia consentimiento interactivo; usa renovación silenciosa.

## 5. Validación posterior y recuperación

- Antes de arrancar servicios existentes, completar el esquema requerido: la tabla de trabajos durables sigue pendiente de su migración autorizada. No aplicar migraciones automáticamente como parte de esta guía.
- Validar montaje/permisos con el usuario de la API, adquisición silenciosa y envío a un destinatario de prueba solo con autorización expresa. Verificar recepción y luego renovación real; no inferirlas de las pruebas con sender sustituido.
- `docker compose config` sin `--quiet` puede imprimir secretos interpolados. Para una validación privada completa usar `--env-file` explícito y `config --quiet`; no adjuntar su salida detallada ni `docker inspect` con variables a incidencias. La comprobación estática del repositorio usa interpolación deshabilitada y no acredita valores reales.
- Si se revoca consentimiento, reautorizar con la utilidad. Si la caché está dañada o cambia su clave, detener API/utilidad, guardar un respaldo protegido y retirar la caché de la ruta antes de autorizar otra vez. No hay reset automático. Reanudar solo tras verificar permisos.
- Cambiar buzón, ClientId o TenantId requiere nuevo consentimiento/caché. La caché tiene una clave única, no un keyring versionado: no cambiarla con consumidores activos; planificar renovación y tratar backups antiguos como datos sensibles.
- El trabajo durable reintenta de forma acotada y puede terminar por ocho fallos o expiración. Tras problemas de configuración, no asumir que los trabajos terminales se reenvían automáticamente; no hay utilidad administrativa de reencolado implementada. Revisar el estado sin exponer payloads.
- La aceptación SMTP seguida de una caída antes del commit puede producir duplicados. No hay garantía exactly-once. Outbox de integración y entrega de eventos no quedan acreditados por este correo local.

## Transición futura a Microsoft 365 empresarial

Microsoft 365 **empresarial** es otro escenario: revisar registro, permiso delegado, consentimiento/políticas del directorio, SMTP AUTH del buzón y disponibilidad del flujo de dispositivo. Usar el GUID corporativo y normalmente `smtp.office365.com:587`, además de un nuevo consentimiento y caché propios. No asumir que una suscripción Personal proporciona estos requisitos. Autenticación app-only y permisos de buzones compartidos no forman parte de la implementación actual.

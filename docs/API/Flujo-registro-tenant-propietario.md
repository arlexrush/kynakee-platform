# Flujo de registro de tenant y propietario

## Alcance y estado

`POST /api/v1/identity/auth/register` registra al propietario pendiente de verificar su correo y devuelve **202 Accepted**, con un `AccountActionAcknowledgement` genérico dentro de `ApiResponse`. No devuelve access token ni refresh token, no inicia sesión y no garantiza que se haya entregado un correo. `kynakee-web` es un repositorio independiente; sus páginas no se verifican aquí.

La evidencia histórica del recorrido manual dev, la migración Identity y sus limitaciones figura en [Cierre Identity](Cierre-Identity-verificacion.md) y [Postman](Postman/README.md). No extrapolarla a producción, a la renovación OAuth ni a todas las carreras. La infraestructura Outbox compartida e Identity ? Billing siguen aplazadas.

## Petición y validación

El cliente envía JSON y una `Idempotency-Key` aleatoria Base64URL sin padding, con al menos 128 bits de entropía. El comando requiere nombre y apellido, correo, contraseña, teléfono, nombre y tipo de tenant, identificador y país fiscal, plan y canal. La dirección fiscal es opcional; si se proporciona algún componente, se requieren país, municipio, código postal y calle, con región y provincia opcionales. Los validadores y el dominio son la referencia para el formato exacto.

Las reglas FluentValidation pasan por `ValidationBehavior`. Las respuestas de validación por campo usan HTTP 400 y `errors` cuando existe un campo identificable; otras validaciones de dominio pueden devolver un ProblemDetails general. No registrar contraseñas, claves de idempotencia ni tokens. Para un reintento de la misma petición conservar la misma clave y cuerpo; un cuerpo distinto bajo la misma clave devuelve 409 si ya se registró el resultado.

## Secuencia de alta

1. El cliente accede mediante Gateway; la API interna recibe la petición y verifica la clave de idempotencia.
2. MediatR ejecuta los behaviors registrados, incluida la validación y la transacción Identity. El handler valida valores de dominio y coordina el acceso por correo.
3. Para un alta elegible, crea el tenant y el usuario propietario, establece su contraseña protegida mediante ASP.NET Core Identity y deja al usuario en `PendingVerification`; crea su membresía Owner.
4. `AccountActionIssuer` crea un token de verificación opaco de un solo uso, con validez de 24 horas, guarda su hash y encola un trabajo de correo cifrado. El token y el trabajo se confirman con los efectos locales y el resultado idempotente en la transacción Identity.
5. La API devuelve 202 con un acuse genérico; para un correo ya registrado el handler también puede responder con el mismo acuse, sin crear otra cuenta. El éxito HTTP no demuestra la entrega SMTP.
6. El usuario confirma el token recibido mediante `POST /api/v1/identity/auth/email/verify`. La confirmación válida activa la cuenta pendiente sin emitir sesión; posteriormente el usuario inicia sesión con `POST /api/v1/identity/auth/login`.

La emisión de correo se procesa de forma durable localmente, pero SMTP puede aceptar un mensaje antes de una caída y producir un duplicado al reintentar. No se garantiza entrega exactamente una vez. No usar consultas SMTP o DNS como prueba de existencia de un buzón.

## Idempotencia y protección

El ámbito anónimo de registro se liga a la operación y la clave; la huella de la petición se protege con HMAC y el resultado con AES-GCM. Un lock transaccional PostgreSQL coordina peticiones concurrentes y efecto/outcome se confirman juntos. El replay de registro reproduce el acuse genérico, no una sesión ni un nuevo correo. La retención del resultado es de 24 horas; la huella se conserva, y una clave expirada no autoriza a repetir la operación como nueva. Véase [política y límites](KynakeeApiContracts.md#idempotency-key).

Para un nuevo correo de verificación se utiliza `POST /api/v1/identity/auth/email/resend` con una clave nueva, respetando el cooldown. La verificación y su consumo son de un solo uso. La respuesta genérica no confirma la existencia de una cuenta ni garantiza entrega.

## Referencias de implementación

- [IdentityEndpoints](../../src/Kynakee.Api/Endpoints/IdentityEndpoints.cs): registro, verificación y mapeo HTTP.
- [RegisterTenantOwnerCommand](../../src/Kynakee.Modules/Kynakee.Modules.Identity/Application/Commands/RegisterTenantOwner/RegisterTenantOwnerCommand.cs), su validador y handler: alta sin sesión.
- [AccountActionIssuer](../../src/Kynakee.Modules/Kynakee.Modules.Identity/Application/Commands/AccountActionIssuer.cs): emisión y encolado del token.
- [Contratos Identity](KynakeeApiContracts.md#matriz-por-endpoint): autorización, respuestas, precondiciones e idempotencia.
- [Guía de garantías](../Gobernanza/Garantias-transversales-por-modulo.md): límites de evidencia por módulo.
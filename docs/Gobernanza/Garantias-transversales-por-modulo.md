# Guía de garantías transversales por módulo

## Propósito y alcance

Esta guía es la memoria de implementación para revisar y cerrar, módulo por módulo, las garantías de unicidad, concurrencia optimista, idempotencia y pruebas. Se consulta antes de implementar persistencia o endpoints nuevos y se actualiza junto con el código y las pruebas. Los requisitos aquí descritos son criterios de diseño; no prueban implementación ni cobertura.

La guía complementa [Estado del repositorio y criterios de gobernanza](Estado-y-gobernanza.md), donde se registran evidencias actuales, y los contratos de [API](../API/KynakeeApiContracts.md) y [módulos/eventos](../Module_Contracts/Module_Contracs.md). Si hay discrepancia, el código y las pruebas reproducibles determinan el estado observable; las decisiones aprobadas permanecen como objetivos de trabajo.

## Procedimiento requerido por módulo

Antes de cerrar un cambio de módulo:

1. Revisar entidades, invariantes de dominio, índices EF/PostgreSQL, comandos, endpoints y efectos externos del módulo. Enumerar claves de negocio que deben ser únicas, recursos modificables por más de un actor y comandos que podrían repetirse por reintentos.
2. Para cada unicidad de negocio, crear o verificar una restricción de base de datos real, incluido su alcance tenant/global, tratamiento de borrados y nombre estable. Las prevalidaciones ayudan al usuario, pero no resuelven carreras.
3. Traducir violaciones solo cuando SQLSTATE y esquema/restricción coincidan con una lista explícita permitida del módulo. Responder `409 Conflict` con código estable y mensaje no sensible; errores desconocidos conservan el tratamiento genérico. No convertir indiscriminadamente toda violación de unicidad a conflicto de negocio.
4. Evaluar concurrencia por recurso. Si dos escrituras pueden sobrescribirse, exponer ETag fuerte ligado al recurso y a su versión, y exigir `If-Match` en las mutaciones identificadas. `428` cuando falte la precondición; `412` si es inválida, de otro recurso o está obsoleta, incluido el conflicto detectado al guardar. No añadir ETags a cada entidad por defecto. En PostgreSQL usar el token de concurrencia configurado para la raíz agregada, sin exponer la versión interna como dato JSON.
5. Enumerar comandos HTTP reintentables con efectos externos y declarar por operación si requieren `Idempotency-Key`. Aplicar el contrato [Request Idempotency](../API/KynakeeApiContracts.md#request-idempotency): scope, fingerprint protegido, exclusión de ejecución concurrente, replay, expiración, fallos recuperables y protección de resultados. Un `409` por clave reutilizada con otro fingerprint no ejecuta un efecto nuevo. La retención Identity de 24 h no es el valor global.
6. Para cada consumidor de integración, definir identidad estable de evento y consumidor, efecto local y atomicidad de la reclamación de idempotencia con esos efectos. Para efectos externos definir deduplicación específica o handoff durable. No usar check-then-mark; no declarar operativo Outbox/transport ni almacenamiento compartido mientras estén aplazados.
7. Añadir pruebas con nombres C# PascalCase según convención del repositorio: unitarias para reglas/codec/mapping; integración para índices, filtros, tokens de concurrencia y transacciones sobre base efímera cuando aplique; HTTP/E2E para headers, status, autorización y replay. No aplicar migraciones a bases persistentes sin autorización.
8. Registrar evidencia por separado: rutas/símbolos, comando exacto de compilación, pruebas ejecutadas y resultados, proveedor/versión de base efímera, forma de crear esquema (migración o `EnsureCreated`), y límites. No convertir una prueba de persistencia en evidencia de HTTP E2E, ni resultados de un módulo en evidencia de otro.

## Estados de evidencia

Usar los estados definidos en [Cómo declarar el estado](Estado-y-gobernanza.md#cómo-declarar-el-estado). Además, para cada garantía registrar explícitamente:

- **Pendiente de evaluación:** falta inspección del modelo/código; no implica que la garantía aplique ni que falte su implementación.
- **Evaluado, no requerido:** se documenta la razón de por qué esa clase de garantía no aplica al módulo o a la operación revisada.
- **Diseñado / implementado / compila / probado / operativo:** usar las definiciones de gobernanza, indicando alcance exacto. Para pruebas, separar unitaria, PostgreSQL efímero, HTTP/E2E y operación real.

No sustituir una celda pendiente de evaluación por «no aplica» sin revisión registrada. No declarar operativa una capacidad compartida basándose solo en contratos, tipos registrados, compilación o pruebas aisladas.

## Matriz de revisión por módulo

Las entradas iniciales «Pendiente de evaluación» indican que esta guía no afirma cobertura. Se rellenan tras revisar el contrato, código y pruebas de cada módulo.

| Módulo | Unicidad de negocio y traducción permitida a 409 | Recursos/operaciones que requieren ETag + `If-Match` | Comandos HTTP que requieren `Idempotency-Key` | Eventos: consumidor, clave y efectos atómicos | Pruebas y evidencia a registrar |
| --- | --- | --- | --- | --- | --- |
| Projects | Pendiente de evaluación | Pendiente de evaluación (considerar mutaciones concurrentes del agregado Project) | Pendiente de evaluación | Pendiente de evaluación; Outbox/transport compartidos aplazados | Inventario general pendiente. Evidencia acotada de medios 2026-10-19: `TrustedMediaContentSourceTests` 12/12, PostgreSQL 17 efímero con `EnsureCreated`, fuente real por DI y SeaweedFS 3.85; consulta por `ProjectId` tipado, pertenencia antes de GET y lectura acotada. No prueba migraciones, unicidad, concurrencia, idempotencia ni HTTP/JWT E2E; véase [guía S3](../Deployment/Almacenamiento-S3.md#validación-y-pendientes). |
| KnowledgeBase | Pendiente de evaluación | Pendiente de evaluación; considerar concurrencia de agregados y orden de componentes APU | Pendiente de evaluación | Pendiente de evaluación; Outbox/transport compartidos aplazados | Pendiente de inventario de pruebas de dominio, persistencia/vectores y HTTP |
| MCP | Pendiente de evaluación | Pendiente de evaluación; considerar administración de servidores/proveedores y auditoría | Pendiente de evaluación | Pendiente de evaluación; Outbox/transport compartidos aplazados | Pendiente de inventario de pruebas de dominio, PostgreSQL efímero y contratos HTTP/SDK |
| AI | Pendiente de evaluación | Pendiente de evaluación; considerar operaciones/configuración mutables, si existen | Pendiente de evaluación; considerar coste/efectos de operaciones AI reintentables | Pendiente de evaluación; Outbox/transport compartidos aplazados | Pendiente de inventario de pruebas de servicio/proveedores y recorrido HTTP |
| Bots | Pendiente de evaluación | Pendiente de evaluación; considerar conversación/mensajes y estado de canal | Pendiente de evaluación; considerar recepción de mensajes/webhooks cuando se implementen | Pendiente de evaluación; Outbox/transport compartidos aplazados | Pendiente de inventario de pruebas de dominio, PostgreSQL efímero, canal y E2E |
| Billing | Pendiente de evaluación | Pendiente de evaluación; considerar ledger, cuenta, lotes y suscripción | Pendiente de evaluación; considerar comandos financieros y reintentos de inicialización/consumo | Pendiente de evaluación; integración Identity → Billing y Outbox aplazadas | Pendiente de inventario de invariantes financieros, PostgreSQL efímero, pipeline y E2E |
| Identity | Cinco índices y allowlist verificados; bloqueo transaccional por tenant para último Owner probado en PostgreSQL. Trabajo de correo único por invitación con migración `20261018121521_AddInvitationEmailJobs` generada/compilada, no aplicada. Véase matriz única de endpoints | ETags HMAC v2 ligados a tenant/tipo/UUID/versión; cinco mutaciones probadas por HTTP+PostgreSQL (428/412/204 y carrera xmin). JWT/Gateway E2E pendientes | Cinco POST con clave; TransactionBehavior existente + PostgreSQL, replay revalidado, outcomes cifrados 24 h y huellas sin caducidad. Rotación AES/compatibilidad legacy y purga SQL probadas; operación periódica/transición de bases existentes pendientes | Handoff local cifrado atómico con invitación/outcome; retry y SKIP LOCKED probados con sender sustituido. Sin exactly-once SMTP; consentimiento/envío real/despliegue pendientes. Outbox/transport e integración Billing aplazados | Paso 7: 496/496 unitarias y 78/78 integración dirigida con postgres:17/EnsureCreated; paso migración: compilación solución correcta, migración generada, no aplicada. No demuestra upgrade en base persistente, JWT/Gateway/correo E2E |

### Referencia única del cierre Identity

**Ampliación de verificación de cuenta en curso:** siete operaciones HTTP con clave (registro, reenvío, solicitud de recuperación, login, refresh, aceptación e invitación). Registro/reenvío/solicitud reproducen un acuse genérico sin credenciales mediante allowlist explícita de `IdentityModuleTransactionScope.ValidateReplayAsync`; el resto conserva revalidación de seguridad. `AccountActionHttpTests` 12/12 prueba 202 sin sesión, replay, fingerprint incompatible, carreras con claves iguales/distintas, consumo único y rechazo HTTP de JWT anterior a reset. PostgreSQL 17 efímero, `EnsureCreated`, endpoints/pipeline reales en TestServer, sin SMTP ni ngrok. Las nuevas tablas y CredentialVersion aún no tienen migración aplicada; regresión ampliada y pruebas del worker en curso. No sustituye los límites históricos por una declaración de operación completa.

**Actualización operativa BD dev 2026-10-21:** aplicadas desde base vacía las migraciones `20261009022444_InitialIdentitySchema` y `20261018121521_AddInvitationEmailJobs` en PostgreSQL Compose dev 17.11, historial EF 10.0.11 y estructura de correo verificados tras respaldo privado. Esta evidencia sustituye únicamente el estado «no aplicada» de la fila histórica Identity; no acredita upgrade de datos, restauración, nuevas pruebas de concurrencia ni Gateway/SMTP E2E. Véase [evidencia de aplicación](Estado-y-gobernanza.md#aplicación-autorizada-de-migraciones-identity-en-dev).

**Base activa dev 2026-10-27:** se encontraron ausentes el esquema Identity y el historial EF; tras respaldo privado se aplicaron las tres migraciones existentes, incluida `20261021013532_AddAccountVerificationAndRecovery`. Historial, tablas e índices verificados en PostgreSQL Compose 17.11; alcance y límites en la [evidencia de aplicación](Estado-y-gobernanza.md#aplicación-autorizada-de-migraciones-identity-en-dev). No demuestra restauración, upgrade de datos existentes ni envío SMTP real.

Consultar la [matriz de los 18 endpoints](../API/KynakeeApiContracts.md#matriz-por-endpoint) antes de modificar o cerrar una garantía; no mantener una segunda tabla de rutas aquí.

**Revisión del perfil fiscal:** el cambio de proyección y semántica omitido/null conserva el ETag existente, sin modificación de esquema. La [evidencia y sus límites](Estado-y-gobernanza.md#revisión-del-perfil-fiscal-y-validaciones) incluye 49 pruebas Identity HTTP/PostgreSQL 17 efímero con `EnsureCreated`, y 13 Billing HTTP/PostgreSQL efímero con migraciones, todas superadas. Se verifican los casos concretos documentados, no el despliegue real ni todas las combinaciones fiscales o carreras posibles.

La [política de recuperación y replay seguro](../API/KynakeeApiContracts.md#recuperación-precondiciones-y-límites-comunes) recoge la revalidación implementada y probada localmente en el paso 6; JWT/Gateway y correo completos siguen pendientes. Las huellas no caducan; purgar solo resultados tras 24 h. Claves HMAC independientes estables y cifrado versionado requieren preparación legacy antes de usar bases existentes. Un ETag firmado protege integridad, no confidencialidad de la versión; no confundirlo con cifrado ni con un bloqueo entre Owners distintos.

## Plantilla de cierre de garantía

**Actualización posterior del cierre backend dev Identity:** la [evidencia ampliada](Estado-y-gobernanza.md#evidencia-ampliada-de-cierre-backend-dev) sustituye los pendientes históricos anteriores únicamente para los casos identificados. Regresión dirigida Identity/Gateway 137/137 y ampliación HTTP/workflows 39/39 con solapamiento; upgrade sintético de cuenta 1/1 mediante migraciones. Tercera migración `20261021013532_AddAccountVerificationAndRecovery` aplicada en dev con respaldo privado y verificación estructural. Titular confirma recorridos manuales por Gateway y recepción/consumo de correos, precondiciones, replay/conflicto de login, refresh revocado y separación de dos tenants. No atribuir esas pruebas a otros módulos ni dar por probado replay de todas las operaciones por ngrok. Último Owner y carreras conservan su alcance efímero automatizado; frontend, renovación OAuth tras expiración, restauración, producción, purga periódica operativa y Outbox compartido no se declaran verificados.

Copiar y completar por garantía y módulo en el registro de gobernanza cuando se evalúe:

| Campo | Evidencia |
| --- | --- |
| Módulo / garantía | Unicidad, concurrencia, idempotencia HTTP o consumidor |
| Regla/operaciones cubiertas | Clave única, recurso versionado, comandos/casos de replay o evento y consumidor |
| Implementación | Archivos y símbolos concretos; restricciones/nombres de base de datos si aplican |
| Compilación | Comando exacto, proyecto/solución y resultado |
| Pruebas | Nombres exactos, tipo, comando y resultado |
| Entorno de integración | Motor/versión, efímero o persistente, `EnsureCreated`/migración y limitaciones |
| Estado | Diseñado, implementado, compilado, probado, operativo o pendiente; sin extrapolar niveles |
| Pendientes/riesgos | HTTP E2E, migración, retención, recuperación, efectos externos u otros |

## Límites globales vigentes

La idempotencia HTTP y la deduplicación de consumidores son problemas relacionados pero distintos. La política HTTP se especifica en API y debe declararse comando a comando. La entrega de integración al menos una vez requiere Outbox y consumidores idempotentes; los efectos del consumidor y la reclamación deben ser atómicos. El repositorio mantiene aplazados el Outbox/transport y el almacén transversal de idempotencia hasta autorización y validación separadas. La tabla o el diseño de una tabla no constituyen por sí solos una implementación operativa.

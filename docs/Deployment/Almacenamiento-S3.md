# Almacenamiento S3: SeaweedFS dev y Hetzner prod

## Decisión y alcance

- **Dev local:** SeaweedFS `chrislusf/seaweedfs:3.85`, servidor integrado con filer y gateway S3, autenticación explícita y volumen propio. Almacenamiento S3 desplegado y operativo en el alcance verificado abajo; no implica API/Gateway o producción operativos.
- **Prod:** Hetzner Object Storage, con bucket privado previamente aprovisionado y endpoint regional HTTPS. La configuración está preparada; no se han creado recursos, contactado Hetzner ni probado credenciales reales.
- **Staging:** aplazado por coste. Se conservan sus archivos con `ObjectStorage__Enabled=false`; no se declara operativo.
- **Cliente .NET:** `AWSSDK.S3` 4.0.104.4, interfaz `IAmazonS3`. `KynakeeApiDependenceExtensions.AddApiDependencies` sigue delegando en `AddProjectsModule`, que registra `ObjectStorageOptions`, `ObjectStorageOptionsValidator` y la implementación concreta `S3TrustedMediaContentSource`. Se mantiene .NET 10.

## Configuración de la aplicación

La única sección de almacenamiento es `ObjectStorage`: `Enabled`, `Endpoint`, `AccessKey`, `SecretKey`, `Bucket`, `Region`, `UseSsl`, `MaxObjectBytes`. No existe fallback a otras secciones. `Region` tiene por defecto `us-east-1`, pero prod la exige explícitamente en Compose. El cliente utiliza firma regional, `ForcePathStyle=true`, timeout de 30 segundos y hasta dos reintentos del SDK.

El endpoint debe ser un origen HTTP(S), sin credenciales, ruta de bucket, query ni fragmento. Debe corresponder a `UseSsl`. El máximo configurable es 100 MiB y se valida el nombre de bucket. El origen de contenido comprueba pertenencia del proyecto/tenant antes de solicitar el objeto, valida MIME y tamaño de la respuesta, limita los bytes durante la lectura y rechaza una longitud distinta de la declarada. No permite descargar desde URLs HTTP arbitrarias. Estas comprobaciones están implementadas; no equivalen a una prueba E2E de aislamiento.

Las referencias de medios usan exclusivamente **`s3://bucket/clave`**. El dominio rechaza referencias sin bucket/clave, traversal, separadores codificados, credenciales, puerto, query o fragmento. El usuario confirmó que no hay datos que conservar: no se preparó ni aplicó una migración de objetos o referencias. Esto no autoriza borrar bases, volúmenes o recursos Docker.

## Dev local

Compose `docker/docker-compose.dev.yml` define:

- `seaweedfs`: imagen fijada 3.85 y publicación host exclusivamente `127.0.0.1:${SEAWEEDFS_S3_PORT:-8333}:8333`. Master, filer y volume server no tienen puertos publicados en el host. No montar el socket Docker ni publicar S3 por ngrok/Cloudflare.
- Volumen `seaweedfs-dev-data:/data`: contenido y metadatos, incluido LevelDB2 en `/data/filerldb2`. El comando integrado utiliza `-dir=/data`, volúmenes de 32 MiB, preasignación deshabilitada y `-volume.max=0`; no es una topología distribuida ni de alta disponibilidad.
- Autenticación: archivo de solo lectura `docker/seaweedfs/s3.dev.json`, sin identidad anónima, con credenciales ficticias `synthetic-access`/`synthetic-secret` y acciones amplias de desarrollo. No usar estas credenciales en prod, ni claves reales de Hetzner/AWS en dev. No se afirma mínimo privilegio o aislamiento por tenant mediante esta identidad compartida.
- Healthcheck: espera un `403` en la raíz S3 sin firma. Comprueba que el endpoint rechaza esa petición, no acredita autenticación válida, persistencia ni aislamiento completo.
- `seaweedfs-init`: imagen `amazon/aws-cli:2.27.49`, ejecuta `docker/seaweedfs/init-s3.sh` después del healthcheck. El listado de buckets debe tener éxito; solo crea el bucket si no aparece y termina con `head-bucket`. No transforma errores de conexión o autorización en intentos de creación. La API depende de su salida exitosa.
- API: endpoint interno `http://seaweedfs:8333`, región `us-east-1` y credenciales de dev. Gateway continúa como única entrada HTTP de la aplicación; el frontend queda fuera de este Compose.

Variables de `.env.dev.example` y `.env.example`:

| Variable | Default local |
| --- | --- |
| `SEAWEEDFS_S3_PORT` | `8333` |
| `OBJECT_STORAGE_BUCKET` | `kynakee-media-dev` |
| `OBJECT_STORAGE_MAX_OBJECT_BYTES` | `104857600` |

Los archivos `.env` privados no se han leído ni modificado en esta sustitución; las plantillas públicas son la referencia vigente. No copiar volcados de configuración ni secretos a logs. Los recursos Docker ya existentes se conservan por decisión del usuario: no ejecutar `down -v`, `prune` ni retirada de huérfanos como parte de este cambio. Declarar un volumen no sustituye una prueba de recuperación o un backup.

### Puesta en marcha posterior

Para despliegues posteriores, desde la raíz y con Docker iniciado, carpeta OAuth existente y variables de API configuradas: `docker compose --env-file docker/.env.dev -f docker/docker-compose.dev.yml up -d seaweedfs seaweedfs-init`. Comprobar el estado de ambos servicios y la salida exitosa del inicializador, repetirlo y verificar escritura/lectura con el cliente vigente antes de recrear API/Gateway. La comprobación de esta sesión extrajo exclusivamente estos dos servicios del Compose a un archivo temporal con rutas resueltas, proyecto `docker`, red y volumen originales y defaults públicos: no cargó el `.env` privado ni arrancó otras dependencias. Las advertencias de servicios huérfanos no autorizan eliminarlos.

Esta guía no autoriza iniciar API/Gateway ni aplicar migraciones: la tabla de correo de Identity requiere su propia aplicación autorizada. Evitar compilación host concurrente con `dotnet watch` en carpetas `obj` compartidas. El volumen local no constituye almacenamiento de producción ni protección frente a borrado accidental.

## Producción Hetzner

La API no ejecuta un servidor S3 ni crea buckets en producción. El administrador debe:

1. Crear un bucket privado en la ubicación elegida de Hetzner y preparar credenciales con el menor alcance disponible que permita las operaciones necesarias. No habilitar ACL pública ni asumir políticas AWS no soportadas por Hetzner.
2. Configurar `OBJECT_STORAGE_ENDPOINT` con el endpoint regional mostrado por Hetzner, sin ruta ni nombre de bucket. La plantilla usa `https://fsn1.your-objectstorage.com` y `OBJECT_STORAGE_REGION=fsn1` como ejemplo; deben coincidir con la ubicación real.
3. Configurar `OBJECT_STORAGE_ACCESS_KEY`, `OBJECT_STORAGE_SECRET_KEY` y `OBJECT_STORAGE_BUCKET` en el mecanismo privado de secretos. `ObjectStorage__UseSsl` se fija a `true`; no deshabilitar validación TLS ni bajar a HTTP.
4. Antes de operar, verificar de forma autorizada firma regional, creación/lectura/borrado de un objeto de prueba no sensible y resolución por la aplicación, aislamiento por tenant, MIME/tamaño, permisos, recuperación y backups. Pruebas en SeaweedFS no acreditan equivalencia total, rendimiento ni garantías de Hetzner.

No se han copiado secretos a plantillas ni se ha probado un bucket Hetzner real. No hacer volcado de `docker compose config`/`docker inspect` con secretos a logs o incidencias. Planificar rotación de credenciales sin romper acceso a objetos existentes.

## Contrato reutilizable de subida

`S3UploadPolicy.CreateRequest` (`src/Kynakee.Modules/Kynakee.Modules.Projects/Infrastructure/Storage/S3UploadPolicy.cs`) construye solicitudes de un único objeto. Exige stream legible y seekable en posición cero, longitud positiva dentro del máximo indicado (hasta 100 MiB) y MIME sintácticamente válido. Fija `Headers.ContentLength`, `UseChunkEncoding=false`, `DisablePayloadSigning=false`, `AutoCloseStream=false` y `AutoResetStreamPosition=false`. El llamador conserva la propiedad del stream y debe mantenerlo abierto y sin modificar hasta finalizar la subida esperada con `await`.

La utilidad local y las cargas de `SeaweedFsS3Tests` usan esta política. Los fixtures de lectura que necesitan crear objetos deliberadamente inválidos mantienen solicitudes directas. La política no autoriza escrituras por tenant/proyecto, reserva claves, impide sobrescrituras ni aporta idempotencia, multipart o un endpoint de subida. Tampoco inspecciona el contenido para determinar su MIME; valida el valor suministrado. No usarla como servicio de subida de negocio.

## Validación y pendientes

Evidencia del cierre del almacenamiento local, regresión final del **2026-10-19**, Windows/Visual Studio, .NET 10:

- **Implementado y compilado:** cliente `IAmazonS3`, opciones, validador y origen de medios; compilación completa de solución correcta desde Visual Studio después de adaptar las pruebas.
- **Configuración estática:** los tres Compose pasan `docker compose --env-file docker/.env.<entorno>.example -f docker/docker-compose.<entorno>.yml config --no-interpolate --no-env-resolution --quiet`. No equivale a un despliegue.
- **Regresión unitaria:** Test Explorer, filtro Assembly=`Kynakee.UnitTests.dll`: **533/533 superadas**, cero omitidas. Incluye 13 casos de captura, ocho de configuración y 12 de `S3UploadPolicyTests` (longitud, límites, posición, stream no seekable, MIME, firma, encoding y propiedad del stream).
- **Integración SDK vigente:** `tests/Kynakee.IntegrationTests/Storage/SeaweedFsS3Tests.cs`, filtro TypeName=`Kynakee.IntegrationTests.Storage.SeaweedFsS3Tests`: **7/7 superadas**, cero omitidas. Bytes exactos, tamaño/MIME, GET anónimo `403`, reinicio y recreación con volumen exclusivo; además, script real ejecutado dos veces conservando el objeto y credenciales inválidas que fallan sin crear bucket. No se ha ejecutado toda la suite de integración.
- **Origen real de medios:** `tests/Kynakee.IntegrationTests/Storage/TrustedMediaContentSourceTests.cs`, TypeName=`Kynakee.IntegrationTests.Storage.TrustedMediaContentSourceTests`: **12/12 superadas**. Resuelve `ITrustedMediaContentSource` por DI, PostgreSQL 17 efímero con `EnsureCreated` y SeaweedFS 3.85. Verifica bytes exactos, cinco referencias ajenas/no registradas rechazadas antes de GET, MIME incorrecto, objeto vacío/sobredimensionado, cancelación y dos longitudes de respuesta inconsistentes con liberación del stream. Estas últimas usan el SDK real con un handler HTTP controlado; los rechazos sin GET sustituyen únicamente `IAmazonS3`. La prueba descubrió y corrigió la traducción EF de `project.Id.Value`: `S3TrustedMediaContentSource.OpenReadAsync` compara ahora el `ProjectId` tipado, sin evaluación cliente. La ejecución conjunta final de las dos clases pasó **19/19**, cero omitidas. No acredita migraciones ni persistencia general de Projects ni autorización HTTP/JWT E2E.
- **Operativo S3 dev:** `kynakee-seaweedfs-dev` healthy y `kynakee-seaweedfs-init-dev` terminado con código 0. Bucket `kynakee-media-dev`, endpoint host `http://127.0.0.1:8333`, volumen `docker_seaweedfs-dev-data`. La utilidad `tools/Kynakee.StorageVerification` resuelve `IAmazonS3` mediante `AddProjectsModule`; verificó escritura, bytes/MIME/longitud, inicializador repetido, lectura tras reinicio limpio y borrado del único objeto sintético creado. El servidor permanece activo. No se han eliminado recursos Docker anteriores.
- **Límite importante del SDK/servidor:** SeaweedFS 3.85 no interpretó correctamente el framing `aws-chunked` del ensayo con SDK v4. Las subidas verificadas usan `PutObjectRequest.UseChunkEncoding=false` con stream de longitud conocida, sin desactivar la firma SigV4. No asumir que subidas con defaults del SDK, multipart o cualquier productor de objetos están cubiertos. El servicio de medios de la aplicación es de lectura; no se ha añadido un endpoint de subida.
- **Verificación final del cliente local:** la utilidad adaptada a `S3UploadPolicy` completó `write`, `read` y `delete` contra `http://127.0.0.1:8333`; contenido, MIME y longitud exactos. Eliminó únicamente su objeto sintético, sin reiniciar servicios ni modificar volúmenes existentes.
- **Cobertura dirigida:** `dotnet-coverage collect -f cobertura -o <archivo-temporal> "dotnet test tests/Kynakee.UnitTests/Kynakee.UnitTests/Kynakee.UnitTests.csproj --no-build --filter FullyQualifiedName~S3UploadPolicyTests"`: 12/12 superadas; `S3UploadPolicy` registra 100 % de líneas y ramas. Informe temporal fuera del repositorio; no es cobertura global ni prueba de todas las implementaciones posibles de Stream.
- **Auditoría final:** 586 archivos existentes enumerados por `git ls-files --cached --others --exclude-standard`, sin coincidencias de componentes retirados. Se excluyen `.env` privados, artefactos generados, cachés e historial; se incluyen plantillas `.example`. Los tres Compose pasan de nuevo la validación estática indicada arriba.
- **Pendiente:** recuperación/backup, protección de medios E2E y operación Hetzner. No se aplicaron migraciones ni se inició API/Gateway. La matriz transversal de Projects conserva sus garantías pendientes; este cierre no declara operativo Identity ni su correo.

La utilidad local solo acepta HTTP loopback y credenciales sintéticas. Uso desde la raíz: `dotnet run --project tools/Kynakee.StorageVerification -- write http://127.0.0.1:8333 <id>`, después `read` y finalmente `delete`, reutilizando un identificador GUID en formato N de 32 caracteres. Opera únicamente sobre `verification/<id>.txt` del bucket dev. Devuelve cero al verificar correctamente, dos si los argumentos no son válidos; no usarla con datos reales ni como prueba de aislamiento tenant.

Los ensayos de un solo contenedor no prueban caída abrupta, reinicio del host, corrupción, backups/restauración, cargas grandes o multipart, TLS, mínimo privilegio, autorización tenant E2E, rendimiento ni compatibilidad S3 completa. Los tests usan un límite de cuatro volúmenes frente a asignación automática en dev; no son un ensayo de capacidad del Compose. El rechazo anónimo no acredita políticas AWS de bloqueo público. No se ha revisado la imagen para vulnerabilidades o soporte productivo. Mantener explícitas estas limitaciones al registrar los próximos resultados.

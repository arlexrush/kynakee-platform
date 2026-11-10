# Procedimiento de despliegue del backend de Kynakee en Hetzner

## 1. Objetivo y alcance

Esta guía describe cómo publicar incrementos del backend en producción: validar el código, actualizar el servidor, construir imágenes, tratar las migraciones y verificar el resultado.

**No ejecutes toda la guía como un único bloque.** Lee cada apartado, ejecuta sus comandos en el entorno indicado y comprueba su resultado antes de continuar. Si un comando falla o el resultado no coincide con lo esperado, detén el procedimiento e investiga el problema.

| Entorno                        | Función                                                                                                   |
| ------------------------------ | --------------------------------------------------------------------------------------------------------- |
| Windows, PowerShell            | Validar el código, gestionar Git, conectarse por SSH y guardar una copia externa del respaldo.            |
| Servidor Hetzner, terminal SSH | Revisar producción, obtener el código, construir imágenes, gestionar contenedores y consultar PostgreSQL. |

### Topología y rutas actuales

- Repositorio local: `C:\NET_Dev\kynakee-platform`.
- Servidor: `kynakee@138.201.93.10`.
- Repositorio del servidor: `/opt/kynakee-platform`.
- Compose: `/opt/kynakee-platform/docker/docker-compose.prod.yml`.
- Variables privadas: `/opt/kynakee-platform/docker/.env.prod`.
- Proyecto Compose observado en el servidor: `docker`.
- Entrada pública: Traefik → `kynakee-gateway` → `kynakee-api`. La API y la infraestructura no se publican directamente.
- Base observada: `kynakee_prod_db`, en `kynakee-postgres-prod`.
- El frontend pertenece a otro repositorio y tiene su propio despliegue; este procedimiento no lo construye ni lo reinicia.

Verifica nombres, rutas y dirección si cambia el servidor. El archivo Compose de *staged* se conserva, pero eso no significa que exista un entorno de staging operativo.

## 2. Reglas de seguridad

1. No publiques `.env.prod`, contraseñas, tokens, claves ni copias de seguridad en Git. No muestres la configuración expandida de Compose en una consola compartida.
2. No ejecutes `docker compose down -v`, `docker volume rm`, `docker system prune` ni otras operaciones de limpieza como parte del despliegue. Conserva los contenedores, volúmenes, imágenes y respaldos existentes salvo autorización específica.
3. No apliques migraciones de todos los módulos automáticamente. Cada migración requiere revisión, ensayo y autorización específicos.
4. No regeneres las claves de Identity, firma JWT, invitaciones u OAuth sin un plan de rotación: el cambio puede invalidar sesiones o impedir descifrar datos existentes.
5. Antes de compartir logs, elimina información sensible. No compartas el contenido de `.env.prod`.
6. La salud del Gateway no demuestra que funcionen la base de datos, el correo o todos los flujos de negocio.
7. El acceso a las copias debe ser restringido. Una copia sin cifrar conserva el riesgo de exposición si se pierde o se accede al equipo.

## 3. Preparar el incremento en Windows

### 3.1. Comprobar el código

**Dónde:** PowerShell en Windows.

```powershell
cd C:\NET_Dev\kynakee-platform
git status
dotnet restore Kynakee.slnx
dotnet build Kynakee.slnx -c Release --no-restore
dotnet test Kynakee.slnx -c Release --no-build
```

- `git status` muestra la rama y los cambios locales.
- `restore` obtiene las dependencias.
- `build` comprueba la compilación en Release.
- `test` ejecuta las pruebas disponibles. Algunas pueden requerir servicios, credenciales o una configuración específica.

Revisa el resultado de **cada comando**. No continúes con errores sin investigar. Registra las pruebas omitidas o no ejecutadas; una compilación correcta no demuestra que los flujos estén operativos.

### 3.2. Revisar y publicar el incremento

**Dónde:** Windows y la plataforma de revisión Git utilizada por el equipo.

1. Revisa los cambios, crea los commits y publica la rama de trabajo conforme al proceso del equipo.
2. Completa la revisión e integra la versión aprobada en `master`. No despliegues una rama no aprobada por accidente.
3. Actualiza las referencias remotas y anota el commit aprobado:

```powershell
git fetch origin
git log -1 --oneline origin/master
git rev-parse origin/master
```

Antes de continuar, identifica si el incremento contiene:

- Migraciones EF Core o cambios en modelos que requieran modificar el esquema.
- Cambios de `docker-compose.prod.yml`, Dockerfiles, variables de entorno o credenciales.
- Cambios de Gateway, Traefik, correo, almacenamiento o servicios de infraestructura.
- Cambios incompatibles con la versión anterior de la API o con el esquema anterior.
- Dependencias entre backend y frontend que obliguen a coordinar ambos despliegues.

Si hay migraciones, cambios de infraestructura o dudas sobre compatibilidad y recuperación, prepara un procedimiento específico antes de intervenir en producción. La existencia de un archivo de migración no demuestra que se haya aplicado ni probado en producción.

## 4. Conectarse y revisar producción

### 4.1. Abrir la sesión SSH

**Dónde:** PowerShell en Windows.

```powershell
ssh kynakee@138.201.93.10
```

Los apartados siguientes indican dónde ejecutar sus comandos. No copies comandos de Linux en PowerShell ni comandos de PowerShell en la sesión SSH.

### 4.2. Comprobar estado y recursos

**Dónde:** servidor, terminal SSH.

```sh
cd /opt/kynakee-platform
git status --short --branch
git branch --show-current
git rev-parse HEAD
docker ps --format 'table {{.Names}}\t{{.Image}}\t{{.Status}}'
df -h
```

Comprueba que estás en `master`, que no hay cambios locales que se puedan sobrescribir y que los servicios necesarios están en marcha. Comprueba también que hay espacio suficiente para las imágenes y los respaldos. Si hay cambios locales o recursos insuficientes, detente y resuelve el problema; no uses `git reset --hard` ni borres archivos para forzar el despliegue.

El commit mostrado corresponde al repositorio actual del servidor, pero **no demuestra por sí solo qué commit se utilizó para construir las imágenes en ejecución**. Registra la versión desplegada y su evidencia durante cada despliegue.

### 4.3. Revisar el incremento entrante

**Dónde:** servidor, terminal SSH.

```sh
git fetch origin
git log --oneline HEAD..origin/master
git diff --name-status HEAD origin/master
git rev-parse origin/master
```

Compara el último resultado con el commit aprobado en Windows y revisa los archivos modificados. Si no coincide, no continúes. El contenido de `origin/master` puede cambiar durante el procedimiento: usa únicamente la revisión aprobada y vuelve a comprobarla antes de actualizar.

**Decisión:**

- Si no hay migraciones ni cambios que afecten a infraestructura, sigue el flujo normal.
- Si hay migraciones, realiza además el apartado 7 antes de desplegar la nueva aplicación.
- Si hay cambios en infraestructura o secretos, revisa su impacto y prepara un procedimiento específico. Los comandos de esta guía recrean solo API y Gateway; no aplican cambios a PostgreSQL, Traefik, Redis, RabbitMQ ni Qdrant.

## 5. Conservar referencias para recuperación

**Dónde:** servidor, terminal SSH. Hazlo antes de construir imágenes nuevas o actualizar el repositorio.

```sh
cd /opt/kynakee-platform
git rev-parse HEAD
MARCA=$(date -u +%Y%m%dT%H%M%SZ)
API_IMAGE=$(docker inspect --format '{{.Image}}' kynakee-api-prod)
GATEWAY_IMAGE=$(docker inspect --format '{{.Image}}' kynakee-gateway-prod)
docker image tag "$API_IMAGE" "kynakee-rollback-api:$MARCA"
docker image tag "$GATEWAY_IMAGE" "kynakee-rollback-gateway:$MARCA"
echo "Etiqueta de recuperación: $MARCA"
```

Comprueba que todos los comandos terminan correctamente. Anota fuera de la sesión el commit, la marca y la versión de configuración que corresponden al despliegue anterior. La marca utiliza la hora UTC.

Las etiquetas conservan referencias a las imágenes actuales, pero no garantizan por sí solas un rollback: también hay que considerar las variables, los archivos de configuración y la compatibilidad con los datos y el esquema. Las etiquetas no son una copia externa y pueden perderse si se pierden las imágenes del servidor.

## 6. Obtener y compilar la versión aprobada

### 6.1. Actualizar el código

**Dónde:** servidor, terminal SSH.

Después de confirmar que `origin/master` sigue apuntando al commit aprobado:

```sh
cd /opt/kynakee-platform
git rev-parse origin/master
git merge --ff-only origin/master
git rev-parse HEAD
```

Compara ambos hashes con el commit aprobado. `--ff-only` evita realizar una fusión si la rama del servidor no puede avanzar directamente. Si falla o hay discrepancias, detente y revisa el estado de Git.

### 6.2. Validar Compose y construir las imágenes

**Dónde:** servidor, terminal SSH.

```sh
cd /opt/kynakee-platform/docker
docker compose -p docker --env-file .env.prod \
  -f docker-compose.prod.yml config --quiet
```

`config --quiet` valida la configuración sin imprimir sus valores expandidos. Si falla, corrige el problema sin mostrar secretos y no continúes.

```sh
docker compose -p docker --env-file .env.prod \
  -f docker-compose.prod.yml build kynakee-api kynakee-gateway
```

La construcción no detiene los contenedores que están en ejecución, pero puede consumir CPU, memoria y disco. Si falla, no recrees los servicios. Las imágenes se construyen en el servidor; se recomienda elegir una ventana de baja carga.

Si el incremento **no requiere migraciones**, continúa con el apartado 8. Si requiere migraciones, continúa con el apartado 7.

## 7. Procedimiento adicional para migraciones

**Este apartado solo se aplica a migraciones revisadas y autorizadas para el incremento concreto.** No repitas las migraciones ya registradas ni uses el SQL de un despliegue anterior para uno nuevo sin verificar su alcance.

### 7.1. Preparar y ensayar

**Dónde:** desarrollo y entorno aislado; consulta de producción solo en modo lectura.

Para cada módulo afectado:

1. Identifica el `DbContext`, los archivos de migración y el historial real de la base destino. Confirma la base, el usuario y el esquema de historial efectivo: cada módulo debe evaluarse por separado.
2. Genera el SQL para el rango de migraciones correspondiente desde el commit aprobado.
3. Revisa su contenido, cambios destructivos, duración, bloqueos, transacciones y compatibilidad con la aplicación anterior.
4. Ensaya el SQL en una copia aislada representativa. No consideres que un ensayo parcial demuestra una recuperación completa de datos, propietarios y permisos.
5. Conserva la ruta y el SHA-256 del SQL ensayado y revisado. Prepara la verificación posterior de sus migraciones y objetos esperados.
6. Coordina una ventana de mantenimiento, la suspensión de todos los escritores y un plan de recuperación. Obtén autorización para aplicar esas migraciones en producción.

Si la migración requiere pasos distintos de los descritos aquí, no improvises: documenta y revisa un procedimiento específico. Las migraciones y pruebas de persistencia aplazadas de otros módulos no se consideran completadas por aplicar las de Identity.

### 7.2. Detener las escrituras

**Dónde:** servidor, terminal SSH, durante la ventana autorizada.

Confirma que no hay otros procesos o clientes que puedan escribir en la base. Si existen, coordina su parada por separado. Cuando la API sea el único escritor de la aplicación:

```sh
docker stop kynakee-gateway-prod kynakee-api-prod
docker ps -a \
  --filter name=kynakee-gateway-prod \
  --filter name=kynakee-api-prod \
  --format 'table {{.Names}}\t{{.Status}}'
docker ps --filter name=kynakee-postgres-prod \
  --format 'table {{.Names}}\t{{.Status}}'
```

Comprueba que Gateway y API están detenidos y PostgreSQL permanece saludable. Durante esta parada el frontend no podrá utilizar el backend. No elimines los contenedores ni detengas la infraestructura que no sea necesario detener.

### 7.3. Crear y verificar un respaldo de corte

**Dónde:** servidor, terminal SSH, con las escrituras detenidas.

El directorio padre `/home/kynakee/kynakee-backups` debe existir y permitir escritura. El siguiente bloque crea un directorio nuevo con permisos privados y no sustituye respaldos anteriores:

```sh
umask 077
BACKUP_DIR=$(mktemp -d /home/kynakee/kynakee-backups/pre-deploy-XXXXXXXX)
```

Si `mktemp` falla o `BACKUP_DIR` está vacío, detente. Si se creó correctamente:

```sh
BACKUP="$BACKUP_DIR/kynakee-prod.dump"
if docker exec kynakee-postgres-prod sh -c \
  'test "$POSTGRES_DB" = "kynakee_prod_db" && exec pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Fc' > "$BACKUP" \
  && docker exec -i kynakee-postgres-prod pg_restore --list < "$BACKUP" > /dev/null \
  && docker exec -i kynakee-postgres-prod pg_restore --exit-on-error --file=/dev/null < "$BACKUP"
then
  echo "Respaldo creado y leído:"
  ls -lh "$BACKUP"
  sha256sum "$BACKUP"
else
  echo "ERROR: respaldo no verificado. No continúes."
fi
```

Anota la ruta completa y el SHA-256. Si hay un error, no apliques la migración. El respaldo es de una base concreta: `pg_dump` no incluye roles globales de PostgreSQL. Leer el catálogo y procesar el archivo no equivale a demostrar una restauración completa. El respaldo tampoco incluye datos de Qdrant, almacenamiento de objetos ni otros servicios.

### 7.4. Copiar el respaldo fuera del servidor

**Dónde:** abre otra ventana de PowerShell en Windows; mantén la sesión SSH disponible.

Sustituye `/RUTA/DEL/RESPALDO/kynakee-prod.dump` por la ruta exacta obtenida en el servidor:

```powershell
$destino = Join-Path $env:USERPROFILE ("kynakee-backup-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $destino | Out-Null
scp "kynakee@138.201.93.10:/RUTA/DEL/RESPALDO/kynakee-prod.dump" "$destino\kynakee-prod.dump"
if ($LASTEXITCODE -eq 0) {
	Get-FileHash "$destino\kynakee-prod.dump" -Algorithm SHA256
} else {
	Write-Error "Falló la copia. No continúes."
}
```

Compara el hash de Windows con el del servidor. Si no coinciden, no continúes. Guarda la copia con acceso restringido y, cuando sea posible, cifrada. La aceptación puntual de una copia sin cifrar no convierte esa opción en una protección permanente.

### 7.5. Revalidar y aplicar el SQL

**Dónde:** servidor, terminal SSH.

Antes de ejecutar el SQL, comprueba de nuevo que los escritores están detenidos, la base y el usuario son los correctos, el historial y los objetos coinciden con el punto de partida esperado y los permisos necesarios están disponibles. Si el estado ha cambiado respecto del ensayo, detente.

El siguiente bloque es una **plantilla**, no un comando listo para ejecutar: sustituye la ruta y el hash por los del SQL concreto revisado y ensayado para este despliegue. Verifica también que el contenedor y la base siguen siendo los correctos.

```sh
SQL_FILE=/ruta/al/sql-revisado.sql
SQL_SHA256=HASH_SHA256_REVISADO
if printf '%s  %s\n' "$SQL_SHA256" "$SQL_FILE" | sha256sum --check &&
  docker exec -i kynakee-postgres-prod sh -c \
  'test "$POSTGRES_DB" = "kynakee_prod_db" && test "$POSTGRES_USER" = "kynakee_prod_user" && exec psql -X -v ON_ERROR_STOP=1 -U kynakee_prod_user -d kynakee_prod_db' \
  < "$SQL_FILE"
then
  echo "SQL terminado; falta comprobar historial y esquema."
else
  echo "ERROR: no repitas el SQL ni restaures automáticamente."
fi
```

`ON_ERROR_STOP` detiene `psql` ante el primer error. Un script puede contener varias transacciones y dejar cambios ya confirmados si falla más adelante. Ante cualquier fallo, mantén la ventana de mantenimiento, inspecciona el historial y el esquema y decide la recuperación de forma controlada. **No reintentes ni restaures a ciegas.**

Después de una ejecución correcta, verifica en PostgreSQL los IDs de migración y los objetos esperados para el módulo concreto. Conserva la salida sin información sensible. Una vez comprobado el resultado, continúa con el apartado 8.

## 8. Desplegar la API y el Gateway

**Dónde:** servidor, terminal SSH, desde `/opt/kynakee-platform/docker`.

Este apartado aplica tanto a incrementos sin migraciones como a incrementos con migraciones ya verificadas. Compose puede recrear los contenedores de aplicación; habrá una interrupción breve. No usa `down` ni elimina volúmenes.

### 8.1. Iniciar la API

```sh
cd /opt/kynakee-platform/docker
docker compose -p docker --env-file .env.prod \
  -f docker-compose.prod.yml \
  up -d --no-deps --wait --wait-timeout 180 kynakee-api
```

`--no-deps` evita recrear las dependencias; `--wait` espera a que el servicio esté en funcionamiento y saludable. Si el comando falla o vence el tiempo de espera, revisa los logs y no inicies el siguiente paso hasta entender la causa.

### 8.2. Iniciar el Gateway

```sh
docker compose -p docker --env-file .env.prod \
  -f docker-compose.prod.yml \
  up -d --no-deps --wait --wait-timeout 180 kynakee-gateway
```

Comprueba el estado:

```sh
docker compose -p docker --env-file .env.prod \
  -f docker-compose.prod.yml ps
```

API y Gateway deben figurar en ejecución y saludables. Si falla el arranque, investiga antes de declarar el despliegue completado. Si el incremento cambia los servicios de infraestructura, estos comandos no aplican esos cambios: necesitan un procedimiento separado.

## 9. Validar el despliegue

### 9.1. Comprobar la salud pública

**Dónde:** servidor, terminal SSH.

```sh
curl -sS -o /dev/null \
  -w 'Gateway público: HTTP %{http_code}\n' \
  https://api.kynakee.com/health/ready
```

El resultado esperado es `200`. Esta comprobación no valida por sí sola el funcionamiento completo de Identity o de los demás módulos.

### 9.2. Comprobar el paso Gateway → API

**Dónde:** servidor, terminal SSH.

La siguiente petición envía JSON malformado a la ruta de login. No proporciona credenciales y debería rechazarse antes de ejecutar el inicio de sesión:

```sh
curl -sS -o /dev/null \
  -w 'Login inválido: HTTP %{http_code}\n' \
  -X POST -H 'Content-Type: application/json' --data '{' \
  https://api.kynakee.com/api/v1/identity/auth/login
```

En la versión comprobada después de la migración de Identity, respondió `400`. Revisa además los logs del Gateway para confirmar que reenviaron la solicitud a la API; un código HTTP por sí solo no identifica necesariamente el componente que respondió. Si cambia la ruta o el comportamiento del endpoint, adapta la prueba después de revisar el código.

### 9.3. Revisar los logs

**Dónde:** servidor, terminal SSH.

```sh
docker logs --since 5m kynakee-api-prod 2>&1 | tail -n 100
docker logs --since 5m kynakee-gateway-prod 2>&1 | tail -n 100
```

Busca errores de arranque, conexión, SQL y proxy. Los mensajes frecuentes pueden desplazar otros mensajes fuera de las últimas líneas; amplía o filtra el intervalo si es necesario. Oculta datos sensibles antes de compartir cualquier salida.

### 9.4. Probar el incremento

**Dónde:** según el flujo, desde el cliente autorizado o una herramienta de pruebas adecuada.

Realiza una prueba funcional específica para el cambio desplegado. Coordina previamente cualquier operación que cree datos o envíe correos reales. La petición con JSON malformado **no prueba** registro, autenticación real, invitaciones, recuperación ni correo. Registra qué se comprobó y qué queda pendiente.

## 10. Cómo actuar ante un fallo

1. Detén el procedimiento y conserva las salidas y los logs pertinentes, sin divulgar secretos.
2. Comprueba si la aplicación nueva llegó a recibir tráfico y si se escribieron datos.
3. Identifica el commit, las etiquetas de imagen anteriores y la configuración correspondiente.
4. Si hubo migraciones, consulta cuáles quedaron confirmadas y si la versión anterior es compatible con el esquema actual.
5. Decide una recuperación específica antes de cambiar imágenes o restaurar datos. Restaurar una copia puede perder las escrituras posteriores al respaldo.

Las etiquetas creadas en el apartado 5 ayudan a localizar las imágenes anteriores, pero **no son un rollback automático**. No repitas migraciones fallidas ni restaures una base sin evaluar su estado y el impacto sobre los datos.

## 11. Registrar el resultado

Al cerrar cada despliegue, conserva un registro accesible al equipo con:

- Fecha y hora, responsable, commit aprobado y versión realmente desplegada.
- Cambios relevantes en Compose y configuración, sin anotar valores secretos.
- Etiquetas de recuperación, ubicación del respaldo, hash y resultado de su comprobación.
- Módulos y migraciones autorizadas, aplicadas y verificadas, o indicación de que no hubo migraciones.
- Estado de API y Gateway, comprobaciones HTTP, logs revisados y pruebas funcionales realizadas.
- Fallos, pruebas no realizadas, limitaciones y acciones pendientes.

## 12. Referencia de la operación ya realizada

En la intervención previa se aplicaron en `kynakee_prod_db` las migraciones de Identity `20261009022444_InitialIdentitySchema`, `20261018121521_AddInvitationEmailJobs` y `20261021013532_AddAccountVerificationAndRecovery`. Se comprobaron los tres registros en el historial y nueve tablas de `schema_identity`; la API y el Gateway volvieron a estado saludable. El Gateway registró el reenvío de una petición de login inválida a la API, que respondió `400`.

El respaldo previo a esa operación se copió a Windows y se comprobó su hash. La copia se aceptó sin cifrado; no se acreditó una restauración completa con propietarios y permisos. Esas comprobaciones **no prueban** los flujos reales de Identity o correo ni la persistencia de otros módulos. Permanecen pendientes la protección duradera de claves Data Protection, el cifrado de respaldos y la evaluación cuidadosa de los secretos potencialmente expuestos.

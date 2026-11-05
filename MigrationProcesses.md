# Comprueba el estado del servicio PostgreSQL de desarrollo. El contenedor está en ejecución y aparece como saludable.

docker compose --env-file docker/.env.dev -f docker/docker-compose.dev.yml ps postgres

# Confirma la base y el usuario, busca el historial de EF y cuenta las tablas de los esquemas de aplicación. No se ha encontrado historial ni tablas en esquemas schema_*.

docker exec kynakee-postgres-dev psql -v ON_ERROR_STOP=1 -U kynakee_user_dev -d kynakee_db_dev -c "SELECT current_database() AS database, current_user AS db_user, to_regclass(format('public.%I', '__EFMigrationsHistory')) AS migration_history; SELECT schemaname, count(*) AS table_count FROM pg_catalog.pg_tables WHERE schemaname LIKE 'schema_%' GROUP BY schemaname ORDER BY schemaname;"

# Busca otras tablas de usuario en la base.

docker exec kynakee-postgres-dev psql -v ON_ERROR_STOP=1 -U kynakee_user_dev -d kynakee_db_dev -c "SELECT schemaname, tablename FROM pg_catalog.pg_tables WHERE schemaname NOT IN ('pg_catalog', 'information_schema') AND schemaname NOT LIKE 'pg_toast%' ORDER BY schemaname, tablename;"

# Identifica el directorio interno de Git. El respaldo se guardará allí, fuera de los archivos versionados y con acceso restringido.

git rev-parse --git-dir

# Crea una carpeta para el respaldo y restringe su acceso al usuario actual. La carpeta creada es .git\kynakee-private-backups\dev-20261103-224802.

$dir = Join-Path '.git\kynakee-private-backups' ('dev-' + (Get-Date -Format 'yyyyMMdd-HHmmss')); $null = New-Item -ItemType Directory -Path $dir -ErrorAction Stop; $sid = [System.Security.Principal.WindowsIdentity]::GetCurrent().User.Value; icacls $dir /inheritance:r /grant:r ("*$sid" + ':(OI)(CI)F'); if ($LASTEXITCODE -ne 0) { throw 'No se pudo restringir el directorio de respaldo.' }; Write-Output "BACKUP_DIRECTORY=$dir"

# Genera una copia de la base de desarrollo en formato PostgreSQL personalizado dentro del contenedor.

docker exec kynakee-postgres-dev pg_dump -U kynakee_user_dev -d kynakee_db_dev -Fc -f /tmp/kynakee-dev-before-migration-20261103-224802.dump

# Copia el respaldo al directorio privado del equipo.

docker cp kynakee-postgres-dev:/tmp/kynakee-dev-before-migration-20261103-224802.dump .git/kynakee-private-backups/dev-20261103-224802/before.dump

# Copia de nuevo el archivo guardado al contenedor para verificar esa copia, no solo el archivo original.

docker cp .git/kynakee-private-backups/dev-20261103-224802/before.dump kynakee-postgres-dev:/tmp/kynakee-dev-verify-20261103-224802.dump

# Verifica que pg_restore puede leer el catálogo del archivo guardado. No se ha probado una restauración completa.

docker exec kynakee-postgres-dev pg_restore --list /tmp/kynakee-dev-verify-20261103-224802.dump

# Comprueba los permisos del respaldo. El archivo muestra acceso heredado para el usuario actual.

icacls .git\kynakee-private-backups\dev-20261103-224802\before.dump

# Elimina únicamente las dos copias temporales creadas en el contenedor. El respaldo privado permanece en el equipo.

docker exec kynakee-postgres-dev rm -f /tmp/kynakee-dev-before-migration-20261103-224802.dump /tmp/kynakee-dev-verify-20261103-224802.dump

# Comprueba el estado de la API de desarrollo. El contenedor está en ejecución.

docker compose --env-file docker/.env.dev -f docker/docker-compose.dev.yml ps kynakee-api

# Comprueba si dotnet-ef está instalado en la ubicación global habitual del contenedor. No se ha encontrado allí; instalaré la versión utilizada en la aplicación anterior, en un directorio temporal del contenedor.

docker exec kynakee-api-dev sh -c "if [ -x /root/.dotnet/tools/dotnet-ef ]; then /root/.dotnet/tools/dotnet-ef --version; else echo EF_TOOL_NOT_INSTALLED; fi"

# Instala dotnet-ef 10.0.11 en un directorio temporal del contenedor, sin añadirlo al repositorio.

docker exec kynakee-api-dev dotnet tool install dotnet-ef --tool-path /tmp/kynakee-ef-tools --version 10.0.11

# Compila el módulo y aplica sus migraciones a la base configurada para la API dev. EF mostró un fallo inicial al consultar el historial inexistente, pero después creó el historial, informó de las tres migraciones y finalizó con Done. Comprobaré el resultado directamente en PostgreSQL.

docker exec -w /workspace kynakee-api-dev /tmp/kynakee-ef-tools/dotnet-ef database update --project src/Kynakee.Modules/Kynakee.Modules.Identity/Kynakee.Modules.Identity.csproj --startup-project src/Kynakee.Modules/Kynakee.Modules.Identity/Kynakee.Modules.Identity.csproj --context IdentityDbContext

# Consulta el historial de EF y las tablas de Identity. PostgreSQL registra las tres migraciones y muestra nueve tablas en schema_identity.

docker exec kynakee-postgres-dev psql -v ON_ERROR_STOP=1 -U kynakee_user_dev -d kynakee_db_dev -c "SELECT query_to_xml(format('SELECT * FROM public.%I ORDER BY 1', '__EFMigrationsHistory'), false, false, '') AS migration_history; SELECT tablename FROM pg_catalog.pg_tables WHERE schemaname = 'schema_identity' ORDER BY tablename;"

# Compara el modelo de Identity con la última migración. No se han detectado cambios pendientes.

docker exec -w /workspace kynakee-api-dev /tmp/kynakee-ef-tools/dotnet-ef migrations has-pending-model-changes --project src/Kynakee.Modules/Kynakee.Modules.Identity/Kynakee.Modules.Identity.csproj --startup-project src/Kynakee.Modules/Kynakee.Modules.Identity/Kynakee.Modules.Identity.csproj --context IdentityDbContext --no-build

# Compila Billing y aplica su migración pendiente. EF ha informado de InitialBillingSchema y ha finalizado con Done.

docker exec -w /workspace kynakee-api-dev /tmp/kynakee-ef-tools/dotnet-ef database update --project src/Kynakee.Modules/Kynakee.Modules.Billing/Kynakee.Modules.Billing.csproj --startup-project src/Kynakee.Modules/Kynakee.Modules.Billing/Kynakee.Modules.Billing.csproj --context BillingDbContext

# Consulta el historial de EF y las tablas de Billing. La migración figura en el historial y existen cinco tablas en schema_billing.

docker exec kynakee-postgres-dev psql -v ON_ERROR_STOP=1 -U kynakee_user_dev -d kynakee_db_dev -c "SELECT query_to_xml(format('SELECT * FROM public.%I ORDER BY 1', '__EFMigrationsHistory'), false, false, '') AS migration_history; SELECT tablename FROM pg_catalog.pg_tables WHERE schemaname = 'schema_billing' ORDER BY tablename;"

# Compara el modelo de Billing con su última migración. No se han detectado cambios pendientes.

docker exec -w /workspace kynakee-api-dev /tmp/kynakee-ef-tools/dotnet-ef migrations has-pending-model-changes --project src/Kynakee.Modules/Kynakee.Modules.Billing/Kynakee.Modules.Billing.csproj --startup-project src/Kynakee.Modules/Kynakee.Modules.Billing/Kynakee.Modules.Billing.csproj --context BillingDbContext --no-build

# Compila MCP y aplica sus migraciones pendientes. EF ha informado de las dos migraciones y ha finalizado con Done.

docker exec -w /workspace kynakee-api-dev /tmp/kynakee-ef-tools/dotnet-ef database update --project src/Kynakee.Modules/Kynakee.Modules.Mcp/Kynakee.Modules.Mcp.csproj --startup-project src/Kynakee.Modules/Kynakee.Modules.Mcp/Kynakee.Modules.Mcp.csproj --context McpDbContext

# Consulta el historial de EF y las tablas de MCP. Las dos migraciones figuran en el historial y existen tres tablas en schema_mcp.

docker exec kynakee-postgres-dev psql -v ON_ERROR_STOP=1 -U kynakee_user_dev -d kynakee_db_dev -c "SELECT query_to_xml(format('SELECT * FROM public.%I ORDER BY 1', '__EFMigrationsHistory'), false, false, '') AS migration_history; SELECT tablename FROM pg_catalog.pg_tables WHERE schemaname = 'schema_mcp' ORDER BY tablename;"

# Compara el modelo de MCP con su última migración. No se han detectado cambios pendientes.

docker exec -w /workspace kynakee-api-dev /tmp/kynakee-ef-tools/dotnet-ef migrations has-pending-model-changes --project src/Kynakee.Modules/Kynakee.Modules.Mcp/Kynakee.Modules.Mcp.csproj --startup-project src/Kynakee.Modules/Kynakee.Modules.Mcp/Kynakee.Modules.Mcp.csproj --context McpDbContext --no-build

# Muestra los servicios en ejecución. PostgreSQL y el gateway aparecen como saludables; la API está en ejecución, aunque este listado no muestra un estado de salud para ella.

docker compose --env-file docker/.env.dev -f docker/docker-compose.dev.yml ps

# Consulta la salud de la API desde la red de Compose. La respuesta ha sido Healthy, HTTP 200.

docker exec kynakee-gateway-dev curl --fail --silent --show-error --write-out ' HTTP %{http_code}\n' http://kynakee-api:8080/health

# Consulta la preparación del gateway dentro de su contenedor. La respuesta ha sido Healthy, HTTP 200.

docker exec kynakee-gateway-dev curl --fail --silent --show-error --write-out ' HTTP %{http_code}\n' http://localhost:8080/health/ready


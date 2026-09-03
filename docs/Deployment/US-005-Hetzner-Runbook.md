# US-005 — Runbook de Production en Hetzner

Este runbook cubre el primer despliegue de `kynakee-platform` en Hetzner. El único entorno remoto será production; development continuará ejecutándose localmente.

## Topología prevista

| Entorno | Servidor | Host público | Compose | Rama |
|---|---|---|---|---|
| Development | Equipo local | `localhost` | `docker/docker-compose.dev.yml` | `develop` |
| Production | Hetzner CX23 inicialmente | `api.kynakee.com` | `docker/docker-compose.prod.yml` | `master` |

El gateway es la única entrada HTTP pública. Traefik publica los puertos 80 y 443; `kynakee-api`, PostgreSQL, Redis, RabbitMQ y Qdrant no publican puertos al exterior.

El Compose staged se conserva en el repositorio como configuración no desplegada para una futura necesidad de pre-production.

## 1. Preparación en Hetzner

1. Crear un proyecto para production.
2. Crear una clave SSH en Hetzner usando una clave pública existente. Nunca subir la clave privada.
3. Crear un servidor **CX23**, imagen **Ubuntu 26.04 LTS**, en la región europea elegida.
4. Asociar un firewall de Hetzner con estas reglas de entrada:
   - TCP 22: únicamente desde la IP pública de administración, si se conoce.
   - TCP 80: cualquier origen.
   - TCP 443: cualquier origen.
   - Sin otras reglas de entrada.
5. Anotar la IP pública del servidor y reservarla si el plan de operación lo requiere.

> **Advertencia de capacidad:** CX23 tiene 2 vCPU y 4 GB de RAM. Es válido para el primer arranque y validación, pero los límites actuales de production superan esos recursos. Antes de considerar production estable se debe ampliar a CX32/CPX32 o superior, o revisar y medir los límites de todos los servicios.

## 2. DNS en Cloudflare

Crear este registro `A` en la zona `kynakee.com`:

- Production: `api` → IP pública del servidor de production.

Para la primera emisión de certificados con el HTTP challenge de Let’s Encrypt, usar **DNS only** (nube gris) y comprobar que los puertos 80 y 443 llegan directamente al servidor. Tras verificar el certificado, se puede activar el proxy de Cloudflare y configurar el modo SSL/TLS **Full (strict)**. No usar `Flexible`.

Comprobación desde el equipo local:

```powershell
Resolve-DnsName api.kynakee.com
```

La dirección devuelta debe coincidir con el servidor antes de iniciar Traefik.

## 3. Acceso inicial y Docker

Conectar usando la IP entregada por Hetzner:

```bash
ssh root@<IP_PRODUCTION>
```

Actualizar Ubuntu, crear un usuario administrativo y configurar Docker siguiendo la documentación oficial de Docker para Ubuntu 26.04. Como mínimo, verificar después:

```bash
sudo docker version
sudo docker compose version
```

Mantener el acceso root solo para la configuración inicial. El usuario administrativo debe usar la misma clave SSH y permisos `sudo`.

Configurar también actualizaciones de seguridad, zona horaria UTC y sincronización horaria. No desactivar el firewall del proveedor; si se usa UFW adicional, permitir solo `22/tcp`, `80/tcp` y `443/tcp` antes de activarlo.

## 4. Desplegar production

Como usuario administrativo, crear un directorio de despliegue y clonar la referencia aprobada:

```bash
sudo mkdir -p /opt/kynakee-platform
sudo chown "$USER":"$USER" /opt/kynakee-platform
cd /opt/kynakee-platform
git clone <URL_DEL_REPOSITORIO> .
git checkout master
cd docker
cp .env.prod.example .env.prod
chmod 600 .env.prod
```

Editar `.env.prod` en el servidor y reemplazar todos los valores `CHANGE_ME` por secretos únicos y largos. Como mínimo deben configurarse:

- `ACME_EMAIL`: correo operativo real.
- `API_HOST=api.kynakee.com`.
- `POSTGRES_DB`, `POSTGRES_USER` y `POSTGRES_PASSWORD`.
- `RABBITMQ_DEFAULT_USER` y `RABBITMQ_DEFAULT_PASS`.

No guardar secretos en commits, issues, capturas ni comandos almacenados en el historial.

Validar y arrancar:

```bash
docker compose --env-file .env.prod -f docker-compose.prod.yml config
docker compose --env-file .env.prod -f docker-compose.prod.yml up -d --build
docker compose --env-file .env.prod -f docker-compose.prod.yml ps
```

## 5. Verificación

Comprobar primero los contenedores y logs:

```bash
docker compose --env-file .env.prod -f docker-compose.prod.yml ps
docker compose --env-file .env.prod -f docker-compose.prod.yml logs --tail=100 traefik
docker compose --env-file .env.prod -f docker-compose.prod.yml logs --tail=100 kynakee-api
```

Comprobar el certificado y la redirección HTTPS desde un equipo externo:

```bash
curl -I http://api.kynakee.com
curl -I https://api.kynakee.com
curl -v https://api.kynakee.com/health
```

Criterios de aceptación:

- HTTP redirige a HTTPS.
- HTTPS presenta un certificado válido para `api.kynakee.com`.
- `/health` responde correctamente.
- `kynakee-api` no es accesible usando la IP pública y un puerto interno.
- Los datos sobreviven a `docker compose restart`.
- El archivo `.env.prod` no aparece en `git status` como archivo versionable.

## 6. Operación posterior al despliegue

1. Ampliar el CX23 a CX32/CPX32 o superior antes de considerar production estable, o aprobar límites reducidos después de medir el consumo.
2. Configurar copias de seguridad verificables de PostgreSQL y Qdrant.
3. Programar actualizaciones y comprobar periódicamente `docker compose ps` y los logs de Traefik.

## Bloqueos conocidos antes de cerrar US-005

- La aplicación y el gateway debían exponer `/health` porque ambos Compose lo usan en sus healthchecks; ya se ha añadido ese endpoint en los dos proyectos.
- El CX23 es una primera configuración económica; se debe medir y ampliar antes de declarar production estable.
- El despliegue requiere un plan de backups antes de guardar datos reales.
- Este runbook no contiene credenciales, IPs reales ni tokens de Hetzner o Cloudflare.

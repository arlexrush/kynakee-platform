# Kynakee — ADRs (Part 3)

> **Estado:** una decisión aprobada fija la dirección, no acredita su despliegue. Contrastar las afirmaciones de ejecución con [Estado y gobernanza](../Gobernanza/Estado-y-gobernanza.md); `/health` está mapeado en gateway y API, pero el arranque/CI, TLS y el tráfico real requieren evidencias de ejecución. La configuración `staged` no acredita despliegue.

## ADR-015 — Separación de repositorios y límites de publicación

**Estado:** Aprobada  
**Fecha:** 2026-09-02

`kynakee-platform` contiene el backend .NET 10, la infraestructura compartida, `Kynakee.Gateway` y `Kynakee.Api`. `kynakee-web` es un repositorio independiente para Next.js 15 y mantiene su propio Docker Compose.

En `kynakee-platform`:

- `Kynakee.Gateway` es un proyecto independiente y constituye la única entrada HTTP de la aplicación publicada; en desarrollo también se publican puertos de infraestructura y el inspector de ngrok.
- `Kynakee.Api` aloja los módulos de negocio y solo es accesible dentro de la red de Compose.
- El gateway y la API se ejecutan como contenedores separados.
- Este repositorio no compila ni arranca `kynakee-web`.

## ADR-016 — Compose por entorno y estrategia de ramas

**Estado:** Aprobada  
**Fecha:** 2026-09-02

Se mantienen tres archivos Compose con nombres estables:

- `docker/docker-compose.dev.yml` para desarrollo local, rama `develop`.
- `docker/docker-compose.staged.yml` se conserva para una futura necesidad de staging/pre-production, pero no se despliega actualmente.
- `docker/docker-compose.prod.yml` para producción en Hetzner, rama `master`.

En producción, el objetivo es publicar el tráfico de negocio a través de Traefik y el gateway. En el Compose de desarrollo PostgreSQL, Redis, RabbitMQ, Qdrant y el inspector de ngrok tienen puertos publicados en el host; la API permanece interna. Los secretos se suministran mediante archivos `.env.*` locales y nunca se versionan.

## ADR-017 — Health checks y validación de CI

**Estado:** Aprobada  
**Fecha:** 2026-09-02

`Kynakee.Gateway` y `Kynakee.Api` exponen `GET /health` mediante ASP.NET Core Health Checks. Los Compose usan este endpoint para comprobar la disponibilidad de sus servicios HTTP.

La integración continua se ejecuta en GitHub Actions para `develop` y para pull requests dirigidas a `develop` o `master`. Usa .NET 10, restaura y compila `Kynakee.slnx`, ejecuta las pruebas con cobertura Cobertura y falla si la cobertura global de líneas es inferior al 80 %.

## ADR-018 — Preparación inicial de production en Hetzner

**Estado:** Aprobada con seguimiento  
**Fecha:** 2026-09-02

El primer despliegue remoto se realizará en Hetzner con Ubuntu 26.04 LTS y CX23 como configuración inicial de coste reducido. CX23 debe considerarse una capacidad de arranque y validación: antes de declarar production estable se debe medir el consumo y ampliar a CX32/CPX32 o superior si los límites actuales no son suficientes.

La guía operativa está en [`docs/Deployment/US-005-Hetzner-Runbook.md`](../Deployment/US-005-Hetzner-Runbook.md). El runbook exige HTTPS mediante Traefik/Let's Encrypt, acceso externo solo por 80/443, backups verificables y comprobación de `/health`.

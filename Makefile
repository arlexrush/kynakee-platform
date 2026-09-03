COMPOSE := docker compose --env-file docker/.env.dev -f docker/docker-compose.dev.yml

.PHONY: dev stop logs migrate

dev:
	$(COMPOSE) up --build

stop:
	$(COMPOSE) down

logs:
	$(COMPOSE) logs -f

migrate:
	$(COMPOSE) run --rm kynakee-api dotnet ef database update --project src/Kynakee.Api/Kynakee.Api.csproj

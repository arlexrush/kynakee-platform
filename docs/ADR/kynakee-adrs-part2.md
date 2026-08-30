# KYNAKEE PLATFORM
## Architecture Decision Records — Part 2 of 2
### ADR-013 to ADR-024 · Implementation Decisions
**Version 1.4 · 2026-08-19 · Confidential**

---

## Table of Contents — Part 2

- [ADR-013 — Token/Credit Economic Model](#adr-013)
- [ADR-014 — Bot Channel Architecture (Telegram + WhatsApp)](#adr-014)
- [ADR-015 — Infrastructure: Docker on Hetzner](#adr-015)
- [ADR-016 — Authentication and Authorization Strategy](#adr-016)
- [ADR-017 — Observability: Serilog + OpenTelemetry](#adr-017)
- [ADR-018 — Soft Delete and Audit Trail Strategy](#adr-018)
- [ADR-019 — Resilience, Saga Pattern and Rollback Strategy](#adr-019)
- [ADR-020 — Dockerization Strategy — Environments and Compose Files](#adr-020)
- [ADR-021 — Git Branching Strategy (Gitflow)](#adr-021)
- [ADR-022 — API Versioning and Contract Strategy](#adr-022)
- [ADR-023 — Testing Strategy — Coverage, Types and Tools](#adr-023)
- [ADR-024 — GitHub Copilot Development Rules (Mandatory)](#adr-024)
- [Appendix A — GitHub Copilot Quick Reference](#appendix-a)

---

## ADR-013 — Token/Credit Economic Model Implementation {#adr-013}

| Field | Value |
|---|---|
| Status | Accepted |
| Date | 2026-08-19 |

Credit-based model. Each AI operation consumes credits. `TokenGateBehavior` in MediatR pipeline reserves credits BEFORE executing AI commands. On success: `Consume()`. On failure: `Release()`. Credits never lost on system errors.

### CreditAccount Aggregate (Billing Module)

```csharp
public class CreditAccount : BaseEntity<Guid>
{
    public decimal AvailableCredits { get; private set; }
    public decimal ReservedCredits { get; private set; }
    public string PlanId { get; private set; }

    public Result Reserve(decimal amount, Guid operationId) { ... }
    public Result Consume(Guid operationId) { ... }   // Confirms reservation
    public Result Release(Guid operationId) { ... }   // Cancels reservation (on failure)
    public Result Recharge(decimal amount, string source) { ... }
}
```

### Credit Consumption by Project Operation

| Project Operation | Credits (est.) | Source |
|---|---|---|
| Project.CompleteCapture() — 5 photos | 5–15 | Vision AI + transcription |
| Project.AddWorkItems() — APU cache hit | 1–3 | Minimal processing |
| Project.AddWorkItems() — new APU | 15–40 | LLM generation via AI module |
| Project.AssignAPUs() — cache | 0 | No AI call |
| Project.AssignAPUs() — new | 20–50 | LLM generation via AI module |
| Project.GenerateSchedule() | 5–15 | LLM sequencing via AI module |
| Project.ValueComponents() — MCP | 8–20 per component | MCP query |
| Project.ValueComponents() — cache | 0–2 | Cache lookup |
| Project.GenerateOffer() | 3–8 | LLM synthesis via AI module |

### Plans

| Plan | Credits/month | Price/month | Cost/credit |
|---|---|---|---|
| Starter | 500 | €29 | €0.058 |
| Pro | 2,000 | €89 | €0.045 |
| Studio | 6,000 | €199 | €0.033 |
| Business | 20,000 | €499 | €0.025 |

---

## ADR-014 — Bot Channel Architecture (Telegram + WhatsApp) {#adr-014}

| Field | Value |
|---|---|
| Status | Accepted |
| Date | 2026-08-19 |

Telegram uses `Telegram.Bot` NuGet (free). WhatsApp uses Meta Cloud API directly (free up to 1,000 conversations/month). The **Bots module is a thin translation layer**: it parses messages and dispatches Project commands via MediatR. **No business logic in Bots module.** All logic lives in the Project aggregate.

```csharp
public interface IBotChannel
{
    string ChannelId { get; }  // "telegram" | "whatsapp"
    Task SendMessageAsync(string conversationId, BotMessage message, CancellationToken ct);
    Task SendFileAsync(string conversationId, byte[] file, string filename, CancellationToken ct);
}

// Webhook: POST /webhooks/telegram  → parse → dispatch ICommand to Projects module
// Webhook: POST /webhooks/whatsapp  → parse → dispatch ICommand to Projects module
// Bot reads Project.CurrentPhase to determine what to show next.
```

### Bot Conversation State (SPA — System of Project Active)

```csharp
// Stored in schema_bots.bot_conversations
public enum ConversationState
{
    NewSession, Onboarding, MainMenu, InProject,
    InPhase, AwaitingInput, Paused, Completed
}
// ActiveProjectId links conversation to Project aggregate.
// Timeout: 30 min → Paused. 24h → ask to resume or change project.
// Bot reads Project.CurrentPhase to determine what to show next.
```

---

## ADR-015 — Infrastructure: Docker on Hetzner {#adr-015}

| Field | Value |
|---|---|
| Status | Accepted |
| Date | 2026-08-19 |

| Service | Image | Notes |
|---|---|---|
| traefik | traefik:v3 | 80, 443 — SSL, Let's Encrypt |
| kynakee-gateway | custom .NET 10 | 8080 — YARP reverse proxy and the only published HTTP entry point. Backend repository: `kynakee-platform`. |
| kynakee-api | custom .NET 10 | Internal application host for all 7 modules. Not published to the host. |
| kynakee-web | custom Next.js 15 | 3000 — Next.js server. Frontend repository: `kynakee-web`. |
| postgres | postgres:17 | 5432 — all module schemas |
| qdrant | qdrant/qdrant:latest | 6333 — KnowledgeBase module only |
| redis | redis:7-alpine | 6379 — cache + sessions |
| rabbitmq | rabbitmq:3-management | 5672, 15672 — message broker |

This platform uses two application containers with independent release lifecycles: the backend API in `kynakee-platform` and the web UI in `kynakee-web`.

### Multi-Region Strategy

Initial: Hetzner Falkenstein (EU). LATAM: Hetzner Ashburn (US East). PostgreSQL logical replication for read replicas. DNS geo-routing via Cloudflare.

---

## ADR-016 — Authentication and Authorization Strategy {#adr-016}

| Field | Value |
|---|---|
| Status | Accepted |
| Date | 2026-08-19 |

ASP.NET Core Identity + JWT. Access tokens: 15 min. Refresh tokens: 30 days (PostgreSQL). YARP validates JWT at the edge and injects trusted identity claims into the request context. All 7 modules trust the gateway for external authentication, but the application layer still enforces authorization, tenant scoping, and business-rule validation as a second security boundary.

This is a defense-in-depth model: the gateway authenticates callers and rejects invalid traffic; the API/application layer verifies permissions and domain invariants before executing commands or queries.

No module accepts untrusted requests as authoritative without tenant, role, and policy validation in the application boundary.

### JWT Claims

```json
{
  "sub": "user-uuid",
  "tenant_id": "tenant-uuid",
  "tenant_slug": "reformas-garcia",
  "plan_id": "plan_pro",
  "role": "owner | admin | technician | commercial | viewer",
  "channel": "web | telegram | whatsapp",
  "exp": 1234567890
}
```

### Authorization Policies

```
"CanCreateProject"  → owner | admin | technician
"CanEditWorkItems"  → owner | admin | technician
"CanGenerateOffer"  → owner | admin | technician | commercial
"CanManageUsers"    → owner | admin
"CanManagePlan"     → owner
"CanViewOnly"       → viewer (read-only projections of Project aggregate)
"IsKynakeeAdmin"    → kynakee_admin = true (special claim)
```

---

## ADR-017 — Observability: Serilog + OpenTelemetry {#adr-017}

| Field | Value |
|---|---|
| Status | Accepted |
| Date | 2026-08-19 |

Serilog structured logging (JSON). OpenTelemetry distributed tracing. All logs include TenantId, CorrelationId, UserId, and **ProjectId** (when in project context).

```csharp
// Every log entry MUST include:
using (LogContext.PushProperty("TenantId", tenantId))
using (LogContext.PushProperty("CorrelationId", correlationId))
using (LogContext.PushProperty("UserId", userId))
using (LogContext.PushProperty("ProjectId", projectId))  // When in project context
{
    _logger.Information("Phase {Phase} advanced for project {ProjectId}", phase, projectId);
}

// AI module additionally logs: Provider, Model, TokensConsumed, CreditsCharged, Duration
// MCP module additionally logs: ProviderId, ComponentType, FallbackActivated, Confidence
```

---

## ADR-018 — Soft Delete and Audit Trail Strategy {#adr-018}

| Field | Value |
|---|---|
| Status | Accepted |
| Date | 2026-08-19 |

All entities use soft delete (`IsDeleted` + `DeletedAt`). Hard deletes FORBIDDEN except for GDPR erasure via `GDPRDataErasureService`. EF Core `SaveChanges` interceptor auto-sets audit fields on all `BaseEntity<TId>` instances.

```csharp
public class AuditInterceptor : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(...)
    {
        foreach (var entry in context.ChangeTracker.Entries<BaseEntity<Guid>>())
        {
            if (entry.State == EntityState.Added)
                { entry.Entity.CreatedAt = now; entry.Entity.CreatedBy = userId; entry.Entity.UpdatedAt = now; }
            if (entry.State == EntityState.Modified)
                { entry.Entity.UpdatedAt = now; entry.Entity.UpdatedBy = userId; }
            if (entry.State == EntityState.Deleted)
                { entry.State = EntityState.Modified; entry.Entity.IsDeleted = true; entry.Entity.DeletedAt = now; }
        }
        return base.SavingChangesAsync(...);
    }
}
```

> **GDPR Compliance:** Hard deletes only via `GDPRDataErasureService` which anonymizes personal data while preserving anonymized APU data for the KnowledgeBase module.

---

## ADR-019 — Resilience, Saga Pattern and Rollback Strategy {#adr-019}

| Field | Value |
|---|---|
| Status | Accepted |
| Date | 2026-08-19 |

### Resilience Layers

| Layer | Tool | Applied to |
|---|---|---|
| Retry | Polly RetryPolicy | AI module, MCP module, Stripe, bot APIs |
| Circuit Breaker | Polly CircuitBreakerPolicy | MCP providers (3 failures → open 30s), AI providers |
| Timeout | Polly TimeoutPolicy | AI (30s), MCP (10s), Stripe (15s) |
| Fallback | Polly FallbackPolicy | AI: fallback to DeepSeek. MCP: fallback to cache. |
| Bulkhead | Polly BulkheadPolicy | AI calls (max 5 concurrent per tenant) |
| Outbox | MassTransit Outbox | All integration events — guaranteed delivery |
| Idempotency | IdempotencyKey table | All integration event handlers |

### Phase Workflow: Aggregate-Based (NOT Distributed Saga)

All 9 phase transitions are **SINGLE TRANSACTIONS** within the Project aggregate. No distributed saga between modules. Integration events (via Outbox) notify Billing and Bots **AFTER** the transaction commits.

```
Project.CompleteCapture()   → PhaseAdvancedEvent(Phase=Context)    [single transaction]
Project.BuildContext()      → PhaseAdvancedEvent(Phase=Scope)      [single transaction]
Project.AddWorkItems()      → PhaseAdvancedEvent(Phase=Production)  [single transaction]
Project.AssignAPUs()        → PhaseAdvancedEvent(Phase=Planning)   [single transaction]
Project.GenerateSchedule()  → PhaseAdvancedEvent(Phase=Valuation)  [single transaction]
Project.ValueComponents()   → PhaseAdvancedEvent(Phase=Review)     [single transaction]
Project.ApproveReview()     → ReviewApprovedEvent                  [single transaction]
Project.GenerateOffer()     → OfferGeneratedEvent → Billing + Bots via Outbox

// PhaseAdvancedEvent → Outbox → RabbitMQ → Billing (charge credits) + Bots (notify user)
// No eventual consistency between phases. Aggregate enforces all invariants.
```

### Compensation per Phase (Aggregate Handles Rollback)

```
F1 Capture failed:    Project.CurrentPhase stays at Capture. No credits consumed. Retry.
F3 Scope failed:      Project.Valuation = null (auto-invalidated). Release reserved credits.
F4 Production failed: APUAssignments generated so far kept in aggregate. Release credits.
F5 Planning failed:   Project.Schedule = null. Release credits. User can retry or skip.
F6 Valuation failed:  Project.Valuation = null. Release MCP credits. Fallback to cache.
F8 Offer failed:      Project.Offer = null. Release credits. Retry via Hangfire.

// Compensation handled by Project aggregate. No distributed rollback needed.
```

---

## ADR-020 — Dockerization Strategy — Environments and Compose Files {#adr-020}

| Field | Value |
|---|---|
| Status | Accepted |
| Date | 2026-08-19 |

| Environment | Branch | Infrastructure | Purpose | Key characteristics |
|---|---|---|---|---|
| Development | `develop` | Local + Docker Desktop + NGrok | Dev, integration, webhook testing | Hot reload, debug ports, NGrok for bot webhooks |
| Staging | `staged` | Hetzner CX21 | QA, UAT, production mirror | SSL, real bot webhooks, anonymized data, Traefik |
| Production | `master` | Hetzner CX31+ | Live system | HA, backups, full observability, SSL, rate limiting |

The branch naming is aligned to the environment lifecycle: `develop` drives development, `staged` validates release candidates, and `master` represents the production-ready state.

```
docker/
├── docker-compose.dev.yml      # Local: hot reload, NGrok, debug ports
├── docker-compose.staged.yml    # Hetzner: Traefik, SSL, real webhooks
├── docker-compose.prod.yml     # Hetzner: HA, health checks, resource limits
└── .env.dev / .env.staged / .env.prod  # Never committed to git
```

```dockerfile
# .NET API Dockerfile (multi-stage) — one Dockerfile for all 7 modules
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/Kynakee.Api/Kynakee.Api.csproj", "src/Kynakee.Api/"]
RUN dotnet restore && dotnet publish -c Release -o /app/publish
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Kynakee.Api.dll"]
```

---

## ADR-021 — Git Branching Strategy (Gitflow) {#adr-021}

| Field | Value |
|---|---|
| Status | Accepted |
| Date | 2026-08-19 |

| Branch | Environment | Purpose |
|---|---|---|
| `master` | Production | Protected production branch. Only release candidates or hotfixes merge here after validation. |
| `staged` | Staging | Pre-production validation branch. Receives approved release candidates and regression checks before production. |
| `develop` | Development | Integration branch for ongoing work. All feature branches merge here first. |
| `feature/*` | Development | New features and non-critical enhancements. Branch from `develop`. PR back to `develop`. |
| `release/*` | Staging | Final stabilization for a release. Branch from `develop`, merge to `staged` and then to `master`. |
| `hotfix/*` | Production | Emergency fixes for live defects. Branch from `master`, merge back to `master`, `staged`, and `develop`. |

```
feature/projects-phase-valuation → develop → staged → release/1.0.0 → master
hotfix/credit-release-bug → master → staged → develop

// Commit format: type(scope): description
// e.g.: feat(projects): add WorkItem.AddWorkItem with Valuation invalidation
// e.g.: fix(billing): correct credit reservation race condition in TokenGateBehavior
```

Gitflow is followed strictly: `develop` is the integration line for day-to-day work, `staged` is the release gate for QA/UAT, and `master` is the immutable production branch. The production and staging environments are therefore represented by the `master` and `staged` branches respectively.

---

## ADR-022 — API Versioning and Contract Strategy {#adr-022}

| Field | Value |
|---|---|
| Status | Accepted |
| Date | 2026-08-19 |

OpenAPI 3.1 (Swashbuckle). URL versioning: `/api/v1/{module}/{resource}`. RFC 7807 Problem Details. All Project phase operations are commands dispatched via MediatR.

```csharp
// Standard response envelope
public record ApiResponse<T>(T Data, string TraceId, DateTime Timestamp);

// Key Project endpoints — each maps to a Project aggregate command:
POST /api/v1/projects                    → CreateProjectCommand
POST /api/v1/projects/{id}/capture       → CompleteCaptureCommand
POST /api/v1/projects/{id}/work-items    → AddWorkItemCommand
POST /api/v1/projects/{id}/apu-assign    → AssignAPUsCommand
POST /api/v1/projects/{id}/schedule      → GenerateScheduleCommand
POST /api/v1/projects/{id}/valuation     → ValueComponentsCommand
POST /api/v1/projects/{id}/review        → ApproveReviewCommand
POST /api/v1/projects/{id}/offer         → GenerateOfferCommand
GET  /api/v1/projects/{id}               → GetProjectQuery (projection, NOT full aggregate)
```

---

## ADR-023 — Testing Strategy — Coverage, Types and Tools {#adr-023}

| Field | Value |
|---|---|
| Status | Accepted |
| Date | 2026-08-19 |

Minimum **80% code coverage** (line + branch + method). **Project aggregate domain logic is the highest-priority unit test target.**

| Type | Tool | Scope |
|---|---|---|
| Unit Tests | xUnit + FluentAssertions + NSubstitute | Project aggregate invariants, domain events, value objects, handlers. No I/O. |
| Integration Tests | xUnit + Testcontainers + WebApplicationFactory | Full HTTP + real PostgreSQL + Redis. Per module. |
| Contract Tests | PactNet | Integration event schemas: Projects→Billing, Projects→Bots. |
| E2E Tests (Backend) | xUnit + WebApplicationFactory + Playwright optional | API lifecycle, contracts, tenant flows, and backend system behavior. |
| E2E Tests (Frontend) | Playwright (TypeScript) | Full project lifecycle via browser in `kynakee-web`. Staging only. |

```
# Backend repo: kynakee-platform
/tests/
├── Kynakee.UnitTests/
│   ├── Modules/Projects/Domain/      # Project aggregate invariants (HIGHEST PRIORITY)
│   ├── Modules/Projects/Application/ # Command/query handlers
│   ├── Modules/Billing/
│   └── Modules/Identity/
├── Kynakee.IntegrationTests/
│   ├── Modules/Projects/             # Full HTTP + DB tests
│   └── Modules/Billing/
├── Kynakee.ContractTests/
│   └── Events/                       # Projects→Billing, Projects→Bots contracts
└── Kynakee.E2ETests/
    └── Journeys/                     # Backend/system lifecycle validation

# Frontend repo: kynakee-web
/e2e/
├── auth/
├── projects/
├── billing/
└── journeys/                         # Browser E2E flow validation
```

This is intentionally duplicated at the repository level: both repos own the E2E coverage relevant to their runtime boundary. The backend validates platform behavior and API contract flows; the frontend validates the UX and browser journeys. 

---

## ADR-024 — GitHub Copilot Development Rules (Mandatory) {#adr-024}

| Field | Value |
|---|---|
| Status | Accepted |
| Date | 2026-08-19 |

Authoritative contract for all code generation. Encoded in `.github/copilot-instructions.md`. **Violations are grounds for PR rejection.**

### SOLID and Clean Code

- Apply SOLID strictly: Single Responsibility, Open/Closed, Liskov, Interface Segregation, Dependency Inversion.
- Clean Code: meaningful names, functions <20 lines, no magic numbers, self-documenting code.
- **No business logic in Controllers/Endpoints.** Only: receive → dispatch to MediatR → return response.
- No database access from UI layer (Next.js). All data via API calls.

### Architecture Rules

- Use CQRS for ALL operations via MediatR. No service classes mixing reads and writes.
- ALL Commands MUST have `AbstractValidator<TCommand>` in the MediatR pipeline.
- ALL endpoints documented with OpenAPI attributes and XML comments.
- ALL responses typed. No `object`, no `dynamic`, no anonymous types in API responses.
- Avoid Repository Pattern. Use EF Core DbContext directly unless complex query justifies abstraction.
- Implement CorrelationId on EVERY request. Propagate to all downstream calls, logs, and events.

### Project Aggregate Rules (CRITICAL)

- **The Project aggregate is the ONLY place where phase transition logic lives.** Never implement phase logic in application services or controllers.
- **Changing a WorkItem MUST automatically invalidate `Project.Valuation` and `Project.Schedule`.** This is enforced by the aggregate, not by the caller.
- **ALL Project mutations go through the aggregate root.** Never modify WorkItem, Schedule, Valuation, or Offer directly via DbContext.
- **Read queries use projections (DTOs).** Never load the full Project aggregate for read-only operations.

### Event and Messaging Rules

- Domain Events: intra-module side effects. Raise from aggregate, handle in same transaction.
- Integration Events: cross-module (Projects→Billing, Projects→Bots). Publish via MassTransit Outbox ONLY.
- ALL integration event handlers MUST be idempotent. Check IdempotencyKey before processing.
- NEVER publish directly to RabbitMQ. Always use MassTransit abstraction.

### Module Rules (7 Modules)

- **7 modules: Projects, KnowledgeBase, MCP, AI, Bots, Billing, Identity.** Design each for future microservice extraction.
- ALL persistent entities MUST inherit `BaseEntity<TId>` with TenantId, CreatedAt, UpdatedAt, IsDeleted, DeletedAt.
- NEVER call `DbContext.Remove()`. Use soft delete via `IsDeleted = true`.
- ALL AI operations via `IKynakeeAgentService` (AI module). NEVER call Microsoft Agents Framework directly from Projects or any other module.
- ALL MCP queries via `IMCPClient` (MCP module). NEVER call provider endpoints directly.

### Quality Rules

- Minimum 80% test coverage. **Project aggregate domain logic is highest-priority test target.**
- ALL log entries MUST include TenantId, CorrelationId, UserId, and ProjectId via Serilog LogContext.
- No hardcoded configuration values. All config via `appsettings.json` + environment variables.
- No secrets committed to git. Use `.env` files (gitignored).

---

## Appendix A — GitHub Copilot Quick Reference {#appendix-a}

Condensed rules for `.github/copilot-instructions.md`.

### Non-Negotiable Rules (14)

1. Every entity: inherit `BaseEntity<TId>` with TenantId, CreatedAt, UpdatedAt, IsDeleted, DeletedAt.
2. Every handler: return `Result<T>`. Never throw for business logic.
3. Every command: `AbstractValidator<TCommand>` in MediatR pipeline.
4. **Project aggregate: ALL phase logic inside the aggregate. Never in services or controllers.**
5. **Project aggregate: changing WorkItem MUST invalidate Valuation and Schedule automatically.**
6. **Project reads: use projections (DTOs). Never load full aggregate for reads.**
7. **7 modules: Projects, KnowledgeBase, MCP, AI, Bots, Billing, Identity.** Inter-module via interfaces or integration events only.
8. AI: ONLY via `IKynakeeAgentService` (AI module). Never call Microsoft Agents Framework directly.
9. MCP: ONLY via `IMCPClient` (MCP module). Never call provider endpoints directly.
10. Events: ONLY via MassTransit Outbox. Never publish directly to RabbitMQ.
11. Deletes: ONLY soft delete. Never `DbContext.Remove()` on business entities.
12. Endpoints: ONLY Minimal API style in `IModuleEndpoints`. Never controllers with business logic.
13. Logs: ALWAYS include TenantId, CorrelationId, UserId, ProjectId via Serilog LogContext.
14. Tests: ALWAYS write unit tests. **Project aggregate domain logic is highest priority.**

---

*KYNAKEE PLATFORM · ADRs Part 2 (ADR-013 to ADR-024) · v1.4 · 2026-08-19*  
*See Part 1 for ADR-001 to ADR-012*
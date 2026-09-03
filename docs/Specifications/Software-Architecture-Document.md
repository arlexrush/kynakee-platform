# KYNAKEE PLATFORM
## Software Architecture Document (SAD)
### Modular Monolith · DDD · CQRS · Event-Driven · .NET 10 · Next.js 15
**Version 1.0 · 2026-08-20 · Confidential**

---

## Table of Contents

- [1. Document Purpose and Scope](#s1)
- [2. Architectural Goals and Constraints](#s2)
- [3. System Context (C4 Level 1)](#s3)
- [4. Container Architecture (C4 Level 2)](#s4)
- [5. Component Architecture (C4 Level 3)](#s5)
- [6. Module Architecture — 7 Bounded Contexts](#s6)
  - [6.1 Projects Module (Core Domain)](#s6-1)
  - [6.2 KnowledgeBase Module](#s6-2)
  - [6.3 MCP Module](#s6-3)
  - [6.4 AI Module](#s6-4)
  - [6.5 Bots Module](#s6-5)
  - [6.6 Billing Module](#s6-6)
  - [6.7 Identity Module](#s6-7)
- [7. Data Architecture](#s7)
- [8. Integration Architecture](#s8)
- [9. Security Architecture](#s9)
- [10. Deployment Architecture](#s10)
- [11. Observability Architecture](#s11)
- [12. Resilience Architecture](#s12)
- [13. AI Architecture](#s13)
- [14. Quality Attributes (Non-Functional Requirements)](#s14)
- [15. Technology Decisions Summary](#s15)

---

## 1. Document Purpose and Scope {#s1}

This Software Architecture Document (SAD) describes the complete technical architecture of the Kynakee platform — an AI-powered construction budget generation system. It serves as the authoritative reference for all development decisions and is designed to enable GitHub Copilot-assisted development with minimal ambiguity.

**Scope:** Kynakee v1.0 MVP — Modular Monolith deployed on Hetzner Cloud. Covers all 7 modules, 9 operational phases, AI integration, MCP provider network, bot channels, billing, and identity. The frontend is intentionally split into the repository `kynakee-web`, while this repository manages the backend platform and shared infrastructure.

| Attribute | Value |
|---|---|
| Architecture Style | Modular Monolith with Vertical Slice Architecture |
| Domain Approach | Domain-Driven Design (DDD) — Tactical Patterns |
| Communication (sync) | REST via YARP Reverse Proxy |
| Communication (async) | RabbitMQ via MassTransit + Outbox Pattern |
| Primary Language | .NET 10 (C#) + Next.js 15 (TypeScript) |
| Repository Split | `kynakee-platform` (backend) + `kynakee-web` (frontend) |
| Database | PostgreSQL 17 (relational) + Qdrant (vector) |
| Infrastructure | Docker on Hetzner Cloud |
| Target Environments | Development (local) · Production (Hetzner) |

---

## 2. Architectural Goals and Constraints {#s2}

### Quality Attribute Goals

| Quality Attribute | Target | Architectural Decision |
|---|---|---|
| Maintainability | High | Modular Monolith + Vertical Slice + DDD. Each module extractable to microservice. |
| Scalability | Vertical first, horizontal later | Backend and frontend scale independently as separate Docker containers. Scale the API vertically on Hetzner before horizontal. |
| Reliability | 99.5% uptime (production) | Polly resilience policies. MassTransit Outbox. Hangfire retry. |
| Security | Multi-tenant isolation | Row-Level Security via TenantId. JWT via YARP. EF Core global query filters. |
| Observability | Full tracing | Serilog + OpenTelemetry. CorrelationId on every request. |
| AI Compliance | AI Act Art. 4,12,13,14,50 | AgentRun audit trail. Mandatory human review (Phase 7). Transparency disclosures. |
| Developer Experience | GitHub Copilot-ready | Consistent patterns. ADRs. Copilot instructions file. |

### Constraints

- **Team:** 1 .NET developer + GitHub Copilot. Architecture must be manageable by a single developer.
- **Timeline:** 1-month MVP. No time for microservices complexity.
- **Budget:** Cost-optimized. Hetzner CX23 initially. No expensive managed services.
- **AI Act compliance** mandatory from day 1 (Art. 4, 12, 13, 14, 50 already enforceable).
- **Multi-tenant from day 1.** No single-tenant shortcuts.
- **Multi-region from day 1.** Architecture must support EU + LATAM without redesign.

---

## 3. System Context (C4 Level 1) {#s3}

The Kynakee platform sits at the center of a network of external actors and systems.

| External Actor/System | Interaction | Protocol |
|---|---|---|
| Reformador (end user) | Creates projects, captures evidence, reviews results, receives offers | HTTPS (Web App), WhatsApp, Telegram |
| WhatsApp (Meta Cloud API) | Receives/sends bot messages. Webhook for incoming messages. | HTTPS webhooks + Meta Graph API |
| Telegram | Receives/sends bot messages. Webhook for incoming messages. | HTTPS webhooks + Telegram Bot API |
| DeepSeek API | LLM calls for APU generation, scope extraction, planning, offers | HTTPS REST (OpenAI-compatible) |
| Google Gemini API | LLM fallback + text embeddings (768d) for KnowledgeBase | HTTPS REST |
| Google Gemma | Lightweight LLM for bot conversations | HTTPS REST or local |
| OpenAI API | Premium vision + embedding fallback | HTTPS REST |
| MCP Provider Network | Price queries for materials, equipment, transport | HTTPS + JSON-RPC 2.0 (MCP protocol) |
| Stripe | Payment processing, subscriptions, webhooks | HTTPS REST + Stripe SDK |
| Cloudflare | DNS, CDN, DDoS protection, geo-routing | DNS + HTTPS |
| Let's Encrypt (via Traefik) | SSL certificate provisioning | ACME protocol |

---

## 4. Container Architecture (C4 Level 2) {#s4}

The platform is composed of separate runtime services running as independent Docker containers. This repository owns the backend and shared infrastructure components; the user interface is maintained in the separate repository `kynakee-web` and runs as its own container.

| Container | Technology | Responsibility |
|---|---|---|
| kynakee-gateway | ASP.NET Core 10 (.NET 10) | YARP reverse proxy and the only published HTTP entry point. Port 8080. |
| kynakee-api | ASP.NET Core 10 (.NET 10) | Internal application host for all 7 modules. Port 8080, not published to the host. |
| kynakee-web | Next.js 15 (Node.js) | React frontend. App Router. Server + Client Components. Port 3000. Repository: `kynakee-web`. |
| postgres | PostgreSQL 17 | Primary relational database. All 8 module schemas. Port 5432. |
| qdrant | Qdrant 1.x | Vector database for KnowledgeBase module. Port 6333. |
| redis | Redis 7 | Distributed cache, session store, rate limiting. Port 6379. |
| rabbitmq | RabbitMQ 3.13 | Message broker for integration events. Ports 5672, 15672. |
| traefik | Traefik 3.x | Reverse proxy, SSL termination, Let's Encrypt. Ports 80, 443. |

### Container Communication

```
Browser/Bot Client
    ↓ HTTPS
Traefik (SSL termination)
    ↓ HTTP
kynakee-gateway (YARP Gateway)
    ↓ HTTP (internal Docker network)
kynakee-api (module host)
    ├── Internal: Routes to module handlers (in-process)
    ├── PostgreSQL: EF Core (TCP 5432)
    ├── Qdrant: HTTP REST (6333)
    ├── Redis: StackExchange.Redis (6379)
    ├── RabbitMQ: MassTransit AMQP (5672)
    ├── DeepSeek/Gemini/OpenAI: HTTPS (external)
    └── MCP Providers: HTTPS JSON-RPC (external)

kynakee-web
    └── kynakee-api: HTTPS REST + SignalR WebSocket
```

---

## 5. Component Architecture (C4 Level 3) {#s5}

Inside the `kynakee-api` container, components interact via in-process calls (MediatR) and async events (MassTransit).

### YARP Gateway Layer

```
Incoming Request
    ↓
YARP Reverse Proxy
    ├── JWT Validation Middleware
    ├── Rate Limiting Middleware (Redis)
    ├── Correlation ID Middleware
    ├── Request Logging Middleware (Serilog)
    └── Route to Module Endpoint
```

### Module Request Pipeline

```
Module Minimal API Endpoint
    ↓ dispatch via MediatR
MediatR Pipeline Behaviors (in order):
    1. LoggingBehavior
    2. ValidationBehavior (FluentValidation)
    3. TenantIsolationBehavior (inject TenantId from JWT)
    4. TokenGateBehavior (reserve credits for AI commands)
    5. TransactionBehavior (begin DB transaction for commands)
    6. DomainEventDispatchBehavior (dispatch after commit)
    ↓
Command/Query Handler
    ├── Domain Logic (Project aggregate)
    ├── EF Core DbContext (PostgreSQL)
    ├── IKynakeeAgentService (AI module)
    ├── IMCPClient (MCP module)
    └── MassTransit Outbox (integration events)
```

### Background Processing

```
MassTransit Outbox Processor (background)
    └── Reads schema_shared.outbox_messages
    └── Publishes to RabbitMQ
    └── Marks as processed

MassTransit Consumers (background)
    └── Subscribes to RabbitMQ queues
    └── Handles integration events (idempotent)

Hangfire (background jobs)
    └── Retry failed AI operations
    └── Subscription renewal
    └── Offer PDF generation retry
```

---

## 6. Module Architecture — 7 Bounded Contexts {#s6}

Each module is a self-contained vertical slice with its own domain model, application layer, infrastructure, and API endpoints. Modules communicate **only** via public interfaces or integration events.

### 6.1 Projects Module (Core Domain) {#s6-1}

| Attribute | Value |
|---|---|
| Type | Core Domain |
| Aggregate Root | Project (rich aggregate owning all 9 phase entities) |
| Schema | schema_projects |
| Key Commands | CreateProject, CompleteCapture, SetContext, AddWorkItem, AssignAPU, SetSchedule, SetValuation, ApproveReview, SetOffer |
| Key Queries | GetProject, GetProjectSummary, GetWorkItems, GetValuation, GetOffer |
| Integration Events Published | ProjectCreated, PhaseAdvanced, WorkItemAdded, APUGenerated, OfferGenerated |
| Integration Events Consumed | CreditsConsumed, CreditDepleted, MCPProviderFailed, BotMessageReceived |

```
Kynakee.Modules.Projects/
├── Domain/Aggregates/Project.cs          # Aggregate root
├── Domain/Entities/                      # 8 phase entities
├── Domain/ValueObjects/                  # Money, Confidence, etc.
├── Domain/Events/                        # 9 domain events
├── Domain/Services/                      # TokenGate, Confidence, Fallback
├── Application/Commands/                 # 9 phase commands
├── Application/Queries/                  # Read projections
├── Application/EventHandlers/            # Integration event consumers
├── Infrastructure/Persistence/           # EF Core, migrations
└── Api/Endpoints/                        # Minimal API
```

> **⚠️ Critical Invariant:** Modifying a WorkItem automatically invalidates Valuation, Schedule, Review, and Offer. Enforced by `Project.InvalidateDownstreamResults()`. Never bypass this via direct DbContext access.

### 6.2 KnowledgeBase Module {#s6-2}

| Attribute | Value |
|---|---|
| Type | Supporting Domain |
| Aggregates | APUTemplate, CanonicalConcept |
| Storage | PostgreSQL (schema_knowledge_base) + Qdrant (vector embeddings 768d) |
| Key Operations | SearchAPUTemplate, SaveAPUTemplate, SearchCanonicalConcept, GetTranslations |
| Integration Events Consumed | WorkItemAdded (triggers APU lookup), APUGenerated (saves new template) |

APUTemplate stores component structures with yields but **NO prices**. Prices are determined per-project in Phase 6. Qdrant stores 768d embeddings for semantic search across languages.

### 6.3 MCP Module {#s6-3}

| Attribute | Value |
|---|---|
| Type | Supporting Domain |
| Aggregate | MCPProvider |
| Schema | schema_mcp |
| Protocol | HTTP + JSON-RPC 2.0 (MCP standard) |
| Resilience | Polly: 3 retries, circuit breaker (3 failures → suspend 30s), timeout 10s |
| Auto-suspension | Provider suspended after 3 consecutive failures. RecordSuccess() resets. |
| Integration Events Published | MCPProviderFailed |

The MCP module is a **stateless price query service**. It receives component queries from the Projects module and returns price data. It has no knowledge of Project state.

### 6.4 AI Module {#s6-4}

| Attribute | Value |
|---|---|
| Type | Supporting Domain |
| Framework | Microsoft Agents Framework |
| Interface | IKynakeeAgentService (only entry point for AI from any module) |
| Agents | CaptureAgent, ScopeAgent, ProductionAgent, PlanningAgent, ValuationAgent, OfferAgent, ConversationAgent, EmbeddingService |
| Primary LLM | DeepSeek-V3 (deepseek-chat) |
| Vision/Conversation | Gemini Flash (gemini-2.0-flash) |
| Bot Conversation | Gemma 4 (lightweight, free) |
| Embeddings | Gemini text-embedding-004 (768d) |
| Universal Fallback | DeepSeek-V3 for all chat agents |
| Audit | AgentRun entity records every AI call (AI Act Art. 12) |

### 6.5 Bots Module {#s6-5}

| Attribute | Value |
|---|---|
| Type | Supporting Domain |
| Channels | Telegram (Telegram.Bot NuGet) + WhatsApp (Meta Cloud API) |
| Aggregate | BotConversation (SPA — System of Project Active) |
| Schema | schema_bots |
| Webhook endpoints | POST /webhooks/telegram, POST /webhooks/whatsapp |
| Architecture | Thin translation layer. No business logic. Dispatches Project commands via MediatR. |
| Integration Events Published | BotMessageReceived |
| Integration Events Consumed | PhaseAdvanced, CreditDepleted, OfferGenerated |

### 6.6 Billing Module {#s6-6}

| Attribute | Value |
|---|---|
| Type | Supporting Domain |
| Aggregates | CreditAccount |
| Entities | Subscription, CreditTransaction |
| Schema | schema_billing |
| Payment | Stripe SDK (.NET). Subscriptions + Payment Intents. |
| Credit Flow | Reserve() → Consume() on success \| Release() on failure |
| Integration Events Published | CreditsConsumed, CreditDepleted, SubscriptionRenewed |
| Integration Events Consumed | ProjectCreated, PhaseAdvanced, TenantCreated |

### 6.7 Identity Module {#s6-7}

| Attribute | Value |
|---|---|
| Type | Generic Subdomain |
| Aggregates | Tenant |
| Entities | User, TenantUser |
| Schema | schema_identity |
| Auth | ASP.NET Core Identity + JWT Bearer. Access: 15min. Refresh: 30 days. |
| Integration Events Published | TenantCreated, UserRegistered |

---

## 7. Data Architecture {#s7}

### PostgreSQL 17 — Schema Organization

Each module owns its own PostgreSQL schema. **Cross-schema queries are FORBIDDEN.**

| Schema | Module | Key Tables |
|---|---|---|
| schema_projects | Projects | projects, work_items, apu_assignments, schedules, valuations, reviews, offers, capture_expedients, project_contexts |
| schema_knowledge_base | KnowledgeBase | canonical_concepts, concept_translations, apu_templates, apu_template_components |
| schema_mcp | MCP | mcp_providers, mcp_query_logs, provider_ratings |
| schema_ai | AI | agent_runs, token_consumption_logs |
| schema_bots | Bots | bot_conversations, bot_messages |
| schema_billing | Billing | credit_accounts, credit_transactions, subscriptions, stripe_events |
| schema_identity | Identity | tenants, users, tenant_users, refresh_tokens |
| schema_shared | Shared | outbox_messages, inbox_messages, idempotency_keys |

### EF Core Configuration Rules

- Code First ONLY. All changes via migrations. Never modify DB directly.
- Soft deletes: `IsDeleted` + `DeletedAt`. Global query filter on all entities.
- Optimistic concurrency: `xmin` (PostgreSQL row version) on aggregate roots.
- Mandatory indexes: `TenantId`, `CreatedAt`, `IsDeleted` on every table.
- `AuditInterceptor` auto-sets `CreatedAt`, `UpdatedAt`, `CreatedBy`, `UpdatedBy`, `IsDeleted`.

### Qdrant Vector Database

Used exclusively by the KnowledgeBase module.

| Collection | Vector Dimensions | Purpose |
|---|---|---|
| canonical_concepts | 768d (Gemini text-embedding-004) | Semantic search of work item concepts across languages |
| apu_structures | 768d (Gemini text-embedding-004) | Find similar APU templates by description and context |
| project_contexts | 768d (Gemini text-embedding-004) | Match project context to relevant APU templates |

> **Privacy:** Qdrant stores ONLY anonymized technical data. No tenant-identifiable data, no client data, no pricing data.

---

## 8. Integration Architecture {#s8}

### Synchronous Communication — REST

All external-facing APIs are REST over HTTPS, routed through YARP. Internal module communication is **in-process via MediatR** (no HTTP between modules).

```
External: HTTPS → Traefik → YARP → Module Endpoint → MediatR Handler
Internal: MediatR.Send(command) → Handler (in-process, same process)

// NEVER: HttpClient between modules in the same process
// ALWAYS: MediatR for in-process, RabbitMQ for cross-module async
```

### Asynchronous Communication — RabbitMQ + MassTransit

```
Publisher (Projects module):
    1. Command handler commits DB transaction
    2. Domain events dispatched in-process
    3. Integration event written to schema_shared.outbox_messages (same transaction)
    4. MassTransit Outbox Processor reads outbox and publishes to RabbitMQ

Consumer (Billing module):
    1. MassTransit receives message from RabbitMQ
    2. Check schema_shared.idempotency_keys (prevent duplicate processing)
    3. Execute handler logic
    4. Mark idempotency key as processed
```

### MCP Provider Integration

```
Kynakee MCP Client → HTTPS POST https://{subdomain}.kynakee.com/mcp
Request:  { "jsonrpc": "2.0", "method": "queryPrice", "params": { ... }, "id": "uuid" }
Response: { "jsonrpc": "2.0", "result": { "price": 28.50, "unit": "m2", ... }, "id": "uuid" }

Polly policy: 3 retries (exponential backoff) + circuit breaker (3 failures → open 30s)
```

---

## 9. Security Architecture {#s9}

### Authentication Flow

```
POST /api/v1/identity/auth/login
    → Validate credentials (ASP.NET Core Identity)
    → Generate JWT (15 min) + Refresh Token (30 days)
    → Store Refresh Token in schema_identity.refresh_tokens

Subsequent requests:
    → Bearer JWT in Authorization header
    → YARP validates JWT signature + expiry at the edge
    → Injects TenantId, UserId, Role into request context
    → API endpoints enforce policy checks (role, tenant, feature flags)
    → TenantIsolationBehavior (MediatR) sets ITenantContext
    → EF Core global query filter applies TenantId automatically
```

The security model is layered:
- Gateway: authentication, TLS, rate limiting, edge validation, request routing.
- Application/API: authorization, tenant validation, business-rule enforcement, command/query validation.
- Database: final data isolation with TenantId global filters and row-level protections.


### Multi-Tenant Data Isolation

- Every entity inherits `BaseEntity<TId>` with `TenantId` (MANDATORY).
- EF Core global query filter: `WHERE tenant_id = @tenantId AND is_deleted = false`.
- `TenantIsolationBehavior` in MediatR pipeline enforces TenantId on every command/query.
- Cross-tenant queries FORBIDDEN. Admin bypass requires explicit `IsAdmin = true` claim.
- Background jobs carry TenantId in job payload. Never infer from context.

### API Security Controls

| Control | Implementation |
|---|---|
| Authentication | JWT Bearer via YARP. All endpoints require valid JWT except /auth/* and /webhooks/*. |
| Authorization | Policy-based: CanCreateProject, CanEditWorkItems, CanGenerateOffer, CanManageUsers, CanManagePlan, CanViewOnly. |
| Rate Limiting | Per-tenant sliding window via Redis. Configurable per plan tier. |
| Input Validation | FluentValidation on all commands. YARP rejects malformed requests. |
| HTTPS | Traefik enforces HTTPS. HTTP redirected to HTTPS. HSTS enabled. |
| Secrets | No secrets in code or appsettings.json. .env files (gitignored). Vault for production. |

---

## 10. Deployment Architecture {#s10}

### Environment Strategy

| Environment | Branch | Infrastructure | Compose File | Key Differences |
|---|---|---|---|---|
| Development | `develop` | Local Docker Desktop + NGrok | docker-compose.dev.yml | Hot reload, debug ports, NGrok for bot webhooks, no SSL |
| Production | `master` | Hetzner CX23 initially | docker-compose.prod.yml | Traefik SSL, backups, observability, resource limits |

The operational environment uses `develop` for local development and `master` for production. The staged Compose remains available for a future pre-production environment but is not deployed.

### Production Docker Compose (key settings)

```yaml
kynakee-gateway:
  restart: unless-stopped
  deploy:
    resources:
      limits: { cpus: "2.0", memory: "2G" }
  healthcheck:
    test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
    interval: 30s
    retries: 3
  labels:
    - "traefik.http.routers.gateway.rule=Host(`api.kynakee.com`)"
    - "traefik.http.routers.gateway.tls.certresolver=letsencrypt"

postgres:
  volumes: ["postgres_data:/var/lib/postgresql/data"]
  environment:
    POSTGRES_DB: kynakee_prod
    POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
```

### Multi-Region Strategy

- **Initial:** Hetzner Falkenstein (EU, Germany). Covers Spain + EU market.
- **LATAM expansion:** Hetzner Ashburn (US East) or partner DC in Mexico/Colombia.
- PostgreSQL logical replication for read replicas in secondary regions.
- Qdrant cluster mode when KnowledgeBase grows beyond single-node capacity.
- DNS geo-routing via Cloudflare. Users routed to nearest region automatically.
- **No code changes required for multi-region.** Configuration-only expansion.

---

## 11. Observability Architecture {#s11}

### Logging — Serilog

```csharp
// Mandatory context on every log entry:
using (LogContext.PushProperty("TenantId", tenantId))
using (LogContext.PushProperty("CorrelationId", correlationId))
using (LogContext.PushProperty("UserId", userId))
using (LogContext.PushProperty("ProjectId", projectId))  // when in project context
{
    _logger.Information("Phase {Phase} advanced", phase);
}

// Sinks: Console (JSON) + File (rolling daily)
// Format: JSON structured logs for machine parsing
// Retention: 30 days file rotation
```

### Tracing — OpenTelemetry

- Distributed traces across YARP → Module → DB → External services.
- CorrelationId propagated via `X-Correlation-Id` header to all downstream calls.
- AI module traces include: Provider, Model, InputTokens, OutputTokens, Duration.
- MCP module traces include: ProviderId, ComponentType, FallbackActivated, Confidence.

### Health Checks

```
GET /health          → Overall system health (used by Docker healthcheck)
GET /health/ready    → Readiness: DB + Redis + RabbitMQ connected
GET /health/live     → Liveness: process alive

// Checks: PostgreSQL, Qdrant, Redis, RabbitMQ, AI providers (ping)
```

---

## 12. Resilience Architecture {#s12}

### Polly Policies per External Service

| Service | Policies Applied | Configuration |
|---|---|---|
| AI Providers (DeepSeek, Gemini) | Timeout + Retry + Circuit Breaker + Fallback | Timeout: 30s. Retry: 3x exponential. CB: 5 failures → open 30s. Fallback: DeepSeek-V3. |
| MCP Providers | Timeout + Retry + Circuit Breaker | Timeout: 10s. Retry: 3x. CB: 3 failures → suspend provider. |
| Stripe | Timeout + Retry | Timeout: 15s. Retry: 3x exponential. |
| Bot APIs (Telegram, WhatsApp) | Timeout + Retry | Timeout: 10s. Retry: 3x with 10s intervals. |

### Phase Rollback Strategy

All 9 phase transitions are **single transactions** within the Project aggregate. No distributed rollback needed. The aggregate handles compensation internally.

```
On AI operation failure:
    1. TokenGateBehavior.Release(operationId)  → credits returned
    2. Project phase stays at current phase
    3. User notified via Bot or Web
    4. Retry available immediately

On MCP failure:
    1. FallbackChainService activates next source (Cache → Internet)
    2. Component marked with fallback indicator
    3. Confidence level reduced accordingly
    4. No credit charge for failed MCP query
```

---

## 13. AI Architecture {#s13}

### Two-Layer AI Abstraction

**Layer 1 (Model Registry)** manages provider connections. **Layer 2 (Agent Layer)** assigns specific models to specific agents. Changing a provider or model requires only configuration changes, no code changes.

| Agent | Primary Model | Justification |
|---|---|---|
| CaptureAgent | gemini-flash | Native vision/multimodal for image and video analysis |
| ScopeAgent | deepseek-v3 | Structured JSON extraction, canonical concept mapping |
| ProductionAgent | deepseek-v3 | Technical APU generation with component coherence |
| PlanningAgent | deepseek-v3 | Logical sequencing, critical path calculation |
| ValuationAgent | deepseek-v3 | Price synthesis, fallback chain orchestration |
| OfferAgent | gemini-flash | Fluent commercial narrative generation |
| ConversationAgent | gemma4 | Lightweight, free, fast bot responses |
| EmbeddingService | gemini-embedding | 768d embeddings for Qdrant KnowledgeBase |

### AI Act Compliance Integration

| AI Act Article | Implementation |
|---|---|
| Art. 4 — AI Literacy | Team training program. Documented per person. Updated annually. |
| Art. 12 — Record-keeping | AgentRun entity records every AI call: model, tokens, confidence, fallback, human review. |
| Art. 13 — Transparency | System Card published. Capabilities and limitations documented. Accuracy levels disclosed. |
| Art. 14 — Human Oversight | Phase 7 (Review) is mandatory and cannot be skipped. Confirmation logged with timestamp and UserId. |
| Art. 50 — Transparency (conversational) | Bot identifies as AI on first message. Offer PDF includes AI disclaimer. AI-generated content marked until human validation. |

---

## 14. Quality Attributes (Non-Functional Requirements) {#s14}

| Quality Attribute | Target | Measurement |
|---|---|---|
| Availability | 99.5% (production) | Uptime monitoring. Docker healthcheck. Traefik health probes. |
| Response Time (API) | < 200ms p95 (non-AI) | OpenTelemetry traces. YARP request logging. |
| Response Time (AI ops) | < 30s p95 | AI operation timeout policy. Hangfire retry for slow ops. |
| Throughput | 50 concurrent users (MVP) | Redis rate limiting. Polly bulkhead (5 concurrent AI calls/tenant). |
| Data Isolation | 100% tenant isolation | EF Core global query filters. TenantId on every entity. Verified by integration tests. |
| Test Coverage | > 80% (line+branch+method) | coverlet. CI fails if below threshold. |
| Security | OWASP Top 10 compliance | JWT auth. Input validation. SQL injection prevention via EF Core parameterized queries. |
| Scalability | Vertical to CX51, then horizontal | Single container scales vertically. Module boundaries enable future horizontal extraction. |

---

## 15. Technology Decisions Summary {#s15}

All technology decisions are documented as Architecture Decision Records (ADRs).

| Decision | Choice | ADR Reference |
|---|---|---|
| Architecture Style | Modular Monolith + Vertical Slice | ADR-001 |
| Backend Framework | .NET 10 + ASP.NET Core | ADR-002 |
| Frontend Framework | Next.js 15 (App Router) | ADR-002 |
| Domain Approach | DDD Tactical Patterns | ADR-003 |
| Command/Query Pattern | CQRS + MediatR | ADR-004 |
| Async Messaging | RabbitMQ + MassTransit | ADR-005 |
| Reliable Messaging | Outbox Pattern (MassTransit) | ADR-006 |
| Multi-Tenancy | Row-Level Security (TenantId) | ADR-007 |
| API Gateway | YARP Reverse Proxy | ADR-008 |
| Primary Database | PostgreSQL 17 | ADR-009 |
| Vector Database | Qdrant | ADR-010 |
| AI Abstraction | Microsoft Agents Framework (2-layer) | ADR-011 |
| MCP Integration | Custom MCP Client (.NET) | ADR-012 |
| Credit Model | Token Gate + Reserve/Consume/Release | ADR-013 |
| Bot Channels | Telegram.Bot + Meta Cloud API | ADR-014 |
| Infrastructure | Docker on Hetzner | ADR-015 |
| Authentication | ASP.NET Core Identity + JWT | ADR-016 |
| Observability | Serilog + OpenTelemetry | ADR-017 |
| Soft Delete | IsDeleted + AuditInterceptor | ADR-018 |
| Resilience | Polly + Choreography Saga | ADR-019 |
| Environments | Development and production operationally; staged Compose retained for future use | ADR-020 / ADR-016 |
| Git Strategy | Gitflow | ADR-021 |
| API Contracts | OpenAPI 3.1 + RFC 7807 | ADR-022 |
| Testing | xUnit + Testcontainers + PactNet + Playwright | ADR-023 |
| Copilot Rules | 24 mandatory rules | ADR-024 |

---

*KYNAKEE PLATFORM · Software Architecture Document v1.0 · 2026-08-20*  
*Confidential · For internal development use only*
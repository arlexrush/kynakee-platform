# KYNAKEE PLATFORM
## Architecture Decision Records — Part 1 of 2
### ADR-001 to ADR-012 · Foundational Decisions
**Version 1.4 · 2026-08-19 · Confidential**

> **Decision vs delivery:** The accepted ADRs govern new code but do not certify deployment. See [current state and governance](../Gobernanza/Estado-y-gobernanza.md). The independent YARP gateway and Projects domain/EF configuration exist; JWT enforcement, working cross-module integration, outbox, the other six DbContexts and real persistence still require implementation or verification. The `kynakee-web` repository is separate. Do not infer operational compliance from the snippets below.

---

## Table of Contents — Part 1

- [ADR-001 — Modular Monolith Architecture (7 Modules)](#adr-001)
- [ADR-002 — Technology Stack Selection](#adr-002)
- [ADR-003 — DDD with Vertical Slice Architecture](#adr-003)
- [ADR-004 — CQRS and MediatR Pattern](#adr-004)
- [ADR-005 — Event-Driven Architecture with RabbitMQ](#adr-005)
- [ADR-006 — Outbox Pattern for Reliable Messaging](#adr-006)
- [ADR-007 — Multi-Tenant Data Isolation Strategy](#adr-007)
- [ADR-008 — YARP Reverse Proxy as API Gateway](#adr-008)
- [ADR-009 — PostgreSQL 17 as Primary Database](#adr-009)
- [ADR-010 — Qdrant as Vector Database](#adr-010)
- [ADR-011 — AI Provider Abstraction (Microsoft Agents Framework)](#adr-011)
- [ADR-012 — MCP Client Architecture](#adr-012)

---

## ADR-001 — Modular Monolith Architecture (7 Modules) {#adr-001}

| Field | Value |
|---|---|
| Status | Accepted |
| Date | 2026-08-19 |
| Deciders | Principal Architect, Technical Lead |

### Architectural Decision: Project as Rich Aggregate Root

After analysis, the 9 operational phases (Capture, Context, Scope, Production, Planning, Valuation, Review, Offer) are **NOT separate modules**. They are phases of the Project aggregate lifecycle. A WorkItem cannot exist without a Project. A Schedule cannot exist without a Project. A Valuation cannot exist without a Project. Therefore, **Project is the rich aggregate root** that owns all phase state.

### Solution Structure (7 Modules + Shared Kernel)

This backend repository is the platform API and domain layer. The UI is intentionally maintained in the separate repository `kynakee-web` and deployed as an independent Docker container.

```
src/
├── Kynakee.Gateway/      # Independent YARP gateway and public HTTP entry point
├── Kynakee.Api/          # Internal API host
└── Kynakee.Modules/      # 7 bounded-context modules plus Shared Kernel
    ├── Projects/           # CORE: Project aggregate + all 9 phases as internal entities
    ├── KnowledgeBase/      # Global APU library (Qdrant + PostgreSQL). Independent.
    ├── MCP/                # Provider network. Stateless price query service.
    ├── AI/                 # Agent orchestration. Stateless processing service.
    ├── Bots/               # Telegram + WhatsApp. Translates messages to Project commands.
    ├── Billing/            # Credits, plans, Stripe. Listens to Project events.
    ├── Identity/           # Tenants, Users, Auth. Fully independent.
    └── Shared/             # Shared Kernel primitives and contracts.
```

Frontend repository: `kynakee-web/` (Next.js 15)

### Why 7 Modules (not 13)

| Concept | Can exist without Project? | Conclusion |
|---|---|---|
| WorkItem (Partida) | No | Entity inside Project aggregate |
| CaptureExpedient | No | Entity inside Project aggregate |
| ProjectContext | No | Entity inside Project aggregate |
| APUAssignment | No | Entity inside Project aggregate |
| Schedule | No | Entity inside Project aggregate |
| Valuation | No | Entity inside Project aggregate |
| Review | No | Entity inside Project aggregate |
| Offer | No | Entity inside Project aggregate |
| APU (canonical) | Yes — global, shared across tenants | Module: KnowledgeBase |
| CreditAccount | Yes — belongs to Tenant | Module: Billing |
| MCPProvider | Yes — independent service | Module: MCP |
| BotConversation | Yes — access channel | Module: Bots |
| Tenant / User | Yes — exists before project | Module: Identity |

### Project Aggregate Internal Structure

```
Kynakee.Modules.Projects/
├── Domain/
│   ├── Aggregates/
│   │   └── Project.cs              # Aggregate root — owns all phase state
│   ├── Entities/
│   │   ├── CaptureExpedient.cs     # Phase 1 state
│   │   ├── ProjectContext.cs       # Phase 2 state
│   │   ├── WorkItem.cs             # Phase 3 — Partida
│   │   ├── APUAssignment.cs        # Phase 4 — APU linked to WorkItem
│   │   ├── Schedule.cs             # Phase 5 — PERT/Gantt
│   │   ├── Valuation.cs            # Phase 6 — economic valuation
│   │   ├── Review.cs               # Phase 7 — human approval
│   │   └── Offer.cs                # Phase 8 — commercial offer
│   ├── ValueObjects/
│   │   ├── ProjectPhase.cs         # Enum: Init|Capture|Context|Scope|...|Offer
│   │   ├── Money.cs                # Immutable monetary value
│   │   ├── Confidence.cs           # 0.0-1.0 confidence level
│   │   ├── CanonicalConceptId.cs   # e.g. PART_TILE_WALL_PORCELAIN
│   │   └── TokenConsumption.cs     # Credits consumed per operation
│   └── Events/
│       ├── ProjectCreatedEvent.cs
│       ├── PhaseAdvancedEvent.cs
│       ├── WorkItemAddedEvent.cs
│       ├── ValuationInvalidatedEvent.cs
│       ├── ReviewApprovedEvent.cs
│       └── OfferGeneratedEvent.cs
├── Application/
│   ├── Commands/  → CreateProject, AdvancePhase, AddWorkItem, ApproveReview...
│   ├── Queries/   → GetProject, GetProjectSummary, GetWorkItems...
│   └── EventHandlers/ → Handle integration events from Billing, Bots
├── Infrastructure/
│   ├── Persistence/ → ProjectDbContext, EF Core config, migrations
│   └── Repositories/ → IProjectRepository implementation
└── Api/
    └── Endpoints/ → Minimal API endpoints for all project operations
```

### Project Aggregate — Phase State Machine

```csharp
public class Project : AggregateRoot<ProjectId>
{
    public ProjectPhase CurrentPhase { get; private set; }
    public CaptureExpedient? Capture { get; private set; }
    public ProjectContext? Context { get; private set; }
    private readonly List<WorkItem> _workItems = new();
    private readonly List<APUAssignment> _apuAssignments = new();
    public Schedule? Schedule { get; private set; }
    public Valuation? Valuation { get; private set; }
    public Review? Review { get; private set; }
    public Offer? Offer { get; private set; }

    // Invariant: changing a WorkItem invalidates Valuation and Schedule
    public Result AddWorkItem(WorkItem item)
    {
        if (CurrentPhase < ProjectPhase.Scope)
            return ResultFactory.Failure<ProjectId>(ProjectErrors.PhaseNotReached("Scope"));
        Valuation = null;   // Invalidated automatically
        Schedule = null;    // Invalidated automatically
        _workItems.Add(item);
        AddDomainEvent(new WorkItemAddedEvent(Id, TenantId, item.Id));
        return ResultFactory.Success(item.Id);
    }

    // Invariant: Review requires complete Valuation
    public Result ApproveReview(UserId reviewer)
    {
        if (Valuation is null || !Valuation.IsComplete)
            return ResultFactory.Failure(ProjectErrors.ValuationRequired);
        Review = Review.Approve(reviewer);
        CurrentPhase = ProjectPhase.Offer;
        AddDomainEvent(new ReviewApprovedEvent(Id, TenantId, reviewer));
        return ResultFactory.Ok();
    }
}
```

### Consequences

- All 9 phases are consistent within a single transaction — no eventual consistency for phase transitions.
- Changing a WorkItem automatically invalidates Valuation and Schedule — enforced by the aggregate.
- Each of the 7 modules can be extracted to a microservice independently.
- Projects module owns `schema_projects`. All phase data lives in this schema.
- Read queries use projections (DTOs) — never load the full aggregate for reads.

---

## ADR-002 — Technology Stack Selection {#adr-002}

| Field | Value |
|---|---|
| Status | Accepted |
| Date | 2026-08-19 |

### Backend Stack

| Technology | Version | Purpose |
|---|---|---|
| .NET | 10 LTS | Runtime and framework |
| ASP.NET Core | 10 | Web API host |
| Entity Framework Core | 10 | ORM, Code First, migrations |
| MediatR | 12.x | CQRS mediator |
| FluentValidation | 11.x | Command/query validation |
| RabbitMQ + MassTransit | 3.13 / 8.x | Async events + Outbox |
| Redis | 7.x | Cache + sessions + rate limiting |
| SignalR | 10 | Real-time push |
| Serilog + OpenTelemetry | 4.x / 1.x | Logging + tracing |
| YARP | 2.x | Reverse proxy / API gateway |
| Polly | 8.x | Resilience policies |
| Hangfire | 1.8+ | Background jobs |
| Stripe.net | latest | Payment processing |
| Telegram.Bot | 21.x | Telegram Bot API |
| Microsoft Agents Framework | 1.x | AI agent orchestration (AI module) |

### Frontend Stack

| Technology | Version | Purpose |
|---|---|---|
| Next.js | 15 | React framework, App Router |
| TypeScript | 5.x | Type safety |
| TailwindCSS | 4.x | Utility-first styling |
| shadcn/ui | latest | Component library |
| TanStack Query | 5.x | Server state management |
| Zustand | 5.x | Client state management |
| React Hook Form + Zod | 7.x / 3.x | Forms + validation |

### Infrastructure Stack

| Technology | Version | Purpose |
|---|---|---|
| PostgreSQL | 17 | Primary relational database |
| Qdrant | 1.x | Vector DB for KnowledgeBase module |
| Redis | 7.x | Cache + session + rate limiting |
| RabbitMQ | 3.13+ | Message broker |
| Docker + Compose | 26+ / 2.x | Containerization |
| Traefik | 3.x | Reverse proxy + SSL |
| Hetzner Cloud | CX21+ | VPS hosting |

---

## ADR-003 — Domain-Driven Design with Vertical Slice Architecture {#adr-003}

| Field | Value |
|---|---|
| Status | Accepted |
| Date | 2026-08-19 |

### DDD Tactical Patterns

| Pattern | Application in Kynakee |
|---|---|
| Aggregate Root | **Project** (rich aggregate root owning all 9 phase entities). WorkItem, Schedule, Valuation, Offer are internal entities. |
| Entity | WorkItem (Partida), CaptureExpedient, ProjectContext, APUAssignment, Schedule, Valuation, Review, Offer — all within Project aggregate. |
| Value Object | Money, Confidence, Location, CanonicalConceptId, TokenConsumption — immutable. |
| Domain Event | ProjectCreated, PhaseAdvanced, WorkItemAdded, ValuationInvalidated, ReviewApproved, OfferGenerated. |
| Repository | One interface per aggregate root. EF Core implementation in Infrastructure. |
| Domain Service | TokenGateService, FallbackChainService, ConfidenceCalculator — stateless. |
| Bounded Context | **7 bounded contexts**: Projects, KnowledgeBase, MCP, AI, Bots, Billing, Identity. |

### Entity Contracts (MANDATORY)

> **MANDATORY:** Tenant-owned persistent entities inherit `BaseEntity<TId>` with the fields below. Genuinely global KnowledgeBase entities inherit the independent SharedKernel `GlobalEntity<TId>` with identity, audit, soft delete and concurrency fields but no `TenantId`; global aggregate roots that raise events inherit `GlobalAggregateRoot<TId>`. Optional `OwnerTenantId` and `OwnerUserId` record provenance only, not visibility or write privileges. Current implementation status and unverified persistence/authorization are tracked in [Estado y gobernanza](../Gobernanza/Estado-y-gobernanza.md).

```csharp
public abstract class BaseEntity<TId>
{
    public TId Id { get; protected set; }
    public Guid TenantId { get; protected set; }       // MANDATORY
    public DateTime CreatedAt { get; protected set; }  // MANDATORY
    public DateTime UpdatedAt { get; protected set; }  // MANDATORY
    public Guid? CreatedBy { get; protected set; }
    public Guid? UpdatedBy { get; protected set; }
    public DateTime? DeletedAt { get; protected set; } // Soft delete
    public bool IsDeleted { get; protected set; }
    public uint Version { get; protected set; }        // Concurrency token
}
```

En la implementación vigente, la colección de eventos no pertenece a `BaseEntity<TId>`.
La gestiona `AggregateRoot<TId>`, que expone `DomainEvents`, `AddDomainEvent(...)` y
`ClearDomainEvents()`.

`GlobalEntity<TId>` no incorpora automáticamente el manejo de eventos de `AggregateRoot<TId>`. El kernel proporciona `GlobalAggregateRoot<TId>` para agregados globales que necesiten eventos, sin cambiar la herencia de agregados tenant-owned.

---

## ADR-004 — CQRS and MediatR Pattern {#adr-004}

| Field | Value |
|---|---|
| Status | Accepted |
| Date | 2026-08-19 |

### MediatR Pipeline Behaviors (execution order)

```
1. LoggingBehavior<TRequest, TResponse>
2. ValidationBehavior<TRequest, TResponse>      // FluentValidation
3. TenantIsolationBehavior<TRequest, TResponse> // Inject TenantId
4. TokenGateBehavior<TRequest, TResponse>       // Check credits (AI commands)
5. TransactionBehavior<TRequest, TResponse>     // DB transaction (commands only)
6. DomainEventDispatchBehavior<TRequest, TResponse> // After commit
```

### Result Pattern (MANDATORY)

> **MANDATORY:** All handlers MUST return `Result<T>`. Never throw exceptions for business logic.

La implementación vigente usa `Result<T>`, `Result`, `ResultFactory`,
`ApplicationError` y `ErrorType`, todos bajo `Kynakee.Modules.SharedKernel.Application`.
Los contratos CQRS actuales son públicos y heredan de MediatR:

```csharp
public interface ICommand<TResponse> : IRequest<Result<TResponse>> { }
public interface ICommand : IRequest<Result> { }
public interface IQuery<TResponse> : IRequest<Result<TResponse>> { }
```

---

## ADR-005 — Event-Driven Architecture with RabbitMQ {#adr-005}

| Field | Value |
|---|---|
| Status | Accepted |
| Date | 2026-08-19 |

### Event Types

| Type | Transport | Purpose |
|---|---|---|
| Domain Event | In-process (MediatR) | Intra-module side effects. Same transaction. |
| Integration Event | RabbitMQ (MassTransit) | Cross-module. Published after commit via Outbox. |

### Key Integration Events (7-Module Architecture)

| Event | Publisher | Subscribers |
|---|---|---|
| ProjectCreatedIntegrationEvent | Projects | Billing, Bots |
| PhaseAdvancedIntegrationEvent | Projects | Billing, Bots |
| APUGeneratedIntegrationEvent | Projects (via AI module) | KnowledgeBase, Billing |
| OfferSentIntegrationEvent | Projects | Billing, Bots |
| CreditConsumedIntegrationEvent | Billing | Projects, Bots |
| CreditDepletedIntegrationEvent | Billing | Projects, Bots |
| MCPProviderFailedIntegrationEvent | MCP | Projects (Valuation entity), Admin |
| BotMessageReceivedIntegrationEvent | Bots | Projects |

```csharp
public abstract class IntegrationEvent : IIntegrationEvent
{
    public Guid Id { get; } = Guid.NewGuid();
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
    public Guid TenantId { get; protected init; }
}
```

---

## ADR-006 — Outbox Pattern for Reliable Messaging {#adr-006}

| Field | Value |
|---|---|
| Status | Accepted |
| Date | 2026-08-19 |

Integration events are written to the outbox table in the same DB transaction as domain changes, then published to RabbitMQ by a background process. Guarantees at-least-once delivery. Consumers MUST be idempotent.

```csharp
services.AddMassTransit(x =>
{
    x.AddEntityFrameworkOutbox<KynakeeDbContext>(o =>
    {
        o.UsePostgres();
        o.UseBusOutbox();
        o.QueryDelay = TimeSpan.FromSeconds(1);
    });
    x.UsingRabbitMq((ctx, cfg) => { cfg.Host("rabbitmq://localhost"); cfg.ConfigureEndpoints(ctx); });
});
// Outbox table: schema_shared.outbox_messages
```

---

## ADR-007 — Multi-Tenant Data Isolation Strategy {#adr-007}

| Field | Value |
|---|---|
| Status | Accepted |
| Date | 2026-08-19 |

Target tenant isolation via a required TenantId column on tenant-owned entities. EF Core filters for those entities require tenant and soft-delete checks; global KnowledgeBase entities have no TenantId and require a soft-delete-only filter plus explicit write authorization. Owner identifiers are provenance, not a tenant filter. Database row-level security and the global mapping must be verified before declaring them operational. TenantId is injected via MediatR pipeline behavior from JWT claims for tenant-owned operations.

```csharp
public interface ITenantContext
{
    Guid TenantId { get; }
    string TenantSlug { get; }
    string PlanId { get; }
    bool IsAdmin { get; }
}
// EF Core global filter:
modelBuilder.Entity<T>().HasQueryFilter(e => e.TenantId == _tenantContext.TenantId && !e.IsDeleted);
// Cross-tenant queries FORBIDDEN except for admin operations with explicit bypass.
```

---

## ADR-008 — YARP Reverse Proxy as API Gateway {#adr-008}

| Field | Value |
|---|---|
| Status | Accepted |
| Date | 2026-08-19 |

| Responsibility | Implementation |
|---|---|
| Routing | Route `/api/v1/{module}/**` to corresponding module handler |
| JWT Validation | Validate Bearer token before forwarding. Reject 401 if invalid. |
| Rate Limiting | Per-tenant rate limiting using Redis sliding window. |
| Correlation ID | Inject `X-Correlation-Id` on every request. Propagate downstream. |
| Request Logging | Log all requests with TenantId, CorrelationId, duration, status. |
| Centralized Auth | Single JWT validation point at the edge. All 7 modules trust the gateway only for authenticated identity claims. |
| Service Security | API and module endpoints enforce authorization, tenant scoping, and domain checks as a second security boundary. |

> Defense-in-depth: the gateway authenticates external callers and strips/validates untrusted input at the edge, while the API/application layer still enforces authorization policies, tenant isolation, and business rules before executing commands or queries.

---

## ADR-009 — PostgreSQL 17 as Primary Database {#adr-009}

| Field | Value |
|---|---|
| Status | Accepted |
| Date | 2026-08-19 |

### Schema Organization (7-Module Architecture)

| Schema | Contents |
|---|---|
| **schema_projects** | projects, project_phases, capture_expedients, media_files, measurements, work_items, apu_assignments, schedules, activities, valuations, valued_components, reviews, offers |
| schema_knowledge_base | canonical_concepts, concept_translations, apu_templates |
| schema_mcp | mcp_providers, mcp_query_logs, provider_ratings |
| schema_ai | agent_runs, token_consumption_logs |
| schema_bots | bot_conversations, bot_messages, conversation_states |
| schema_billing | credit_accounts, credit_transactions, subscriptions, stripe_events |
| schema_identity | tenants, users, roles, plans, refresh_tokens |
| schema_shared | outbox_messages, inbox_messages, idempotency_keys |

> **Key change from v1.3:** The 8 separate phase schemas (schema_capture, schema_context, schema_scope, schema_production, schema_planning, schema_valuation, schema_review, schema_offer) are consolidated into a single **schema_projects**. All phase state belongs to the Project aggregate.

### EF Core Rules

- Code First ONLY. Never modify the database directly.
- Soft deletes via `IsDeleted` + `DeletedAt`. Global query filter excludes soft-deleted records.
- Optimistic concurrency via `xmin` (PostgreSQL row version) on all aggregate roots.
- Index tenant-owned tables by `TenantId` when their query patterns require it; genuinely global KnowledgeBase tables have no `TenantId`. Index `CreatedAt` and soft-delete fields according to actual queries.

---

## ADR-010 — Qdrant as Vector Database for Knowledge Base {#adr-010}

| Field | Value |
|---|---|
| Status | Accepted |
| Date | 2026-08-19 |

Qdrant is used exclusively by the **KnowledgeBase module** for the Global APU Knowledge Base (semantic search). All other data lives in PostgreSQL. Qdrant runs as a Docker container on the same host.

### Collections

| Collection | Vector Model | Purpose |
|---|---|---|
| canonical_concepts | Gemini text-embedding-004 (768d) | Semantic search of work item concepts across languages |
| apu_structures | Gemini text-embedding-004 (768d) | Find similar APU templates by description and context |
| project_contexts | Gemini text-embedding-004 (768d) | Match project context to relevant APU templates |

> **Privacy:** Qdrant stores ONLY anonymized technical data. No tenant-identifiable data, no client data, no pricing data.

---

## ADR-011 — AI Provider Abstraction — Two-Layer Architecture {#adr-011}

| Field | Value |
|---|---|
| Status | Accepted |
| Date | 2026-08-19 |

### Two-Layer Design

**Layer 1 (Model Registry):** manages all AI provider connections. Single point of configuration.  
**Layer 2 (Agent Layer — AI Module):** Microsoft Agents Framework orchestrates specialized agents, each independently configured to use a specific model.  
**Fallback selection is capability-aware; a model is eligible only when it supports every modality present in a request.**

> **Key principle:** The AI module is a stateless service. It receives context from the Projects module, processes it, and returns structured results. It has no knowledge of Project state.

### Layer 1: Model Registry — 6 Models

| ModelId | Provider | Model | Capabilities |
|---|---|---|---|
| deepseek-v3 | DeepSeek | deepseek-chat | chat, reasoning, JSON, low cost |
| gemini-flash | Google Gemini | gemini-2.0-flash | chat, vision, multimodal, fast |
| gemini-embedding | Google Gemini | text-embedding-004 | embeddings 768d |
| gemma4 | Google Gemma | gemma-4 | chat, lightweight, free, privacy |
| openai-gpt4o | OpenAI | gpt-4o | chat, vision, reasoning, premium |
| openai-text-embedding-3-small | OpenAI | text-embedding-3-small | embeddings 768d |

### Layer 2: Agent → Model Assignment (AI Module)

| Agent | Primary Model | Fallback | Phase served |
|---|---|---|---|
| CaptureAgent | gemini-flash | openai-gpt4o | Phase 1 — multimodal input (eligibility depends on request modalities) |
| ScopeAgent | deepseek-v3 | deepseek-v3 | Phase 3 — JSON extraction |
| ProductionAgent | deepseek-v3 | deepseek-v3 | Phase 4 — APU generation |
| PlanningAgent | deepseek-v3 | deepseek-v3 | Phase 5 — sequencing |
| ValuationAgent | deepseek-v3 | deepseek-v3 | Phase 6 — price synthesis |
| OfferAgent | gemini-flash | deepseek-v3 | Phase 8 — commercial narrative |
| ConversationAgent | gemma4 | deepseek-v3 | Bots — lightweight, free |
| EmbeddingService | gemini-embedding | openai-text-embedding-3-small | KnowledgeBase — 768d |

> A configured fallback is not automatically usable for every request. Runtime routing must verify its capabilities against all requested modalities; if no model can process the complete request, return a failure without dropping media. Credits are NOT consumed for failed agent runs.

```csharp
// IKynakeeAgentService — the ONLY way to call AI from any module.
// Canonical signatures and DTOs: docs/Module_Contracts/Module_Contracs.md.
public interface IKynakeeAgentService
{
    Task<Result<CaptureAnalysisResult>> RunCaptureAgentAsync(CaptureAgentRequest request, CancellationToken ct);
    Task<Result<IReadOnlyList<ExtractedWorkItem>>> RunScopeAgentAsync(ScopeAgentRequest request, CancellationToken ct);
    Task<Result<APUStructureResult>> RunProductionAgentAsync(ProductionAgentRequest request, CancellationToken ct);
    Task<Result<ScheduleResult>> RunPlanningAgentAsync(PlanningAgentRequest request, CancellationToken ct);
    Task<Result<ValuationResult>> RunValuationAgentAsync(ValuationAgentRequest request, CancellationToken ct);
    Task<Result<OfferNarrativeResult>> RunOfferAgentAsync(OfferAgentRequest request, CancellationToken ct);
    Task<Result<ConversationResponse>> RunConversationAgentAsync(ConversationAgentRequest request, CancellationToken ct);
    Task<Result<float[]>> GenerateEmbeddingAsync(string text, CancellationToken ct);
}
```

```json
{
  "AI": {
    "Enabled": true,
    "Providers": {
      "DeepSeek": { "Protocol": "OpenAiCompatible", "BaseUrl": "https://api.deepseek.com/v1/", "ApiKey": "<from secret store>" },
      "Gemini":   { "Protocol": "Gemini", "BaseUrl": "https://generativelanguage.googleapis.com/v1beta/", "ApiKey": "<from secret store>" },
      "Gemma":    { "Protocol": "OpenAiCompatible", "BaseUrl": "<deployment HTTPS endpoint>", "ApiKey": "<from secret store>" },
      "OpenAI":   { "Protocol": "OpenAiCompatible", "BaseUrl": "https://api.openai.com/v1/", "ApiKey": "<from secret store>" }
    },
    "Models": {
      "deepseek-v3":      { "Provider": "DeepSeek", "ModelId": "deepseek-chat",      "CostTier": "low",      "Capabilities": ["Chat", "Reasoning"] },
      "gemini-flash":     { "Provider": "Gemini",   "ModelId": "gemini-2.0-flash",   "CostTier": "low",      "Capabilities": ["Chat", "Vision", "Reasoning"] },
      "gemini-embedding": { "Provider": "Gemini",   "ModelId": "text-embedding-004", "CostTier": "very-low", "Capabilities": ["Embedding"], "Dimensions": 768 },
      "gemma4":           { "Provider": "Gemma",    "ModelId": "gemma-4",            "CostTier": "free",     "Capabilities": ["Chat", "Lightweight"] },
      "openai-gpt4o":     { "Provider": "OpenAI",   "ModelId": "gpt-4o",             "CostTier": "high",     "Capabilities": ["Chat", "Vision", "Reasoning"] },
      "openai-text-embedding-3-small": { "Provider": "OpenAI", "ModelId": "text-embedding-3-small", "CostTier": "low", "Capabilities": ["Embedding"], "Dimensions": 768 }
    },
    "Agents": {
      "CaptureAgent":      { "ModelId": "gemini-flash",    "FallbackModelId": "openai-gpt4o", "PremiumModelId": "openai-gpt4o" },
      "ScopeAgent":        { "ModelId": "deepseek-v3",     "FallbackModelId": "deepseek-v3",  "PremiumModelId": "openai-gpt4o" },
      "ProductionAgent":   { "ModelId": "deepseek-v3",     "FallbackModelId": "deepseek-v3",  "PremiumModelId": "openai-gpt4o" },
      "PlanningAgent":     { "ModelId": "deepseek-v3",     "FallbackModelId": "deepseek-v3",  "PremiumModelId": "openai-gpt4o" },
      "ValuationAgent":    { "ModelId": "deepseek-v3",     "FallbackModelId": "deepseek-v3",  "PremiumModelId": "openai-gpt4o" },
      "OfferAgent":        { "ModelId": "gemini-flash",    "FallbackModelId": "deepseek-v3",  "PremiumModelId": "openai-gpt4o" },
      "ConversationAgent": { "ModelId": "gemma4",          "FallbackModelId": "deepseek-v3",  "PremiumModelId": "gemini-flash" },
      "EmbeddingService":  { "ModelId": "gemini-embedding","FallbackModelId": "openai-text-embedding-3-small", "PremiumModelId": null }
    }
  }
}
```

The `openai-text-embedding-3-small` fallback requests 768 dimensions to match the Gemini embedding contract. Provider endpoints and API keys are supplied through external configuration/secrets; never commit credentials.

#### Multimodal input and routing

User-provided context may combine text, PDF, images, audio, and video. Every attachment carries its MIME type; the AI layer derives required modalities from the request and may use a primary, fallback, or premium model only when that model declares every required capability. A fallback that cannot process the complete request is not eligible; the AI layer must not silently omit, transcode, or summarize unsupported media.

Model capabilities are declared independently (`Pdf`, `Vision`, `Audio`, and `Video`), in addition to `Chat`. `Vision` does not imply PDF, audio, or video support. The AI service resolves media only through the trusted Projects-owned content source; arbitrary storage URLs are never sent to a provider. The implemented Gemini transport uploads prepared media through Files API and passes the returned trusted `ProviderFileUri` as `fileData`. OpenAI-compatible transport currently accepts inline images only. Unsupported protocol/modality combinations and untrusted Gemini file URIs are rejected before a provider request.

Implementation status: capability classification, trusted-media resolution and byte limits, Gemini Files upload, OpenAI-compatible inline-image preparation, generic Gemini `fileData` serialization, typed agent orchestration, and AI run auditing are implemented. Unit tests cover the resolver and Gemini upload/trust checks, but do not verify live provider/model compatibility. In particular, successful PDF, audio, and video processing depends on the configured model/provider accepting those media types; it is not established merely by declaring a capability. Automatic fallback selection/retry, a verified multimodal fallback chain, and live-provider integration tests remain pending. The remaining agent methods currently use text JSON requests and do not accept attachments through their public request contracts.

> **Key principle:** Adding a new AI provider = change Layer 1 only. Changing which model an agent uses = change Layer 2 config only. No business logic changes required.

---

## ADR-012 — MCP Client Architecture for Provider Network {#adr-012}

| Field | Value |
|---|---|
| Status | Accepted |
| Date | 2026-08-19 |

The developer has built a custom MCP Server in .NET. Each provider gets a dedicated MCP Server instance with a unique subdomain. The **MCP module** in Kynakee implements an MCP Client that queries these servers via HTTP + JSON-RPC 2.0 and maintains a provider registry in PostgreSQL.

> **Key principle:** The MCP module is a stateless price query service. It receives component queries from the Projects module (via Valuation entity operations) and returns price data. It has no knowledge of Project state.

### Provider Registry (schema_mcp.mcp_providers)

```sql
id              UUID PRIMARY KEY
tenant_id       UUID          -- NULL = global provider
name            VARCHAR(200)
subdomain       VARCHAR(100) UNIQUE
endpoint        VARCHAR(500)  -- https://{subdomain}.kynakee.com/mcp
api_key_hash    VARCHAR(500)
categories      TEXT[]        -- ['ceramics', 'adhesives']
geo_regions     TEXT[]        -- ['ES-VC', 'ES-CT']
status          VARCHAR(20)   -- active | suspended | testing
rating          DECIMAL(3,2) DEFAULT 5.0
consecutive_failures INT DEFAULT 0
```

```csharp
// IMCPClient — the ONLY way to query MCP providers from any module
public interface IMCPClient
{
    Task<MCPQueryResult> QueryPriceAsync(
        string canonicalConceptId, string unit, decimal quantity,
        GeoLocation location, CancellationToken ct);
}
// Polly: 3 retries, exponential backoff, circuit breaker after 3 failures
```

---

*KYNAKEE PLATFORM · ADRs Part 1 (ADR-001 to ADR-012) · v1.4 · 2026-08-19*  
*See Part 2 for ADR-013 to ADR-024*

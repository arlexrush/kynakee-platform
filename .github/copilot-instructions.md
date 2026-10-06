# Kynakee Platform — Copilot Instructions

You are assisting with the **Kynakee** platform: an AI-powered construction budget generation system built with .NET 10, Next.js 15, PostgreSQL 17, RabbitMQ, Redis, Qdrant, and Docker on Hetzner.

**Architecture:** Modular Monolith · DDD · CQRS · Vertical Slice · Event-Driven · Outbox Pattern  
**7 Modules:** Projects (Core Domain) · KnowledgeBase · MCP · AI · Bots · Billing · Identity

The repository uses three branches/environments: develop for development, staged for staging, and master for production. There must be three separate Compose files, and the naming convention must be consistent: docker-compose.dev.yml, docker-compose.staged.yml, and docker-compose.prod.yml. Each repository maintains its own Docker Compose. The kynakee-platform repository does not build or start kynakee-web; its local Compose includes backend infrastructure, `Kynakee.Gateway`, `Kynakee.Api`, and ngrok. The gateway is the only published HTTP entry point and the API is internal to the Compose network. The kynakee-web repository retains its independent Compose. The repository contains an independent project `Kynakee.Gateway`; it must not be assumed that the gateway YARP is within `Kynakee.Api`. The Docker topology and conclusions regarding the Compose must be reviewed considering this explicit project.

---

## ABSOLUTE RULES — Enforce on every code generation

### Entities
- Every persistent entity MUST inherit `BaseEntity<TId>` with: `TenantId`, `CreatedAt`, `UpdatedAt`, `CreatedBy`, `UpdatedBy`, `DeletedAt`, `IsDeleted`
- NEVER create entities without these fields
- In project documentation, persistent entities must be documented using `BaseEntity<TId>`, not `Entity<TId>`. Review and correct all occurrences of `Entity<TId>` in the documentation.

### Handlers
- Every command/query handler MUST return `Result<T>`
- NEVER throw exceptions for business logic — use `Result.Failure(Error.XXX(...))`
- Use `Result<T>` in handlers as per .github/copilot-instructions.md.

### Validation
- Every command MUST have a corresponding `AbstractValidator<TCommand>` class
- NEVER create a command without a validator

### Soft Delete
- NEVER call `DbContext.Remove()` on business entities
- ALWAYS use soft delete: `entity.IsDeleted = true; entity.DeletedAt = DateTime.UtcNow;`

### AI Operations
- NEVER call Microsoft Agents Framework, DeepSeek, Gemini, or OpenAI directly
- ALWAYS use `IKynakeeAgentService` for all AI operations
- For MCP, each provider belongs to an Identity tenant, and their MCP Servers are registered as separate resources. Prioritize the query function and postpone the provider's economic compensation. The Internet fallback must perform AI searches (e.g., Gemini) always passing through `IKynakeeAgentService`, without direct calls from the MCP module to the AI provider.

### MCP Operations
- NEVER call MCP provider endpoints directly via HttpClient
- ALWAYS use `IMCPClient` for all price queries
- Design the query contract from the client without conditioning it on the existing MCP Server; the user will adapt their MCP Server later. The query must support providers of materials, equipment, labor, and other components.
- Price queries serve primarily to value projects but allow an optional `ProjectId` for queries from the provider dashboard. Only the Owner/Admin of the owning tenant can manage their providers and servers.

### Integration Events
- NEVER publish directly to RabbitMQ
- ALWAYS use MassTransit `IPublishEndpoint` (Outbox Pattern)
- ALL integration event consumers MUST check idempotency key before processing

### Cross-Module Access
- NEVER access another module's `DbContext` directly
- ONLY communicate via public module interfaces or integration events

### Endpoints
- NEVER put business logic in endpoints
- Endpoints ONLY: receive request → dispatch to MediatR → return response
- ALL endpoints MUST have full OpenAPI documentation (WithName, WithSummary, WithDescription, Produces<>)

### Logging
- ALL log entries MUST include `TenantId`, `CorrelationId`, `UserId` via Serilog `LogContext`
- When in project context, also include `ProjectId`

### Interactive Progress
- Advance interactively step by step: iterate each step of the plan until 100% completion and request explicit authorization before starting the next step.
- Each step MUST include the complete and compilable implementation of the involved classes, not just the folder design or structure declaration.
- Verify that each registered type has a visible concrete implementation; do not infer that compilation confirms the requested implementation.

### Dependency Registration
- Use the global container extension `KynakeeApiDependenceExtensions.AddApiDependencies` to register dependencies; maintain this single composition point when adding services.

### EF Core Configuration
- Separate EF Core configuration from the `DbContext` by using `IEntityTypeConfiguration<TEntity>` files within the `Infrastructure/Persistence/Configurations` folder, keeping the `DbContext` as the registration point for configurations.

### Documentation Governance
- Ensure all repository documentation aligns with existing code and serves as governance and verifiable guidance for pending development; distinguish between implemented, compiled, tested, and pending states.

### Quick Checks
- Prefer quick and scoped checks on edited files to prioritize updating the rest of the documentation.

### Migration Verification
- Before asserting that a migration exists, check the actual files on disk; ensure that the Migrations folder is not empty, as it may contain unreported migrations.
- In the current work on Identity, postpone the application of the migration to any database until further authorization; maintain it generated and compiled without declaring application or persistence via verified migrations.

### Bots Authentication
- Users MUST authenticate via Telegram or WhatsApp before establishing a business conversation; the verified identity determines the associated tenant.
- For this phase of Bots, plan for migration generation and testing in ephemeral PostgreSQL, without applying to existing databases.

## Test Naming
- ALL C# test methods MUST use PascalCase without underscores, hyphens, or other separators
- Use the structure `[Subject][Scenario][ExpectedBehavior]`
- Examples: `ConstructorShouldInitializeIdentityAndTenant`, `GenericFailureShouldContainErrorAndFailureState`
- Apply this convention consistently to unit, contract, integration, architecture, and E2E test projects

---

## Project Aggregate — CRITICAL RULES

The `Project` class is the aggregate root that owns all 9 phase entities.  
Phase order: Initialization → Capture → Context → Scope → Production → Planning → Valuation → Review → Offer
1. **ALL phase transition logic lives inside the `Project` aggregate** — never in handlers or services
2. **Modifying a WorkItem MUST call `InvalidateDownstreamResults()`** which sets `Schedule`, `Valuation`, `Review`, `Offer` to null
3. **ALL Project mutations go through aggregate methods** — never modify child entities directly via DbContext
4. **READ queries use projections (DTOs)** — never load the full aggregate for reads
5. **WRITE commands load the full aggregate** via `IProjectRepository.GetByIdWithFullStateAsync()`
6. **Follow the reviewed documentation strictly** when implementing the `Project` class, ensuring that all phase mutations are encapsulated within the aggregate.
7. **Transition/State Testing:** In the current work on the `Project` aggregate, tests for the transition/state behavior `Paused` are postponed and will be added later. Do not treat this as a blocker for continuing code review, but do not assert that the behavior is tested.
8. **EF Core Migrations and Persistence Testing:** In the current review of Projects, postpone both EF Core migrations and persistence tests; continue with code corrections without declaring persistence that depends on those migrations as completed or operational.
9. **APU Construction Rule:** The APU is constructed from the WorkItem unit; the output unit of APUAssignment must exactly match the unit of the item, without implicit conversions.
10. **Prioritize domain and data models across all modules; postpone unit tests for the invariants of `Project.SetOffer` and `Project.SendOffer` to a later phase without asserting that the gap is tested or closed 100%.**

---

## MediatR Pipeline Order (do not change)
1. LoggingBehavior
2. ValidationBehavior (FluentValidation)
3. TenantIsolationBehavior (inject TenantId from JWT)
4. TokenGateBehavior (reserve credits — AI commands only)
5. TransactionBehavior (DB transaction — commands only)
6. DomainEventDispatchBehavior (after commit)

---

## File Structure Pattern
Kynakee.Modules.{Module}/
├── Domain/Aggregates/        # Aggregate roots
├── Domain/Entities/          # Child entities
├── Domain/ValueObjects/      # Immutable value types
├── Domain/Events/            # Domain events
├── Domain/Repositories/      # Repository interfaces
├── Domain/Services/          # Domain services
├── Application/Commands/{CommandName}/
│   ├── {CommandName}Command.cs
│   ├── {CommandName}CommandHandler.cs
│   ├── {CommandName}CommandValidator.cs
│   └── {CommandName}Response.cs
├── Application/Queries/{QueryName}/
│   ├── {QueryName}Query.cs
│   ├── {QueryName}QueryHandler.cs
│   └── {QueryName}Dto.cs
├── Application/EventHandlers/
├── Infrastructure/Persistence/
│   ├── {Module}DbContext.cs
│   ├── Configurations/
│   └── Migrations/
├── Infrastructure/Repositories/
└── Api/Endpoints/
---

## EF Core Rules

- `HasDefaultSchema("schema_{module_name}")` in every DbContext
- Global query filter on ALL entities: `!e.IsDeleted && e.TenantId == _tenantContext.TenantId`
- `UseXminAsConcurrencyToken()` on all aggregate roots
- Value Objects as owned entities: `OwnsOne()`
- JSONB columns: `HasColumnType("jsonb")` with JSON serialization converter

---

## Result Pattern
// Success
return Result<T>.Success(value);

// Failure
return Result<T>.Failure(new Error("ERROR_CODE", "Human message", ErrorType.Conflict));

// Error types: Validation | NotFound | Conflict | Unauthorized | AI | MCP | Credits
---

## API Response Pattern
// Success
Results.Ok(new ApiResponse<T>(result.Value!, traceId, DateTime.UtcNow))

// Error mapping
result.Error!.Type switch
{
    ErrorType.NotFound   => Results.NotFound(result.Error.ToProblemDetails()),
    ErrorType.Conflict   => Results.UnprocessableEntity(result.Error.ToProblemDetails()),
    ErrorType.Validation => Results.BadRequest(result.Error.ToProblemDetails()),
    _                    => Results.Problem(result.Error.ToProblemDetails())
}
---

## AI Agent → Model Assignment

| Agent | Primary Model | Fallback |
|---|---|---|
| CaptureAgent | gemini-flash | deepseek-v3 |
| ScopeAgent | deepseek-v3 | deepseek-v3 |
| ProductionAgent | deepseek-v3 | deepseek-v3 |
| PlanningAgent | deepseek-v3 | deepseek-v3 |
| ValuationAgent | deepseek-v3 | deepseek-v3 |
| OfferAgent | gemini-flash | deepseek-v3 |
| ConversationAgent | gemma4 | deepseek-v3 |
| EmbeddingService | gemini-embedding | openai-gpt4o |

---

## PostgreSQL Schemas

| Schema | Module |
|---|---|
| schema_projects | Projects |
| schema_knowledge_base | KnowledgeBase |
| schema_mcp | MCP |
| schema_ai | AI |
| schema_bots | Bots |
| schema_billing | Billing |
| schema_identity | Identity |
| schema_shared | Shared (Outbox, Idempotency) |

---

## Common Mistakes → Correct Approach

| ❌ Wrong | ✅ Correct |
|---|---|
| `throw new NotFoundException(...)` | `return Result.Failure(Error.NotFound(...))` |
| `_dbContext.Projects.Remove(p)` | `p.Delete(userId)` via aggregate |
| `new HttpClient().PostAsync("mcp-url")` | `await _mcpClient.QueryPriceAsync(...)` |
| `new Kernel().InvokeAsync(...)` | `await _agentService.RunProductionAgentAsync(...)` |
| `channel.BasicPublish(...)` | `await _publishEndpoint.Publish(new IntegrationEvent {...})` |
| `_kbDbContext.APUTemplates.Find(id)` | `await _knowledgeBaseService.GetAPUTemplateAsync(id)` |
| `project.WorkItems.Add(item)` | `project.AddWorkItem(item)` via aggregate |
| Log without context | Include TenantId, CorrelationId, UserId |
| Endpoint with business logic | Endpoint → MediatR only |
| Command without validator | Always create `AbstractValidator<TCommand>` |

---

## Task Closure Confirmation
- For the closure of US-003, tasks numbered 2, 3, 4, and 6 are confirmed resolved. They should not be treated as pending again unless new validation provides contrary evidence.

---

## Naming Convention Update
- Rename `IPostCommitActionQueue`/`PostCommitActionQueue` to `IPostCommitActionDispatcher`/`PostCommitActionDispatcher` as per .NET conventions; do not reintroduce the previous names.

---

## Module Implementation Process
- Implement the DbContext for each of the seven modules one by one, following the documentation, patterns, and best practices.
- Iterate until reaching 100% completion for each module; only then present a summary and request explicit authorization to start the next module.

---

## KnowledgeBase APU Templates
- In the APU templates of KnowledgeBase, maintain an explicit and unique order of components; the aggregate assigns the position (SortOrder) and EF must persist it and respect it when reconstructing the collection.

## Additional Prioritization
- Prioritize completing domain models, data, and persistence for all modules and finalize the Behaviors. 
- Close the local preparation of Billing and document its boundary explicitly; do not implement cross-cutting infrastructure for Outbox/transport and long-term integration between Identity and Billing until a later phase, indicating pending tasks without declaring full integration.


# KYNAKEE PLATFORM
## Module Contracts
### Public Interfaces Between the 7 Bounded Contexts
**Version 1.0 · 2026-08-20 · Confidential**

---

## Table of Contents

- [1. Purpose and Rules](#s1)
- [2. Projects Module — IProjectService](#s2)
- [3. KnowledgeBase Module — IKnowledgeBaseService](#s3)
- [4. MCP Module — IMCPClient](#s4)
- [5. AI Module — IKynakeeAgentService](#s5)
- [6. Bots Module — IBotNotificationService](#s6)
- [7. Billing Module — IBillingService](#s7)
- [8. Identity Module — IIdentityService](#s8)
- [9. Cross-Module Communication Rules](#s9)
- [10. Integration Event Contracts](#s10)

---

## 1. Purpose and Rules {#s1}

Module Contracts define the public interfaces that each of the 7 bounded contexts exposes to other modules. These contracts are the **ONLY legal way** for modules to communicate synchronously. Any code that bypasses these interfaces and accesses another module's DbContext, repositories, or domain objects directly is a violation of the architecture.

> **Rule 1:** Modules communicate ONLY via these public interfaces (synchronous) or integration events via MassTransit (asynchronous). Never via direct DbContext access.

> **Rule 2:** These interfaces are injected via DI. Never instantiate implementations directly.

> **Rule 3:** All methods return `Result<T>`. Never throw exceptions across module boundaries.

> **Rule 4:** DTOs in these contracts are defined in the consuming module, not the providing module. This prevents circular dependencies.

---

## 2. Projects Module — IProjectService {#s2}

The Projects module is the Core Domain. Other modules query project state via `IProjectService`. Only the Projects module can mutate Project aggregate state.

```csharp
namespace Kynakee.Modules.Projects.Contracts;

/// <summary>
/// Public contract for the Projects module.
/// Used by: Bots, Billing, AI (for context), KnowledgeBase
/// </summary>
public interface IProjectService
{
    // ── Read operations (return projections, never full aggregate) ──

    Task<Result<ProjectSummaryDto>> GetProjectSummaryAsync(
        Guid projectId, Guid tenantId, CancellationToken ct);

    Task<Result<ProjectPhaseDto>> GetCurrentPhaseAsync(
        Guid projectId, Guid tenantId, CancellationToken ct);

    Task<Result<IReadOnlyList<WorkItemDto>>> GetWorkItemsAsync(
        Guid projectId, Guid tenantId, CancellationToken ct);

    Task<Result<ValuationSummaryDto>> GetValuationSummaryAsync(
        Guid projectId, Guid tenantId, CancellationToken ct);

    Task<Result<TokenConsumptionDto>> GetTokenConsumptionAsync(
        Guid projectId, Guid tenantId, CancellationToken ct);

    // ── Used by Bots module to get active project for a conversation ──

    Task<Result<ProjectSummaryDto?>> GetActiveProjectForConversationAsync(
        string externalConversationId, string channel, Guid tenantId, CancellationToken ct);
}

// DTOs exposed by this contract
public record ProjectSummaryDto(
    Guid Id, string Name, string CurrentPhase, string Status,
    string ClientName, DateTime UpdatedAt, int WorkItemCount,
    decimal? TotalCost, double? ConfidenceLevel);

public record ProjectPhaseDto(
    Guid ProjectId, string CurrentPhase, string Status,
    bool CanAdvance, string? BlockingReason);

public record WorkItemDto(
    Guid Id, string CanonicalConceptId, string Description,
    string Unit, decimal Quantity, double Confidence, string AIStatus);

public record ValuationSummaryDto(
    Guid ProjectId, decimal TotalCost, string Currency,
    double ConfidenceLevel, bool IsComplete, DateTime? ValuedAt);

public record TokenConsumptionDto(
    int TotalTokens, decimal TotalCredits,
    Dictionary<string, int> TokensByPhase);
```

---

## 3. KnowledgeBase Module — IKnowledgeBaseService {#s3}

The KnowledgeBase module provides APU template lookup and canonical concept search. Used primarily by the Projects module (Phase 4: Production) and the AI module.

```csharp
namespace Kynakee.Modules.KnowledgeBase.Contracts;

/// <summary>
/// Public contract for the KnowledgeBase module.
/// Used by: Projects (Phase 4), AI module
/// </summary>
public interface IKnowledgeBaseService
{
    // ── APU Template operations ──

    Task<Result<APUTemplateDto?>> FindAPUTemplateAsync(
        string canonicalConceptId,
        string geoRegion,
        string projectType,
        CancellationToken ct);

    Task<Result<IReadOnlyList<APUTemplateDto>>> SearchAPUTemplatesAsync(
        string query,
        string geoRegion,
        int limit,
        CancellationToken ct);

    Task<Result<APUTemplateId>> SaveAPUTemplateAsync(
        SaveAPUTemplateRequest request,
        CancellationToken ct);

    // ── Canonical Concept operations ──

    Task<Result<CanonicalConceptDto?>> FindCanonicalConceptAsync(
        string conceptId, CancellationToken ct);

    Task<Result<IReadOnlyList<CanonicalConceptDto>>> SearchCanonicalConceptsAsync(
        string query, string language, int limit, CancellationToken ct);

    // ── Embedding operations (used by AI module) ──

    Task<Result<float[]>> GenerateEmbeddingAsync(
        string text, CancellationToken ct);
}

public record APUTemplateDto(
    Guid Id, string CanonicalConceptId, string Description,
    string ProjectType, string GeoRegion, string Unit,
    double YieldHoursPerUnit, string CrewDescription,
    IReadOnlyList<APUComponentDto> Components,
    int UsageCount, double AverageConfidence, string Source);

public record APUComponentDto(
    string Description, string ComponentType, string Unit, decimal Yield);
    // NOTE: NO price field here. Prices are determined in Phase 6.

public record CanonicalConceptDto(
    string Id, string Category, string Subcategory,
    string DefaultUnit, Dictionary<string, string> Translations);

public record SaveAPUTemplateRequest(
    string CanonicalConceptId, string Description, string ProjectType,
    string GeoRegion, string Unit, double YieldHoursPerUnit,
    IReadOnlyList<APUComponentDto> Components, string Source);
```

---

## 4. MCP Module — IMCPClient {#s4}

The MCP module provides price queries to the provider network. Used by the Projects module (Phase 6: Valuation). Stateless service — no knowledge of project state.

```csharp
namespace Kynakee.Modules.MCP.Contracts;

/// <summary>
/// Public contract for the MCP module.
/// Used by: Projects (Phase 6 Valuation)
/// </summary>
public interface IMCPClient
{
    Task<Result<MCPQueryResult>> QueryPriceAsync(
        MCPPriceQuery query,
        CancellationToken ct);

    Task<Result<IReadOnlyList<MCPProviderSummaryDto>>> GetAvailableProvidersAsync(
        string geoRegion,
        string category,
        CancellationToken ct);
}

public record MCPPriceQuery(
    string CanonicalConceptId,
    string ComponentType,   // Material|Labor|Equipment|Subcontract|Transport
    string Unit,
    decimal Quantity,
    string GeoRegion,
    string PostalCode);

public record MCPQueryResult(
    bool Success,
    decimal? Price,
    string? Currency,
    string? ProviderName,
    string? FallbackSource,  // null|Cache|Internet
    double Confidence,
    DateTime QueriedAt);

public record MCPProviderSummaryDto(
    Guid Id, string Name, string Subdomain,
    string[] Categories, string[] GeoRegions,
    double Rating, string Status);
```

---

## 5. AI Module — IKynakeeAgentService {#s5}

The AI module is a stateless processing service. It receives context from the Projects module, processes it via Microsoft Agents Framework, and returns structured results. It has **NO knowledge of Project state**.

```csharp
namespace Kynakee.Modules.AI.Contracts;

/// <summary>
/// Public contract for the AI module.
/// Used by: Projects (all AI phases), Bots, KnowledgeBase (embeddings)
/// CRITICAL: This is the ONLY way to call AI. Never call providers directly.
/// </summary>
public interface IKynakeeAgentService
{
    // ── Phase agents ──

    Task<Result<CaptureAnalysisResult>> RunCaptureAgentAsync(
        CaptureAgentRequest request, CancellationToken ct);

    Task<Result<IReadOnlyList<ExtractedWorkItem>>> RunScopeAgentAsync(
        ScopeAgentRequest request, CancellationToken ct);

    Task<Result<APUStructureResult>> RunProductionAgentAsync(
        ProductionAgentRequest request, CancellationToken ct);

    Task<Result<ScheduleResult>> RunPlanningAgentAsync(
        PlanningAgentRequest request, CancellationToken ct);

    Task<Result<ValuationResult>> RunValuationAgentAsync(
        ValuationAgentRequest request, CancellationToken ct);

    Task<Result<OfferNarrativeResult>> RunOfferAgentAsync(
        OfferAgentRequest request, CancellationToken ct);

    // ── Conversation agent (used by Bots module) ──

    Task<Result<ConversationResponse>> RunConversationAgentAsync(
        ConversationAgentRequest request, CancellationToken ct);

    // ── Embedding service (used by KnowledgeBase module) ──

    Task<Result<float[]>> GenerateEmbeddingAsync(
        string text, CancellationToken ct);
}

// Key request/response types
public record CaptureAgentRequest(
    Guid ProjectId, Guid TenantId,
    IReadOnlyList<MediaFileRef> MediaFiles,
    IReadOnlyList<MeasurementRef> Measurements,
    IReadOnlyList<string> Transcriptions);

public record CaptureAnalysisResult(
    IReadOnlyList<ExtractedWorkItem> WorkItems,
    IReadOnlyList<string> Observations,
    int TokensConsumed, double Confidence);

public record ExtractedWorkItem(
    string CanonicalConceptId, string Description,
    string Unit, decimal Quantity, string Location,
    double Confidence, string AIStatus);

public record ConversationAgentRequest(
    Guid TenantId, string Channel,
    string UserMessage, string ConversationHistory,
    string? ActiveProjectPhase, string? ActiveProjectName);

public record ConversationResponse(
    string Message, string? SuggestedAction,
    bool RequiresHumanInput, int TokensConsumed);
```

---

## 6. Bots Module — IBotNotificationService {#s6}

The Bots module exposes a notification service so other modules can push messages to users via Telegram or WhatsApp.

```csharp
namespace Kynakee.Modules.Bots.Contracts;

/// <summary>
/// Public contract for the Bots module.
/// Used by: Projects (phase notifications), Billing (credit alerts)
/// </summary>
public interface IBotNotificationService
{
    Task<Result> SendMessageAsync(
        Guid tenantId,
        string externalConversationId,
        string channel,
        BotMessage message,
        CancellationToken ct);

    Task<Result> SendPhaseCompletedNotificationAsync(
        Guid tenantId,
        Guid projectId,
        string newPhase,
        CancellationToken ct);

    Task<Result> SendCreditAlertAsync(
        Guid tenantId,
        decimal remainingCredits,
        string alertType,  // Low|Depleted
        CancellationToken ct);

    Task<Result> SendOfferReadyNotificationAsync(
        Guid tenantId,
        Guid projectId,
        string pdfUrl,
        CancellationToken ct);
}

public record BotMessage(
    string Text,
    BotMessageType Type,
    string? ActionButton = null,
    string? ActionPayload = null);

public enum BotMessageType { Info, Success, Warning, Error, Question }
```

---

## 7. Billing Module — IBillingService {#s7}

The Billing module manages credits and subscriptions. Used by the Projects module (via TokenGateBehavior) and the Identity module (tenant creation).

```csharp
namespace Kynakee.Modules.Billing.Contracts;

/// <summary>
/// Public contract for the Billing module.
/// Used by: Projects (TokenGateBehavior), Identity (tenant creation)
/// </summary>
public interface IBillingService
{
    // ── Credit operations (called by TokenGateBehavior) ──

    Task<Result<CreditReservation>> ReserveCreditsAsync(
        Guid tenantId, decimal amount, Guid operationId, CancellationToken ct);

    Task<Result> ConsumeCreditsAsync(
        Guid tenantId, Guid operationId, CancellationToken ct);

    Task<Result> ReleaseCreditsAsync(
        Guid tenantId, Guid operationId, CancellationToken ct);

    // ── Balance queries ──

    Task<Result<CreditBalanceDto>> GetBalanceAsync(
        Guid tenantId, CancellationToken ct);

    Task<Result<bool>> HasSufficientCreditsAsync(
        Guid tenantId, decimal requiredAmount, CancellationToken ct);

    // ── Tenant initialization (called by Identity on tenant creation) ──

    Task<Result> InitializeCreditAccountAsync(
        Guid tenantId, string planId, CancellationToken ct);
}

public record CreditReservation(
    Guid OperationId, decimal Amount, DateTime ExpiresAt);

public record CreditBalanceDto(
    decimal AvailableCredits, decimal ReservedCredits,
    string PlanId, DateTime? NextRenewalDate);
```

---

## 8. Identity Module — IIdentityService {#s8}

The Identity module provides tenant and user information to other modules.

```csharp
namespace Kynakee.Modules.Identity.Contracts;

/// <summary>
/// Public contract for the Identity module.
/// Used by: YARP (JWT), all modules (tenant context)
/// </summary>
public interface IIdentityService
{
    Task<Result<TenantDto?>> GetTenantAsync(
        Guid tenantId, CancellationToken ct);

    Task<Result<TenantDto?>> GetTenantBySlugAsync(
        string slug, CancellationToken ct);

    Task<Result<UserDto?>> GetUserAsync(
        Guid userId, Guid tenantId, CancellationToken ct);

    Task<Result<CompanySettingsDto>> GetCompanySettingsAsync(
        Guid tenantId, CancellationToken ct);

    Task<Result<bool>> ValidateUserRoleAsync(
        Guid userId, Guid tenantId, string requiredRole, CancellationToken ct);
}

public record TenantDto(
    Guid Id, string Name, string Slug, string TenantType,
    string PlanId, string Status, CompanySettingsDto Settings);

public record UserDto(
    Guid Id, string FirstName, string LastName,
    string Email, string Role, string Status);

public record CompanySettingsDto(
    decimal Administration, decimal Profit, decimal Quality,
    decimal SafetyHealth, decimal Environment, decimal Contingency);
```

---

## 9. Cross-Module Communication Rules {#s9}

### Synchronous Communication (In-Process)

Use the public interfaces above. Inject via DI. All methods return `Result<T>`.

```csharp
// ✅ CORRECT: Inject and use public interface
public class AssignAPUsCommandHandler
{
    private readonly IKnowledgeBaseService _knowledgeBase;
    private readonly IKynakeeAgentService _agentService;

    public async Task<Result<OperationId>> Handle(AssignAPUsCommand cmd, CancellationToken ct)
    {
        var template = await _knowledgeBase.FindAPUTemplateAsync(
            cmd.CanonicalConceptId, cmd.GeoRegion, cmd.ProjectType, ct);

        if (!template.IsSuccess) return Result.Failure(template.Error!);

        if (template.Value is null)
        {
            var generated = await _agentService.RunProductionAgentAsync(
                new ProductionAgentRequest(cmd.ProjectId, cmd.WorkItems), ct);
        }
    }
}

// ❌ WRONG: Direct DbContext access across modules
public class AssignAPUsCommandHandler
{
    private readonly KnowledgeBaseDbContext _kbContext; // VIOLATION
    var template = await _kbContext.APUTemplates.FindAsync(id); // VIOLATION
}
```

### Asynchronous Communication (Cross-Module Events)

Use MassTransit integration events via the Outbox Pattern. Never publish directly to RabbitMQ.

| Event | Publisher | Subscribers |
|---|---|---|
| ProjectCreatedIntegrationEvent | Projects | Billing, Bots |
| PhaseAdvancedIntegrationEvent | Projects | Billing, Bots |
| WorkItemAddedIntegrationEvent | Projects | KnowledgeBase |
| APUGeneratedIntegrationEvent | Projects (via AI) | KnowledgeBase, Billing |
| OfferGeneratedIntegrationEvent | Projects | Billing, Bots |
| CreditsConsumedIntegrationEvent | Billing | Projects, Bots |
| CreditDepletedIntegrationEvent | Billing | Projects, Bots |
| MCPProviderFailedIntegrationEvent | MCP | Projects |
| TenantCreatedIntegrationEvent | Identity | Billing |
| BotMessageReceivedIntegrationEvent | Bots | Projects |

---

## 10. Integration Event Contracts {#s10}

All integration events inherit from `IntegrationEvent` (schema_shared). All consumers must be idempotent.

```csharp
// Base class (Kynakee.Modules.Shared)
public abstract record IntegrationEvent
{
    public Guid Id { get; } = Guid.NewGuid();
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
    public Guid TenantId { get; init; }
    public string EventType => GetType().Name;
}

// Key event contracts:
public record ProjectCreatedIntegrationEvent : IntegrationEvent
{
    public Guid ProjectId { get; init; }
    public string ProjectName { get; init; } = string.Empty;
    public string Channel { get; init; } = string.Empty;
    public string GeoRegion { get; init; } = string.Empty;
}

public record PhaseAdvancedIntegrationEvent : IntegrationEvent
{
    public Guid ProjectId { get; init; }
    public string NewPhase { get; init; } = string.Empty;
    public string PreviousPhase { get; init; } = string.Empty;
    public decimal CreditsConsumed { get; init; }
}

public record CreditsConsumedIntegrationEvent : IntegrationEvent
{
    public Guid OperationId { get; init; }
    public decimal Amount { get; init; }
    public decimal RemainingCredits { get; init; }
    public string Phase { get; init; } = string.Empty;
}

public record CreditDepletedIntegrationEvent : IntegrationEvent
{
    public Guid ProjectId { get; init; }
    public decimal RemainingCredits { get; init; }
}

// Idempotency pattern for all consumers:
public class ProjectCreatedConsumer : IConsumer<ProjectCreatedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<ProjectCreatedIntegrationEvent> context)
    {
        var key = $"ProjectCreated:{context.Message.Id}";
        if (await _idempotencyService.IsProcessedAsync(key)) return;
        // ... process event ...
        await _idempotencyService.MarkProcessedAsync(key, GetType().Name);
    }
}
```

---

*KYNAKEE PLATFORM · Module Contracts v1.0 · 2026-08-20*  
*Confidential · For internal development use only*

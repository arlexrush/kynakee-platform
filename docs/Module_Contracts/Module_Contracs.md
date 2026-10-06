# KYNAKEE PLATFORM
## Module Contracts
### Public Interfaces Between the 7 Bounded Contexts
**Version 1.0 · 2026-08-20 · Confidential**

> **Implementation status:** The interfaces below specify required public boundaries; snippets do not prove that an interface, DTO, service or consumer exists in the current solution. See [current state and governance](../Gobernanza/Estado-y-gobernanza.md). Projects currently exposes an [`IProjectRepository`](../../src/Kynakee.Modules/Kynakee.Modules.Projects/Domain/Repositories/IProjectRepository.cs) and a concrete EF repository for full-state writes; this is not the proposed cross-module `IProjectService`. Before using a proposed contract, implement its interface, concrete adapter, DI registration, result handling and contract tests. The other six modules remain partially scaffolded; direct cross-module DbContext access remains prohibited.

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

The MCP module provides price queries to the provider network for Projects valuation, without knowledge of project state. The **client defines this contract**; provider servers must adapt to it rather than constraining the client to an existing server implementation.

**Public .NET contract:** [IMCPClient and DTOs](../../src/Kynakee.Modules/Kynakee.Modules.Mcp/Contracts/IMCPClient.cs), namespace `Kynakee.Modules.Mcp.Contracts`. `QueryPriceAsync` returns `Result<MCPQueryResult>` and `GetAvailableProvidersAsync` returns `Result<IReadOnlyList<MCPProviderSummaryDto>>`. Success/failure belongs to `Result`, not a duplicate flag on the quote.

| Contract | Fields and semantics |
| --- | --- |
| MCPPriceQuery | `CanonicalConceptId`, typed `ComponentType` (`Material`, `Labor`, `Equipment`, `Subcontract`, `Transport`), `Category`, `Unit`, positive `Quantity`, `GeoRegion`, `PostalCode`, uppercase three-letter `Currency`, optional local `ProjectId` (null for dashboard queries; an empty Guid is invalid). |
| MCPQueryResult | Nonnegative decimal **unit** `Price`, exact `Unit` and `Currency`, `ProviderId`, `ProviderName`, `ServerId`, decimal `Confidence` from 0 to 1, client-side UTC `QueriedAt`. No implicit unit or currency conversions. |
| MCPProviderSummaryDto | `Id`, `Name`, read-only `Categories` and `GeoRegions`, decimal `Rating`. No server configuration, credential references or secrets. |

### Provider-side tool contract

[McpPriceTool.json](McpPriceTool.json) is the tool descriptor with input/output schemas and read-only/idempotency annotations. The provider server must:

1. Implement MCP **Streamable HTTP over HTTPS**, version negotiation and a read-only `query_price` tool. The official client SDK handles discovery and/or the `initialize` handshake according to the negotiated revision; the tests simulate `server/discover` returning MethodNotFound and falling back to `initialize`.
2. Accept bearer authentication. The registered endpoint must exactly match its externally configured credential entry, without user information, query string or fragment. Automatic HTTP redirects, cookies and implicit proxies are disabled.
3. Accept `tools/call` with name `query_price` and camelCase arguments from the input schema. Quantity supplies the volume context (including price tiers); the returned price is per requested unit, **not** the total for the quantity. Component type is independent of the provider category.
4. Return `structuredContent` with `price`, `unit`, `currency` and `confidence`. The SDK can also carry MCP text content, but this client requires structured content and does not extract quotes from prose. The unit and currency must exactly match the request; missing or invalid fields fail validation. Confidence is provider-supplied and does not certify independent verification.
5. Mark tool failures with `isError: true` or return MCP protocol errors as appropriate. Do not encode unavailable prices as a zero quote; zero is a valid price only when genuinely quoted.

Credentials are supplied outside source control through `Mcp:Servers:{credentialReference}:Endpoint` and `Mcp:Servers:{credentialReference}:ApiKey`, for example reference `provider-one`. Equivalent environment-variable keys use `__` instead of `:`. The endpoint and secret are bound to the same reference, preventing a registered arbitrary destination from receiving another credential. The database stores the reference only, not the bearer secret.

Providers are selected by region/category, availability and rating. The client tries registered live servers until it receives a compatible quote; it does not claim geographic-distance ranking or price comparison across all providers. Each attempt is cancelable with a 10-second timeout; transient transport/timeouts allow three retries with exponential backoff and jitter. The process-local Polly circuit is keyed by server, opens for 30 seconds after its configured 100%-failure sampling threshold (minimum three logical operations within one minute), and **does not replace** persistent domain `RecordFailure`/`RecordSuccess` or their three-consecutive-failure rule.

Price queries require an authenticated tenant/user context. `ProjectId` is audit context only: the client neither checks project ownership nor transmits it to `query_price`. The consumer tenant owns the audit record even when the provider belongs to another tenant.

The client persists final server outcomes after retries and updates provider health once per provider traversal. Audit uses an independent PostgreSQL transaction and survives caller rollback; concurrent health updates are serialized by a row lock. Local configuration failures and open circuits do not penalize the provider. Caller cancellation propagates without counting a failure; results buffered in the interrupted provider traversal may remain unaudited. Audit persistence failures propagate rather than silently returning an unaudited quote. The current log does not retain server ID, currency or detailed error code. Provider suspension is persisted, but durable delivery of its domain event requires the pending Outbox.

Management commands and DTO queries are implemented for active Owner/Admin users of an active owning tenant through Identity's public `ITenantManagementAuthorization` contract. Reads use tenant-filtered projections and bounded pagination; they do not expose credentials or their references. There are no MCP HTTP endpoints yet.

**Implemented and tested locally:** SDK-backed client, summaries, management, independent audit and persistent health; 27 unit tests and 84 integration tests (26 client, 36 management, 22 persistence) passed at closure on 2026-10-02 with ephemeral PostgreSQL and simulated HTTP. **Pending:** real provider interoperability, project ownership verification, cache/AI-search fallback, billing/compensation, endpoints, durable event delivery and E2E verification. These DTOs describe provider quotes only; no fallback result is claimed. Internet fallback must go through `IKynakeeAgentService` with verifiable sources, never a direct Gemini call. See [governance evidence](../Gobernanza/Estado-y-gobernanza.md).

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
    IReadOnlyList<string> Transcriptions,
    IReadOnlyList<string>? TextInputs = null);

public record MediaFileRef(
    Uri Url, string MimeType, string? Room,
    bool IsPathology, bool Processed, string? FileName = null);

public record MeasurementRef(
    string Description, decimal Value, string Unit);

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

public record ScopeAgentRequest(
    Guid ProjectId, Guid TenantId,
    IReadOnlyList<ExtractedWorkItem> CapturedWorkItems,
    IReadOnlyList<string> Observations,
    ProjectContextData? Context);

public record ProjectContextData(
    string? Country, string? Region, string? Province,
    string? Municipality, string? Address,
    string? UrbanRegulation, string? ConstructionCode,
    string? CollectiveAgreement,
    decimal? SalaryOfficial1, decimal? SalaryLaborer,
    decimal? InflationRate, decimal? VatRate,
    decimal? ConstructionIndex);

public record WorkItemRef(
    Guid Id, string CanonicalConceptId, string Description,
    string Unit, decimal Quantity, string? Location,
    string? Observations, double Confidence);

public record ProductionAgentRequest(
    Guid ProjectId, Guid TenantId,
    IReadOnlyList<WorkItemRef> WorkItems,
    IReadOnlyList<APUTemplateRef> CandidateTemplates,
    ProjectContextData? Context);

public record APUTemplateRef(
    Guid Id, string CanonicalConceptId, string OutputUnit,
    string? Description);

public record APUStructureResult(
    IReadOnlyList<APUAssignmentStructure> Assignments,
    int TokensConsumed, double Confidence);

public record APUAssignmentStructure(
    Guid WorkItemId, string OutputUnit, Guid? APUTemplateId,
    string Source, IReadOnlyList<APUComponentStructure> Components,
    double Confidence);

// APUComponentStructure is a JSON polymorphic union. Its discriminator values are:
// Material, Labor, Equipment, AuxiliaryMeans, Subcontract and Transport.
// Each variant carries the corresponding technical fields from the Projects APU component definitions.
public abstract record APUComponentStructure(
    Guid? SourceComponentId, string Description, string Unit);

public record MaterialAPUComponentStructure(
    Guid? SourceComponentId, string Description, string Unit,
    decimal WastePercentage, bool TransportIncluded,
    decimal QuantityPerApuUnit)
    : APUComponentStructure(SourceComponentId, Description, Unit);

public record LaborAPUComponentStructure(
    Guid? SourceComponentId, string Description, string Unit,
    string Trade, decimal CrewSize, decimal Productivity)
    : APUComponentStructure(SourceComponentId, Description, Unit);

public record EquipmentAPUComponentStructure(
    Guid? SourceComponentId, string Description, string Unit,
    string EquipmentCategory, decimal EquipmentCount,
    decimal HoursPerApuUnit)
    : APUComponentStructure(SourceComponentId, Description, Unit);

public record AuxiliaryMeansAPUComponentStructure(
    Guid? SourceComponentId, string Description, string Unit,
    decimal ApplicationPercentage)
    : APUComponentStructure(SourceComponentId, Description, Unit);

public record SubcontractAPUComponentStructure(
    Guid? SourceComponentId, string Description, string Unit,
    string ContractConditions, decimal QuantityPerApuUnit)
    : APUComponentStructure(SourceComponentId, Description, Unit);

public record TransportAPUComponentStructure(
    Guid? SourceComponentId, string Description, string Unit,
    decimal DistanceKm, decimal VehicleCapacity,
    string TransportRateBasis, decimal RoundTripFactor,
    decimal QuantityPerApuUnit)
    : APUComponentStructure(SourceComponentId, Description, Unit);

public record PlanningAgentRequest(
    Guid ProjectId, Guid TenantId,
    IReadOnlyList<WorkItemRef> WorkItems,
    IReadOnlyList<APUAssignmentStructure> APUAssignments,
    ProjectContextData? Context, DateOnly? StartDate);

public record ScheduleResult(
    IReadOnlyList<ScheduleActivityResult> Activities,
    IReadOnlyList<SchedulePrecedenceResult> Precedences,
    IReadOnlyList<Guid> CriticalPath, int TotalDurationDays,
    DateOnly? StartDate, DateOnly? EndDate,
    IReadOnlyList<ScheduleMilestoneResult> Milestones,
    int TokensConsumed, double Confidence);

public record ScheduleActivityResult(
    Guid Id, string Name, int DurationDays,
    IReadOnlyList<Guid> WorkItemIds);

public record SchedulePrecedenceResult(
    Guid ActivityId, Guid PredecessorActivityId,
    string Type, int LagDays);

public record ScheduleMilestoneResult(string Name, DateOnly Date);

public record ValuationAgentRequest(
    Guid ProjectId, Guid TenantId,
    IReadOnlyList<WorkItemRef> WorkItems,
    IReadOnlyList<APUAssignmentStructure> APUAssignments,
    IReadOnlyList<APUComponentPriceRef> ComponentPrices,
    string Currency, ProjectContextData? Context);

public record APUComponentPriceRef(
    Guid ComponentId, decimal UnitPrice, string Currency,
    string QuotedUnit, string PricingSource, string? ProviderName,
    bool IsFallback, double Confidence);

public record ValuationResult(
    IReadOnlyList<ValuedComponentResult> ValuedComponents,
    IReadOnlyList<ValuedWorkItemResult> ValuedWorkItems,
    decimal DirectCost, decimal AuxiliaryCost, decimal IndirectCost,
    decimal Administration, decimal Quality, decimal SafetyHealth,
    decimal Environment, decimal Contingency, decimal Profit, decimal VAT,
    decimal TotalCost, string Currency, double Confidence,
    int TokensConsumed);

public record ValuedComponentResult(
    Guid ComponentId, string ComponentType, string Description,
    string Unit, decimal QuotedUnitPrice, decimal ComponentSubtotal,
    string Currency, string PricingSource, string? ProviderName,
    bool IsFallback, double Confidence);

public record ValuedWorkItemResult(
    Guid WorkItemId, Guid APUAssignmentId, decimal Quantity,
    decimal DirectUnitCost, decimal AuxiliaryUnitCost,
    decimal TotalUnitPrice, decimal TotalAmount, string Currency);

public record OfferAgentRequest(
    Guid ProjectId, Guid TenantId, string ProjectName,
    ValuationResult Valuation, string? ScheduleSummary,
    string? Conditions, string? Warranties, int ValidityDays,
    string Language);

public record OfferNarrativeResult(
    string Title, string ExecutiveSummary, string ScopeDescription,
    string? Conditions, string? Warranties, int ValidityDays,
    string AIActDisclaimer, int TokensConsumed, double Confidence);
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

        if (!template.IsSuccess) return ResultFactory.Failure(template.Error!);

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

All integration events inherit from `Kynakee.Modules.SharedKernel.Integration.IntegrationEvent`
and implement `IIntegrationEvent`. The current base type is an abstract class, not a record.
Events are published through the MassTransit Outbox and all consumers must be idempotent.
The public shape of the Shared Kernel integration-event contract is covered by
`Kynakee.ContractTests.SharedKernel.IntegrationEventContractTests`; transport, Outbox and
consumer idempotency remain integration concerns.

```csharp
// Base class (Kynakee.Modules.SharedKernel.Integration)
public abstract class IntegrationEvent : IIntegrationEvent
{
    public Guid Id { get; } = Guid.NewGuid();
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
    public Guid TenantId { get; protected init; }
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

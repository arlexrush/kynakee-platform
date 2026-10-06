using Kynakee.Modules.SharedKernel.Application;

namespace Kynakee.Modules.Billing.Application.Queries.HasSufficientCredits;

public sealed record HasSufficientCreditsQuery(
    Guid TenantId,
    decimal RequiredAmount) : IQuery<bool>;
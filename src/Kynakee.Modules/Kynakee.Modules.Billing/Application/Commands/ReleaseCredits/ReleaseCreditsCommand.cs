using Kynakee.Modules.SharedKernel.Application;

namespace Kynakee.Modules.Billing.Application.Commands.ReleaseCredits;

public sealed record ReleaseCreditsCommand(Guid TenantId, Guid OperationId) : ICommand;
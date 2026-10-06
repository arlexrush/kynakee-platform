using Kynakee.Modules.SharedKernel.Application;

namespace Kynakee.Modules.Billing.Application.Commands.ConsumeCredits;

public sealed record ConsumeCreditsCommand(Guid TenantId, Guid OperationId) : ICommand;
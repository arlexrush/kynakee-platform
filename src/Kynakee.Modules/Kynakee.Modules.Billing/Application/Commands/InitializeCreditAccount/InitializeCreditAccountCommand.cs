using Kynakee.Modules.SharedKernel.Application;

namespace Kynakee.Modules.Billing.Application.Commands.InitializeCreditAccount;

public sealed record InitializeCreditAccountCommand(Guid TenantId, string PlanId) : ICommand;
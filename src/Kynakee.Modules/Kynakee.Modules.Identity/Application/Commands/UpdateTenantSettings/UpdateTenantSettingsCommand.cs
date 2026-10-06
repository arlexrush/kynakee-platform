using Kynakee.Modules.SharedKernel.Application;

namespace Kynakee.Modules.Identity.Application.Commands.UpdateTenantSettings;

public sealed record UpdateTenantSettingsCommand(
    Guid TenantId,
    decimal Administration,
    decimal Profit,
    decimal Quality,
    decimal SafetyHealth,
    decimal Environment,
    decimal Contingency) : ICommand;
using Kynakee.Modules.Identity.Application.Contracts;
using Kynakee.Modules.Identity.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;
using Kynakee.Modules.SharedKernel.Contracts;
using System.Text.Json.Serialization;

namespace Kynakee.Modules.Identity.Application.Commands.InviteUser;

public sealed record InviteUserCommand(
    Guid TenantId,
    Guid InviterUserId,
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    UserRole Role) : ICommand<InvitationCreatedResponse>, IHttpIdempotentCommand
{
    [JsonIgnore]
    public HttpIdempotencyContext? IdempotencyContext { get; init; }
}

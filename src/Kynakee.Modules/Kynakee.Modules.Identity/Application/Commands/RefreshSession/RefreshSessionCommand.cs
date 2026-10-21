using Kynakee.Modules.Identity.Application.Contracts;
using Kynakee.Modules.SharedKernel.Application;
using Kynakee.Modules.SharedKernel.Contracts;
using System.Text.Json.Serialization;

namespace Kynakee.Modules.Identity.Application.Commands.RefreshSession;

public sealed record RefreshSessionCommand(string RefreshToken) : ICommand<IdentitySessionResponse>, IHttpIdempotentCommand
{
    [JsonIgnore]
    public HttpIdempotencyContext? IdempotencyContext { get; init; }
}

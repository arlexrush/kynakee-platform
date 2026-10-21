using Kynakee.Modules.Identity.Application.Contracts;
using Kynakee.Modules.SharedKernel.Application;
using Kynakee.Modules.SharedKernel.Contracts;
using System.Text.Json.Serialization;

namespace Kynakee.Modules.Identity.Application.Commands.AccountActions;

/// <summary>Requests another verification email without disclosing account existence.</summary>
public sealed record ResendVerificationCommand(string Email) : ICommand<AccountActionAcknowledgement>, IHttpIdempotentCommand
{
    [JsonIgnore]
    public HttpIdempotencyContext? IdempotencyContext { get; init; }
}

/// <summary>Requests recovery without activating an account or issuing a session.</summary>
public sealed record RequestPasswordResetCommand(string Email) : ICommand<AccountActionAcknowledgement>, IHttpIdempotentCommand
{
    [JsonIgnore]
    public HttpIdempotencyContext? IdempotencyContext { get; init; }
}

/// <summary>Confirms possession of the registered mailbox using a single-use credential.</summary>
public sealed record VerifyEmailCommand(string Token) : ICommand<AccountActionAcknowledgement>;

/// <summary>Changes a password using a purpose-bound credential, without issuing a session.</summary>
public sealed record ResetPasswordCommand(string Token, string Password) : ICommand<AccountActionAcknowledgement>;

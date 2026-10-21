using Kynakee.Modules.Identity.Domain.Entities;

namespace Kynakee.Modules.Identity.Application.Contracts;

/// <summary>External delivery boundary for verification and password recovery credentials.</summary>
public interface IAccountEmailSender
{
    /// <summary>Delivers a single-use credential without logging it.</summary>
    Task SendAsync(string recipient, string token, AccountActionPurpose purpose, DateTimeOffset expiresAt, CancellationToken cancellationToken);
}

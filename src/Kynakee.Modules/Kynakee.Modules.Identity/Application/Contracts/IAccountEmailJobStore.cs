using Kynakee.Modules.Identity.Domain.Entities;

namespace Kynakee.Modules.Identity.Application.Contracts;

/// <summary>Durable email handoff participating in the caller's Identity transaction.</summary>
public interface IAccountEmailJobStore
{
    /// <summary>Stages one encrypted delivery without sending or committing independently.</summary>
    void Enqueue(AccountActionToken action, string recipient, string plaintextToken);
}

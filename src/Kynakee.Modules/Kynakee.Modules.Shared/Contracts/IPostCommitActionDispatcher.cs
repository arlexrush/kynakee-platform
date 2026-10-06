namespace Kynakee.Modules.SharedKernel.Contracts;

/// <summary>Schedules external actions to run after a successful module transaction.</summary>
public interface IPostCommitActionDispatcher
{
    /// <summary>Adds an asynchronous action to the current request's post-commit queue.</summary>
    void Enqueue(Func<CancellationToken, Task> action);

    /// <summary>Runs queued actions after the transaction has committed.</summary>
    Task ExecuteAsync(CancellationToken cancellationToken);
}
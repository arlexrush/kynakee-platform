using System.Text.Json.Serialization;
using Kynakee.Api.Application.Abstractions;
using Kynakee.Api.Application.Behaviors;
using Kynakee.Api.Infrastructure.Identity;
using Kynakee.Modules.SharedKernel.Application;
using Kynakee.Modules.SharedKernel.Contracts;
using MediatR;
using Xunit;

namespace Kynakee.UnitTests.Identity.Application;

public sealed class IdentityIdempotencyTransactionBehaviorTests
{
    private static readonly IHttpIdempotencyProtector Protector = new IdentityHttpIdempotencyProtector(new string('k', 32));

    [Fact]
    public async Task ExpiredClaimShouldRejectRetryWithoutHandlerExecution()
    {
        using var manager = new FakeTransactionManager();
        var behavior = CreateBehavior(manager);
        var command = CreateCommand("payload");
        await behavior.Handle(command, _ => Task.FromResult(ResultFactory.Success("completed")), TestContext.Current.CancellationToken);
        manager.ExpireRecords();
        var handlerCalls = 0;

        var result = await behavior.Handle(command, _ =>
        {
            handlerCalls++;
            return Task.FromResult(ResultFactory.Success("unexpected"));
        }, TestContext.Current.CancellationToken);

        Assert.Equal(("IDENTITY_IDEMPOTENCY_OUTCOME_UNAVAILABLE", 0), (result.Error?.Code, handlerCalls));
    }

    [Fact]
    public async Task ReplaySecurityFailureShouldNotExecuteHandler()
    {
        using var manager = new FakeTransactionManager();
        var behavior = CreateBehavior(manager);
        var command = CreateCommand("payload");
        await behavior.Handle(command, _ => Task.FromResult(ResultFactory.Success("completed")), TestContext.Current.CancellationToken);
        manager.ReplayError = ApplicationError.Unauthorized("IDENTITY_REPLAY_SESSION_UNAVAILABLE", "Unavailable.");
        var handlerCalls = 0;

        var result = await behavior.Handle(command, _ =>
        {
            handlerCalls++;
            return Task.FromResult(ResultFactory.Success("unexpected"));
        }, TestContext.Current.CancellationToken);

        Assert.Equal(("IDENTITY_REPLAY_SESSION_UNAVAILABLE", 0), (result.Error?.Code, handlerCalls));
    }

    [Fact]
    public async Task CorruptedStoredOutcomeShouldReturnConflictWithoutHandlerExecution()
    {
        using var manager = new FakeTransactionManager();
        var behavior = CreateBehavior(manager);
        var command = CreateCommand("payload");
        await behavior.Handle(command, _ => Task.FromResult(ResultFactory.Success("completed")), TestContext.Current.CancellationToken);
        manager.CorruptRecords();
        var handlerCalls = 0;

        var result = await behavior.Handle(command, _ =>
        {
            handlerCalls++;
            return Task.FromResult(ResultFactory.Success("unexpected"));
        }, TestContext.Current.CancellationToken);

        Assert.Equal(("IDENTITY_IDEMPOTENCY_OUTCOME_UNAVAILABLE", 0), (result.Error?.Code, handlerCalls));
    }

    [Fact]
    public async Task FailedBusinessRequestShouldNotReserveClaim()
    {
        using var manager = new FakeTransactionManager();
        var behavior = CreateBehavior(manager);
        var command = CreateCommand("payload");
        await behavior.Handle(command, _ => Task.FromResult(ResultFactory.Failure<string>(
            ApplicationError.Conflict("BUSINESS_FAILED", "Failure."))), TestContext.Current.CancellationToken);

        var retry = await behavior.Handle(command, _ => Task.FromResult(ResultFactory.Success("completed")), TestContext.Current.CancellationToken);

        Assert.Equal("completed", retry.Value);
    }

    [Fact]
    public async Task RepeatedRequestShouldReplaySuccessfulOutcomeWithoutExecutingHandlerAgain()
    {
        using var manager = new FakeTransactionManager();
        var behavior = CreateBehavior(manager);
        var command = CreateCommand("payload");
        var handlerCalls = 0;

        var first = await behavior.Handle(
            command,
            _ =>
            {
                handlerCalls++;
                return Task.FromResult(ResultFactory.Success("completed"));
            },
            TestContext.Current.CancellationToken);
        var replay = await behavior.Handle(
            command,
            _ =>
            {
                handlerCalls++;
                return Task.FromResult(ResultFactory.Success("unexpected"));
            },
            TestContext.Current.CancellationToken);

        Assert.Equal("completed", first.Value);
        Assert.Equal("completed", replay.Value);
        Assert.Equal(1, handlerCalls);
    }

    [Fact]
    public async Task SameKeyWithDifferentRequestShouldReturnConflictWithoutExecutingHandlerAgain()
    {
        using var manager = new FakeTransactionManager();
        var behavior = CreateBehavior(manager);
        var handlerCalls = 0;

        await behavior.Handle(
            CreateCommand("first"),
            _ =>
            {
                handlerCalls++;
                return Task.FromResult(ResultFactory.Success("completed"));
            },
            TestContext.Current.CancellationToken);
        var conflict = await behavior.Handle(
            CreateCommand("different"),
            _ =>
            {
                handlerCalls++;
                return Task.FromResult(ResultFactory.Success("unexpected"));
            },
            TestContext.Current.CancellationToken);

        Assert.True(conflict.IsFailure);
        Assert.Equal("IDENTITY_IDEMPOTENCY_KEY_REUSED", conflict.Error!.Code);
        Assert.Equal(1, handlerCalls);
    }

    [Fact]
    public async Task ConcurrentSameKeyRequestsShouldExecuteHandlerOnce()
    {
        using var manager = new FakeTransactionManager();
        var behavior = CreateBehavior(manager);
        var command = CreateCommand("payload");
        var handlerCalls = 0;
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<Result<string>> SendAsync()
        {
            await start.Task.ConfigureAwait(false);
            return await behavior.Handle(
                command,
                async cancellationToken =>
                {
                    Interlocked.Increment(ref handlerCalls);
                    await Task.Delay(50, cancellationToken).ConfigureAwait(false);
                    return ResultFactory.Success("completed");
                },
                TestContext.Current.CancellationToken).ConfigureAwait(false);
        }

        var first = SendAsync();
        var second = SendAsync();
        start.SetResult();
        var results = await Task.WhenAll(first, second);

        Assert.All(results, result => Assert.Equal("completed", result.Value));
        Assert.Equal(1, handlerCalls);
    }

    private static TransactionBehavior<TestCommand, Result<string>> CreateBehavior(FakeTransactionManager manager) =>
        new(manager, new FakePostCommitActionDispatcher(), Protector);

    private static TestCommand CreateCommand(string value) => new(value)
    {
        IdempotencyContext = new HttpIdempotencyContext(
            "identity.test",
            "0123456789abcdefghijkl",
            Guid.Empty,
            Guid.Empty)
    };

    private sealed record TestCommand(string Value) : ICommand<Result<string>>, IHttpIdempotentCommand
    {
        [JsonIgnore]
        public HttpIdempotencyContext? IdempotencyContext { get; init; }
    }

    private sealed class FakeTransactionManager : ITransactionManager, IDisposable
    {
        private readonly SemaphoreSlim _lock = new(1, 1);
        private readonly Dictionary<string, (string Fingerprint, byte[] Outcome, DateTimeOffset ExpiresAt)> _records = [];
        internal ApplicationError? ReplayError { get; set; }

        internal void CorruptRecords()
        {
            foreach (var key in _records.Keys.ToArray())
            {
                var record = _records[key];
                _records[key] = (record.Fingerprint, new byte[28], record.ExpiresAt);
            }
        }

        internal void ExpireRecords()
        {
            foreach (var key in _records.Keys.ToArray())
            {
                var record = _records[key];
                _records[key] = (record.Fingerprint, record.Outcome, DateTimeOffset.UtcNow.AddHours(-1));
            }
        }

        public Task<IModuleTransactionScope> BeginAsync(Type requestType, CancellationToken cancellationToken) =>
            Task.FromResult<IModuleTransactionScope>(new FakeTransactionScope(this));

        public void Dispose() => _lock.Dispose();

        private sealed class FakeTransactionScope(FakeTransactionManager owner) : IModuleTransactionScope, IIdempotencyTransactionScope
        {
            private (string KeyHash, string Fingerprint, byte[] Outcome, DateTimeOffset ExpiresAt)? _pending;
            private bool _ownsLock;

            public IIdempotencyTransactionScope? Idempotency => this;

            public Task<Kynakee.Modules.SharedKernel.Application.ApplicationError?> ValidateReplayAsync(
                object request, object response, CancellationToken cancellationToken) =>
                Task.FromResult(owner.ReplayError);

            public async Task AcquireLockAsync(IdempotencyRequest request, CancellationToken cancellationToken)
            {
                await owner._lock.WaitAsync(cancellationToken).ConfigureAwait(false);
                _ownsLock = true;
            }

            public Task<IdempotencyLookupResult> AcquireAsync(IdempotencyRequest request, CancellationToken cancellationToken)
            {
                if (!owner._records.TryGetValue(request.KeyHash, out var record))
                {
                    return Task.FromResult(new IdempotencyLookupResult(false, true, null));
                }

                return Task.FromResult(new IdempotencyLookupResult(
                    true,
                    record.Fingerprint == request.Fingerprint,
                    record.Outcome,
                    record.ExpiresAt <= DateTimeOffset.UtcNow));
            }

            public Task StoreAsync(
                IdempotencyRequest request,
                byte[] encryptedOutcome,
                DateTimeOffset expiresAt,
                CancellationToken cancellationToken)
            {
                _pending = (request.KeyHash, request.Fingerprint, encryptedOutcome.ToArray(), expiresAt);
                return Task.CompletedTask;
            }

            public Task CommitAsync(CancellationToken cancellationToken)
            {
                if (_pending is { } pending)
                {
                    owner._records[pending.KeyHash] = (pending.Fingerprint, pending.Outcome, pending.ExpiresAt);
                }

                return Task.CompletedTask;
            }

            public Task RollbackAsync(CancellationToken cancellationToken)
            {
                _pending = null;
                return Task.CompletedTask;
            }

            public ValueTask DisposeAsync()
            {
                if (_ownsLock)
                {
                    owner._lock.Release();
                    _ownsLock = false;
                }

                return ValueTask.CompletedTask;
            }
        }
    }

    private sealed class FakePostCommitActionDispatcher : IPostCommitActionDispatcher
    {
        public void Enqueue(Func<CancellationToken, Task> action)
        {
        }

        public Task ExecuteAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}

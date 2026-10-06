using System.Collections.Concurrent;
using Kynakee.Modules.Mcp.Contracts;
using Kynakee.Modules.SharedKernel.Application;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace Kynakee.Modules.Mcp.Infrastructure.Client;

internal sealed class McpServerResilience
{
    private readonly ConcurrentDictionary<Guid, ResiliencePipeline<Result<MCPQueryResult>>> _pipelines = new();

    internal ResiliencePipeline<Result<MCPQueryResult>> ForServer(Guid serverId) =>
        _pipelines.GetOrAdd(serverId, _ => new ResiliencePipelineBuilder<Result<MCPQueryResult>>()
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<Result<MCPQueryResult>>
            {
                ShouldHandle = new PredicateBuilder<Result<MCPQueryResult>>()
                    .HandleResult(IsTransient),
                FailureRatio = 1,
                MinimumThroughput = 3,
                SamplingDuration = TimeSpan.FromMinutes(1),
                BreakDuration = TimeSpan.FromSeconds(30)
            })
            .AddRetry(new RetryStrategyOptions<Result<MCPQueryResult>>
            {
                ShouldHandle = new PredicateBuilder<Result<MCPQueryResult>>()
                    .HandleResult(IsTransient),
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(200),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true
            })
            .Build());

    private static bool IsTransient(Result<MCPQueryResult> result) =>
        result.Error?.Code is "MCP_TRANSPORT_FAILURE" or "MCP_TIMEOUT";
}

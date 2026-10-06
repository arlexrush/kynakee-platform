using FluentAssertions;
using Kynakee.Modules.Ai.Domain.Entities;
using Kynakee.Modules.Ai.Domain.Enums;
using Xunit;

namespace Kynakee.UnitTests.Ai.Domain;

public class AgentRunTests
{
    [Fact]
    public void CreateShouldInitializeQueuedRun()
    {
        var run = CreateQueuedRun();

        run.Status.Should().Be(AgentRunStatus.Queued);
        run.InputTokens.Should().Be(0);
        run.OutputTokens.Should().Be(0);
        run.CreditsCharged.Should().Be(0);
    }

    [Fact]
    public void CreateShouldAllowTenantScopedRunWithoutProject()
    {
        var result = AgentRun.Create(
            Guid.NewGuid(),
            null,
            AgentType.Embedding,
            "gemini-embedding",
            "Gemini");

        result.IsSuccess.Should().BeTrue();
        result.Value!.ProjectId.Should().BeNull();
    }

    [Fact]
    public void CreateShouldRejectEmptyTenant()
    {
        var result = AgentRun.Create(
            Guid.Empty,
            Guid.NewGuid(),
            AgentType.Capture,
            "gemini-flash",
            "Gemini");

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("AI_001");
    }

    [Fact]
    public void CompleteShouldRecordSuccessfulUsage()
    {
        var run = CreateQueuedRun();

        var result = run.Complete(
            "deepseek-chat",
            "DeepSeek",
            120,
            45,
            2.5m,
            TimeSpan.FromSeconds(3),
            false,
            null);

        result.IsSuccess.Should().BeTrue();
        run.Status.Should().Be(AgentRunStatus.Success);
        run.InputTokens.Should().Be(120);
        run.OutputTokens.Should().Be(45);
        run.CreditsCharged.Should().Be(2.5m);
    }

    [Fact]
    public void CompleteShouldMarkFallbackUse()
    {
        var run = CreateQueuedRun();

        var result = run.Complete(
            "deepseek-chat",
            "DeepSeek",
            120,
            45,
            2.5m,
            TimeSpan.FromSeconds(3),
            true,
            "Primary model unavailable.");

        result.IsSuccess.Should().BeTrue();
        run.Status.Should().Be(AgentRunStatus.FallbackUsed);
        run.FallbackActivated.Should().BeTrue();
        run.FallbackReason.Should().Be("Primary model unavailable.");
    }

    [Fact]
    public void CompleteShouldRequireFallbackReason()
    {
        var run = CreateQueuedRun();

        var result = run.Complete(
            "deepseek-chat",
            "DeepSeek",
            120,
            45,
            2.5m,
            TimeSpan.FromSeconds(3),
            true,
            null);

        result.IsFailure.Should().BeTrue();
        run.Status.Should().Be(AgentRunStatus.Queued);
    }

    [Fact]
    public void FailShouldNotChargeCredits()
    {
        var run = CreateQueuedRun();

        var result = run.Fail(
            "deepseek-chat",
            "DeepSeek",
            120,
            0,
            TimeSpan.FromSeconds(3),
            "Fallback model also failed.",
            true);

        result.IsSuccess.Should().BeTrue();
        run.Status.Should().Be(AgentRunStatus.Failed);
        run.CreditsCharged.Should().Be(0);
        run.FallbackActivated.Should().BeTrue();
    }

    [Fact]
    public void CompleteShouldRejectRunThatIsNotQueued()
    {
        var run = CreateQueuedRun();
        run.Complete(
            "deepseek-chat",
            "DeepSeek",
            1,
            1,
            0,
            TimeSpan.Zero,
            false,
            null);

        var result = run.Complete(
            "deepseek-chat",
            "DeepSeek",
            1,
            1,
            0,
            TimeSpan.Zero,
            false,
            null);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void MarkHumanReviewedShouldRecordReviewer()
    {
        var run = CreateQueuedRun();
        run.Complete(
            "deepseek-chat",
            "DeepSeek",
            1,
            1,
            0,
            TimeSpan.Zero,
            false,
            null);
        var reviewerId = Guid.NewGuid();

        var result = run.MarkHumanReviewed(reviewerId);

        result.IsSuccess.Should().BeTrue();
        run.HumanReviewed.Should().BeTrue();
        run.HumanReviewerId.Should().Be(reviewerId);
        run.HumanReviewedAt.Should().NotBeNull();
    }

    private static AgentRun CreateQueuedRun() =>
        AgentRun.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            AgentType.Capture,
            "gemini-flash",
            "Gemini").Value!;
}

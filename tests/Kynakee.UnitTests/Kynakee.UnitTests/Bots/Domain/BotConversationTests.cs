using FluentAssertions;
using Kynakee.Modules.Bots.Domain.Aggregates;
using Kynakee.Modules.Bots.Domain.Enums;
using Kynakee.Modules.Bots.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;
using Xunit;

namespace Kynakee.UnitTests.Bots.Domain;

public sealed class BotConversationTests
{
    [Theory]
    [InlineData(BotChannel.Telegram)]
    [InlineData(BotChannel.WhatsApp)]
    public void ConversationCreationAuthenticatedIdentityShouldInitializeSession(BotChannel channel)
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var result = BotConversation.Create(tenantId, userId, "  external-123  ", channel);

        result.Value.Should().BeEquivalentTo(new
        {
            TenantId = tenantId,
            UserId = userId,
            ExternalId = "external-123",
            Channel = channel,
            State = ConversationState.NewSession,
            Verbosity = BotVerbosity.Normal,
            CreatedBy = (Guid?)userId,
            ActiveProjectId = (Guid?)null,
            IsDeleted = false
        });
    }

    [Fact]
    public void ConversationCreationValidIdentityShouldAssignNonemptyId()
    {
        var conversation = CreateConversation();

        conversation.Id.Value.Should().NotBeEmpty();
    }

    [Fact]
    public void ConversationCreationMissingTenantShouldReturnValidation()
    {
        var result = BotConversation.Create(Guid.Empty, Guid.NewGuid(), "external", BotChannel.Telegram);

        result.Error.Should().BeEquivalentTo(new { Code = "BOTS_TENANT_REQUIRED", Type = ErrorType.Validation });
    }

    [Fact]
    public void ConversationCreationMissingUserShouldReturnValidation()
    {
        var result = BotConversation.Create(Guid.NewGuid(), Guid.Empty, "external", BotChannel.Telegram);

        result.Error.Should().BeEquivalentTo(new { Code = "BOTS_USER_REQUIRED", Type = ErrorType.Validation });
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ConversationCreationMissingExternalIdShouldReturnValidation(string? externalId)
    {
        var result = BotConversation.Create(Guid.NewGuid(), Guid.NewGuid(), externalId!, BotChannel.Telegram);

        result.Error!.Code.Should().Be("BOTS_EXTERNAL_ID_INVALID");
    }

    [Fact]
    public void ConversationCreationLongExternalIdShouldReturnValidation()
    {
        var result = BotConversation.Create(Guid.NewGuid(), Guid.NewGuid(), new string('a', 101), BotChannel.Telegram);

        result.Error!.Code.Should().Be("BOTS_EXTERNAL_ID_INVALID");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public void ConversationCreationUnknownChannelShouldReturnValidation(int channel)
    {
        var result = BotConversation.Create(Guid.NewGuid(), Guid.NewGuid(), "external", (BotChannel)channel);

        result.Error!.Code.Should().Be("BOTS_CHANNEL_INVALID");
    }

    [Fact]
    public void ConversationCreationEmptyCreatorShouldReturnValidation()
    {
        var result = BotConversation.Create(Guid.NewGuid(), Guid.NewGuid(), "external", BotChannel.Telegram, Guid.Empty);

        result.Error!.Code.Should().Be("BOTS_CREATOR_INVALID");
    }

    [Fact]
    public void ConversationActivationValidProjectShouldSetProjectAndAudit()
    {
        var conversation = CreateConversation();
        var projectId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        conversation.ActivateProject(projectId, actorId);

        conversation.Should().BeEquivalentTo(new
        {
            ActiveProjectId = (Guid?)projectId,
            State = ConversationState.InProject,
            UpdatedBy = (Guid?)actorId
        });
    }

    [Fact]
    public void ConversationActivationEmptyProjectShouldPreserveSession()
    {
        var conversation = CreateConversation();

        conversation.ActivateProject(Guid.Empty);

        conversation.Should().BeEquivalentTo(new { ActiveProjectId = (Guid?)null, State = ConversationState.NewSession });
    }

    [Fact]
    public void ConversationPauseActiveProjectShouldRetainProject()
    {
        var conversation = CreateConversation();
        var projectId = Guid.NewGuid();
        conversation.ActivateProject(projectId);

        conversation.Pause();

        conversation.Should().BeEquivalentTo(new
        {
            ActiveProjectId = (Guid?)projectId,
            State = ConversationState.Paused,
            UpdatedBy = (Guid?)conversation.UserId
        });
    }

    [Theory]
    [InlineData(BotVerbosity.Normal)]
    [InlineData(BotVerbosity.Detailed)]
    [InlineData(BotVerbosity.Concise)]
    public void ConversationVerbosityValidValueShouldUpdatePreference(BotVerbosity verbosity)
    {
        var conversation = CreateConversation();

        conversation.ChangeVerbosity(verbosity);

        conversation.Verbosity.Should().Be(verbosity);
    }

    [Fact]
    public void ConversationVerbosityImplicitActorShouldAuditConversationUser()
    {
        var conversation = CreateConversation();

        conversation.ChangeVerbosity(BotVerbosity.Concise);

        conversation.UpdatedBy.Should().Be(conversation.UserId);
    }

    [Fact]
    public void ConversationVerbosityUnknownValueShouldPreservePreference()
    {
        var conversation = CreateConversation();

        conversation.ChangeVerbosity((BotVerbosity)99);

        conversation.Verbosity.Should().Be(BotVerbosity.Normal);
    }

    [Fact]
    public void ConversationInteractionPausedSessionShouldNotResume()
    {
        var conversation = CreateConversation();
        conversation.Pause();

        conversation.RegisterInteraction();

        conversation.State.Should().Be(ConversationState.Paused);
    }

    [Fact]
    public void ConversationInteractionValidActorShouldRecordUtcActivity()
    {
        var conversation = CreateConversation();
        var before = DateTime.UtcNow;

        conversation.RegisterInteraction();

        conversation.LastInteractionAt.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void ConversationInteractionEmptyActorShouldReturnValidation()
    {
        var conversation = CreateConversation();

        var result = conversation.RegisterInteraction(Guid.Empty);

        result.Error!.Code.Should().Be("BOTS_UPDATER_INVALID");
    }

    [Fact]
    public void ConversationActivationDeletedSessionShouldReturnConflict()
    {
        var conversation = CreateConversation();
        conversation.Delete();

        var result = conversation.ActivateProject(Guid.NewGuid());

        result.Error.Should().BeEquivalentTo(new { Code = "BOTS_CONVERSATION_DELETED", Type = ErrorType.Conflict });
    }

    [Fact]
    public void ConversationPauseDeletedSessionShouldReturnConflict()
    {
        var conversation = CreateConversation();
        conversation.Delete();

        var result = conversation.Pause();

        result.Error!.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public void ConversationVerbosityDeletedSessionShouldReturnConflict()
    {
        var conversation = CreateConversation();
        conversation.Delete();

        var result = conversation.ChangeVerbosity(BotVerbosity.Detailed);

        result.Error!.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public void ConversationInteractionDeletedSessionShouldReturnConflict()
    {
        var conversation = CreateConversation();
        conversation.Delete();

        var result = conversation.RegisterInteraction();

        result.Error!.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public void ConversationTimeoutNewSessionShouldRemainActiveWithinThirtyMinutes()
    {
        var conversation = CreateConversation();

        var timedOut = conversation.IsTimedOut(TimeSpan.FromMinutes(30));

        timedOut.Should().BeFalse();
    }

    [Fact]
    public void ConversationTimeoutQueryShouldNotMutateActivity()
    {
        var conversation = CreateConversation();
        var activity = conversation.LastInteractionAt;

        conversation.IsTimedOut(TimeSpan.FromHours(24));

        conversation.LastInteractionAt.Should().Be(activity);
    }

    [Fact]
    public void ConversationIdentifierNewShouldGenerateDistinctValues()
    {
        var first = BotConversationId.New();

        var second = BotConversationId.New();

        second.Should().NotBe(first);
    }

    private static BotConversation CreateConversation() =>
        BotConversation.Create(Guid.NewGuid(), Guid.NewGuid(), "external", BotChannel.Telegram).Value
        ?? throw new InvalidOperationException();
}

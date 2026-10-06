using Kynakee.Modules.Bots.Domain.Enums;
using Kynakee.Modules.Bots.Domain.Entities;
using Kynakee.Modules.Bots.Domain.Resources;
using Kynakee.Modules.Bots.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;
using Kynakee.Modules.SharedKernel.Domain;

namespace Kynakee.Modules.Bots.Domain.Aggregates;

public sealed class BotConversation : AggregateRoot<BotConversationId>
{
    private readonly List<BotMessage> _messages = [];

    private BotConversation()
    {
    }

    private BotConversation(
        BotConversationId id,
        Guid tenantId,
        Guid userId,
        string externalId,
        BotChannel channel,
        Guid? createdBy)
        : base(id, tenantId, createdBy)
    {
        UserId = userId;
        ExternalId = externalId;
        Channel = channel;
        State = ConversationState.NewSession;
        LastInteractionAt = DateTime.UtcNow;
        Verbosity = BotVerbosity.Normal;
    }

    public string ExternalId { get; private set; } = string.Empty;

    public BotChannel Channel { get; private set; }

    public Guid UserId { get; private set; }

    public Guid? ActiveProjectId { get; private set; }

    public ConversationState State { get; private set; }

    public DateTime LastInteractionAt { get; private set; }

    public BotVerbosity Verbosity { get; private set; }

    /// <summary>Creates a business conversation for a user already authenticated by its channel.</summary>
    public static Result<BotConversation> Create(
        Guid tenantId,
        Guid authenticatedUserId,
        string externalId,
        BotChannel channel,
        Guid? createdBy = null)
    {
        if (tenantId == Guid.Empty)
        {
            return Failure<BotConversation>("BOTS_TENANT_REQUIRED", "TenantRequired");
        }

        if (authenticatedUserId == Guid.Empty)
        {
            return Failure<BotConversation>("BOTS_USER_REQUIRED", "UserRequired");
        }

        if (string.IsNullOrWhiteSpace(externalId) || externalId.Trim().Length > 100)
        {
            return Failure<BotConversation>("BOTS_EXTERNAL_ID_INVALID", "ExternalIdInvalid");
        }

        if (!Enum.IsDefined(channel))
        {
            return Failure<BotConversation>("BOTS_CHANNEL_INVALID", "ChannelInvalid");
        }

        if (createdBy == Guid.Empty)
        {
            return Failure<BotConversation>("BOTS_CREATOR_INVALID", "CreatorInvalid");
        }

        return ResultFactory.Success(new BotConversation(
            BotConversationId.New(),
            tenantId,
            authenticatedUserId,
            externalId.Trim(),
            channel,
            createdBy ?? authenticatedUserId));
    }

    /// <summary>Associates the conversation with a project without accessing another module's persistence.</summary>
    public Result ActivateProject(Guid projectId, Guid? updatedBy = null)
    {
        var canUpdate = EnsureCanUpdate(updatedBy);
        if (!canUpdate.IsSuccess)
        {
            return canUpdate;
        }

        if (projectId == Guid.Empty)
        {
            return Failure("BOTS_PROJECT_ID_INVALID", "ProjectIdInvalid");
        }

        ActiveProjectId = projectId;
        State = ConversationState.InProject;
        RegisterInteractionCore(updatedBy);
        return ResultFactory.Ok();
    }

    /// <summary>Adds a message to this conversation without loading its existing history.</summary>
    public Result<BotMessage> AddMessage(
        BotMessageDirection direction,
        BotMessageContentType contentType,
        string? content,
        Uri? mediaUrl = null,
        string? parsedCommand = null,
        Guid? createdBy = null)
    {
        var canUpdate = EnsureCanUpdate(createdBy);
        if (!canUpdate.IsSuccess)
        {
            return ResultFactory.Failure<BotMessage>(canUpdate.Error!);
        }

        var messageResult = BotMessage.Create(
            Id,
            TenantId,
            direction,
            contentType,
            content,
            mediaUrl,
            parsedCommand,
            createdBy ?? UserId);

        if (messageResult.IsFailure)
        {
            return messageResult;
        }

        _messages.Add(messageResult.Value!);
        RegisterInteractionCore(createdBy);
        return messageResult;
    }

    /// <summary>Pauses an active business conversation.</summary>
    public Result Pause(Guid? updatedBy = null)
    {
        var canUpdate = EnsureCanUpdate(updatedBy);
        if (!canUpdate.IsSuccess)
        {
            return canUpdate;
        }

        State = ConversationState.Paused;
        RegisterInteractionCore(updatedBy);
        return ResultFactory.Ok();
    }

    /// <summary>Changes how verbose responses should be.</summary>
    public Result ChangeVerbosity(BotVerbosity verbosity, Guid? updatedBy = null)
    {
        var canUpdate = EnsureCanUpdate(updatedBy);
        if (!canUpdate.IsSuccess)
        {
            return canUpdate;
        }

        if (!Enum.IsDefined(verbosity))
        {
            return Failure("BOTS_VERBOSITY_INVALID", "VerbosityInvalid");
        }

        Verbosity = verbosity;
        RegisterUpdate(updatedBy ?? UserId);
        return ResultFactory.Ok();
    }

    /// <summary>Records business-message activity without implicitly resuming a paused conversation.</summary>
    public Result RegisterInteraction(Guid? updatedBy = null)
    {
        var canUpdate = EnsureCanUpdate(updatedBy);
        if (!canUpdate.IsSuccess)
        {
            return canUpdate;
        }

        RegisterInteractionCore(updatedBy);
        return ResultFactory.Ok();
    }

    /// <summary>Checks whether the conversation has been inactive longer than the supplied timeout.</summary>
    public bool IsTimedOut(TimeSpan timeout) =>
        timeout > TimeSpan.Zero && DateTime.UtcNow - LastInteractionAt > timeout;

    private Result EnsureCanUpdate(Guid? updatedBy)
    {
        if (IsDeleted)
        {
            return Conflict("BOTS_CONVERSATION_DELETED", "ConversationDeleted");
        }

        if (State == ConversationState.Completed)
        {
            return Conflict("BOTS_CONVERSATION_COMPLETED", "ConversationCompleted");
        }

        if (updatedBy == Guid.Empty)
        {
            return Failure("BOTS_UPDATER_INVALID", "UpdaterInvalid");
        }

        return ResultFactory.Ok();
    }

    private void RegisterInteractionCore(Guid? updatedBy)
    {
        LastInteractionAt = DateTime.UtcNow;
        RegisterUpdate(updatedBy ?? UserId);
    }

    private static Result<T> Failure<T>(string code, string message) =>
        ResultFactory.Failure<T>(ApplicationError.Validation(code, BotsMessages.Get(message)));

    private static Result Failure(string code, string message) =>
        ResultFactory.Failure(ApplicationError.Validation(code, BotsMessages.Get(message)));

    private static Result Conflict(string code, string message) =>
        ResultFactory.Failure(ApplicationError.Conflict(code, BotsMessages.Get(message)));
}
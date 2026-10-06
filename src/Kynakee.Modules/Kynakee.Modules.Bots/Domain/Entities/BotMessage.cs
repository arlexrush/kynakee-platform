using Kynakee.Modules.Bots.Domain.Enums;
using Kynakee.Modules.Bots.Domain.Aggregates;
using Kynakee.Modules.Bots.Domain.Resources;
using Kynakee.Modules.Bots.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;
using Kynakee.Modules.SharedKernel.Domain;

namespace Kynakee.Modules.Bots.Domain.Entities;

public sealed class BotMessage : BaseEntity<BotMessageId>
{
    private BotMessage()
    {
    }

    private BotMessage(
        BotMessageId id,
        BotConversationId conversationId,
        Guid tenantId,
        BotMessageDirection direction,
        BotMessageContentType contentType,
        string? content,
        Uri? mediaUrl,
        string? parsedCommand,
        Guid createdBy)
        : base(id, tenantId, createdBy)
    {
        ConversationId = conversationId;
        Direction = direction;
        ContentType = contentType;
        Content = content;
        MediaUrl = mediaUrl;
        ParsedCommand = parsedCommand;
    }

    public BotConversationId ConversationId { get; private set; }

    public BotConversation Conversation { get; private set; } = null!;

    public BotMessageDirection Direction { get; private set; }

    public BotMessageContentType ContentType { get; private set; }

    public string? Content { get; private set; }

    public Uri? MediaUrl { get; private set; }

    public string? ParsedCommand { get; private set; }

    internal static Result<BotMessage> Create(
        BotConversationId conversationId,
        Guid tenantId,
        BotMessageDirection direction,
        BotMessageContentType contentType,
        string? content,
        Uri? mediaUrl,
        string? parsedCommand,
        Guid createdBy)
    {
        if (conversationId.Value == Guid.Empty)
        {
            return Failure("BOTS_CONVERSATION_REQUIRED", "ConversationRequired");
        }

        if (tenantId == Guid.Empty)
        {
            return Failure("BOTS_TENANT_REQUIRED", "TenantRequired");
        }

        if (createdBy == Guid.Empty)
        {
            return Failure("BOTS_MESSAGE_CREATOR_INVALID", "CreatorInvalid");
        }

        if (!Enum.IsDefined(direction))
        {
            return Failure("BOTS_MESSAGE_DIRECTION_INVALID", "DirectionInvalid");
        }

        if (!Enum.IsDefined(contentType))
        {
            return Failure("BOTS_MESSAGE_CONTENT_TYPE_INVALID", "ContentTypeInvalid");
        }

        var normalizedContent = string.IsNullOrWhiteSpace(content) ? null : content.Trim();
        var normalizedCommand = string.IsNullOrWhiteSpace(parsedCommand) ? null : parsedCommand.Trim();

        if (normalizedContent is null && mediaUrl is null)
        {
            return Failure("BOTS_MESSAGE_CONTENT_REQUIRED", "ContentRequired");
        }

        if (mediaUrl is not null &&
            (!mediaUrl.IsAbsoluteUri ||
             mediaUrl.Scheme != Uri.UriSchemeHttps ||
             mediaUrl.ToString().Length > 500))
        {
            return Failure("BOTS_MESSAGE_MEDIA_URL_INVALID", "MediaUrlInvalid");
        }

        if (normalizedCommand?.Length > 100 ||
            (normalizedCommand is not null && contentType != BotMessageContentType.Command))
        {
            return Failure("BOTS_MESSAGE_PARSED_COMMAND_INVALID", "ParsedCommandInvalid");
        }

        return ResultFactory.Success(new BotMessage(
            BotMessageId.New(),
            conversationId,
            tenantId,
            direction,
            contentType,
            normalizedContent,
            mediaUrl,
            normalizedCommand,
            createdBy));
    }

    private static Result<BotMessage> Failure(string code, string message) =>
        ResultFactory.Failure<BotMessage>(
            ApplicationError.Validation(code, BotsMessages.Get(message)));
}
using FluentAssertions;
using Kynakee.Modules.Bots.Domain.Aggregates;
using Kynakee.Modules.Bots.Domain.Enums;
using Kynakee.Modules.Bots.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;
using Xunit;

namespace Kynakee.UnitTests.Bots.Domain;

public sealed class BotMessageTests
{
    [Theory]
    [InlineData(BotMessageDirection.Inbound)]
    [InlineData(BotMessageDirection.Outbound)]
    public void MessageCreationValidTextShouldInheritConversationIdentity(BotMessageDirection direction)
    {
        var conversation = CreateConversation();

        var result = conversation.AddMessage(direction, BotMessageContentType.Text, " hello ");

        result.Value.Should().BeEquivalentTo(new
        {
            ConversationId = conversation.Id,
            TenantId = conversation.TenantId,
            Direction = direction,
            ContentType = BotMessageContentType.Text,
            Content = "hello",
            CreatedBy = (Guid?)conversation.UserId,
            IsDeleted = false
        });
    }

    [Theory]
    [InlineData(BotMessageContentType.Image)]
    [InlineData(BotMessageContentType.Audio)]
    [InlineData(BotMessageContentType.Video)]
    [InlineData(BotMessageContentType.Document)]
    public void MessageCreationMediaOnlyShouldRetainHttpsAddress(BotMessageContentType contentType)
    {
        var conversation = CreateConversation();
        var media = new Uri("https://media.example.test/file");

        var result = conversation.AddMessage(BotMessageDirection.Inbound, contentType, null, media);

        result.Value!.MediaUrl.Should().Be(media);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MessageCreationEmptyPayloadShouldReturnValidation(string? content)
    {
        var conversation = CreateConversation();

        var result = conversation.AddMessage(BotMessageDirection.Inbound, BotMessageContentType.Text, content);

        result.Error.Should().BeEquivalentTo(new { Code = "BOTS_MESSAGE_CONTENT_REQUIRED", Type = ErrorType.Validation });
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public void MessageCreationUnknownDirectionShouldReturnValidation(int direction)
    {
        var conversation = CreateConversation();

        var result = conversation.AddMessage((BotMessageDirection)direction, BotMessageContentType.Text, "hello");

        result.Error!.Code.Should().Be("BOTS_MESSAGE_DIRECTION_INVALID");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(6)]
    public void MessageCreationUnknownContentTypeShouldReturnValidation(int contentType)
    {
        var conversation = CreateConversation();

        var result = conversation.AddMessage(BotMessageDirection.Inbound, (BotMessageContentType)contentType, "hello");

        result.Error!.Code.Should().Be("BOTS_MESSAGE_CONTENT_TYPE_INVALID");
    }

    [Theory]
    [InlineData("http://media.example.test/file")]
    [InlineData("file:///tmp/file")]
    [InlineData("relative/file")]
    public void MessageCreationUnsafeAddressShouldReturnValidation(string address)
    {
        var conversation = CreateConversation();
        var media = new Uri(address, UriKind.RelativeOrAbsolute);

        var result = conversation.AddMessage(BotMessageDirection.Inbound, BotMessageContentType.Image, null, media);

        result.Error!.Code.Should().Be("BOTS_MESSAGE_MEDIA_URL_INVALID");
    }

    [Fact]
    public void MessageCreationLongAddressShouldReturnValidation()
    {
        var conversation = CreateConversation();
        var media = new Uri("https://media.example.test/" + new string('a', 500));

        var result = conversation.AddMessage(BotMessageDirection.Inbound, BotMessageContentType.Image, null, media);

        result.Error!.Code.Should().Be("BOTS_MESSAGE_MEDIA_URL_INVALID");
    }

    [Fact]
    public void MessageCreationCommandShouldNormalizeParsedCommand()
    {
        var conversation = CreateConversation();

        var result = conversation.AddMessage(
            BotMessageDirection.Inbound, BotMessageContentType.Command, "/pause", parsedCommand: " pause ");

        result.Value!.ParsedCommand.Should().Be("pause");
    }

    [Fact]
    public void MessageCreationLongCommandShouldReturnValidation()
    {
        var conversation = CreateConversation();

        var result = conversation.AddMessage(
            BotMessageDirection.Inbound, BotMessageContentType.Command, "/pause", parsedCommand: new string('a', 101));

        result.Error!.Code.Should().Be("BOTS_MESSAGE_PARSED_COMMAND_INVALID");
    }

    [Fact]
    public void MessageCreationCommandOnTextShouldReturnValidation()
    {
        var conversation = CreateConversation();

        var result = conversation.AddMessage(
            BotMessageDirection.Inbound, BotMessageContentType.Text, "hello", parsedCommand: "pause");

        result.Error!.Code.Should().Be("BOTS_MESSAGE_PARSED_COMMAND_INVALID");
    }

    [Fact]
    public void MessageCreationEmptyActorShouldReturnValidation()
    {
        var conversation = CreateConversation();

        var result = conversation.AddMessage(
            BotMessageDirection.Inbound, BotMessageContentType.Text, "hello", createdBy: Guid.Empty);

        result.Error!.Code.Should().Be("BOTS_UPDATER_INVALID");
    }

    [Fact]
    public void MessageCreationDeletedConversationShouldReturnConflict()
    {
        var conversation = CreateConversation();
        conversation.Delete();

        var result = conversation.AddMessage(BotMessageDirection.Inbound, BotMessageContentType.Text, "hello");

        result.Error!.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public void MessageCreationValidPayloadShouldAuditConversationActor()
    {
        var conversation = CreateConversation();
        var actorId = Guid.NewGuid();

        conversation.AddMessage(BotMessageDirection.Outbound, BotMessageContentType.Text, "hello", createdBy: actorId);

        conversation.UpdatedBy.Should().Be(actorId);
    }

    [Fact]
    public void MessageCreationInvalidPayloadShouldPreserveActivity()
    {
        var conversation = CreateConversation();
        var previous = conversation.LastInteractionAt;

        conversation.AddMessage(BotMessageDirection.Inbound, BotMessageContentType.Text, null);

        conversation.LastInteractionAt.Should().Be(previous);
    }

    [Fact]
    public void MessageIdentifierNewShouldGenerateDistinctValues()
    {
        var first = BotMessageId.New();

        var second = BotMessageId.New();

        second.Should().NotBe(first);
    }

    private static BotConversation CreateConversation() =>
        BotConversation.Create(Guid.NewGuid(), Guid.NewGuid(), "external", BotChannel.Telegram).Value
        ?? throw new InvalidOperationException();
}

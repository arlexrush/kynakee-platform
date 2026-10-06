using FluentAssertions;
using Kynakee.Modules.SharedKernel.Contracts;
using Xunit;

namespace Kynakee.UnitTests.SharedKernel.Contracts
{
#pragma warning disable CA1515 // Considere la posibilidad de hacer que los tipos públicos sean internos
    public class BotMessageReceivedTests
    {
        [Fact]
        public void WhatsAppShouldHaveExpectedChannelValue()
        {
            BotMessageReceived.WhatsApp.Should().Be("whatsapp");
        }

        [Fact]
        public void TelegramShouldHaveExpectedChannelValue()
        {
            BotMessageReceived.Telegram.Should().Be("telegram");
        }

        [Fact]
        public void ConstructorShouldPreserveAllMessageProperties()
        {
            var tenantId = Guid.NewGuid();
            var externalUserId = "+34612345678";
            var mediaUrl = new Uri("https://example.com/image.jpg");
            var receivedAt = new DateTime(
                2026,
                9,
                9,
                12,
                30,
                0,
                DateTimeKind.Utc);

            var message = new BotMessageReceived(
                tenantId,
                BotMessageReceived.WhatsApp,
                externalUserId,
                "Mensaje de prueba",
                mediaUrl,
                "image",
                receivedAt);

            message.TenantId.Should().Be(tenantId);
            message.Channel.Should().Be(BotMessageReceived.WhatsApp);
            message.ExternalUserId.Should().Be(externalUserId);
            message.Text.Should().Be("Mensaje de prueba");
            message.MediaUrl.Should().Be(mediaUrl);
            message.MediaType.Should().Be("image");
            message.ReceivedAt.Should().Be(receivedAt);
        }

        [Fact]
        public void HasTextShouldBeTrueWhenTextContainsContent()
        {
            var message = CreateMessage(text: "Mensaje de prueba");

            message.HasText.Should().BeTrue();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("   ")]
        public void HasTextShouldBeFalseWhenTextIsNullOrWhitespace(
            string? text)
        {
            var message = CreateMessage(text: text);

            message.HasText.Should().BeFalse();
        }

        [Fact]
        public void HasMediaShouldBeTrueWhenMediaUrlIsPresent()
        {
            var message = CreateMessage(
                mediaUrl: new Uri("https://example.com/document.pdf"));

            message.HasMedia.Should().BeTrue();
        }

        [Fact]
        public void HasMediaShouldBeFalseWhenMediaUrlIsNull()
        {
            var message = CreateMessage(mediaUrl: null);

            message.HasMedia.Should().BeFalse();
        }

        [Fact]
        public void IsWhatsAppShouldBeTrueOnlyForWhatsAppChannel()
        {
            var message = CreateMessage(
                channel: BotMessageReceived.WhatsApp);

            message.IsWhatsApp.Should().BeTrue();
            message.IsTelegram.Should().BeFalse();
        }

        [Fact]
        public void IsTelegramShouldBeTrueOnlyForTelegramChannel()
        {
            var message = CreateMessage(
                channel: BotMessageReceived.Telegram);

            message.IsWhatsApp.Should().BeFalse();
            message.IsTelegram.Should().BeTrue();
        }

        [Fact]
        public void UnknownChannelShouldNotMatchKnownChannels()
        {
            var message = CreateMessage(channel: "unknown");

            message.IsWhatsApp.Should().BeFalse();
            message.IsTelegram.Should().BeFalse();
        }

        [Fact]
        public void MessagesWithSameValuesShouldBeEqual()
        {
            var tenantId = Guid.NewGuid();
            var receivedAt = DateTime.UtcNow;

            var first = new BotMessageReceived(
                tenantId,
                BotMessageReceived.Telegram,
                "123456789",
                "Mensaje",
                null,
                null,
                receivedAt);

            var second = new BotMessageReceived(
                tenantId,
                BotMessageReceived.Telegram,
                "123456789",
                "Mensaje",
                null,
                null,
                receivedAt);

            first.Should().Be(second);
        }

        private static BotMessageReceived CreateMessage(
            string channel = BotMessageReceived.WhatsApp,
            string? text = "Mensaje de prueba",
            Uri? mediaUrl = null)
        {
            return new BotMessageReceived(
                Guid.NewGuid(),
                channel,
                "+34612345678",
                text,
                mediaUrl,
                mediaUrl is null ? null : "image",
                DateTime.UtcNow);
        }
    }
#pragma warning restore CA1515 // Considere la posibilidad de hacer que los tipos públicos sean internos
}

namespace Kynakee.Modules.SharedKernel.Contracts
{
    /// <summary>
    /// Shared contract representing an incoming message from any bot channel.
    /// Published by Kynakee.Modules.Bots and consumed by Kynakee.Modules.Projects.
    ///
    /// Both WhatsApp (Meta Cloud API) and Telegram produce this same record,
    /// enabling the Projects module to handle bot messages without knowing
    /// which channel originated them (ADR-014, ADR-025).
    ///
    /// Flow:
    ///   WhatsApp webhook  ─┐
    ///                      ├─→ BotMessageReceived ─→ MassTransit Outbox ─→ Projects module
    ///   Telegram webhook  ─┘
    ///
    /// Rules:
    /// - This record is IMMUTABLE — all properties are init-only
    /// - TenantId MUST always be set — resolved from ExternalUserId during bot registration
    /// - MediaUrl and MediaType are null for text-only messages
    /// - Text may be null if the message is media-only (e.g., photo without caption)
    /// </summary>
    /// <param name="TenantId">
    /// Tenant identifier resolved from the bot user registration.
    /// </param>
    /// <param name="Channel">
    /// Origin channel. Valid values: "whatsapp" | "telegram".
    /// </param>
    /// <param name="ExternalUserId">
    /// Channel-specific user identifier.
    /// WhatsApp: E.164 phone number (e.g., "+34612345678").
    /// Telegram: Chat ID as string (e.g., "123456789").
    /// </param>
    /// <param name="Text">
    /// Text content of the message. Null for media-only messages.
    /// </param>
    /// <param name="MediaUrl">
    /// URL of the attached media file. Null for text-only messages.
    /// WhatsApp: Temporary Meta CDN URL (expires after 5 minutes — download immediately).
    /// Telegram: Telegram file URL.
    /// </param>
    /// <param name="MediaType">
    /// Type of the attached media. Null for text-only messages.
    /// Valid values: "image" | "video" | "audio" | "document" | "location".
    /// </param>
    /// <param name="ReceivedAt">
    /// UTC timestamp when the message was received by the webhook.
    /// </param>
    public sealed record BotMessageReceived(
    Guid TenantId,
    string Channel,
    string ExternalUserId,
    string? Text,
    Uri? MediaUrl,
    string? MediaType,
    DateTime ReceivedAt)
    {
        // ── Channel constants ─────────────────────────────────────────────────────

        /// <summary>WhatsApp channel identifier.</summary>
        public const string WhatsApp = "whatsapp";

        /// <summary>Telegram channel identifier.</summary>
        public const string Telegram = "telegram";

        // ── Helpers ───────────────────────────────────────────────────────────────

        /// <summary>Indicates whether this message contains text content.</summary>
        public bool HasText => !string.IsNullOrWhiteSpace(Text);

        /// <summary>Indicates whether this message contains media content.</summary>
        public bool HasMedia => MediaUrl is not null;

        /// <summary>Indicates whether this message originated from WhatsApp.</summary>
        public bool IsWhatsApp => Channel == WhatsApp;

        /// <summary>Indicates whether this message originated from Telegram.</summary>
        public bool IsTelegram => Channel == Telegram;
    }
}

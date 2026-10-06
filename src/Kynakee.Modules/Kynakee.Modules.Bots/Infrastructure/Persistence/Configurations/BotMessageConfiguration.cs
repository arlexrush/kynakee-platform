using Kynakee.Modules.Bots.Domain.Entities;
using Kynakee.Modules.Bots.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Kynakee.Modules.Bots.Infrastructure.Persistence.Configurations;

public sealed class BotMessageConfiguration : IEntityTypeConfiguration<BotMessage>
{
    public void Configure(EntityTypeBuilder<BotMessage> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("bot_messages", table =>
        {
            table.HasCheckConstraint(
                "ck_bot_messages_deleted",
                "is_deleted = (deleted_at IS NOT NULL)");
            table.HasCheckConstraint(
                "ck_bot_messages_content",
                "content IS NOT NULL OR media_url IS NOT NULL");
            table.HasCheckConstraint(
                "ck_bot_messages_media_url_https",
                "media_url IS NULL OR (media_url LIKE 'https://%' AND length(media_url) <= 500)");
        });
        builder.HasKey(message => message.Id);
        builder.Property(message => message.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, value => new BotMessageId(value))
            .ValueGeneratedNever();
        builder.Property(message => message.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();
        builder.Property(message => message.ConversationId)
            .HasColumnName("conversation_id")
            .HasConversion(id => id.Value, value => new BotConversationId(value))
            .IsRequired();
        builder.Property(message => message.Direction)
            .HasColumnName("direction")
            .HasConversion<string>()
            .HasMaxLength(10)
            .IsRequired();
        builder.Property(message => message.ContentType)
            .HasColumnName("message_type")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(message => message.Content)
            .HasColumnName("content")
            .HasColumnType("text");
        builder.Property(message => message.MediaUrl)
            .HasColumnName("media_url")
            .HasConversion(new UriToStringConverter())
            .HasMaxLength(500);
        builder.Property(message => message.ParsedCommand)
            .HasColumnName("parsed_command")
            .HasMaxLength(100);

        builder.Property(message => message.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(message => message.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(message => message.CreatedBy).HasColumnName("created_by");
        builder.Property(message => message.UpdatedBy).HasColumnName("updated_by");
        builder.Property(message => message.DeletedAt).HasColumnName("deleted_at");
        builder.Property(message => message.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Ignore(message => message.Version);

        builder.HasIndex(message => new
            {
                message.TenantId,
                message.ConversationId,
                message.CreatedAt
            })
            .HasDatabaseName("idx_bot_messages_conversation");
    }
}
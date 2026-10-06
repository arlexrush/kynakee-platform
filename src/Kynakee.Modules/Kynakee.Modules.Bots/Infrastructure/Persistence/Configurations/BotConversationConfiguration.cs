using Kynakee.Modules.Bots.Domain.Aggregates;
using Kynakee.Modules.Bots.Domain.Entities;
using Kynakee.Modules.Bots.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kynakee.Modules.Bots.Infrastructure.Persistence.Configurations;

public sealed class BotConversationConfiguration : IEntityTypeConfiguration<BotConversation>
{
    public void Configure(EntityTypeBuilder<BotConversation> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("bot_conversations", table =>
            table.HasCheckConstraint(
                "ck_bot_conversations_deleted",
                "is_deleted = (deleted_at IS NOT NULL)"));
        builder.HasKey(conversation => conversation.Id);
        builder.Property(conversation => conversation.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, value => new BotConversationId(value))
            .ValueGeneratedNever();
        builder.Property(conversation => conversation.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();
        builder.HasAlternateKey(conversation => new { conversation.TenantId, conversation.Id })
            .HasName("ak_bot_conversations_tenant_id_id");

        builder.Property(conversation => conversation.ExternalId)
            .HasColumnName("external_id")
            .HasMaxLength(100)
            .IsRequired();
        builder.Property(conversation => conversation.Channel)
            .HasColumnName("channel")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(conversation => conversation.UserId)
            .HasColumnName("user_id")
            .IsRequired();
        builder.Property(conversation => conversation.ActiveProjectId)
            .HasColumnName("active_project_id");
        builder.Property(conversation => conversation.State)
            .HasColumnName("state")
            .HasConversion<string>()
            .HasMaxLength(30)
            .HasDefaultValueSql("'NewSession'")
            .IsRequired();
        builder.Property(conversation => conversation.Verbosity)
            .HasColumnName("verbosity")
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValueSql("'Normal'")
            .IsRequired();
        builder.Property(conversation => conversation.LastInteractionAt)
            .HasColumnName("last_interaction_at")
            .IsRequired();

        builder.Property(conversation => conversation.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(conversation => conversation.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(conversation => conversation.CreatedBy).HasColumnName("created_by");
        builder.Property(conversation => conversation.UpdatedBy).HasColumnName("updated_by");
        builder.Property(conversation => conversation.DeletedAt).HasColumnName("deleted_at");
        builder.Property(conversation => conversation.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(conversation => conversation.Version)
            .HasColumnName("xmin")
            .IsRowVersion()
            .IsConcurrencyToken();

        builder.HasIndex(conversation => new
            {
                conversation.TenantId,
                conversation.Channel,
                conversation.ExternalId
            })
            .IsUnique()
            .HasFilter("is_deleted = FALSE")
            .HasDatabaseName("idx_bot_conversations_external");

        builder.HasMany<BotMessage>("_messages")
            .WithOne(message => message.Conversation)
            .HasForeignKey(message => new { message.TenantId, message.ConversationId })
            .HasPrincipalKey(conversation => new { conversation.TenantId, conversation.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation("_messages")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
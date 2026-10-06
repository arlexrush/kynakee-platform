using Kynakee.Modules.Bots.Domain.Aggregates;
using Kynakee.Modules.Bots.Domain.Entities;
using Kynakee.Modules.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Kynakee.Modules.Bots.Infrastructure.Persistence;

public sealed class BotsDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    public BotsDbContext(
        DbContextOptions<BotsDbContext> options,
        ITenantContext tenantContext)
        : base(options)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        _tenantContext = tenantContext;
    }

    public Guid CurrentTenantId => _tenantContext.TenantId;

    public DbSet<BotConversation> Conversations => Set<BotConversation>();

    public DbSet<BotMessage> Messages => Set<BotMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema("schema_bots");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BotsDbContext).Assembly);
        modelBuilder.Entity<BotConversation>()
            .HasQueryFilter(conversation =>
                !conversation.IsDeleted && conversation.TenantId == CurrentTenantId);
        modelBuilder.Entity<BotMessage>()
            .HasQueryFilter(message =>
                !message.IsDeleted &&
                message.TenantId == CurrentTenantId &&
                !message.Conversation.IsDeleted);

        base.OnModelCreating(modelBuilder);
    }
}

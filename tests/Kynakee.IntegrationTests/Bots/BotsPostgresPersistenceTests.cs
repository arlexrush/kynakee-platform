using FluentAssertions;
using Kynakee.Modules.Bots;
using Kynakee.Modules.Bots.Domain.Aggregates;
using Kynakee.Modules.Bots.Domain.Entities;
using Kynakee.Modules.Bots.Domain.Enums;
using Kynakee.Modules.Bots.Domain.Repositories;
using Kynakee.Modules.Bots.Infrastructure.Persistence;
using Kynakee.Modules.Bots.Infrastructure.Repositories;
using Kynakee.Modules.SharedKernel.Contracts;
using Kynakee.Modules.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Kynakee.IntegrationTests.Bots;

public sealed class BotsPostgresPersistenceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17").Build();
    private readonly TestTenantContext _tenantContext = new();

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
        var context = CreateContext();
        await using var contextLifetime = context.ConfigureAwait(false);
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
    }

    public ValueTask DisposeAsync() => _postgres.DisposeAsync();

    [Fact]
    public async Task MigrationFreshDatabaseShouldApplyInitialBotsSchema()
    {
        await using var context = CreateContext();

        var migrations = await context.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken);

        migrations.Should().Equal("20261001231739_InitialBotsSchema");
    }

    [Fact]
    public async Task ConversationRoundTripShouldPreserveIdentityStateAndAudit()
    {
        var conversation = NewConversation();
        var projectId = Guid.NewGuid();
        conversation.ActivateProject(projectId);
        conversation.ChangeVerbosity(BotVerbosity.Detailed);
        await SeedAsync(conversation);
        await using var context = CreateContext();

        var loaded = await new EfBotConversationRepository(context)
            .GetByIdForUpdateAsync(conversation.Id, TestContext.Current.CancellationToken);

        loaded.Should().BeEquivalentTo(new
        {
            conversation.Id,
            conversation.TenantId,
            conversation.UserId,
            conversation.ExternalId,
            conversation.Channel,
            conversation.ActiveProjectId,
            conversation.State,
            conversation.Verbosity,
            conversation.CreatedBy,
            conversation.UpdatedBy,
            conversation.CreatedAt,
            conversation.LastInteractionAt
        }, options => options
            .Using<DateTime>(date => date.Subject.Should().BeCloseTo(date.Expectation, TimeSpan.FromMicroseconds(1)))
            .WhenTypeIs<DateTime>());
    }

    [Fact]
    public async Task MessageRoundTripShouldPreserveMediaAndAudit()
    {
        var conversation = NewConversation();
        var media = new Uri("https://media.example.test/file");
        var message = conversation.AddMessage(
            BotMessageDirection.Inbound, BotMessageContentType.Image, "caption", media).Value
            ?? throw new InvalidOperationException();
        await SeedAsync(conversation);
        await using var context = CreateContext();

        var loaded = await context.Messages.SingleAsync(TestContext.Current.CancellationToken);

        loaded.Should().BeEquivalentTo(new
        {
            message.Id,
            ConversationId = conversation.Id,
            conversation.TenantId,
            Direction = BotMessageDirection.Inbound,
            ContentType = BotMessageContentType.Image,
            Content = "caption",
            MediaUrl = media,
            CreatedBy = (Guid?)conversation.UserId,
            IsDeleted = false
        });
    }

    [Fact]
    public async Task ConversationLoadForUpdateShouldNotLoadHistory()
    {
        var conversation = NewConversation();
        conversation.AddMessage(BotMessageDirection.Inbound, BotMessageContentType.Text, "first");
        await SeedAsync(conversation);
        await using var context = CreateContext();

        await new EfBotConversationRepository(context)
            .GetByIdForUpdateAsync(conversation.Id, TestContext.Current.CancellationToken);

        context.ChangeTracker.Entries<BotMessage>().Should().BeEmpty();
    }

    [Fact]
    public async Task MessageAppendToReloadedConversationShouldPersistWithoutLoadingHistory()
    {
        var conversation = NewConversation();
        conversation.AddMessage(BotMessageDirection.Inbound, BotMessageContentType.Text, "first");
        await SeedAsync(conversation);
        await using (var context = CreateContext())
        {
            var loaded = await new EfBotConversationRepository(context)
                .GetByIdForUpdateAsync(conversation.Id, TestContext.Current.CancellationToken)
                ?? throw new InvalidOperationException();
            loaded.AddMessage(BotMessageDirection.Outbound, BotMessageContentType.Text, "second");
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        await using var verification = CreateContext();

        var contents = await verification.Messages.OrderBy(message => message.Content)
            .Select(message => message.Content).ToListAsync(TestContext.Current.CancellationToken);

        contents.Should().Equal("first", "second");
    }

    [Fact]
    public async Task ConversationUniquenessSameTenantAndChannelShouldRejectDuplicate()
    {
        await SeedAsync(NewConversation());
        await using var context = CreateContext();
        await new EfBotConversationRepository(context).AddAsync(NewConversation(), TestContext.Current.CancellationToken);

        var error = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync(TestContext.Current.CancellationToken));

        error.InnerException.Should().BeEquivalentTo(new
        {
            SqlState = PostgresErrorCodes.UniqueViolation,
            ConstraintName = "idx_bot_conversations_external"
        });
    }

    [Fact]
    public async Task ConversationUniquenessDifferentTenantShouldAllowSameChannelIdentity()
    {
        await SeedAsync(NewConversation());
        _tenantContext.TenantId = Guid.NewGuid();
        await SeedAsync(NewConversation());
        await using var context = CreateContext();

        var count = await context.Conversations.IgnoreQueryFilters().CountAsync(TestContext.Current.CancellationToken);

        count.Should().Be(2);
    }

    [Fact]
    public async Task ConversationUniquenessDifferentChannelShouldAllowSameExternalId()
    {
        await SeedAsync(NewConversation());
        var whatsapp = BotConversation.Create(
            _tenantContext.TenantId, _tenantContext.UserId, "external", BotChannel.WhatsApp).Value
            ?? throw new InvalidOperationException();
        await SeedAsync(whatsapp);
        await using var context = CreateContext();

        var count = await context.Conversations.CountAsync(TestContext.Current.CancellationToken);

        count.Should().Be(2);
    }

    [Fact]
    public async Task ConversationFilterDifferentTenantShouldHideConversation()
    {
        var conversation = NewConversation();
        await SeedAsync(conversation);
        _tenantContext.TenantId = Guid.NewGuid();
        await using var context = CreateContext();

        var loaded = await new EfBotConversationRepository(context)
            .GetByIdForUpdateAsync(conversation.Id, TestContext.Current.CancellationToken);

        loaded.Should().BeNull();
    }

    [Fact]
    public async Task MessageFilterDifferentTenantShouldHideHistory()
    {
        var conversation = NewConversation();
        conversation.AddMessage(BotMessageDirection.Inbound, BotMessageContentType.Text, "private");
        await SeedAsync(conversation);
        _tenantContext.TenantId = Guid.NewGuid();
        await using var context = CreateContext();

        var messages = await new EfBotConversationRepository(context)
            .GetMessagesPageAsync(conversation.Id, 0, 10, TestContext.Current.CancellationToken);

        messages.Should().BeEmpty();
    }

    [Fact]
    public async Task ConversationFilterContextTenantChangeShouldUseCurrentTenant()
    {
        await SeedAsync(NewConversation());
        await using var context = CreateContext();
        _tenantContext.TenantId = Guid.NewGuid();

        var count = await context.Conversations.CountAsync(TestContext.Current.CancellationToken);

        count.Should().Be(0);
    }

    [Fact]
    public async Task MessageForeignKeyDifferentTenantShouldRejectAssociation()
    {
        var conversation = NewConversation();
        conversation.AddMessage(BotMessageDirection.Inbound, BotMessageContentType.Text, "original");
        await SeedAsync(conversation);
        await using var context = CreateContext();
        var wrongTenant = Guid.NewGuid();
        var wrongMessageId = Guid.NewGuid();

        var error = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO schema_bots.bot_messages (id, tenant_id, conversation_id, direction, message_type, content, created_at, updated_at, is_deleted) VALUES ({wrongMessageId}, {wrongTenant}, {conversation.Id.Value}, 'Inbound', 'Text', 'invalid', NOW(), NOW(), FALSE)",
            TestContext.Current.CancellationToken));

        error.SqlState.Should().Be(PostgresErrorCodes.ForeignKeyViolation);
    }

    [Fact]
    public async Task ConversationSoftDeleteShouldHideConversationAndRetainRow()
    {
        var conversation = NewConversation();
        conversation.Delete(_tenantContext.UserId);
        await SeedAsync(conversation);
        await using var context = CreateContext();

        var visible = await context.Conversations.CountAsync(TestContext.Current.CancellationToken);
        var stored = await context.Conversations.IgnoreQueryFilters().CountAsync(TestContext.Current.CancellationToken);

        (visible, stored).Should().Be((0, 1));
    }

    [Fact]
    public async Task ConversationSoftDeleteShouldHideMessageHistory()
    {
        var conversation = NewConversation();
        conversation.AddMessage(BotMessageDirection.Inbound, BotMessageContentType.Text, "private");
        conversation.Delete(_tenantContext.UserId);
        await SeedAsync(conversation);
        await using var context = CreateContext();

        var messages = await new EfBotConversationRepository(context)
            .GetMessagesPageAsync(conversation.Id, 0, 10, TestContext.Current.CancellationToken);

        messages.Should().BeEmpty();
    }

    [Fact]
    public async Task ConversationSoftDeleteShouldAllowReplacementChannelIdentity()
    {
        var original = NewConversation();
        original.Delete();
        await SeedAsync(original);
        var replacement = NewConversation();
        await SeedAsync(replacement);
        await using var context = CreateContext();

        var visibleId = await context.Conversations.Select(conversation => conversation.Id)
            .SingleAsync(TestContext.Current.CancellationToken);

        visibleId.Should().Be(replacement.Id);
    }

    [Fact]
    public async Task MessageSoftDeleteShouldHideOnlyDeletedMessage()
    {
        var conversation = NewConversation();
        var message = conversation.AddMessage(BotMessageDirection.Inbound, BotMessageContentType.Text, "deleted").Value
            ?? throw new InvalidOperationException();
        message.Delete();
        conversation.AddMessage(BotMessageDirection.Inbound, BotMessageContentType.Text, "visible");
        await SeedAsync(conversation);
        await using var context = CreateContext();

        var page = await new EfBotConversationRepository(context)
            .GetMessagesPageAsync(conversation.Id, 0, 10, TestContext.Current.CancellationToken);

        page.Select(item => item.Content).Should().Equal("visible");
    }

    [Fact]
    public async Task MessagePaginationEqualTimestampsShouldReturnStableRemainingPage()
    {
        var conversation = NewConversation();
        conversation.AddMessage(BotMessageDirection.Inbound, BotMessageContentType.Text, "one");
        conversation.AddMessage(BotMessageDirection.Inbound, BotMessageContentType.Text, "two");
        conversation.AddMessage(BotMessageDirection.Outbound, BotMessageContentType.Text, "three");
        await SeedAsync(conversation);
        await using var context = CreateContext();
        await context.Database.ExecuteSqlRawAsync(
            "UPDATE schema_bots.bot_messages SET created_at = TIMESTAMPTZ '2026-01-01 00:00:00+00'",
            TestContext.Current.CancellationToken);
        var repository = new EfBotConversationRepository(context);
        var complete = await repository.GetMessagesPageAsync(conversation.Id, 0, 100, TestContext.Current.CancellationToken);

        var remaining = await repository.GetMessagesPageAsync(conversation.Id, 1, 1, TestContext.Current.CancellationToken);

        remaining.Select(message => message.Id).Should().Equal(complete.Skip(1).Take(1).Select(message => message.Id));
    }

    [Theory]
    [InlineData(-1, 10)]
    [InlineData(0, 0)]
    [InlineData(0, 101)]
    public async Task MessagePaginationInvalidBoundsShouldRejectRequest(int offset, int pageSize)
    {
        await using var context = CreateContext();
        var repository = new EfBotConversationRepository(context);
        Func<Task> query = () => repository.GetMessagesPageAsync(
            NewConversation().Id, offset, pageSize, TestContext.Current.CancellationToken);

        await query.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task ConversationQueryCancelledTokenShouldCancelOperation()
    {
        await using var context = CreateContext();
        var repository = new EfBotConversationRepository(context);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();
        Func<Task> query = () => repository.GetByIdForUpdateAsync(NewConversation().Id, cancellation.Token);

        await query.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task MessageQueryCancelledTokenShouldCancelOperation()
    {
        await using var context = CreateContext();
        var repository = new EfBotConversationRepository(context);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();
        Func<Task> query = () => repository.GetMessagesPageAsync(NewConversation().Id, 0, 10, cancellation.Token);

        await query.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ConversationConcurrencyStaleWriteShouldFail()
    {
        var conversation = NewConversation();
        await SeedAsync(conversation);
        await using var firstContext = CreateContext();
        await using var secondContext = CreateContext();
        var first = await firstContext.Conversations.SingleAsync(TestContext.Current.CancellationToken);
        var second = await secondContext.Conversations.SingleAsync(TestContext.Current.CancellationToken);
        first.ChangeVerbosity(BotVerbosity.Concise);
        second.ChangeVerbosity(BotVerbosity.Detailed);
        await firstContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        Func<Task> save = () => secondContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        await save.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    [Fact]
    public async Task TransactionCommitRepeatedCallShouldPersistSingleConversationAndMessage()
    {
        await using var context = CreateContext();
        var participant = new BotsTransactionParticipant(context);
        var transaction = await participant.BeginTransactionAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using var transactionLifetime = transaction.ConfigureAwait(true);
        var conversation = NewConversation();
        conversation.AddMessage(BotMessageDirection.Inbound, BotMessageContentType.Text, "committed");
        await new EfBotConversationRepository(context).AddAsync(conversation, TestContext.Current.CancellationToken);

        await transaction.CommitAsync(TestContext.Current.CancellationToken);
        await transaction.CommitAsync(TestContext.Current.CancellationToken);
        await using var verification = CreateContext();
        var conversations = await verification.Conversations.CountAsync(TestContext.Current.CancellationToken);
        var messages = await verification.Messages.CountAsync(TestContext.Current.CancellationToken);

        (conversations, messages).Should().Be((1, 1));
    }

    [Fact]
    public async Task TransactionRollbackSavedChangesShouldDiscardRowsAndTrackedEntities()
    {
        await using var context = CreateContext();
        var participant = new BotsTransactionParticipant(context);
        var transaction = await participant.BeginTransactionAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using var transactionLifetime = transaction.ConfigureAwait(true);
        await new EfBotConversationRepository(context).AddAsync(NewConversation(), TestContext.Current.CancellationToken);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await transaction.RollbackAsync(TestContext.Current.CancellationToken);
        await using var verification = CreateContext();
        var persisted = await verification.Conversations.CountAsync(TestContext.Current.CancellationToken);

        (persisted, context.ChangeTracker.Entries().Count()).Should().Be((0, 0));
    }

    [Fact]
    public async Task TransactionParticipantNoEventsShouldCollectEmptySnapshot()
    {
        await using var context = CreateContext();
        await context.Conversations.AddAsync(NewConversation(), TestContext.Current.CancellationToken);
        var participant = new BotsTransactionParticipant(context);

        participant.ClearDomainEvents(Array.Empty<IDomainEvent>());
        var events = participant.CollectDomainEvents();

        events.Should().BeEmpty();
    }

    [Fact]
    public void DependencyRegistrationShouldResolveConcreteScopedImplementations()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = _postgres.GetConnectionString()
        }).Build();
        var services = new ServiceCollection();
        services.AddScoped<ITenantContext>(_ => _tenantContext);
        services.AddBotsModule(configuration);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        using var scope = provider.CreateScope();

        var repository = scope.ServiceProvider.GetRequiredService<IBotConversationRepository>();
        var participant = scope.ServiceProvider.GetRequiredService<IModuleTransactionParticipant>();
        var context = scope.ServiceProvider.GetRequiredService<BotsDbContext>();

        (repository.GetType(), participant.GetType(), context.GetType()).Should().Be(
            (typeof(EfBotConversationRepository), typeof(BotsTransactionParticipant), typeof(BotsDbContext)));
    }

    private BotsDbContext CreateContext() => new(
        new DbContextOptionsBuilder<BotsDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options,
        _tenantContext);

    private BotConversation NewConversation() =>
        BotConversation.Create(_tenantContext.TenantId, _tenantContext.UserId, "external", BotChannel.Telegram).Value
        ?? throw new InvalidOperationException();

    private async Task SeedAsync(BotConversation conversation)
    {
        var context = CreateContext();
        await using var contextLifetime = context.ConfigureAwait(false);
        await new EfBotConversationRepository(context).AddAsync(conversation, TestContext.Current.CancellationToken).ConfigureAwait(false);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
    }

    private sealed class TestTenantContext : ITenantContext
    {
        public Guid TenantId { get; set; } = Guid.NewGuid();

        public Guid UserId { get; } = Guid.NewGuid();

        public bool IsAuthenticated => true;
    }
}

using FluentAssertions;
using Kynakee.Modules.Identity.Domain.Aggregates;
using Kynakee.Modules.Identity.Domain.ValueObjects;
using Kynakee.Modules.Identity.Infrastructure.Persistence;
using Kynakee.Modules.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kynakee.UnitTests.Identity.Infrastructure;

public class IdentityTransactionParticipantTests
{
    [Fact]
    public void CanHandleShouldAcceptRequestsFromIdentityAssembly()
    {
        using var context = CreateContext();
        var participant = new IdentityTransactionParticipant(context);

        participant.CanHandle(typeof(Tenant)).Should().BeTrue();
    }

    [Fact]
    public void CanHandleShouldRejectRequestsFromOtherAssemblies()
    {
        using var context = CreateContext();
        var participant = new IdentityTransactionParticipant(context);

        participant.CanHandle(typeof(string)).Should().BeFalse();
    }

    [Fact]
    public void ClearDomainEventsShouldRemoveCollectedEventsFromIdentityEntities()
    {
        using var context = CreateContext();
        var tenant = Tenant.Create(
            "Kynakee",
            TenantSlug.Create("kynakee").Value,
            TenantType.Company,
            null,
            null,
            "starter").Value!;
        context.Tenants.Add(tenant);
        var participant = new IdentityTransactionParticipant(context);
        var events = participant.CollectDomainEvents();

        participant.ClearDomainEvents(events);

        tenant.DomainEvents.Should().BeEmpty();
    }

    private static IdentityDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql("Host=localhost;Database=identity-tests;Username=test;Password=test")
            .Options;
        return new IdentityDbContext(options, new TestTenantContext());
    }

    private sealed class TestTenantContext : ITenantContext
    {
        public Guid TenantId { get; } = Guid.NewGuid();

        public Guid UserId { get; } = Guid.NewGuid();

        public bool IsAuthenticated => true;
    }
}
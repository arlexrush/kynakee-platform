using FluentAssertions;
using Kynakee.Modules.SharedKernel.Domain;
using Xunit;

namespace Kynakee.UnitTests.SharedKernel.Domain
{
#pragma warning disable CA1515 // Considere la posibilidad de hacer que los tipos públicos sean internos
    public class GlobalEntityTests
    {
        [Fact]
        public void ConstructorWithOwnerShouldInitializeIdentityAndProvenance()
        {
            var id = Guid.NewGuid();
            var ownerTenantId = Guid.NewGuid();
            var ownerUserId = Guid.NewGuid();

            var entity = new TestGlobalEntity(id, ownerTenantId, ownerUserId);

            entity.Id.Should().Be(id);
            entity.OwnerTenantId.Should().Be(ownerTenantId);
            entity.OwnerUserId.Should().Be(ownerUserId);
            entity.CreatedBy.Should().Be(ownerUserId);
        }

        [Fact]
        public void ConstructorWithoutOwnerShouldLeaveProvenanceAndCreatorEmpty()
        {
            var entity = new TestGlobalEntity(Guid.NewGuid(), null, null);

            entity.OwnerTenantId.Should().BeNull();
            entity.OwnerUserId.Should().BeNull();
            entity.CreatedBy.Should().BeNull();
        }

        [Fact]
        public void ConstructorShouldInitializeAuditAndDeletionState()
        {
            var beforeCreation = DateTime.UtcNow;

            var entity = new TestGlobalEntity(Guid.NewGuid(), null, null);

            var afterCreation = DateTime.UtcNow;
            entity.CreatedAt.Should().BeOnOrAfter(beforeCreation);
            entity.CreatedAt.Should().BeOnOrBefore(afterCreation);
            entity.UpdatedAt.Should().Be(entity.CreatedAt);
            entity.UpdatedBy.Should().BeNull();
            entity.IsDeleted.Should().BeFalse();
            entity.DeletedAt.Should().BeNull();
            entity.Version.Should().Be(0);
        }

        [Fact]
        public void DeleteShouldMarkEntityAsDeletedAndPreserveProvenance()
        {
            var ownerTenantId = Guid.NewGuid();
            var ownerUserId = Guid.NewGuid();
            var deletedBy = Guid.NewGuid();
            var entity = new TestGlobalEntity(Guid.NewGuid(), ownerTenantId, ownerUserId);
            var beforeDeletion = DateTime.UtcNow;

            entity.Delete(deletedBy);

            var afterDeletion = DateTime.UtcNow;
            entity.IsDeleted.Should().BeTrue();
            entity.DeletedAt.Should().BeOnOrAfter(beforeDeletion);
            entity.DeletedAt.Should().BeOnOrBefore(afterDeletion);
            entity.UpdatedAt.Should().Be(entity.DeletedAt);
            entity.UpdatedBy.Should().Be(deletedBy);
            entity.OwnerTenantId.Should().Be(ownerTenantId);
            entity.OwnerUserId.Should().Be(ownerUserId);
            entity.CreatedBy.Should().Be(ownerUserId);
        }

        [Fact]
        public void DeleteWithoutUserShouldLeaveUpdatedByEmpty()
        {
            var entity = new TestGlobalEntity(Guid.NewGuid(), null, null);

            entity.Delete();

            entity.UpdatedBy.Should().BeNull();
        }

        [Fact]
        public void RegisterUpdateShouldRecordTimestampAndUser()
        {
            var updatedBy = Guid.NewGuid();
            var ownerUserId = Guid.NewGuid();
            var entity = new TestGlobalEntity(Guid.NewGuid(), null, ownerUserId);
            var createdAt = entity.CreatedAt;
            var beforeUpdate = DateTime.UtcNow;

            entity.RegisterUpdateForTest(updatedBy);

            var afterUpdate = DateTime.UtcNow;
            entity.UpdatedAt.Should().BeOnOrAfter(beforeUpdate);
            entity.UpdatedAt.Should().BeOnOrBefore(afterUpdate);
            entity.UpdatedBy.Should().Be(updatedBy);
            entity.CreatedAt.Should().Be(createdAt);
            entity.CreatedBy.Should().Be(ownerUserId);
        }

        [Fact]
        public void RegisterUpdateWithoutUserShouldClearPreviousUpdater()
        {
            var entity = new TestGlobalEntity(Guid.NewGuid(), null, null);
            entity.RegisterUpdateForTest(Guid.NewGuid());

            entity.RegisterUpdateForTest();

            entity.UpdatedBy.Should().BeNull();
        }

        [Fact]
        public void GlobalEntityShouldNotExposeTenantId()
        {
            typeof(GlobalEntity<Guid>).GetProperty("TenantId").Should().BeNull();
        }

        private sealed class TestGlobalEntity : GlobalEntity<Guid>
        {
            public TestGlobalEntity(Guid id, Guid? ownerTenantId, Guid? ownerUserId)
                : base(id, ownerTenantId, ownerUserId)
            {
            }

            public void RegisterUpdateForTest(Guid? updatedBy = null)
            {
                RegisterUpdate(updatedBy);
            }
        }
    }
#pragma warning restore CA1515
}
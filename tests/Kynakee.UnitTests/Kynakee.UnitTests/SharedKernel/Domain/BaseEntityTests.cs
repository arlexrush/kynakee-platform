using FluentAssertions;
using Kynakee.Modules.SharedKernel.Domain;
using Xunit;

namespace Kynakee.UnitTests.SharedKernel.Domain
{
#pragma warning disable CA1515 // Considere la posibilidad de hacer que los tipos públicos sean internos
    public class BaseEntityTests
    {
        [Fact]
        public void ConstructorShouldInitializeIdentityAndTenant()
        {
            var id = Guid.NewGuid();
            var tenantId = Guid.NewGuid();

            var entity = new TestEntity(id, tenantId);

            entity.Id.Should().Be(id);
            entity.TenantId.Should().Be(tenantId);
        }

        [Fact]
        public void ConstructorShouldInitializeCreatedBy()
        {
            var createdBy = Guid.NewGuid();

            var entity = CreateEntity(createdBy: createdBy);

            entity.CreatedBy.Should().Be(createdBy);
        }

        [Fact]
        public void ConstructorWithoutCreatedByShouldSetCreatedByToNull()
        {
            var entity = CreateEntity();

            entity.CreatedBy.Should().BeNull();
        }

        [Fact]
        public void ConstructorShouldInitializeAuditDates()
        {
            var beforeCreation = DateTime.UtcNow;

            var entity = CreateEntity();

            var afterCreation = DateTime.UtcNow;

            entity.CreatedAt.Should().BeOnOrAfter(beforeCreation);
            entity.CreatedAt.Should().BeOnOrBefore(afterCreation);
            entity.UpdatedAt.Should().BeOnOrAfter(beforeCreation);
            entity.UpdatedAt.Should().BeOnOrBefore(afterCreation);
        }

        [Fact]
        public void ConstructorShouldInitializeEntityAsNotDeleted()
        {
            var entity = CreateEntity();

            entity.IsDeleted.Should().BeFalse();
            entity.DeletedAt.Should().BeNull();
            entity.UpdatedBy.Should().BeNull();
            entity.Version.Should().Be(0);
        }

        [Fact]
        public void DeleteShouldMarkEntityAsDeleted()
        {
            var entity = CreateEntity();
            var beforeDeletion = DateTime.UtcNow;

            entity.Delete();

            var afterDeletion = DateTime.UtcNow;

            entity.IsDeleted.Should().BeTrue();
            entity.DeletedAt.Should().NotBeNull();
            entity.DeletedAt!.Value.Should().BeOnOrAfter(beforeDeletion);
            entity.DeletedAt.Value.Should().BeOnOrBefore(afterDeletion);
            entity.UpdatedAt.Should().BeOnOrAfter(beforeDeletion);
        }

        [Fact]
        public void DeleteShouldSetUpdatedBy()
        {
            var deletedBy = Guid.NewGuid();
            var entity = CreateEntity();

            entity.Delete(deletedBy);

            entity.UpdatedBy.Should().Be(deletedBy);
        }

        [Fact]
        public void DeleteWithoutUserShouldLeaveUpdatedByNull()
        {
            var entity = CreateEntity();

            entity.Delete();

            entity.UpdatedBy.Should().BeNull();
        }

        [Fact]
        public void RegisterUpdateShouldUpdateAuditInformation()
        {
            var updatedBy = Guid.NewGuid();
            var entity = CreateEntity();
            var originalCreatedAt = entity.CreatedAt;
            var beforeUpdate = DateTime.UtcNow;

            entity.RegisterUpdateForTest(updatedBy);

            var afterUpdate = DateTime.UtcNow;

            entity.CreatedAt.Should().Be(originalCreatedAt);
            entity.UpdatedBy.Should().Be(updatedBy);
            entity.UpdatedAt.Should().BeOnOrAfter(beforeUpdate);
            entity.UpdatedAt.Should().BeOnOrBefore(afterUpdate);
        }

        [Fact]
        public void RegisterUpdateWithoutUserShouldSetUpdatedByToNull()
        {
            var entity = CreateEntity(updatedBy: Guid.NewGuid());

            entity.RegisterUpdateForTest();

            entity.UpdatedBy.Should().BeNull();
        }

        private static TestEntity CreateEntity(
            Guid? createdBy = null,
            Guid? updatedBy = null)
        {
            var entity = new TestEntity(
                Guid.NewGuid(),
                Guid.NewGuid(),
                createdBy);

            if (updatedBy.HasValue)
            {
                entity.RegisterUpdateForTest(updatedBy);
            }

            return entity;
        }

        private sealed class TestEntity : BaseEntity<Guid>
        {
            public TestEntity(
                Guid id,
                Guid tenantId,
                Guid? createdBy = null)
                : base(id, tenantId, createdBy)
            {
            }

            public void RegisterUpdateForTest(Guid? updatedBy = null)
            {
                RegisterUpdate(updatedBy);
            }
        }
    }
#pragma warning restore CA1515 // Considere la posibilidad de hacer que los tipos públicos sean internos
}

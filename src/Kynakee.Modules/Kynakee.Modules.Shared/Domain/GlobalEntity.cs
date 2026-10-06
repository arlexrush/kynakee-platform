namespace Kynakee.Modules.SharedKernel.Domain
{
    /// <summary>
    /// Base class for persistent resources shared across all tenants.
    /// Ownership fields record provenance and do not restrict visibility.
    /// </summary>
    /// <typeparam name="TId">Type of the entity identifier.</typeparam>
    public abstract class GlobalEntity<TId>
    {
        public TId Id { get; protected set; } = default!;

        public Guid? OwnerTenantId { get; protected set; }

        public Guid? OwnerUserId { get; protected set; }

        public DateTime CreatedAt { get; protected set; }

        public DateTime UpdatedAt { get; protected set; }

        public Guid? CreatedBy { get; protected set; }

        public Guid? UpdatedBy { get; protected set; }

        public bool IsDeleted { get; protected set; }

        public DateTime? DeletedAt { get; protected set; }

        public uint Version { get; protected set; }

        protected GlobalEntity() { }

        protected GlobalEntity(
            TId id,
            Guid? ownerTenantId,
            Guid? ownerUserId)
        {
            Id = id;
            OwnerTenantId = ownerTenantId;
            OwnerUserId = ownerUserId;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = CreatedAt;
            CreatedBy = ownerUserId;
        }

        /// <summary>
        /// Marks the resource as deleted without removing its audit history.
        /// </summary>
        public virtual void Delete(Guid? deletedBy = null)
        {
            IsDeleted = true;
            DeletedAt = DateTime.UtcNow;
            UpdatedAt = DeletedAt.Value;
            UpdatedBy = deletedBy;
        }

        /// <summary>
        /// Records the user and timestamp of a modification.
        /// </summary>
        protected void RegisterUpdate(Guid? updatedBy = null)
        {
            UpdatedAt = DateTime.UtcNow;
            UpdatedBy = updatedBy;
        }
    }
}
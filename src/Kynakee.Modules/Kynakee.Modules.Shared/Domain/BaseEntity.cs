namespace Kynakee.Modules.SharedKernel.Domain
{
    /// <summary>
    /// Base class for tenant-owned persistent entities in the Kynakee platform.
    /// Global persistent entities derive from the independent GlobalEntity&lt;TId&gt;.
    /// Provides: multi-tenant isolation, audit trail, and soft delete support (ADR-018).
    /// </summary>
    /// <typeparam name="TId">Type of the entity identifier (Guid, int, strongly-typed Id).</typeparam>
    public abstract class BaseEntity<TId>
    {
        // ── Identity ──────────────────────────────────────────────────────────────

        /// <summary>Entity primary key.</summary>
        public TId Id { get; protected set; } = default!;

        // ── Multi-Tenancy (ADR-007) ───────────────────────────────────────────────

        /// <summary>
        /// Tenant identifier. MANDATORY on every tenant-owned entity.
        /// Tenant-owned queries MUST filter by TenantId to enforce data isolation.
        /// </summary>
        public Guid TenantId { get; protected set; }

        // ── Audit Trail (ADR-018) ─────────────────────────────────────────────────

        /// <summary>UTC timestamp when the entity was created.</summary>
        public DateTime CreatedAt { get; protected set; }

        /// <summary>UTC timestamp of the last update.</summary>
        public DateTime UpdatedAt { get; protected set; }

        /// <summary>UserId of the user who created this entity. Optional.</summary>
        public Guid? CreatedBy { get; protected set; }

        /// <summary>UserId of the user who last updated this entity. Optional.</summary>
        public Guid? UpdatedBy { get; protected set; }

        // ── Soft Delete (ADR-018) ─────────────────────────────────────────────────

        /// <summary>
        /// Indicates whether this entity has been soft-deleted.
        /// NEVER use DbContext.Remove() — always call Delete() instead (Copilot Rule #3).
        /// </summary>
        public bool IsDeleted { get; protected set; }

        /// <summary>UTC timestamp when the entity was soft-deleted. Null if not deleted.</summary>
        public DateTime? DeletedAt { get; protected set; }

        // ── Concurrency (ADR-009) ─────────────────────────────────────────────────

        /// <summary>
        /// Optimistic concurrency token.
        /// EF Core uses this to detect concurrent modifications.
        /// </summary>
        public uint Version { get; protected set; }

        // ── Protected constructor ─────────────────────────────────────────────────

        /// <summary>
        /// Protected constructor for EF Core and derived classes.
        /// Use factory methods or constructors in derived classes to create instances.
        /// </summary>
        protected BaseEntity() { }

        /// <summary>
        /// Initializes a new entity with required audit fields.
        /// </summary>
        protected BaseEntity(TId id, Guid tenantId, Guid? createdBy = null)
        {
            Id = id;
            TenantId = tenantId;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
            CreatedBy = createdBy;
            IsDeleted = false;
        }

        // ── Soft Delete ───────────────────────────────────────────────────────────

        /// <summary>
        /// Marks this entity as deleted (soft delete).
        /// NEVER call DbContext.Remove() — use this method instead (ADR-018).
        /// </summary>
        public virtual void Delete(Guid? deletedBy = null)
        {
            IsDeleted = true;
            DeletedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
            UpdatedBy = deletedBy;
        }

        // ── Audit update ──────────────────────────────────────────────────────────

        /// <summary>
        /// Updates the audit trail after a modification.
        /// Call this in every method that modifies the entity state.
        /// </summary>
        protected void RegisterUpdate(Guid? updatedBy = null)
        {
            UpdatedAt = DateTime.UtcNow;
            UpdatedBy = updatedBy;
        }
    }
}

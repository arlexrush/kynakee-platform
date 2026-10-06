namespace Kynakee.Modules.SharedKernel.Contracts
{
    /// <summary>
    /// Provides access to the current tenant and user context.
    /// Populated by TenantIsolationBehavior from the JWT claims (ADR-007, ADR-016).
    ///
    /// Inject this interface in handlers, services, or behaviors that need
    /// to know the current tenant or user without receiving them as parameters.
    ///
    /// Implementation: HttpTenantContext (registered as Scoped)
    /// reads TenantId and UserId from IHttpContextAccessor → JWT claims.
    ///
    /// Rules:
    /// - NEVER hardcode TenantId — always read from ITenantContext
    /// - NEVER pass TenantId as a method parameter when ITenantContext is available
    /// - All EF Core queries MUST filter by TenantId from this context (ADR-007)
    ///
    /// Usage in handler:
    ///   public sealed class GetProjectsHandler : IRequestHandler{...}
    ///   {
    ///       private readonly ITenantContext _tenantContext;
    ///
    ///       public async Task{Result{...}} Handle(...)
    ///       {
    ///           var tenantId = _tenantContext.TenantId;
    ///           var projects = await _db.Projects
    ///               .Where(p => p.TenantId == tenantId)
    ///               .ToListAsync(ct);
    ///       }
    ///   }
    /// </summary>
    public interface ITenantContext
    {
        /// <summary>
        /// Identifier of the current tenant.
        /// Extracted from the JWT 'tenant_id' claim by TenantIsolationBehavior.
        /// </summary>
        Guid TenantId { get; }

        /// <summary>
        /// Identifier of the currently authenticated user.
        /// Extracted from the JWT 'sub' claim by TenantIsolationBehavior.
        /// </summary>
        Guid UserId { get; }

        /// <summary>
        /// Indicates whether the current request is authenticated.
        /// False for anonymous or system-initiated operations.
        /// </summary>
        bool IsAuthenticated { get; }
    }
}

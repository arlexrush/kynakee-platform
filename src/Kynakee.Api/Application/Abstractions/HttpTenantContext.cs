namespace Kynakee.Api.Application.Abstractions
{
#pragma warning disable CA1515 // Considere la posibilidad de hacer que los tipos públicos sean internos
    public sealed class HttpTenantContext : ITenantContextWriter
    {
        public Guid TenantId { get; private set; }
        public Guid UserId { get; private set; }
        public bool IsAuthenticated { get; private set; }

        public void Initialize(Guid tenantId, Guid userId, bool isAuthenticated)
        {
            TenantId = tenantId;
            UserId = userId;
            IsAuthenticated = isAuthenticated;
        }
    }

#pragma warning restore CA1515 // Considere la posibilidad de hacer que los tipos públicos sean internos
}

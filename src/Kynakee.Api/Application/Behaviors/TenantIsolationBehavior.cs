using Kynakee.Api.Application.Abstractions;
using Kynakee.Modules.SharedKernel.Application;
using MediatR;
using System.Security.Claims;

namespace Kynakee.Api.Application.Behaviors
{
#pragma warning disable CA1515 // Considere la posibilidad de hacer que los tipos públicos sean internos

    public sealed class TenantIsolationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ITenantContextWriter _tenantContext;

        public TenantIsolationBehavior(
            IHttpContextAccessor httpContextAccessor,
            ITenantContextWriter tenantContext)
        {
            _httpContextAccessor = httpContextAccessor;
            _tenantContext = tenantContext;
        }

        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(next);

            var httpContext = _httpContextAccessor.HttpContext;
            var identity = httpContext?.User?.Identity;
            var isAuthenticated = identity?.IsAuthenticated == true;

            if (!isAuthenticated || httpContext is null)
            {
                _tenantContext.Initialize(
                    Guid.Empty,
                    Guid.Empty,
                    false);

                return await next(cancellationToken)
                    .ConfigureAwait(false);
            }

            var tenantId = GetClaimGuid(
                httpContext.User,
                "tenant_id");

            var userId = GetClaimGuid(
                httpContext.User,
                ClaimTypes.NameIdentifier,
                "sub");

            if (tenantId == Guid.Empty || userId == Guid.Empty)
            {
                var error = ApplicationError.Unauthorized(
                    "TENANT_001",
                    "The authenticated request does not contain valid tenant and user claims.");

                return ResultResponseFactory.CreateFailure<TResponse>(error);
            }

            _tenantContext.Initialize(
                tenantId,
                userId,
                true);

            return await next(cancellationToken)
                .ConfigureAwait(false);
        }

        private static Guid GetClaimGuid(
            ClaimsPrincipal principal,
            params string[] claimTypes)
        {
            foreach (var claimType in claimTypes)
            {
                var claimValue = principal.FindFirst(claimType)?.Value;

                if (Guid.TryParse(claimValue, out var value))
                {
                    return value;
                }
            }

            return Guid.Empty;
        }
    }

#pragma warning restore CA1515 // Considere la posibilidad de hacer que los tipos públicos sean internos
}

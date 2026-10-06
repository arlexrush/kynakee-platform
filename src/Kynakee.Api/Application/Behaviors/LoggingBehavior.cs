using Kynakee.Api.Application.Abstractions;
using Kynakee.Modules.SharedKernel.Contracts;
using MediatR;
using Microsoft.Extensions.Logging;
using Serilog.Context;
using System.Diagnostics;
using System.Security.Claims;

namespace Kynakee.Api.Application.Behaviors
{
#pragma warning disable CA1515 // Considere la posibilidad de hacer que los tipos públicos sean internos

    /// <summary>
    ///Comportamiento de la tubería de MediatR que añade contexto de registro (TenantId, CorrelationId, UserId,
    /// ProjectId), mide la duración de la petición y registra inicio, finalización y errores de cada solicitud.
    /// </summary>
    /// <remarks>Empuja propiedades en Serilog LogContext, utiliza Stopwatch para medir el tiempo transcurrido
    /// y resuelve identificadores desde ITenantContext, ICorrelationContext o las reclamaciones del HttpContext; las
    /// excepciones capturadas se registran y se re-lanzan.</remarks>
    /// <typeparam name="TRequest">Tipo de la petición MediatR.</typeparam>
    /// <typeparam name="TResponse">Tipo de la respuesta MediatR.</typeparam>
    public sealed class LoggingBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    {
        private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ICorrelationContext _correlationContext;
        private readonly ITenantContext? _tenantContext;

        public LoggingBehavior(
            ILogger<LoggingBehavior<TRequest, TResponse>> logger,
            IHttpContextAccessor httpContextAccessor,
            ICorrelationContext correlationContext,
            ITenantContext? tenantContext = null)
        {
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
            _correlationContext = correlationContext;
            _tenantContext = tenantContext;
        }

        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            var requestName = typeof(TRequest).Name;
            var stopwatch = Stopwatch.StartNew();

            var tenantId = ResolveTenantId();
            var userId = ResolveUserId();
            var correlationId = ResolveCorrelationId();
            var projectId = _correlationContext.ProjectId;

            using var tenantProperty = LogContext.PushProperty("TenantId", tenantId);

            using var correlationProperty = LogContext.PushProperty("CorrelationId", correlationId);

            using var userProperty = LogContext.PushProperty("UserId", userId);

            using var projectProperty = LogContext.PushProperty("ProjectId", projectId);

            LoggingBehaviorLogMessages.RequestStarted(_logger, requestName);

            try
            {
                ArgumentNullException.ThrowIfNull(next, nameof(next));

                var response = await next(cancellationToken).ConfigureAwait(false);

                stopwatch.Stop();

                LoggingBehaviorLogMessages.RequestCompleted(
                    _logger,
                    requestName,
                    stopwatch.ElapsedMilliseconds);

                return response;
            }
            catch (Exception exception)
            {
                stopwatch.Stop();

                LoggingBehaviorLogMessages.RequestFailed(
                    _logger,
                    exception,
                    requestName,
                    stopwatch.ElapsedMilliseconds);

                throw;
            }
        }
        
        private Guid ResolveTenantId()
        {
            if (_tenantContext is not null &&
                _tenantContext.TenantId != Guid.Empty)
            {
                return _tenantContext.TenantId;
            }

            return TryParseClaim(
                "tenant_id",
                ClaimTypes.GroupSid);
        }

        private Guid ResolveUserId()
        {
            if (_tenantContext is not null &&
                _tenantContext.UserId != Guid.Empty)
            {
                return _tenantContext.UserId;
            }

            return TryParseClaim(
                ClaimTypes.NameIdentifier,
                "sub");
        }

        private string ResolveCorrelationId()
        {
            if (!string.IsNullOrWhiteSpace(_correlationContext.CorrelationId))
            {
                return _correlationContext.CorrelationId;
            }

            var httpContext = _httpContextAccessor.HttpContext;

            return httpContext?.Request.Headers["X-Correlation-ID"].FirstOrDefault()
                ?? "N/A";
        }

        private Guid TryParseClaim(params string[] claimTypes)
        {
            var user = _httpContextAccessor.HttpContext?.User;

            foreach (var claimType in claimTypes)
            {
                var claimValue = user?.FindFirst(claimType)?.Value;

                if (Guid.TryParse(claimValue, out var value))
                {
                    return value;
                }
            }

            return Guid.Empty;
        }
    }

    internal static partial class LoggingBehaviorLogMessages
    {
        [LoggerMessage(
            EventId = 1000,
            Level = LogLevel.Information,
            Message = "MediatR request started. RequestName: {RequestName}")]
        internal static partial void RequestStarted(
            ILogger logger,
            string requestName);

        [LoggerMessage(
            EventId = 1001,
            Level = LogLevel.Information,
            Message = "MediatR request completed. RequestName: {RequestName}, ElapsedMilliseconds: {ElapsedMilliseconds}")]
        internal static partial void RequestCompleted(
            ILogger logger,
            string requestName,
            long elapsedMilliseconds);

        [LoggerMessage(
            EventId = 1002,
            Level = LogLevel.Error,
            Message = "MediatR request failed. RequestName: {RequestName}, ElapsedMilliseconds: {ElapsedMilliseconds}")]
        internal static partial void RequestFailed(
            ILogger logger,
            Exception exception,
            string requestName,
            long elapsedMilliseconds);
    }

#pragma warning restore CA1515 // Considere la posibilidad de hacer que los tipos públicos sean internos
}

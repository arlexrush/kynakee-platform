using System.Diagnostics.CodeAnalysis;
using Serilog.Context;

namespace Kynakee.Gateway;

[SuppressMessage("Performance", "CA1812", Justification = "Created by ASP.NET Core middleware activation.")]
internal sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-ID";
    private const int MaximumHeaderLength = 128;

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[HeaderName].Count == 1
            ? context.Request.Headers[HeaderName].ToString().Trim()
            : string.Empty;

        if (correlationId.Length is 0 or > MaximumHeaderLength || correlationId.Any(char.IsControl))
        {
            correlationId = Guid.NewGuid().ToString("D");
        }

        context.Request.Headers[HeaderName] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context).ConfigureAwait(false);
        }
    }
}
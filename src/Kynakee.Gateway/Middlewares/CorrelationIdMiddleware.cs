using System.Diagnostics.CodeAnalysis;
using Serilog.Context;

namespace Kynakee.Gateway;

/// <summary>
/// Middleware que garantiza y propaga un identificador de correlación (X-Correlation-ID) en la solicitud y la
/// respuesta, validando el encabezado y generando un GUID cuando falta o es inválido.
/// </summary>
/// <remarks>Valida el encabezado "X-Correlation-ID": acepta un único valor, lo recorta y lo rechaza si está
/// vacío, excede 128 caracteres o contiene caracteres de control; en esos casos se genera un nuevo GUID en formato "D".
/// Establece el identificador en los encabezados de petición y respuesta y lo añade al LogContext como "CorrelationId"
/// durante la ejecución del siguiente middleware.</remarks>
/// <param name="next">RequestDelegate que representa el siguiente componente de la tubería HTTP.</param>
[SuppressMessage("Performance", "CA1812", Justification = "Created by ASP.NET Core middleware activation.")]
internal sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-ID"; // Nombre del encabezado HTTP para el identificador de correlación
    private const int MaximumHeaderLength = 128; // Longitud máxima permitida para el valor del encabezado

    /// <summary>
    /// Genera o reutiliza un identificador de correlación, lo asegura en las cabeceras de petición y respuesta y lo
    /// añade al contexto de logging durante la ejecución del siguiente middleware.
    /// </summary>
    /// <remarks>Si la cabecera existe se valida su longitud y ausencia de caracteres de control; si es
    /// inválida o está ausente se genera un GUID. El identificador se asigna a Request y Response y se publica en
    /// Serilog LogContext como la propiedad "CorrelationId" mientras se ejecuta el siguiente middleware.</remarks>
    /// <param name="context">HttpContext de la petición actual, utilizado para leer y escribir cabeceras, acceder a la respuesta y delegar en
    /// el siguiente middleware del pipeline.</param>
    /// <returns>Una Task que representa la ejecución asíncrona del middleware.</returns>
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
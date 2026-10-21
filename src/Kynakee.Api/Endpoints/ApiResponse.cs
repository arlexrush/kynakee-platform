namespace Kynakee.Api.Endpoints;

/// <summary>
/// Contenedor de respuesta estándar para APIs que encapsula el resultado junto con información de trazabilidad y marca
/// temporal.
/// </summary>
/// <remarks>Registro inmutable pensado para serializarse en respuestas HTTP y facilitar el rastreo y la
/// correlación de solicitudes.</remarks>
/// <typeparam name="T">Tipo del valor devuelto contenido en la respuesta.</typeparam>
/// <param name="Data">Valor devuelto por la operación.</param>
/// <param name="TraceId">Identificador de traza o correlación para seguimiento y diagnóstico.</param>
/// <param name="Timestamp">Marca temporal (UTC) que indica cuándo se generó la respuesta.</param>
internal sealed record ApiResponse<T>(T Data, string TraceId, DateTime Timestamp);

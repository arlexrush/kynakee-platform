namespace Kynakee.Api.Endpoints;

internal sealed record ApiResponse<T>(T Data, string TraceId, DateTime Timestamp);

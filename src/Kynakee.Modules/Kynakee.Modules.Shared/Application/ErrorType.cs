namespace Kynakee.Modules.SharedKernel.Application
{
    /// <summary>
    /// Classifies the type of error returned by a Result failure.
    /// Used by the API layer to map errors to appropriate HTTP status codes.
    ///
    /// Mapping to HTTP status codes:
    /// - Validation    → 400 Bad Request
    /// - NotFound      → 404 Not Found
    /// - Conflict      → 409 Conflict
    /// - Unauthorized  → 401 Unauthorized / 403 Forbidden
    /// - AI            → 502 Bad Gateway (upstream AI provider failure)
    /// - MCP           → 502 Bad Gateway (upstream MCP provider failure)
    /// - Credits       → 402 Payment Required
    /// </summary>
    public enum ErrorType
    {
        /// <summary>Input validation failed (FluentValidation).</summary>
        Validation,

        /// <summary>Requested resource does not exist.</summary>
        NotFound,

        /// <summary>Operation conflicts with current state (e.g., duplicate, wrong phase).</summary>
        Conflict,

        /// <summary>User is not authenticated or not authorized for this operation.</summary>
        Unauthorized,

        /// <summary>AI provider call failed (all retries exhausted, ADR-019).</summary>
        AI,

        /// <summary>MCP provider call failed (all retries exhausted, ADR-019).</summary>
        MCP,

        /// <summary>Insufficient credits to execute the operation (ADR-013).</summary>
        Credits,

        /// <summary>Unexpected internal error.</summary>
        Unexpected
    }
}

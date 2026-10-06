namespace Kynakee.Modules.SharedKernel.Application
{
    /// <summary>
    /// Represents a structured error returned by a Result failure.
    /// Immutable record — errors are value objects, not entities.
    ///
    /// Usage: Return via Result.Failure(Error.Validation("PROJ_001", "Project name is required"))
    /// Never throw exceptions for business logic — always return Result with Error (Copilot Rule #2).
    /// </summary>
    /// <param name="Code">
    /// Unique error code in format MODULE_NUMBER (e.g., PROJ_001, BILL_003, AI_002).
    /// Used by clients to handle specific error cases programmatically.
    /// </param>
    /// <param name="Message">
    /// Human-readable error description. May be shown to end users.
    /// </param>
    /// <param name="Type">
    /// Error classification used to map to HTTP status codes.
    /// </param>
    public sealed record ApplicationError(string Code, string Message, ErrorType Type)
    {
        // ── Static factory methods ────────────────────────────────────────────────

        /// <summary>Creates a Validation error (HTTP 400).</summary>
        public static ApplicationError Validation(string code, string message)
            => new(code, message, ErrorType.Validation);

        /// <summary>Creates a NotFound error (HTTP 404).</summary>
        public static ApplicationError NotFound(string code, string message)
            => new(code, message, ErrorType.NotFound);

        /// <summary>Creates a Conflict error (HTTP 409).</summary>
        public static ApplicationError Conflict(string code, string message)
            => new(code, message, ErrorType.Conflict);

        /// <summary>Creates an Unauthorized error (HTTP 401/403).</summary>
        public static ApplicationError Unauthorized(string code, string message)
            => new(code, message, ErrorType.Unauthorized);

        /// <summary>Creates an AI provider error (HTTP 502).</summary>
        public static ApplicationError AI(string code, string message)
            => new(code, message, ErrorType.AI);

        /// <summary>Creates an MCP provider error (HTTP 502).</summary>
        public static ApplicationError MCP(string code, string message)
            => new(code, message, ErrorType.MCP);

        /// <summary>Creates a Credits error (HTTP 402).</summary>
        public static ApplicationError Credits(string code, string message)
            => new(code, message, ErrorType.Credits);

        /// <summary>Creates an Unexpected error (HTTP 500).</summary>
        public static ApplicationError Unexpected(string code, string message)
            => new(code, message, ErrorType.Unexpected);


        /// <summary>
        /// Represents a "no error" state. Useful for default values or when no error is present.
        /// </summary>
        public static ApplicationError None => new("NONE", "No error", ErrorType.Validation);

        // ── Predefined common errors ──────────────────────────────────────────────

        /// <summary>Generic not found error when no specific code is needed.</summary>
        public static readonly ApplicationError NullValue =
            new("SHARED_001", "A null value was provided.", ErrorType.Validation);

        /// <summary>Generic unauthorized error.</summary>
        public static readonly ApplicationError AccessDenied =
            new("SHARED_002", "Access denied.", ErrorType.Unauthorized);

        /// <summary>Generic insufficient credits error.</summary>
        public static readonly ApplicationError InsufficientCredits =
            new("SHARED_003", "Insufficient credits to execute this operation.", ErrorType.Credits);
    }
}

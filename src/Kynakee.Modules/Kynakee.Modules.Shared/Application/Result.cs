namespace Kynakee.Modules.SharedKernel.Application
{

    /// <summary>
    /// Representa el resultado de una operación, indicando si se completó con éxito.
    /// </summary>
    /// <remarks>Funciona como contrato mínimo para devolver el estado de éxito; las implementaciones pueden
    /// aportar información adicional (por ejemplo, errores, mensajes o códigos).</remarks>
    public interface IResult
    {
        bool IsSuccess { get; }        
    }


    public static class ResultFactory
    {
        
        public static Result<T> Success<T>(T value) => new(true, value, null);
        public static Result<T> Failure<T>(ApplicationError error) => new(false, default, error);
        public static Result Ok() => new(true, null);
        public static Result Failure(ApplicationError error) => new(false, error);
    }


    /// <summary>
    /// Represents the outcome of an operation that can either succeed or fail.
    /// 
    /// MANDATORY: Every command and query handler MUST return Result{T}.
    /// NEVER throw exceptions for business logic (Copilot Rule #2).
    /// Exceptions are only acceptable for truly unexpected infrastructure failures.
    ///
    /// Usage:
    ///   return Result.Ok(project);
    ///   return Result.Failure{Project}(Error.NotFound("PROJ_001", "Project not found"));
    /// </summary>
    /// <typeparam name="T">The type of the value returned on success.</typeparam>
    public sealed class Result<T>: IResult
    {
        // ── Properties ────────────────────────────────────────────────────────────

        /// <summary>Indicates whether the operation succeeded.</summary>
        public bool IsSuccess { get; }

        /// <summary>Indicates whether the operation failed.</summary>
        public bool IsFailure => !IsSuccess;

        /// <summary>
        /// The value returned on success.
        /// Only access this when IsSuccess is true.
        /// </summary>
        public T? Value { get; }

        /// <summary>
        /// The error returned on failure.
        /// Only access this when IsFailure is true.
        /// </summary>
        public ApplicationError? Error { get; }

        // ── Private constructor ───────────────────────────────────────────────────

        internal Result(bool isSuccess, T? value, ApplicationError? error)
        {
            IsSuccess = isSuccess;
            Value = value;
            Error = error;
        }
        

        // ── Functional helpers ────────────────────────────────────────────────────

        /// <summary>
        /// Executes onSuccess if the result is successful, otherwise onFailure.
        /// Useful for mapping results in endpoint handlers.
        /// </summary>
        public TResult Match<TResult>(
            Func<T, TResult> onSuccess,
            Func<ApplicationError, TResult> onFailure)
        {
            ArgumentNullException.ThrowIfNull(onSuccess);
            ArgumentNullException.ThrowIfNull(onFailure);

            if(IsSuccess)
            {
                return onSuccess(Value!);
            }
            else
            {
                return onFailure(Error!);
            }
        }

        /// <summary>
        /// Maps the value of a successful result to a new type.
        /// If the result is a failure, the error is propagated.
        /// </summary>
        public Result<TNew> Map<TNew>(Func<T, TNew> mapper)
        {
            ArgumentNullException.ThrowIfNull(mapper);

            return IsSuccess
                ? ResultFactory.Success(mapper(Value!))
                : ResultFactory.Failure<TNew>(Error!);
        }
    }    

    /// <summary>
    /// Non-generic Result for operations that do not return a value (void commands).
    /// </summary>
    public sealed class Result: IResult
    {
        /// <summary>Indicates whether the operation succeeded.</summary>
        public bool IsSuccess { get; }

        /// <summary>Indicates whether the operation failed.</summary>
        public bool IsFailure => !IsSuccess;

        /// <summary>The error returned on failure. Null on success.</summary>
        public ApplicationError? Error { get; }

        internal Result(bool isSuccess, ApplicationError? error)
        {
            IsSuccess = isSuccess;
            Error = error;
        }        
               
        /// <summary>
        /// Executes onSuccess if the result is successful, otherwise onFailure.
        /// </summary>
        public TResult Match<TResult>(
            Func<TResult> onSuccess,
            Func<ApplicationError, TResult> onFailure)
        {
            ArgumentNullException.ThrowIfNull(onSuccess);
            ArgumentNullException.ThrowIfNull(onFailure);

            return IsSuccess ? onSuccess() : onFailure(Error!);
        }
    }


}

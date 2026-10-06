using Kynakee.Modules.SharedKernel.Application;

namespace Kynakee.Modules.SharedKernel.Contracts
{
    /// <summary>
    /// Provides credit reservation operations for commands that require credits.
    /// Billing implements this contract; the API pipeline consumes it.
    /// </summary>
    public interface ITokenGateService
    {
        /// <summary>
        /// Reserves credits for a command that requires them. If the tenant does not have enough credits, the operation will fail.
        /// </summary>
        /// <param name="tenantId">The ID of the tenant for whom to reserve credits.</param>
        /// <param name="creditsRequired">The number of credits required for the operation.</param>
        /// <param name="operationId">The ID of the operation for which to reserve credits.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A <see cref="Result"/> indicating the success or failure of the operation.</returns>
        Task<Result> ReserveAsync(
        Guid tenantId,
        int creditsRequired,
        Guid operationId,
        CancellationToken cancellationToken);

        /// <summary>
        /// Consumes previously reserved credits for a command.
        /// </summary>
        /// <param name="tenantId">The ID of the tenant for whom to consume credits.</param>
        /// <param name="operationId">The ID of the operation for which to consume credits.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A <see cref="Result"/> indicating the success or failure of the operation.</returns>
        Task<Result> ConsumeAsync(
            Guid tenantId,
            Guid operationId,
            CancellationToken cancellationToken);

        /// <summary>
        /// Releases previously reserved credits for a command.
        /// </summary>
        /// <param name="tenantId">The ID of the tenant for whom to release credits.</param>
        /// <param name="operationId">The ID of the operation for which to release credits.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A <see cref="Result"/> indicating the success or failure of the operation.</returns>
        Task<Result> ReleaseAsync(
            Guid tenantId,
            Guid operationId,
            CancellationToken cancellationToken);
    }
}

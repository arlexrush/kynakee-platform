namespace Kynakee.Modules.SharedKernel.Contracts
{
    /// <summary>
    /// Marker interface for commands that require credits to execute.
    /// Implemented by commands that trigger AI or MCP operations (ADR-013).
    ///
    /// The TokenGateBehavior (MediatR pipeline) checks this interface
    /// BEFORE executing the command handler. If the tenant has insufficient
    /// credits, the command is rejected with Error.InsufficientCredits
    /// and the handler is never invoked.
    ///
    /// Pipeline execution order (ADR-004):
    ///   1. LoggingBehavior
    ///   2. ValidationBehavior
    ///   3. TenantIsolationBehavior
    ///   4. TokenGateBehavior      ← checks IRequiresCredits here
    ///   5. TransactionBehavior
    ///   6. DomainEventDispatchBehavior
    ///   → Handler executes
    ///
    /// Usage:
    ///   public sealed record RunScopeAgentCommand(
    ///       Guid ProjectId,
    ///       Guid TenantId) : ICommand{IReadOnlyList{WorkItemDto}}, IRequiresCredits
    ///   {
    ///       public int CreditsRequired => 10; // cost of this AI operation
    ///   }
    /// </summary>
    public interface IRequiresCredits
    {
        /// <summary>
        /// Number of credits required to execute this command.
        /// The TokenGateBehavior verifies the tenant has at least this many credits
        /// before allowing the command to proceed.
        ///
        /// Credit costs are defined per command and reflect the AI/MCP token
        /// consumption estimate for that operation (ADR-013).
        /// </summary>
        int CreditsRequired { get; }
    }
}

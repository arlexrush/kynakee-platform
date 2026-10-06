using Kynakee.Modules.SharedKernel.Application;

namespace Kynakee.Modules.Projects.Domain.ValueObjects
{
    public sealed record ComponentPricingResult
    {
        private ComponentPricingResult(
            Guid componentId,
            PricingStatus status,
            Money? unitPrice,
            string? providerName,
            PricingSource source,
            DateTime queriedAt,
            Confidence confidence,
            bool isFallback,
            string? failureReason)
        {
            ComponentId = componentId;
            Status = status;
            UnitPrice = unitPrice;
            ProviderName = providerName;
            Source = source;
            QueriedAt = queriedAt;
            Confidence = confidence;
            IsFallback = isFallback;
            FailureReason = failureReason;
        }

        public Guid ComponentId { get; }

        public PricingStatus Status { get; }

        public Money? UnitPrice { get; }

        public string? ProviderName { get; }

        public PricingSource Source { get; }

        public DateTime QueriedAt { get; }

        public Confidence Confidence { get; }

        public bool IsFallback { get; }

        public string? FailureReason { get; }

        public bool HasPrice =>
            Status == PricingStatus.Found &&
            UnitPrice is not null;

        public static Result<ComponentPricingResult> CreateSuccessful(
            Guid componentId,
            Money unitPrice,
            string? providerName,
            PricingSource source,
            Confidence confidence,
            bool isFallback,
            DateTime? queriedAt = null)
        {
            if (componentId == Guid.Empty)
            {
                return ResultFactory.Failure<ComponentPricingResult>(
                    ApplicationError.Validation(
                        "PROJ_COMPONENT_PRICING_COMPONENT_REQUIRED",
                        "The priced component identifier is required."));
            }

            ArgumentNullException.ThrowIfNull(unitPrice);
            ArgumentNullException.ThrowIfNull(confidence);

            if (source == PricingSource.None)
            {
                return ResultFactory.Failure<ComponentPricingResult>(
                    ApplicationError.Validation(
                        "PROJ_COMPONENT_PRICING_SOURCE_REQUIRED",
                        "The pricing source is required."));
            }

            return ResultFactory.Success(
                new ComponentPricingResult(
                    componentId,
                    PricingStatus.Found,
                    unitPrice,
                    Normalize(providerName),
                    source,
                    queriedAt ?? DateTime.UtcNow,
                    confidence,
                    isFallback,
                    null));
        }

        public static Result<ComponentPricingResult> CreateUnavailable(
            Guid componentId,
            PricingSource source,
            string failureReason,
            DateTime? queriedAt = null)
        {
            if (componentId == Guid.Empty)
            {
                return ResultFactory.Failure<ComponentPricingResult>(
                    ApplicationError.Validation(
                        "PROJ_COMPONENT_PRICING_COMPONENT_REQUIRED",
                        "The priced component identifier is required."));
            }

            if (string.IsNullOrWhiteSpace(failureReason))
            {
                return ResultFactory.Failure<ComponentPricingResult>(
                    ApplicationError.Validation(
                        "PROJ_COMPONENT_PRICING_FAILURE_REASON_REQUIRED",
                        "The pricing failure reason is required."));
            }

            return ResultFactory.Success(
                new ComponentPricingResult(
                    componentId,
                    PricingStatus.Unavailable,
                    null,
                    null,
                    source,
                    queriedAt ?? DateTime.UtcNow,
                    new Confidence(0m),
                    source != PricingSource.McpProvider,
                    failureReason.Trim()));
        }

        private static string? Normalize(string? value) =>
            string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
    }

    
}

using Kynakee.Modules.SharedKernel.Application;

namespace Kynakee.Modules.Mcp.Domain.ValueObjects;

public readonly record struct ProviderRating
{
    private ProviderRating(decimal value) => Value = value;

    public decimal Value { get; }

    public static ProviderRating Default => new(5m);

    public static Result<ProviderRating> Create(decimal value) =>
        value is < 0m or > 5m
            ? ResultFactory.Failure<ProviderRating>(ApplicationError.Validation(
                "MCP_RATING_INVALID",
                "Provider rating must be between 0 and 5."))
            : ResultFactory.Success(new ProviderRating(value));
}

using FluentValidation;

namespace Kynakee.Modules.Billing.Application.Queries.GetCreditTransactions;

public sealed class GetCreditTransactionsQueryValidator
    : AbstractValidator<GetCreditTransactionsQuery>
{
    public GetCreditTransactionsQueryValidator()
    {
        RuleFor(query => query.TenantId).NotEmpty();
        RuleFor(query => query.Type)
            .Must(type => !type.HasValue || Enum.IsDefined(type.Value));
        RuleFor(query => query.From)
            .Must(from => !from.HasValue || from.Value.Kind == DateTimeKind.Utc);
        RuleFor(query => query.EndDate)
            .Must(to => !to.HasValue || to.Value.Kind == DateTimeKind.Utc);
        RuleFor(query => query.EndDate)
            .Must((query, endDate) => !endDate.HasValue || !query.From.HasValue || endDate >= query.From)
            .WithMessage("The end date must be on or after the start date.");
        RuleFor(query => query.Cursor)
            .MaximumLength(32)
            .Must(cursor => cursor is null || Guid.TryParseExact(cursor, "N", out _));
        RuleFor(query => query.Limit).InclusiveBetween(1, 100);
    }
}
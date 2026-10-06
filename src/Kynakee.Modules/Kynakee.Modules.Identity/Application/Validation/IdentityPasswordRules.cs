using FluentValidation;

namespace Kynakee.Modules.Identity.Application.Validation;

internal static class IdentityPasswordRules
{
    internal static IRuleBuilderOptions<T, string> StrongPassword<T>(
        this IRuleBuilder<T, string> ruleBuilder) =>
        ruleBuilder
            .NotEmpty()
            .MinimumLength(12)
            .Matches(@"\p{Lu}")
            .Matches(@"\p{Ll}")
            .Matches(@"\p{Nd}")
            .Matches(@"[^\p{L}\p{N}]");
}
namespace Kynakee.Modules.Billing.Application.Plans;

internal sealed record BillingPlan(string Id, decimal MonthlyPriceEur, int CreditsPerCycle);

internal static class BillingPlanCatalog
{
    private static readonly Dictionary<string, BillingPlan> Plans =
        new Dictionary<string, BillingPlan>(StringComparer.OrdinalIgnoreCase)
        {
            ["starter"] = new("starter", 29m, 500),
            ["pro"] = new("pro", 89m, 2_000),
            ["studio"] = new("studio", 199m, 6_000),
            ["business"] = new("business", 499m, 20_000)
        };

    public static bool TryGet(string? planId, out BillingPlan? plan)
    {
        if (string.IsNullOrWhiteSpace(planId))
        {
            plan = null;
            return false;
        }

        return Plans.TryGetValue(planId.Trim(), out plan);
    }
}
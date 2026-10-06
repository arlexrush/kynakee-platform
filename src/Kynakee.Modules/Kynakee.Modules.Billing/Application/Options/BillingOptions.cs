namespace Kynakee.Modules.Billing.Application.Options;

public sealed class BillingOptions
{
    public TimeSpan ReservationLifetime { get; set; } = TimeSpan.FromMinutes(15);
}
namespace Kynakee.Modules.Projects.Domain.ValueObjects
{
    public sealed record Money
    {
        public decimal Amount { get; }
        public string Currency { get; }

        public static Money Zero => new(0m, "EUR");

        public Money(decimal amount, string currency = "EUR")
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(amount),
                    "Money amount cannot be negative.");
            }

            if (string.IsNullOrWhiteSpace(currency) ||
                currency.Trim().Length != 3)
            {
                throw new ArgumentException(
                    "Currency must be a three-character ISO code.",
                    nameof(currency));
            }

            Amount = amount;
            Currency = currency.Trim().ToUpperInvariant();
        }

        public Money Add(Money other)
        {
            ArgumentNullException.ThrowIfNull(other);

            if (!string.Equals(
                    Currency,
                    other.Currency,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Money values must use the same currency.");
            }

            return new Money(Amount + other.Amount, Currency);
        }

        public Money Multiply(decimal factor)
        {
            if (factor < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(factor),
                    "Multiplication factor cannot be negative.");
            }

            return new Money(Amount * factor, Currency);
        }
    }
}

namespace Kynakee.Modules.Projects.Domain.ValueObjects
{
    public sealed record Confidence
    {
        public decimal Value { get; }

        public static Confidence High => new(0.90m);
        public static Confidence Medium => new(0.70m);
        public static Confidence Low => new(0.50m);

        public bool IsHigh => Value >= 0.80m;
        public bool RequiresHumanReview => Value < 0.60m;

        public Confidence(decimal value)
        {
            if (value is < 0m or > 1m)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    "Confidence must be between 0 and 1.");
            }

            Value = value;
        }
    }
}

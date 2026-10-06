namespace Kynakee.Modules.Projects.Domain.ValueObjects
{
    public sealed record MeasurementUnit
    {
        public string Code { get; }
        public string Symbol { get; }
        public bool IsComposite { get; }

        private MeasurementUnit(string code, string symbol, bool isComposite)
        {
            Code = code;
            Symbol = symbol;
            IsComposite = isComposite;
        }

        public static readonly MeasurementUnit SquareMeter = new("M2", "m²", false);
        public static readonly MeasurementUnit CubicMeter = new("M3", "m³", false);
        public static readonly MeasurementUnit LinearMeter = new("ML", "ml", false);
        public static readonly MeasurementUnit Unit = new("UN", "u", false);
        public static readonly MeasurementUnit Kilogram = new("KG", "kg", false);
        public static readonly MeasurementUnit Ton = new("TN", "t", false);
        public static readonly MeasurementUnit Hour = new("HR", "h", false);
        public static readonly MeasurementUnit Day = new("DY", "d", false);

        // Unidades compuestas
        public static MeasurementUnit Composite(
        string numerator,
        string denominator)
        {
            if (string.IsNullOrWhiteSpace(numerator))
            {
                throw new ArgumentException(
                    "The numerator is required.",
                    nameof(numerator));
            }

            if (string.IsNullOrWhiteSpace(denominator))
            {
                throw new ArgumentException(
                    "The denominator is required.",
                    nameof(denominator));
            }

            var normalizedNumerator = numerator.Trim().ToUpperInvariant();
            var normalizedDenominator = denominator.Trim().ToUpperInvariant();

            return new MeasurementUnit(
                $"{normalizedNumerator}/{normalizedDenominator}",
                $"{normalizedNumerator}/{normalizedDenominator}",
                true);
        }

        public override string ToString() => Symbol;
    }
}

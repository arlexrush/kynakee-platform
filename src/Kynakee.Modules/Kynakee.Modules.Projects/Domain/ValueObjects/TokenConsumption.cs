using System.Collections.ObjectModel;

namespace Kynakee.Modules.Projects.Domain.ValueObjects
{
    public sealed record TokenConsumption
    {
        // Constructor utilizado por EF Core al materializar el objeto poseído.
        private TokenConsumption()
        {
        }

        public int TotalTokens { get; private set; }

        public decimal TotalCredits { get; private set; }

        internal Dictionary<ProjectPhase, int>? ByPhaseStorage { get; private set; }

        public bool HasCompletePhaseBreakdown { get; private set; }

        public IReadOnlyDictionary<ProjectPhase, int> ByPhase =>
            new ReadOnlyDictionary<ProjectPhase, int>(
                new Dictionary<ProjectPhase, int>(
                    ByPhaseStorage ?? new Dictionary<ProjectPhase, int>()));

        public static TokenConsumption Empty =>
            new(0, 0m, new Dictionary<ProjectPhase, int>());

        public TokenConsumption(
            int totalTokens,
            decimal totalCredits,
            IReadOnlyDictionary<ProjectPhase, int>? byPhase = null)
        {
            if (totalTokens < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(totalTokens),
                    "Total tokens cannot be negative.");
            }

            if (totalCredits < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(totalCredits),
                    "Total credits cannot be negative.");
            }

            TotalTokens = totalTokens;
            TotalCredits = totalCredits;
            ByPhaseStorage = new Dictionary<ProjectPhase, int>(
                byPhase ?? new Dictionary<ProjectPhase, int>());
            HasCompletePhaseBreakdown = true;
        }

        public TokenConsumption With(
            ProjectPhase phase,
            int tokens,
            decimal credits)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(tokens, nameof(tokens));
                       

            ArgumentOutOfRangeException.ThrowIfNegative(credits, nameof(credits));

            
            var values = new Dictionary<ProjectPhase, int>(
                ByPhaseStorage ?? new Dictionary<ProjectPhase, int>());

            values[phase] = values.TryGetValue(phase, out var current)
                ? checked(current + tokens)
                : tokens;

            return new TokenConsumption(
                checked(TotalTokens + tokens),
                TotalCredits + credits,
                values)
            {
                HasCompletePhaseBreakdown = HasCompletePhaseBreakdown
            };
        }
    }
}

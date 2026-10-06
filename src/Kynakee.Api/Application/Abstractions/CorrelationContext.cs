namespace Kynakee.Api.Application.Abstractions
{
#pragma warning disable CA1515 // Considere la posibilidad de hacer que los tipos públicos sean internos

    /// <summary>
    /// Contains the correlation context for the current request, including the correlation ID and project ID.
    /// </summary>
    public sealed class CorrelationContext : ICorrelationContext
    {
        private string _correlationId = string.Empty;

        public string CorrelationId => _correlationId;

        public Guid? ProjectId { get; private set; }

        public void SetCorrelationId(string correlationId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

            _correlationId = correlationId;
        }

        public void SetProjectId(Guid? projectId)
        {
            ProjectId = projectId;
        }
    }
#pragma warning restore CA1515 // Considere la posibilidad de hacer que los tipos públicos sean internos
}

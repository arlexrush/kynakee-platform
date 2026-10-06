namespace Kynakee.Api.Application.Abstractions
{
#pragma warning disable CA1515 // Considere la posibilidad de hacer que los tipos públicos sean internos
    public interface ICorrelationContext
    {
        string CorrelationId { get; }

        Guid? ProjectId { get; }

        void SetCorrelationId(string correlationId);

        void SetProjectId(Guid? projectId);
    }
#pragma warning restore CA1515 // Considere la posibilidad de hacer que los tipos públicos sean internos
}

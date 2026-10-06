using MediatR;

namespace Kynakee.Modules.SharedKernel.Application;

#pragma warning disable CA1040 // Evitar interfaces vacías
/// <summary>
/// Represents a query that returns a typed application result without modifying state.
/// </summary>
/// <typeparam name="TResponse">The successful response type.</typeparam>
public interface IQuery<TResponse> : IRequest<Result<TResponse>>
{
}
#pragma warning restore CA1040 // Evitar interfaces vacías
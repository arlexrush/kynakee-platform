using MediatR;

namespace Kynakee.Modules.SharedKernel.Application;

#pragma warning disable CA1040

/// <summary>
/// Represents a command that returns a typed application result.
/// </summary>
/// <typeparam name="TResponse">The successful response type.</typeparam>
public interface ICommand<TResponse> : IRequest<Result<TResponse>>
{
}

/// <summary>
/// Represents a command that returns only an application result status.
/// </summary>
public interface ICommand : IRequest<Result>
{
}

#pragma warning restore CA1040

using Kynakee.Modules.SharedKernel.Contracts;

namespace Kynakee.Api.Application.Abstractions
{
    /// <summary>
    /// Contexto de tenant modificable que permite establecer el identificador del tenant, el identificador del usuario
    /// y el estado de autenticación para la operación en curso.
    /// </summary>
    /// <remarks>Las implementaciones deben ser seguras para entornos concurrentes y, habitualmente,
    /// registradas con alcance por solicitud (scoped). Se utiliza para inicializar el contexto antes de ejecutar lógica
    /// que dependa del tenant o del usuario.</remarks>
#pragma warning disable CA1515 // Considere la posibilidad de hacer que los tipos públicos sean internos
    public interface ITenantContextWriter : ITenantContext
    {
        void Initialize(
        Guid tenantId,
        Guid userId,
        bool isAuthenticated);
    }
#pragma warning restore CA1515 // Considere la posibilidad de hacer que los tipos públicos sean internos
}

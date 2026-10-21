using Kynakee.Modules.Identity.Application.Contracts;
using Kynakee.Modules.Identity.Domain.Aggregates;
using Kynakee.Modules.Identity.Domain.Entities;

namespace Kynakee.Modules.Identity.Application.Abstractions;

/// <summary>
/// Abstracción de repositorio para operaciones de identidad y multiarrendatario. Expone métodos asíncronos para
/// consultar y administrar tenants, usuarios, membresías, tokens de actualización e invitaciones, y para adjuntar
/// nuevas entidades al contexto de persistencia.
/// </summary>
/// <remarks>Las implementaciones deben respetar el aislamiento por tenant y aceptar CancellationToken en las
/// operaciones asíncronas. Los métodos Get/Find no deben aplicar lógica de negocio; los métodos Add deben adjuntar
/// entidades al contexto sin persistir cambios inmediatamente. Evitar exponer detalles de persistencia (por ejemplo
/// DbContext) y aplicar las políticas de soft-delete, auditoría y control de concurrencia según la infraestructura de
/// la aplicación.</remarks>
public interface IIdentityRepository
{
    /// <summary>
    /// Determina si existe un tenant con el slug especificado. Slug es un identificador único legible por humanos para un tenant. Este método permite verificar la existencia de un tenant antes de crear uno nuevo o realizar operaciones dependientes del slug.
    /// </summary>
    /// <param name="slug">Slug único del tenant a comprobar.</param>
    /// <param name="cancellationToken">Token para cancelar la operación asíncrona.</param>
    /// <returns>true si existe un tenant con el slug especificado; en caso contrario, false.</returns>
    Task<bool> TenantSlugExistsAsync(string slug, CancellationToken cancellationToken);

    /// <summary>
    /// Comprueba si el slug proporcionado está en uso por un tenant distinto del especificado.
    /// </summary>
    /// <param name="slug">Slug del tenant a verificar.</param>
    /// <param name="tenantId">Identificador del tenant que se debe excluir de la comprobación.</param>
    /// <param name="cancellationToken">Token de cancelación para abortar la operación asíncrona.</param>
    /// <returns>True si existe otro tenant con el mismo slug; en caso contrario, False.</returns>
    Task<bool> TenantSlugExistsForOtherTenantAsync(
        string slug,
        Guid tenantId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Obtiene un Tenant por su identificador.
    /// </summary>
    /// <param name="tenantId">Identificador único del tenant a recuperar.</param>
    /// <param name="cancellationToken">Token para cancelar la operación asincrónica.</param>
    /// <returns>Tarea que contiene el Tenant encontrado o null si no existe.</returns>
    Task<Tenant?> GetTenantAsync(Guid tenantId, CancellationToken cancellationToken);

    /// <summary>
    /// Obtiene el tenant especificado para fines de autenticación.
    /// </summary>
    /// <param name="tenantId">Identificador único del tenant.</param>
    /// <param name="cancellationToken">Token de cancelación para la operación asíncrona.</param>
    /// <returns>Una tarea que contiene el Tenant si se encuentra; en caso contrario, null.</returns>
    Task<Tenant?> GetTenantForAuthenticationAsync(
        Guid tenantId,
        CancellationToken cancellationToken);

    Task<User?> GetUserAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Obtiene el usuario destinado a procesos de autenticación dentro del contexto del inquilino especificado.
    /// </summary>
    /// <remarks>La implementación debe aplicar aislamiento por tenant y respetar políticas de privacidad y
    /// estado del usuario (p. ej. borrado lógico).</remarks>
    /// <param name="tenantId">Identificador único del inquilino (tenant) al que pertenece el usuario.</param>
    /// <param name="userId">Identificador único del usuario que se desea recuperar.</param>
    /// <param name="cancellationToken">Token que permite cancelar la operación asincrónica.</param>
    /// <returns>Una tarea que completa con el usuario encontrado, o null si no existe ninguno que coincida.</returns>
    Task<User?> GetUserForAuthenticationAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Obtiene el usuario identificado por userId para los procesos de autenticación.
    /// </summary>
    /// <param name="userId">Identificador único del usuario a buscar.</param>
    /// <param name="cancellationToken">Token para cancelar la operación asíncrona.</param>
    /// <returns>Un usuario si existe y es apto para autenticación; en caso contrario, null.</returns>
    Task<User?> FindUserForAuthenticationAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Busca de forma asíncrona un usuario por su correo electrónico normalizado.
    /// </summary>
    /// <remarks>La comparación debe usar la forma normalizada del correo proporcionada; no realiza
    /// normalización adicional.</remarks>
    /// <param name="normalizedEmail">Correo electrónico ya normalizado que se usará para la búsqueda.</param>
    /// <param name="cancellationToken">Token para cancelar la operación asíncrona.</param>
    /// <returns>Tarea que contiene el usuario encontrado, o null si no se encuentra.</returns>
    Task<User?> FindUserByNormalizedEmailAsync(
        string normalizedEmail,
        CancellationToken cancellationToken);

    /// <summary>
    /// Busca un usuario por su correo electrónico normalizado en todos los tenants.
    /// </summary>
    /// <remarks>La búsqueda es global entre tenants y respeta la cancelación; puede implicar consultas en
    /// múltiples contextos.</remarks>
    /// <param name="normalizedEmail">Correo electrónico normalizado del usuario a buscar (por ejemplo, ToUpperInvariant).</param>
    /// <param name="cancellationToken">Token para cancelar la operación asincrónica.</param>
    /// <returns>El usuario encontrado, o null si no existe ningún usuario con ese correo en ningún tenant.</returns>
    Task<User?> FindUserByEmailAcrossTenantsAsync(
        string normalizedEmail,
        CancellationToken cancellationToken);

    /// <summary>
    /// Recupera de forma asíncrona el TenantUser asociado al tenant y al usuario especificados.
    /// </summary>
    /// <remarks>Operación de solo lectura; no modifica el estado del sistema.</remarks>
    /// <param name="tenantId">Identificador único del tenant al que pertenece el usuario.</param>
    /// <param name="userId">Identificador único del usuario dentro del tenant.</param>
    /// <param name="cancellationToken">Token que permite cancelar la operación asíncrona.</param>
    /// <returns>Tarea que devuelve el TenantUser correspondiente, o null si no existe.</returns>
    Task<TenantUser?> GetTenantUserAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Obtiene el usuario del tenant utilizado para el proceso de autenticación.
    /// </summary>
    /// <remarks>La operación respeta el token de cancelación y puede devolver null cuando el usuario no
    /// existe o no reúne las condiciones de autenticación.</remarks>
    /// <param name="tenantId">Identificador del tenant al que pertenece el usuario.</param>
    /// <param name="userId">Identificador único del usuario dentro del tenant.</param>
    /// <param name="cancellationToken">Token para cancelar la operación asíncrona.</param>
    /// <returns>Una instancia de TenantUser si se encuentra y es válida para autenticación; en caso contrario, null.</returns>
    Task<TenantUser?> GetTenantUserForAuthenticationAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Obtiene un RefreshToken por su hash.
    /// </summary>
    /// <param name="tokenHash">Hash del token de actualización a buscar.</param>
    /// <param name="cancellationToken">Token de cancelación que permite abortar la operación asincrónica.</param>
    /// <returns>Tarea que devuelve el RefreshToken correspondiente, o null si no existe.</returns>
    Task<RefreshToken?> FindRefreshTokenByHashAsync(
        string tokenHash,
        CancellationToken cancellationToken);

    /// <summary>Resuelve una clave de bloqueo inmutable de cuenta sin bloquear una fila de refresh antes del bloqueo de cuenta.</summary>
    Task<string?> FindRefreshTokenEmailAsync(string tokenHash, CancellationToken cancellationToken);

    /// <summary>
    /// Obtiene de forma asíncrona la invitación de usuario cuyo hash de token coincide con el valor proporcionado.
    /// </summary>
    /// <param name="tokenHash">Hash del token de la invitación a buscar.</param>
    /// <param name="cancellationToken">Token de cancelación para la operación asincrónica.</param>
    /// <returns>Tarea que contiene la invitación encontrada, o null si no se encuentra ninguna.</returns>
    Task<UserInvitation?> FindInvitationByHashAsync(
        string tokenHash,
        CancellationToken cancellationToken);

    /// <summary>
    /// Obtiene de forma asíncrona el perfil del inquilino especificado por su identificador o null si no existe.
    /// </summary>
    /// <remarks>Realiza una consulta al almacén de identidad y respeta el token de cancelación.</remarks>
    /// <param name="tenantId">Identificador único del inquilino.</param>
    /// <param name="cancellationToken">Token de cancelación para abortar la operación asíncrona.</param>
    /// <returns>Una tarea que contiene el IdentityTenantProfile si se encuentra; en caso contrario, null.</returns>
    Task<IdentityTenantProfile?> GetTenantProfileAsync(
        Guid tenantId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Obtiene de forma asíncrona el perfil del usuario indicado dentro del inquilino especificado.
    /// </summary>
    /// <param name="tenantId">Identificador del inquilino (tenant) al que pertenece el usuario.</param>
    /// <param name="userId">Identificador del usuario cuyo perfil se recupera.</param>
    /// <param name="cancellationToken">Token para cancelar la operación asíncrona.</param>
    /// <returns>Una instancia de IdentityUserProfile con los datos del usuario, o null si no existe.</returns>
    Task<IdentityUserProfile?> GetUserProfileAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Obtiene los usuarios asociados al inquilino especificado.
    /// </summary>
    /// <param name="tenantId">Identificador del inquilino cuyos usuarios se deben recuperar.</param>
    /// <param name="cancellationToken">Token de cancelación para la operación asincrónica.</param>
    /// <returns>Una lista de solo lectura de TenantUserListItem que representa los usuarios del inquilino.</returns>
    Task<IReadOnlyList<TenantUserListItem>> GetTenantUsersAsync(
        Guid tenantId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Obtiene los tokens de actualización activos para el usuario especificado dentro del inquilino dado.
    /// </summary>
    /// <param name="tenantId">Identificador del inquilino al que pertenece el usuario.</param>
    /// <param name="userId">Identificador del usuario cuyos tokens de actualización se deben recuperar.</param>
    /// <param name="cancellationToken">Token de cancelación para la operación asincrónica.</param>
    /// <returns>Una lista de solo lectura de RefreshToken que representa los tokens de actualización activos del usuario.</returns>
    Task<IReadOnlyList<RefreshToken>> GetActiveRefreshTokensAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Serializa las mutaciones de propietarios del tenant actual hasta finalizar la transacción.
    /// </summary>
    /// <remarks>Requiere una transacción de módulo con aislamiento ReadCommitted. Debe invocarse antes
    /// de leer permisos, membresías o el recuento de propietarios que condicionan la mutación.</remarks>
    Task AcquireTenantOwnerLockAsync(Guid tenantId, CancellationToken cancellationToken);

    /// <summary>
    /// Cuenta el número de propietarios activos dentro del inquilino especificado.
    /// </summary>
    /// <param name="tenantId">Identificador del inquilino cuyos propietarios activos se deben contar.</param>
    /// <param name="cancellationToken">Token de cancelación para la operación asincrónica.</param>
    /// <returns>El número de propietarios activos dentro del inquilino.</returns>
    Task<int> CountActiveOwnersAsync(Guid tenantId, CancellationToken cancellationToken);

    /// <summary>
    /// Obtiene las invitaciones pendientes para el usuario especificado dentro del inquilino dado.
    /// </summary>
    /// <param name="tenantId">Identificador del inquilino al que pertenece el usuario.</param>
    /// <param name="userId">Identificador del usuario cuyas invitaciones pendientes se deben recuperar.</param>
    /// <param name="cancellationToken">Token de cancelación para la operación asincrónica.</param>
    /// <returns>Una lista de solo lectura de UserInvitation que representa las invitaciones pendientes del usuario.</returns>
    Task<IReadOnlyList<UserInvitation>> GetPendingInvitationsAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Añade un tenant al almacén de datos aplicando las validaciones y metadatos de auditoría requeridos.
    /// </summary>
    /// <remarks>La implementación debe validar nulidad y unicidad, establecer campos de auditoría y persistir
    /// mediante el contexto de datos siguiendo la política de borrado suave del dominio.</remarks>
    /// <param name="tenant">Instancia de Tenant que representa al inquilino a agregar, incluyendo identificadores y metadatos.</param>
    void AddTenant(Tenant tenant);

    /// <summary>
    /// Añade el usuario especificado al repositorio.
    /// </summary>
    /// <remarks>No garantiza persistencia inmediata; puede requerir una operación adicional para guardar los
    /// cambios y debe respetar las invariantes y restricciones del dominio.</remarks>
    /// <param name="user">Usuario a añadir; no debe ser null y debe cumplir las reglas de validación del dominio.</param>
    void AddUser(User user);

    /// <summary>
    /// Añade una entidad TenantUser que representa la pertenencia de un usuario al inquilino.
    /// </summary>
    /// <remarks>La implementación debe validar invariantes del dominio y garantizar la persistencia adecuada
    /// (por ejemplo, dentro de una transacción) y el cumplimiento de las políticas de soft‑delete y control de
    /// tenencia.</remarks>
    /// <param name="membership">Entidad TenantUser que contiene los datos de la relación entre el usuario y el inquilino.</param>
    void AddTenantUser(TenantUser membership);

    /// <summary>
    /// Agrega un RefreshToken al almacén de tokens de actualización.
    /// </summary>
    /// <param name="token">Token de actualización a agregar.</param>
    void AddRefreshToken(RefreshToken token);

    /// <summary>
    /// Añade una invitación de usuario al almacén gestionado por la implementación.
    /// </summary>
    /// <param name="invitation">Invitación de usuario que se añadirá.</param>
    void AddInvitation(UserInvitation invitation);
}

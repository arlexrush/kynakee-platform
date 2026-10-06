using Kynakee.Modules.Mcp.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;
using Kynakee.Modules.SharedKernel.Domain;

namespace Kynakee.Modules.Mcp.Domain.Entities;

public sealed class McpServer : BaseEntity<McpServerId>
{
    private McpServer()
    {
    }

    private McpServer(
        McpServerId id,
        McpProviderId providerId,
        Guid tenantId,
        Uri endpoint,
        string credentialSecretReference,
        Guid? createdBy)
        : base(id, tenantId, createdBy)
    {
        ProviderId = providerId;
        Endpoint = endpoint;
        CredentialSecretReference = credentialSecretReference;
    }

    public McpProviderId ProviderId { get; private set; }

    public Uri Endpoint { get; private set; } = null!;

    public string CredentialSecretReference { get; private set; } = string.Empty;

    internal static Result<McpServer> Create(
        McpProviderId providerId,
        Guid tenantId,
        Uri? endpoint,
        string? credentialSecretReference,
        Guid? createdBy)
    {
        if (providerId.Value == Guid.Empty || tenantId == Guid.Empty)
        {
            return Failure("MCP_SERVER_OWNER_REQUIRED", "Server provider and tenant are required.");
        }

        if (endpoint is null || !endpoint.IsAbsoluteUri ||
            endpoint.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(endpoint.UserInfo) ||
            !string.IsNullOrEmpty(endpoint.Fragment))
        {
            return Failure("MCP_SERVER_ENDPOINT_INVALID", "Server endpoint must be an absolute HTTPS URI without user information or a fragment.");
        }

        if (string.IsNullOrWhiteSpace(credentialSecretReference) ||
            credentialSecretReference.Trim().Length > 200)
        {
            return Failure("MCP_SERVER_CREDENTIAL_REFERENCE_INVALID", "A valid credential secret reference is required.");
        }

        if (createdBy == Guid.Empty)
        {
            return Failure("MCP_SERVER_CREATOR_INVALID", "Server creator is invalid.");
        }

        return ResultFactory.Success(new McpServer(
            McpServerId.New(),
            providerId,
            tenantId,
            endpoint,
            credentialSecretReference.Trim(),
            createdBy));
    }

    private static Result<McpServer> Failure(string code, string message) =>
        ResultFactory.Failure<McpServer>(ApplicationError.Validation(code, message));
}

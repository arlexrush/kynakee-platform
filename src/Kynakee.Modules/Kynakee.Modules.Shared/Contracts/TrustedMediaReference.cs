namespace Kynakee.Modules.SharedKernel.Contracts;

/// <summary>Identifies media that must be resolved and authorized by its owning module.</summary>
public sealed record TrustedMediaReference(
    Guid TenantId,
    Guid ProjectId,
    Uri StorageUri,
    string DeclaredMimeType);

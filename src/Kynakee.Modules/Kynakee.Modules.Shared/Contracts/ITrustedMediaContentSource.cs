namespace Kynakee.Modules.SharedKernel.Contracts;

/// <summary>Resolves tenant-owned media without exposing arbitrary network fetching to consumers.</summary>
public interface ITrustedMediaContentSource
{
    /// <summary>Opens a media object only after validating its tenant, project and storage identity.</summary>
    Task<TrustedMediaContent?> OpenReadAsync(
        TrustedMediaReference reference,
        CancellationToken cancellationToken);
}

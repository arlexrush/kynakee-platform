namespace Kynakee.Modules.Projects.Infrastructure.Storage;

public sealed class ObjectStorageOptions
{
    public const string SectionName = "ObjectStorage";

    public bool Enabled { get; set; }

    public string Endpoint { get; set; } = string.Empty;

    public string AccessKey { get; set; } = string.Empty;

    public string SecretKey { get; set; } = string.Empty;

    public string Bucket { get; set; } = string.Empty;

    public bool UseSsl { get; set; }

    public string Region { get; set; } = "us-east-1";

    public long MaxObjectBytes { get; set; } = 104_857_600;
}

using Kynakee.Modules.Projects.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;
using Kynakee.Modules.SharedKernel.Domain;

namespace Kynakee.Modules.Projects.Domain.Entities.DataCapture
{
    public sealed class CaptureExpedient : BaseEntity<CaptureExpedientId>
    {
        private readonly List<CaptureMediaFile> _mediaFiles = [];
        private readonly List<CaptureMeasurement> _measurements = [];
        private readonly List<CaptureTranscription> _transcriptions = [];
        private readonly List<CaptureObservation> _observations = [];

        private CaptureExpedient()
        {
        }

        private CaptureExpedient(
            CaptureExpedientId id,
            Guid tenantId,
            ProjectId projectId,
            IEnumerable<CaptureMediaFile> mediaFiles,
            IEnumerable<CaptureMeasurement> measurements,
            IEnumerable<CaptureTranscription> transcriptions,
            IEnumerable<CaptureObservation> observations,
            Guid? createdBy)
            : base(id, tenantId, createdBy)
        {
            ProjectId = projectId;

            _mediaFiles.AddRange(mediaFiles);
            _measurements.AddRange(measurements);
            _transcriptions.AddRange(transcriptions);
            _observations.AddRange(observations);
        }

        public ProjectId ProjectId { get; private set; }

        public IReadOnlyList<CaptureMediaFile> MediaFiles =>
            _mediaFiles.AsReadOnly();

        public IReadOnlyList<CaptureMeasurement> Measurements =>
            _measurements.AsReadOnly();

        public IReadOnlyList<CaptureTranscription> Transcriptions =>
            _transcriptions.AsReadOnly();

        public IReadOnlyList<CaptureObservation> Observations =>
            _observations.AsReadOnly();

        public static Result<CaptureExpedient> Create(
            Guid tenantId,
            ProjectId projectId,
            IEnumerable<CaptureMediaFile>? mediaFiles = null,
            IEnumerable<CaptureMeasurement>? measurements = null,
            IEnumerable<CaptureTranscription>? transcriptions = null,
            IEnumerable<CaptureObservation>? observations = null,
            Guid? createdBy = null)
        {
            if (tenantId == Guid.Empty)
            {
                return ResultFactory.Failure<CaptureExpedient>(
                    ApplicationError.Validation(
                        "PROJ_CAPTURE_TENANT_REQUIRED",
                        "The capture tenant is required."));
            }

            if (projectId.Value == Guid.Empty)
            {
                return ResultFactory.Failure<CaptureExpedient>(
                    ApplicationError.Validation(
                        "PROJ_CAPTURE_PROJECT_REQUIRED",
                        "The capture project identifier is required."));
            }

            var resolvedMediaFiles = mediaFiles?.ToArray() ?? [];
            if (resolvedMediaFiles.Any(mediaFile =>
                    !IsValidStorageUri(mediaFile.Url) ||
                    !IsValidMediaType(mediaFile.Type)))
            {
                return ResultFactory.Failure<CaptureExpedient>(
                    ApplicationError.Validation(
                        "PROJ_CAPTURE_MEDIA_REFERENCE_INVALID",
                        "Capture media must reference a MinIO object and declare its media type."));
            }

            var resolvedMeasurements = measurements?.ToArray() ?? [];
            var resolvedTranscriptions = transcriptions?.ToArray() ?? [];
            var resolvedObservations = observations?.ToArray() ?? [];

            return ResultFactory.Success(
                new CaptureExpedient(
                    CaptureExpedientId.New(),
                    tenantId,
                    projectId,
                    resolvedMediaFiles,
                    resolvedMeasurements,
                    resolvedTranscriptions,
                    resolvedObservations,
                    createdBy));
        }

        private static bool IsValidStorageUri(Uri? uri)
        {
            if (uri is not { IsAbsoluteUri: true } ||
                !string.Equals(uri.Scheme, "minio", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(uri.Host) ||
                string.IsNullOrEmpty(uri.AbsolutePath.Trim('/')) ||
                !string.IsNullOrEmpty(uri.UserInfo) ||
                !string.IsNullOrEmpty(uri.Query) ||
                !string.IsNullOrEmpty(uri.Fragment))
            {
                return false;
            }

            var originalPathStart = uri.OriginalString.IndexOf('/', "minio://".Length);
            if (originalPathStart < 0)
            {
                return false;
            }

            var originalPath = uri.OriginalString[originalPathStart..];
            return originalPath.Split('/').Skip(1).All(segment =>
            {
                var decodedSegment = Uri.UnescapeDataString(segment);
                return decodedSegment.Length > 0 &&
                       decodedSegment is not "." and not ".." &&
                       !decodedSegment.Contains('/', StringComparison.Ordinal);
            });
        }

        private static bool IsValidMediaType(string? mediaType) =>
            !string.IsNullOrWhiteSpace(mediaType) &&
            mediaType.Length <= 100;
    }
}

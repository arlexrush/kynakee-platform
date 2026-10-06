using Kynakee.Modules.Projects.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;
using Kynakee.Modules.SharedKernel.Domain;

namespace Kynakee.Modules.Projects.Domain.Entities.OfferProject
{
    public sealed class Offer : BaseEntity<OfferId>
    {
        private Offer()
        {
        }

        private Offer(
            OfferId id,
            Guid tenantId,
            ProjectId projectId,
            int offerNumber,
            Money totalAmount,
            string? conditions,
            string? warranties,
            int validityDays,
            Uri? pdfUrl,
            string? aiDisclaimer,
            Guid? createdBy)
            : base(id, tenantId, createdBy)
        {
            ProjectId = projectId;
            OfferNumber = offerNumber;
            TotalAmount = totalAmount;
            Conditions = conditions;
            Warranties = warranties;
            ValidityDays = validityDays;
            PdfUrl = pdfUrl;
            AIActDisclaimer = aiDisclaimer;
            Status = OfferStatus.Draft;
        }

        public ProjectId ProjectId { get; private set; }

        public int OfferNumber { get; private set; }  

        public Money TotalAmount { get; private set; } = default!;

        public string? Conditions { get; private set; }

        public string? Warranties { get; private set; }

        public int ValidityDays { get; private set; }

        public Uri? PdfUrl { get; private set; }

        public DateTime? SentAt { get; private set; }

        public string? AIActDisclaimer { get; private set; }

        public OfferStatus Status { get; private set; }

        public static Result<Offer> Create(
            Guid tenantId,
            ProjectId projectId,
            Money totalAmount,
            string? conditions = null,
            string? warranties = null,
            int validityDays = 30,
            Uri? pdfUrl = null,
            string? aiDisclaimer = null,
            int offerNumber = 1,
            Guid? createdBy = null)
        {
            if (tenantId == Guid.Empty)
            {
                return ResultFactory.Failure<Offer>(
                    ApplicationError.Validation(
                        "PROJ_OFFER_TENANT_REQUIRED",
                        "The offer tenant is required."));
            }

            if (projectId.Value == Guid.Empty)
            {
                return ResultFactory.Failure<Offer>(
                    ApplicationError.Validation(
                        "PROJ_OFFER_PROJECT_REQUIRED",
                        "The offer project identifier is required."));
            }

            ArgumentNullException.ThrowIfNull(totalAmount);

            if (offerNumber <= 0)
            {
                return ResultFactory.Failure<Offer>(
                    ApplicationError.Validation(
                        "PROJ_OFFER_NUMBER_INVALID",
                        "The offer number must be greater than zero."));
            }

            if (validityDays <= 0)
            {
                return ResultFactory.Failure<Offer>(
                    ApplicationError.Validation(
                        "PROJ_OFFER_VALIDITY_INVALID",
                        "The offer validity must be greater than zero days."));
            }

            return ResultFactory.Success(
                new Offer(
                    OfferId.New(),
                    tenantId,
                    projectId,
                    offerNumber,
                    totalAmount,
                    Normalize(conditions),
                    Normalize(warranties),
                    validityDays,
                    pdfUrl,
                    Normalize(aiDisclaimer),
                    createdBy));
        }

        internal Result MarkAsSent(
            Uri? pdfUrl = null,
            Guid? updatedBy = null)
        {
            if (Status != OfferStatus.Draft)
            {
                return ResultFactory.Failure(
                    ApplicationError.Conflict(
                        "PROJ_OFFER_STATUS_INVALID_FOR_SENDING",
                        "Only a draft offer can be sent."));
            }
                        
            PdfUrl = pdfUrl;
            Status = OfferStatus.Sent;
            SentAt = DateTime.UtcNow;

            RegisterUpdate(updatedBy);

            return ResultFactory.Ok();
        }

        internal Result Accept(Guid? updatedBy = null)
        {
            if (Status != OfferStatus.Sent)
            {
                return ResultFactory.Failure(
                    ApplicationError.Conflict(
                        "PROJ_OFFER_STATUS_INVALID_FOR_ACCEPTANCE",
                        "Only a sent offer can be accepted."));
            }

            Status = OfferStatus.Accepted;
            RegisterUpdate(updatedBy);

            return ResultFactory.Ok();
        }

        internal Result Reject(Guid? updatedBy = null)
        {
            if (Status != OfferStatus.Sent)
            {
                return ResultFactory.Failure(
                    ApplicationError.Conflict(
                        "PROJ_OFFER_STATUS_INVALID_FOR_REJECTION",
                        "Only a sent offer can be rejected."));
            }

            Status = OfferStatus.Rejected;
            RegisterUpdate(updatedBy);

            return ResultFactory.Ok();
        }

        internal Result Expire(Guid? updatedBy = null)
        {
            if (Status != OfferStatus.Sent)
            {
                return ResultFactory.Failure(
                    ApplicationError.Conflict(
                        "PROJ_OFFER_STATUS_INVALID_FOR_EXPIRATION",
                        "Only a sent offer can expire."));
            }

            if (SentAt.HasValue &&
                SentAt.Value.AddDays(ValidityDays) > DateTime.UtcNow)
            {
                return ResultFactory.Failure(
                    ApplicationError.Conflict(
                        "PROJ_OFFER_NOT_EXPIRED",
                        "The offer validity period has not elapsed."));
            }

            Status = OfferStatus.Expired;
            RegisterUpdate(updatedBy);

            return ResultFactory.Ok();
        }

        private static string? Normalize(string? value) =>
            string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
    }
}

using Kynakee.Modules.Projects.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;

namespace Kynakee.Modules.Projects.Domain.Entities.Scoped.ConcreteComponent
{
    public sealed class AuxiliaryMeansComponent : APUComponent
    {
        private AuxiliaryMeansComponent()
        {
        }

        private AuxiliaryMeansComponent(
            APUComponentId id,
            Guid tenantId,
            APUAssignmentId apuAssignmentId,
            Guid sourceComponentId,
            string description,
            MeasurementUnit unit,            
            decimal applicationPercentage,
            string? fallbackIndicator,
            Guid? createdBy)
            : base(
                id,
                tenantId,
                apuAssignmentId,
                sourceComponentId,
                description,
                unit,                
                fallbackIndicator,
                createdBy)
        {
            ApplicationPercentage = applicationPercentage;
        }

        public override APUComponentType Type =>
            APUComponentType.AuxiliaryMeans;

        public decimal ApplicationPercentage { get; private set; }

        public static Result<AuxiliaryMeansComponent> Create(
            Guid tenantId,
            APUAssignmentId apuAssignmentId,
            Guid sourceComponentId,
            string description,
            MeasurementUnit unit,
            decimal applicationPercentage,
            string? fallbackIndicator = null,
            Guid? createdBy = null)
        {
            if (!IsValidIdentity(
                    tenantId,
                    apuAssignmentId,
                    sourceComponentId))
            {
                return ResultFactory.Failure<AuxiliaryMeansComponent>(
                    ApplicationError.Validation(
                        "PROJ_APU_AUXILIARY_IDENTITY_INVALID",
                        "The auxiliary means identity is invalid."));
            }

            if (!IsValidDescription(description))
            {
                return ResultFactory.Failure<AuxiliaryMeansComponent>(
                    ApplicationError.Validation(
                        "PROJ_APU_AUXILIARY_DESCRIPTION_REQUIRED",
                        "The auxiliary means description is required."));
            }

            if (!IsValidUnit(unit))
            {
                return ResultFactory.Failure<AuxiliaryMeansComponent>(
                    ApplicationError.Validation(
                        "PROJ_APU_AUXILIARY_UNIT_REQUIRED",
                        "The auxiliary means unit is required."));
            }

            if (applicationPercentage < 0)
            {
                return ResultFactory.Failure<AuxiliaryMeansComponent>(
                    ApplicationError.Validation(
                        "PROJ_APU_AUXILIARY_PERCENTAGE_INVALID",
                        "The application percentage cannot be negative."));
            }

            return ResultFactory.Success(
                new AuxiliaryMeansComponent(
                    APUComponentId.New(),
                    tenantId,
                    apuAssignmentId,
                    sourceComponentId,
                    description.Trim(),
                    unit!,                    
                    applicationPercentage,
                    Normalize(fallbackIndicator),
                    createdBy));
        }
    }
}

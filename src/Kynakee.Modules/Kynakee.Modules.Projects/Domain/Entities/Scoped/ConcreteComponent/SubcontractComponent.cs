using Kynakee.Modules.Projects.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;

namespace Kynakee.Modules.Projects.Domain.Entities.Scoped.ConcreteComponent
{
    public sealed class SubcontractComponent : APUComponent
    {
        private SubcontractComponent()
        {
        }

        private SubcontractComponent(
            APUComponentId id,
            Guid tenantId,
            APUAssignmentId apuAssignmentId,
            Guid sourceComponentId,
            string description,
            MeasurementUnit unit,            
            string? fallbackIndicator,
            string contractConditions,
            decimal quantityPerApuUnit,
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
            ContractConditions = contractConditions;
            QuantityPerApuUnit = quantityPerApuUnit;
        }

        public override APUComponentType Type => APUComponentType.Subcontract;        

        public string ContractConditions { get; private set; }=string.Empty;

        public decimal QuantityPerApuUnit { get; private set; }

        public static Result<SubcontractComponent> Create(
            Guid tenantId,
            APUAssignmentId apuAssignmentId,
            Guid sourceComponentId,
            string description,
            MeasurementUnit unit,            
            string contractConditions,
            decimal quantityPerApuUnit,
            string? fallbackIndicator = null,
            Guid? createdBy = null)
        {
            if (!IsValidIdentity(
                    tenantId,
                    apuAssignmentId,
                    sourceComponentId))
            {
                return ResultFactory.Failure<SubcontractComponent>(
                    ApplicationError.Validation(
                        "PROJ_APU_SUBCONTRACT_IDENTITY_INVALID",
                        "The subcontract identity is invalid."));
            }
            if (!IsValidDescription(description))
            {
                return ResultFactory.Failure<SubcontractComponent>(
                    ApplicationError.Validation(
                        "PROJ_APU_SUBCONTRACT_DESCRIPTION_INVALID",
                        "The subcontract description is invalid."));
            }
            
            if (!IsValidUnit(unit))
            {
                return ResultFactory.Failure<SubcontractComponent>(
                    ApplicationError.Validation(
                        "PROJ_APU_SUBCONTRACT_UNIT_INVALID",
                        "The subcontract unit is invalid."));
            }
            if (string.IsNullOrWhiteSpace(contractConditions))
            {
                return ResultFactory.Failure<SubcontractComponent>(
                    ApplicationError.Validation(
                        "PROJ_APU_SUBCONTRACT_CONDITIONS_REQUIRED",
                        "Contract conditions are required for a subcontract component."));
            }
            Normalize(description);
            Normalize(contractConditions);
            Normalize(fallbackIndicator);

            var id = APUComponentId.New();
            var component = new SubcontractComponent(
                id,
                tenantId,
                apuAssignmentId,
                sourceComponentId,
                description.Trim(),
                unit,                
                fallbackIndicator,
                contractConditions,
                quantityPerApuUnit,
                createdBy);
            return ResultFactory.Success(component);
        }
    }
}


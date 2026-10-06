using Kynakee.Modules.Projects.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;

namespace Kynakee.Modules.Projects.Domain.Entities.Scoped.ConcreteComponent
{
    public sealed class MaterialComponent : APUComponent
    {
        private MaterialComponent()
        {
        }

        private MaterialComponent(
            APUComponentId id,
            Guid tenantId,
            APUAssignmentId apuAssignmentId,
            Guid sourceComponentId,
            string description,
            MeasurementUnit unit,            
            decimal wastePercentage,
            bool transportIncluded,
            decimal quantityPerApuUnit,
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
            WastePercentage = wastePercentage;
            TransportIncluded = transportIncluded;
            QuantityPerApuUnit = quantityPerApuUnit;
        }

        public override APUComponentType Type =>
            APUComponentType.Material;

        public decimal WastePercentage { get; private set; }

        public bool TransportIncluded { get; private set; }

        public decimal QuantityPerApuUnit { get; private set; }

        public static Result<MaterialComponent> Create(
            Guid tenantId,
            APUAssignmentId apuAssignmentId,
            Guid sourceComponentId,
            string description,
            MeasurementUnit unit,
            decimal wastePercentage = 0m,
            bool transportIncluded = false,
            decimal quantityPerApuUnit = 0m,
            string? fallbackIndicator = null,
            Guid? createdBy = null)
        {
            if (!IsValidIdentity(
                    tenantId,
                    apuAssignmentId,
                    sourceComponentId))
            {
                return ResultFactory.Failure<MaterialComponent>(
                    ApplicationError.Validation(
                        "PROJ_APU_MATERIAL_IDENTITY_INVALID",
                        "The material component identity is invalid."));
            }

            if (!IsValidDescription(description))
            {
                return ResultFactory.Failure<MaterialComponent>(
                    ApplicationError.Validation(
                        "PROJ_APU_MATERIAL_DESCRIPTION_REQUIRED",
                        "The material description is required."));
            }

            if (!IsValidUnit(unit))
            {
                return ResultFactory.Failure<MaterialComponent>(
                    ApplicationError.Validation(
                        "PROJ_APU_MATERIAL_UNIT_REQUIRED",
                        "The material unit is required."));
            }
                        
            if (wastePercentage < 0)
            {
                return ResultFactory.Failure<MaterialComponent>(
                    ApplicationError.Validation(
                        "PROJ_APU_MATERIAL_WASTE_INVALID",
                        "The material waste percentage cannot be negative."));
            }

            return ResultFactory.Success(
                new MaterialComponent(
                    APUComponentId.New(),
                    tenantId,
                    apuAssignmentId,
                    sourceComponentId,
                    description.Trim(),
                    unit!,
                    wastePercentage,
                    transportIncluded,
                    quantityPerApuUnit,
                    Normalize(fallbackIndicator),
                    createdBy));
        }
    }
}

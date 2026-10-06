using Kynakee.Modules.Projects.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;

namespace Kynakee.Modules.Projects.Domain.Entities.Scoped.ConcreteComponent
{
    public sealed class EquipmentComponent : APUComponent
    {
        private EquipmentComponent()
        {
        }

        private EquipmentComponent(
            APUComponentId id,
            Guid tenantId,
            APUAssignmentId apuAssignmentId,
            Guid sourceComponentId,
            string description,
            MeasurementUnit unit,            
            string equipmentCategory,
            decimal equipmentCount,
            decimal hoursPerApuUnit,
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
            EquipmentCategory = equipmentCategory;            
        }

        public override APUComponentType Type =>
            APUComponentType.Equipment;

        public string EquipmentCategory { get; private set; } = string.Empty;

        public decimal EquipmentCount { get; private set; }

        public decimal HoursPerApuUnit { get; private set; }


        public static Result<EquipmentComponent> Create(
            Guid tenantId,
            APUAssignmentId apuAssignmentId,
            Guid sourceComponentId,
            string description,
            MeasurementUnit unit,
            string equipmentCategory,
            decimal equipmentCount,
            decimal hoursPerApuUnit,
            string? fallbackIndicator = null,
            Guid? createdBy = null)
        {
            if (!IsValidIdentity(
                    tenantId,
                    apuAssignmentId,
                    sourceComponentId))
            {
                return ResultFactory.Failure<EquipmentComponent>(
                    ApplicationError.Validation(
                        "PROJ_APU_EQUIPMENT_IDENTITY_INVALID",
                        "The equipment component identity is invalid."));
            }

            if (!IsValidDescription(description) ||
                string.IsNullOrWhiteSpace(equipmentCategory))
            {
                return ResultFactory.Failure<EquipmentComponent>(
                    ApplicationError.Validation(
                        "PROJ_APU_EQUIPMENT_DATA_REQUIRED",
                        "The equipment description and category are required."));
            }

            if (!IsValidUnit(unit))
            {
                return ResultFactory.Failure<EquipmentComponent>(
                    ApplicationError.Validation(
                        "PROJ_APU_EQUIPMENT_UNIT_REQUIRED",
                        "The equipment unit is required."));
            }
            

            return ResultFactory.Success(
                new EquipmentComponent(
                    APUComponentId.New(),
                    tenantId,
                    apuAssignmentId,
                    sourceComponentId,
                    description.Trim(),
                    unit!,                    
                    equipmentCategory.Trim(),
                    equipmentCount,
                    hoursPerApuUnit,
                    Normalize(fallbackIndicator),
                    createdBy));
        }
    }
}

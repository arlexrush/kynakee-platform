using Kynakee.Modules.Projects.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;

namespace Kynakee.Modules.Projects.Domain.Entities.Scoped.ConcreteComponent
{
    public sealed class LaborComponent : APUComponent
    {
        private LaborComponent()
        {
        }

        private LaborComponent(
            APUComponentId id,
            Guid tenantId,
            APUAssignmentId apuAssignmentId,
            Guid sourceComponentId,
            string description,
            MeasurementUnit unit,
            string trade,
            decimal crewSize,
            decimal productivity,
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
            Trade = trade;
            CrewSize = crewSize;
            Productivity = productivity;
        }

        public override APUComponentType Type => APUComponentType.Labor;

        public string Trade { get; private set; } = string.Empty;

        public decimal CrewSize { get; private set; }

        public decimal Productivity { get; private set; }

        public static Result<LaborComponent> Create(
            Guid tenantId,
            APUAssignmentId apuAssignmentId,
            Guid sourceComponentId,
            string description,
            MeasurementUnit unit,
            string trade,
            decimal crewSize,
            decimal productivity,
            string? fallbackIndicator = null,
            Guid? createdBy = null)
        {
            if (!IsValidIdentity(
                    tenantId,
                    apuAssignmentId,
                    sourceComponentId))
            {
                return ResultFactory.Failure<LaborComponent>(
                    ApplicationError.Validation(
                        "PROJ_APU_LABOR_IDENTITY_INVALID",
                        "The labor component identity is invalid."));
            }

            if (!IsValidDescription(description) ||
                string.IsNullOrWhiteSpace(trade))
            {
                return ResultFactory.Failure<LaborComponent>(
                    ApplicationError.Validation(
                        "PROJ_APU_LABOR_DATA_REQUIRED",
                        "The labor description and trade are required."));
            }

            if (!IsValidUnit(unit))
            {
                return ResultFactory.Failure<LaborComponent>(
                    ApplicationError.Validation(
                        "PROJ_APU_LABOR_UNIT_REQUIRED",
                        "The labor unit is required."));
            }

            if (crewSize <= 0 || productivity <= 0)
            {
                return ResultFactory.Failure<LaborComponent>(
                    ApplicationError.Validation(
                        "PROJ_APU_LABOR_TECHNICAL_DATA_INVALID",
                        "Crew size and productivity must be greater than zero."));
            }

            return ResultFactory.Success(
                new LaborComponent(
                    APUComponentId.New(),
                    tenantId,
                    apuAssignmentId,
                    sourceComponentId,
                    description.Trim(),
                    unit,
                    trade.Trim(),
                    crewSize,
                    productivity,
                    Normalize(fallbackIndicator),
                    createdBy));
        }

        internal Result UpdateCrewSize(
            decimal crewSize,
            Guid? updatedBy = null)
        {
            if (crewSize <= 0)
            {
                return ResultFactory.Failure(
                    ApplicationError.Validation(
                        "PROJ_APU_LABOR_CREW_SIZE_INVALID",
                        "Crew size must be greater than zero."));
            }

            if (CrewSize == crewSize)
            {
                return ResultFactory.Ok();
            }

            CrewSize = crewSize;
            RegisterUpdate(updatedBy);

            return ResultFactory.Ok();
        }

        internal Result UpdateProductivity(
            decimal productivity,
            Guid? updatedBy = null)
        {
            if (productivity <= 0)
            {
                return ResultFactory.Failure(
                    ApplicationError.Validation(
                        "PROJ_APU_LABOR_PRODUCTIVITY_INVALID",
                        "Productivity must be greater than zero."));
            }

            if (Productivity == productivity)
            {
                return ResultFactory.Ok();
            }

            Productivity = productivity;
            RegisterUpdate(updatedBy);

            return ResultFactory.Ok();
        }
    }
}

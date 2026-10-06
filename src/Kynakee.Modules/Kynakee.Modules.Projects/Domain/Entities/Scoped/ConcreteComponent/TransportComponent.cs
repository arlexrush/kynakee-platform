using Kynakee.Modules.Projects.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;

namespace Kynakee.Modules.Projects.Domain.Entities.Scoped.ConcreteComponent
{
    public sealed class TransportComponent : APUComponent
    {
        private TransportComponent()
        {
        }

        private TransportComponent(
            APUComponentId id,
            Guid tenantId,
            APUAssignmentId apuAssignmentId,
            Guid sourceComponentId,
            string description,
            MeasurementUnit unit,           
            string? fallbackIndicator,
            decimal distanceKm,
            decimal vehicleCapacity,
            TransportRateBasis transportRateBasis,
            decimal roundTripFactor,
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
            DistanceKm = distanceKm;
            VehicleCapacity = vehicleCapacity;
            TransportRateBasis = transportRateBasis;
            RoundTripFactor = roundTripFactor;
        }

        public override APUComponentType Type =>
            APUComponentType.Transport;

        public decimal QuantityPerApuUnit { get; private set; }
        public decimal DistanceKm { get; private set; }
        public decimal VehicleCapacity { get; private set; }
        public TransportRateBasis TransportRateBasis { get; private set; } 
        public decimal RoundTripFactor { get; private set; }

        public static Result<TransportComponent> Create(
            Guid tenantId,
            APUAssignmentId apuAssignmentId,
            Guid sourceComponentId,
            string description,
            MeasurementUnit unit,            
            decimal distanceKm,
            decimal vehicleCapacity,
            TransportRateBasis transportRateBasis,
            decimal roundTripFactor,
            decimal quantityPerApuUnit,
            string? fallbackIndicator = null,
            Guid? createdBy = null)
        {
            if (!IsValidIdentity(
                    tenantId,
                    apuAssignmentId,
                    sourceComponentId))
            {
                return ResultFactory.Failure<TransportComponent>(
                    ApplicationError.Validation(
                        "PROJ_APU_TRANSPORT_IDENTITY_INVALID",
                        "The transport identity is invalid."));
            }
            if(IsValidDescription(description))
            {
                return ResultFactory.Failure<TransportComponent>(
                    ApplicationError.Validation(
                        "PROJ_APU_TRANSPORT_DESCRIPTION_INVALID",
                        "The transport description is invalid."));
            }
            
            if(!IsValidUnit(unit))
            {
                return ResultFactory.Failure<TransportComponent>(
                    ApplicationError.Validation(
                        "PROJ_APU_TRANSPORT_UNIT_INVALID",
                        "The transport unit is invalid."));
            }
            if (distanceKm <= 0)
            {
                return ResultFactory.Failure<TransportComponent>(
                    ApplicationError.Validation(
                        "PROJ_APU_TRANSPORT_DISTANCE_INVALID",
                        "The transport distance must be greater than zero."));
            }
            if (vehicleCapacity <= 0)
            {
                return ResultFactory.Failure<TransportComponent>(
                    ApplicationError.Validation(
                        "PROJ_APU_TRANSPORT_CAPACITY_INVALID",
                        "The vehicle capacity must be greater than zero."));
            }

            Normalize(description);
            Normalize(fallbackIndicator);

            var id = APUComponentId.New();
            var component = new TransportComponent(
                id,
                tenantId,
                apuAssignmentId,
                sourceComponentId,
                description,
                unit,                
                fallbackIndicator,
                distanceKm,
                vehicleCapacity,
                transportRateBasis,
                roundTripFactor,
                quantityPerApuUnit,
                createdBy);
            return ResultFactory.Success(component);
        }

    }
}

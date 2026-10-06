using Kynakee.Modules.Projects.Domain.Entities.Scoped.ConcreteComponent;
using Kynakee.Modules.Projects.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;
using Kynakee.Modules.SharedKernel.Domain;

namespace Kynakee.Modules.Projects.Domain.Entities.Scoped
{
    /// <summary>
    /// Representa la asignación de una APU a un WorkItem, incluyendo plantilla APU, fuente, componentes, precio
    /// unitario y nivel de confianza.
    /// </summary>
    /// <remarks>Se instancia mediante el método de fábrica Create, que aplica validaciones y devuelve
    /// Result<APUAssignment>. Hereda BaseEntity<TId> y contiene metadatos de tenant y auditoría. La colección de
    /// componentes es de solo lectura. El precio unitario y la confianza se actualizan mediante SetUnitPrice, que
    /// registra la modificación.</remarks>
    public sealed class APUAssignment : BaseEntity<APUAssignmentId>
    {
        private List<APUComponent> _components = new List<APUComponent>();
        public IReadOnlyList<APUComponent> Components => _components.AsReadOnly();

        private APUAssignment()
        {
        }

        private APUAssignment(
            APUAssignmentId id,
            Guid tenantId,
            ProjectId projectId,
            WorkItemId workItemId,
            MeasurementUnit outputUnit,
            APUTemplateId apuTemplateId,
            APUSource source,
            IEnumerable<APUComponent> components,
            Confidence confidence,
            Guid? createdBy)
            : base(id, tenantId, createdBy)
        {
            WorkItemId = workItemId;
            OutputUnit = outputUnit;
            APUTemplateId = apuTemplateId;
            Source = source;
            Confidence = confidence;
            ProjectId = projectId;
            _components.AddRange(components);
        }

        public WorkItemId WorkItemId { get; private set; }

        public MeasurementUnit OutputUnit { get; private set; } = default!;

        public ProjectId ProjectId { get; private set; }

        public APUTemplateId APUTemplateId { get; private set; }

        public APUSource Source { get; private set; }

        public Confidence Confidence { get; private set; } = default!;



        public Result<APUUnitPriceCalculation> CalculateUnitPrice()
        {
            if (_components.Count == 0)
            {
                return ResultFactory.Failure<APUUnitPriceCalculation>(
                    ApplicationError.Validation(
                        "PROJ_APU_COMPONENTS_REQUIRED",
                        "An APU assignment must contain at least one component."));
            }

            var directComponents = _components
                .Where(component => component.Type != APUComponentType.AuxiliaryMeans)
                .ToList();

            var auxiliaryComponents = _components
                .OfType<AuxiliaryMeansComponent>()
                .ToList();

            if (_components.Any(component =>
                    component.Type == APUComponentType.AuxiliaryMeans &&
                    component is not AuxiliaryMeansComponent))
            {
                return ResultFactory.Failure<APUUnitPriceCalculation>(
                    ApplicationError.Validation(
                        "PROJ_APU_AUXILIARY_TYPE_INVALID",
                        "An auxiliary-means component has an invalid runtime type."));
            }

            if (directComponents.Count == 0)
            {
                return ResultFactory.Failure<APUUnitPriceCalculation>(
                    ApplicationError.Validation(
                        "PROJ_APU_DIRECT_COMPONENTS_REQUIRED",
                        "At least one priced direct component is required."));
            }

            foreach (var component in directComponents)
            {
                var pricing = component.CurrentPricing;

                if (pricing is null || !pricing.HasPrice || pricing.UnitPrice is null)
                {
                    return ResultFactory.Failure<APUUnitPriceCalculation>(
                        ApplicationError.Conflict(
                            "PROJ_APU_DIRECT_COMPONENT_PRICING_REQUIRED",
                            $"The direct component '{component.Description}' has no valid price."));
                }

                if (pricing.APUComponentId != component.Id)
                {
                    return ResultFactory.Failure<APUUnitPriceCalculation>(
                        ApplicationError.Conflict(
                            "PROJ_APU_DIRECT_COMPONENT_PRICE_MISMATCH",
                            $"The price does not belong to component '{component.Id}'."));
                }
            }

            var currency = directComponents[0].CurrentPricing!.UnitPrice!.Currency;

            if (directComponents.Any(component =>
                    !string.Equals(
                        component.CurrentPricing!.UnitPrice!.Currency,
                        currency,
                        StringComparison.OrdinalIgnoreCase)))
            {
                return ResultFactory.Failure<APUUnitPriceCalculation>(
                    ApplicationError.Validation(
                        "PROJ_APU_DIRECT_COMPONENT_CURRENCY_MISMATCH",
                        "All direct component prices must use the same currency."));
            }

            var materialComponents = directComponents.OfType<MaterialComponent>().ToList();
            var laborComponents = directComponents.OfType<LaborComponent>().ToList();
            var equipmentComponents = directComponents.OfType<EquipmentComponent>().ToList();
            var subcontractComponents = directComponents.OfType<SubcontractComponent>().ToList();
            var transportComponents = directComponents.OfType<TransportComponent>().ToList();

            var supportedComponentCount =
                materialComponents.Count +
                laborComponents.Count +
                equipmentComponents.Count +
                subcontractComponents.Count +
                transportComponents.Count;

            if (supportedComponentCount != directComponents.Count)
            {
                return ResultFactory.Failure<APUUnitPriceCalculation>(
                    ApplicationError.Validation(
                        "PROJ_APU_COMPONENT_TYPE_UNSUPPORTED",
                        "One or more direct APU component types are not supported."));
            }

            var componentSnapshots = new List<ValuedComponent>();

            var materialResult = CalculateComponentGroup(
                materialComponents,
                component => CalculatePricedComponentCost(
                    component,
                    component.QuantityPerApuUnit *
                    (1m + component.WastePercentage / 100m),
                    "PROJ_APU_MATERIAL_CALCULATION_INVALID"),
                currency,
                componentSnapshots);

            if (!materialResult.IsSuccess)
            {
                return ResultFactory.Failure<APUUnitPriceCalculation>(
                    materialResult.Error!);
            }

            var laborResult = CalculateComponentGroup(
                laborComponents,
                component =>
                {
                    if (component.CrewSize <= 0 || component.Productivity <= 0)
                    {
                        return ComponentCalculationFailure(
                            "PROJ_APU_LABOR_CALCULATION_INVALID",
                            $"Labor component '{component.Description}' must have positive crew size and productivity.");
                    }

                    return CalculatePricedComponentCost(
                        component,
                        component.CrewSize / component.Productivity,
                        "PROJ_APU_LABOR_CALCULATION_INVALID");
                },
                currency,
                componentSnapshots);

            if (!laborResult.IsSuccess)
            {
                return ResultFactory.Failure<APUUnitPriceCalculation>(
                    laborResult.Error!);
            }

            var equipmentResult = CalculateComponentGroup(
                equipmentComponents,
                component =>
                {
                    if (component.EquipmentCount <= 0 ||
                        component.HoursPerApuUnit <= 0)
                    {
                        return ComponentCalculationFailure(
                            "PROJ_APU_EQUIPMENT_CALCULATION_INVALID",
                            $"Equipment component '{component.Description}' must have positive count and hours per APU unit.");
                    }

                    return CalculatePricedComponentCost(
                        component,
                        component.EquipmentCount * component.HoursPerApuUnit,
                        "PROJ_APU_EQUIPMENT_CALCULATION_INVALID");
                },
                currency,
                componentSnapshots);

            if (!equipmentResult.IsSuccess)
            {
                return ResultFactory.Failure<APUUnitPriceCalculation>(
                    equipmentResult.Error!);
            }

            var subcontractResult = CalculateComponentGroup(
                subcontractComponents,
                component => CalculatePricedComponentCost(
                    component,
                    component.QuantityPerApuUnit,
                    "PROJ_APU_SUBCONTRACT_CALCULATION_INVALID"),
                currency,
                componentSnapshots);

            if (!subcontractResult.IsSuccess)
            {
                return ResultFactory.Failure<APUUnitPriceCalculation>(
                    subcontractResult.Error!);
            }

            var transportResult = CalculateComponentGroup(
                transportComponents,
                CalculateTransportComponentCost,
                currency,
                componentSnapshots);

            if (!transportResult.IsSuccess)
            {
                return ResultFactory.Failure<APUUnitPriceCalculation>(
                    transportResult.Error!);
            }

            var materialSubtotal = materialResult.Value!;
            var laborSubtotal = laborResult.Value!;
            var equipmentSubtotal = equipmentResult.Value!;
            var subcontractSubtotal = subcontractResult.Value!;
            var transportSubtotal = transportResult.Value!;

            var directUnitCost = new Money(
                materialSubtotal.Amount +
                laborSubtotal.Amount +
                equipmentSubtotal.Amount +
                subcontractSubtotal.Amount +
                transportSubtotal.Amount,
                currency);

            var auxiliaryPercentage = auxiliaryComponents.Sum(
                component => component.ApplicationPercentage);

            if (auxiliaryComponents.Any(component =>
                    component.ApplicationPercentage < 0))
            {
                return ResultFactory.Failure<APUUnitPriceCalculation>(
                    ApplicationError.Validation(
                        "PROJ_APU_AUXILIARY_PERCENTAGE_INVALID",
                        "Auxiliary application percentages cannot be negative."));
            }

            var auxiliaryUnitCost = new Money(
                directUnitCost.Amount * auxiliaryPercentage / 100m,
                currency);

            return ResultFactory.Success(
                new APUUnitPriceCalculation(
                    directUnitCost,
                    auxiliaryUnitCost,
                    materialSubtotal,
                    laborSubtotal,
                    equipmentSubtotal,
                    subcontractSubtotal,
                    transportSubtotal,
                    componentSnapshots.AsReadOnly(),
                    auxiliaryComponents.AsReadOnly()));
        }

        private static Result<Money> CalculateComponentGroup<TComponent>(
            IEnumerable<TComponent> components,
            Func<TComponent, Result<Money>> calculateComponentCost,
            string currency,
            List<ValuedComponent> componentSnapshots)
            where TComponent : APUComponent
        {
            decimal subtotalAmount = 0m;

            foreach (var component in components)
            {
                var costResult = calculateComponentCost(component);

                if (!costResult.IsSuccess)
                {
                    return ResultFactory.Failure<Money>(costResult.Error!);
                }

                var componentSubtotal = costResult.Value!;

                if (!string.Equals(
                        componentSubtotal.Currency,
                        currency,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return ResultFactory.Failure<Money>(
                        ApplicationError.Validation(
                            "PROJ_APU_COMPONENT_CURRENCY_MISMATCH",
                            "All component subtotals must use the APU currency."));
                }

                var snapshotResult = ValuedComponent.Create(
                    component,
                    componentSubtotal);

                if (!snapshotResult.IsSuccess)
                {
                    return ResultFactory.Failure<Money>(
                        snapshotResult.Error!);
                }

                componentSnapshots.Add(snapshotResult.Value!);
                subtotalAmount += componentSubtotal.Amount;
            }

            return ResultFactory.Success(new Money(subtotalAmount, currency));
        }

        private static Result<Money> CalculatePricedComponentCost(
            APUComponent component,
            decimal quantityFactor,
            string errorCode)
        {
            if (quantityFactor <= 0)
            {
                return ComponentCalculationFailure(
                    errorCode,
                    $"The technical quantity for component '{component.Description}' must be positive.");
            }

            var pricing = component.CurrentPricing;

            if (pricing is null || !pricing.HasPrice || pricing.UnitPrice is null)
            {
                return ComponentCalculationFailure(
                    errorCode,
                    $"Component '{component.Description}' has no valid unit price.");
            }

            return ResultFactory.Success(
                pricing.UnitPrice.Multiply(quantityFactor));
        }

        private static Result<Money> CalculateTransportComponentCost(
            TransportComponent component)
        {
            if (component.QuantityPerApuUnit <= 0)
            {
                return ComponentCalculationFailure(
                    "PROJ_APU_TRANSPORT_CALCULATION_INVALID",
                    $"Transport component '{component.Description}' must have a positive quantity per APU unit.");
            }

            decimal factor;

            switch (component.TransportRateBasis)
            {
                case TransportRateBasis.PerTrip:
                    if (component.VehicleCapacity <= 0)
                    {
                        return ComponentCalculationFailure(
                            "PROJ_APU_TRANSPORT_CAPACITY_INVALID",
                            "Vehicle capacity must be positive for a per-trip rate.");
                    }

                    factor = component.QuantityPerApuUnit / component.VehicleCapacity;
                    break;

                case TransportRateBasis.PerVehicleKilometer:
                    if (component.VehicleCapacity <= 0 ||
                        component.DistanceKm <= 0 ||
                        component.RoundTripFactor <= 0)
                    {
                        return ComponentCalculationFailure(
                            "PROJ_APU_TRANSPORT_PARAMETERS_INVALID",
                            "Capacity, distance and round-trip factor must be positive for a per-vehicle-kilometer rate.");
                    }

                    factor =
                        component.QuantityPerApuUnit /
                        component.VehicleCapacity *
                        component.DistanceKm *
                        component.RoundTripFactor;
                    break;

                case TransportRateBasis.PerTonKilometer:
                    if (component.DistanceKm <= 0)
                    {
                        return ComponentCalculationFailure(
                            "PROJ_APU_TRANSPORT_DISTANCE_INVALID",
                            "Distance must be positive for a per-ton-kilometer rate.");
                    }

                    factor = component.QuantityPerApuUnit * component.DistanceKm;
                    break;

                case TransportRateBasis.PerUnit:
                    factor = component.QuantityPerApuUnit;
                    break;

                default:
                    return ComponentCalculationFailure(
                        "PROJ_APU_TRANSPORT_RATE_BASIS_INVALID",
                        "The transport rate basis is not supported.");
            }

            return CalculatePricedComponentCost(
                component,
                factor,
                "PROJ_APU_TRANSPORT_CALCULATION_INVALID");
        }

        private static Result<Money> ComponentCalculationFailure(
            string code,
            string message) =>
            ResultFactory.Failure<Money>(
            ApplicationError.Validation(code, message));

        public static Result<APUAssignment> Create(
            Guid tenantId,
            ProjectId projectId,
            WorkItemId workItemId,
            MeasurementUnit outputUnit,
            APUTemplateId apuTemplateId,
            APUSource source,
            IEnumerable<APUComponentDefinition> componentDefinitions,
            Confidence confidence,
            Guid? createdBy = null)
        {
            ArgumentNullException.ThrowIfNull(componentDefinitions);
            ArgumentNullException.ThrowIfNull(confidence);

            if (tenantId == Guid.Empty)
            {
                return ResultFactory.Failure<APUAssignment>(
                    ApplicationError.Validation(
                        "PROJ_APU_TENANT_REQUIRED",
                        "The APU assignment tenant is required."));
            }

            if (projectId.Value == Guid.Empty)
            {
                return ResultFactory.Failure<APUAssignment>(
                    ApplicationError.Validation(
                        "PROJ_APU_PROJECT_REQUIRED",
                        "The APU assignment project identifier is required."));
            }

            if (workItemId.Value == Guid.Empty)
            {
                return ResultFactory.Failure<APUAssignment>(
                    ApplicationError.Validation(
                        "PROJ_APU_WORKITEM_REQUIRED",
                        "The work item identifier is required."));
            }

            if (apuTemplateId.Value == Guid.Empty)
            {
                return ResultFactory.Failure<APUAssignment>(
                    ApplicationError.Validation(
                        "PROJ_APU_TEMPLATE_REQUIRED",
                        "The APU template identifier is required."));
            }

            if (outputUnit is null || string.IsNullOrWhiteSpace(outputUnit.Code))
            {
                return ResultFactory.Failure<APUAssignment>(
                    ApplicationError.Validation(
                        "PROJ_APU_OUTPUT_UNIT_REQUIRED",
                        "The APU output unit is required."));
            }

            var definitions = componentDefinitions.ToArray();

            if (definitions.Length == 0)
            {
                return ResultFactory.Failure<APUAssignment>(
                    ApplicationError.Validation(
                        "PROJ_APU_COMPONENTS_REQUIRED",
                        "An APU assignment must contain at least one component."));
            }



            var assignmentId = APUAssignmentId.New();

            var assignment = new APUAssignment(
                assignmentId,
                tenantId,
                projectId,
                workItemId,
                outputUnit,
                apuTemplateId,
                source,
                [],
                confidence,
                createdBy);

            foreach (var definition in definitions)
            {
                var componentResult = CreateComponent(
                    definition,
                    tenantId,
                    assignmentId,
                    createdBy);

                if (!componentResult.IsSuccess)
                {
                    return ResultFactory.Failure<APUAssignment>(
                        componentResult.Error!);
                }

                assignment._components.Add(componentResult.Value!);
            }

            return ResultFactory.Success(assignment);
        }

        internal Result AddPricingResult(
            APUComponentPricing pricing,
            Guid? updatedBy = null)
        {
            ArgumentNullException.ThrowIfNull(pricing);

            if (pricing.TenantId != TenantId)
            {
                return ResultFactory.Failure(
                    ApplicationError.Conflict(
                        "PROJ_APU_PRICING_TENANT_MISMATCH",
                        "The pricing belongs to another tenant."));
            }
                       

            var componentExists = _components.SingleOrDefault(
                component => component.Id == pricing.APUComponentId);

            if (componentExists is null)
            {
                return ResultFactory.Failure(
                    ApplicationError.NotFound(
                        "PROJ_APU_COMPONENT_NOT_FOUND",
                        "The priced component was not found in the assignment."));
            }

            if (componentExists is AuxiliaryMeansComponent)
            {
                return ResultFactory.Failure(
                    ApplicationError.Validation(
                        "PROJ_APU_AUXILIARY_PRICING_NOT_ALLOWED",
                        "Auxiliary means are calculated from their percentage and do not have a quoted price."));
            }

            var pricingResult = componentExists.AddPricing(pricing);

            if (!pricingResult.IsSuccess)
            {
                return pricingResult;
            }

            RegisterUpdate(updatedBy);

            return ResultFactory.Ok();
        }

        private static Result<APUComponent> CreateComponent(
            APUComponentDefinition definition,
            Guid tenantId,
            APUAssignmentId assignmentId,
            Guid? createdBy)
        {
            ArgumentNullException.ThrowIfNull(definition);

            return definition switch
            {
                MaterialComponentDefinition material =>
                    MaterialComponent.Create(
                        tenantId,
                        assignmentId,
                        material.SourceComponentId,
                        material.Description,
                        material.Unit,                        
                        material.WastePercentage,
                        material.TransportIncluded,
                        material.QuantityPerApuUnit,
                        material.FallbackIndicator,
                        createdBy).Map(component => (APUComponent)component),

                LaborComponentDefinition labor =>
                    LaborComponent.Create(
                        tenantId,
                        assignmentId,
                        labor.SourceComponentId,
                        labor.Description,
                        labor.Unit,
                        labor.Trade,                            
                        labor.CrewSize,
                        labor.Productivity,
                        labor.FallbackIndicator,
                        createdBy).Map(component => (APUComponent)component),

                EquipmentComponentDefinition equipment =>
                    EquipmentComponent.Create(
                        tenantId,
                        assignmentId,
                        equipment.SourceComponentId,
                        equipment.Description,
                        equipment.Unit,                        
                        equipment.EquipmentCategory,                       
                        equipment.EquipmentCount,
                        equipment.HoursPerApuUnit,
                        equipment.FallbackIndicator,
                        createdBy).Map(component => (APUComponent)component),

                TransportComponentDefinition transport =>
                    TransportComponent.Create(
                        tenantId,
                        assignmentId,
                        transport.SourceComponentId,
                        transport.Description,
                        transport.Unit,                        
                        transport.DistanceKm,
                        transport.VehicleCapacity,
                        transport.TransportRateBasis,
                        transport.RoundTripFactor,
                        transport.QuantityPerApuUnit,
                        transport.FallbackIndicator,
                        createdBy).Map(component => (APUComponent)component),

                SubcontractComponentDefinition subcontract =>
                    SubcontractComponent.Create(
                        tenantId,
                        assignmentId,
                        subcontract.SourceComponentId,
                        subcontract.Description,
                        subcontract.Unit,                        
                        subcontract.ContractConditions,
                        subcontract.QuantityPerApuUnit,
                        subcontract.FallbackIndicator,                        
                        createdBy).Map(component => (APUComponent)component),

                AuxiliaryMeansComponentDefinition auxiliary =>
                    AuxiliaryMeansComponent.Create(
                        tenantId,
                        assignmentId,
                        auxiliary.SourceComponentId,
                        auxiliary.Description,
                        auxiliary.Unit,
                        auxiliary.ApplicationPercentage,
                        auxiliary.FallbackIndicator,
                        createdBy).Map(component => (APUComponent)component),

                _ => ResultFactory.Failure<APUComponent>(
                    ApplicationError.Validation(
                        "PROJ_APU_COMPONENT_TYPE_UNSUPPORTED",
                        "The APU component type is not supported."))
            };
        }


    }
}

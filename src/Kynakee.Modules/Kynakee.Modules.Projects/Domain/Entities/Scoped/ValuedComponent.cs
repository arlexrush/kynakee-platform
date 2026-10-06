using Kynakee.Modules.Projects.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;

namespace Kynakee.Modules.Projects.Domain.Entities.Scoped
{
    /// <summary>
    /// Representa un componente de APU con su precio calculado, cantidad aplicada y metadatos de la fuente de precios.
    /// </summary>
    /// <remarks>Se crea mediante el método fábrica Create que valida la cantidad (> 0), exige precio asociado
    /// y comprueba que el resultado de precio pertenezca al componente. El coste total se calcula multiplicando el
    /// precio unitario por la cantidad requerida (workItemQuantity × yield). Instancia inmutable que agrupa datos de
    /// valoración y trazabilidad de la fuente.</remarks>
    public sealed record ValuedComponent
    {
        private ValuedComponent(
            APUComponentId componentId,
            APUComponentType componentType,
            string description,
            MeasurementUnit unit,
            Money quotedUnitPrice,
            Money componentSubtotal,
            PricingSource pricingSource,
            string? providerName,
            bool isFallback,
            Confidence confidence)
        {
            ComponentId = componentId;
            ComponentType = componentType;
            Description = description;
            Unit = unit;
            QuotedUnitPrice = quotedUnitPrice;
            ComponentSubtotal = componentSubtotal;
            PricingSource = pricingSource;
            ProviderName = providerName;
            IsFallback = isFallback;
            Confidence = confidence;
        }

        public APUComponentId ComponentId { get; }

        public APUComponentType ComponentType { get; }

        public string Description { get; }

        public MeasurementUnit Unit { get; }

        /// <summary>
        /// Precio cotizado por unidad del recurso, antes de aplicar los datos técnicos del componente.
        /// </summary>
        public Money QuotedUnitPrice { get; }

        /// <summary>
        /// Importe que aporta este componente al precio de una unidad de APU.
        /// Lo calcula APUAssignment según el tipo y los datos técnicos del componente.
        /// </summary>
        public Money ComponentSubtotal { get; }

        public PricingSource PricingSource { get; }

        public string? ProviderName { get; }

        public bool IsFallback { get; }

        public Confidence Confidence { get; }

        /// <summary>
        /// Crea el snapshot de un componente directo usando su subtotal unitario ya calculado.
        /// No calcula cantidades ni aplica la medición del WorkItem.
        /// </summary>
        public static Result<ValuedComponent> Create(
            APUComponent component,
            Money componentSubtotal)
        {
            ArgumentNullException.ThrowIfNull(component);
            ArgumentNullException.ThrowIfNull(componentSubtotal);

            if (component.Type == APUComponentType.AuxiliaryMeans)
            {
                return ResultFactory.Failure<ValuedComponent>(
                    ApplicationError.Validation(
                        "PROJ_VALUED_COMPONENT_AUXILIARY_NOT_DIRECT",
                        "Auxiliary means are calculated separately from direct components."));
            }

            var pricing = component.CurrentPricing;

            if (pricing is null || !pricing.HasPrice || pricing.UnitPrice is null)
            {
                return ResultFactory.Failure<ValuedComponent>(
                    ApplicationError.Conflict(
                        "PROJ_VALUED_COMPONENT_PRICE_REQUIRED",
                        $"No valid resource price exists for component '{component.Id}'."));
            }

            if (pricing.APUComponentId != component.Id)
            {
                return ResultFactory.Failure<ValuedComponent>(
                    ApplicationError.Conflict(
                        "PROJ_VALUED_COMPONENT_PRICE_MISMATCH",
                        "The pricing result does not belong to the component."));
            }

            if (!string.Equals(
                    pricing.UnitPrice.Currency,
                    componentSubtotal.Currency,
                    StringComparison.OrdinalIgnoreCase))
            {
                return ResultFactory.Failure<ValuedComponent>(
                    ApplicationError.Validation(
                        "PROJ_VALUED_COMPONENT_CURRENCY_MISMATCH",
                        "The quoted unit price and component subtotal must use the same currency."));
            }

            return ResultFactory.Success(
                new ValuedComponent(
                    component.Id,
                    component.Type,
                    component.Description,
                    component.Unit,
                    pricing.UnitPrice,
                    componentSubtotal,
                    pricing.Source,
                    pricing.ProviderName,
                    pricing.IsFallback,
                    pricing.Confidence));
        }


        internal static ValuedComponent Restore(
            APUComponentId componentId,
            APUComponentType componentType,
            string description,
            MeasurementUnit unit,
            Money quotedUnitPrice,
            Money componentSubtotal,
            PricingSource pricingSource,
            string? providerName,
            bool isFallback,
            Confidence confidence) =>
            new(
                componentId,
                componentType,
                description,
                unit,
                quotedUnitPrice,
                componentSubtotal,
                pricingSource,
                providerName,
                isFallback,
                confidence);


    }
}

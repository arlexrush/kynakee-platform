using Kynakee.Modules.Projects.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;
using Kynakee.Modules.SharedKernel.Domain;

namespace Kynakee.Modules.Projects.Domain.Entities.Scoped
{
    public abstract class APUComponent :
    BaseEntity<APUComponentId>
    {
        private readonly List<APUComponentPricing> _pricingHistory = new();

        public APUComponentPricing? CurrentPricing => SelectCurrentPricing();

        protected APUComponent()
        {
        }

        protected APUComponent(
            APUComponentId id,
            Guid tenantId,
            APUAssignmentId apuAssignmentId,
            Guid sourceComponentId,
            string description,
            MeasurementUnit unit,           
            string? fallbackIndicator,
            Guid? createdBy)
            : base(id, tenantId, createdBy)
        {
            APUAssignmentId = apuAssignmentId;
            SourceComponentId = sourceComponentId;
            Description = description;
            Unit = unit;            
            FallbackIndicator = fallbackIndicator;
        }

        /// <summary>
        /// Identificador único de la asignación APU asociada a la entidad.
        /// </summary>
        /// <remarks>Propiedad de solo lectura pública; su valor se establece internamente por la entidad
        /// y se utiliza para referenciar la APU relacionada.</remarks>
        public APUAssignmentId APUAssignmentId { get; private set; }

        /// <summary>
        /// Identificador del componente original procedente de KnowledgeBase.
        /// </summary>
        public Guid SourceComponentId { get; private set; }

        /// <summary>
        /// Descripcion del componente, que proporciona información sobre su naturaleza o propósito.
        /// </summary>
        /// <remarks>Valor predeterminado: cadena vacía. La propiedad tiene un setter privado y se
        /// modifica únicamente desde la propia clase.</remarks>
        public string Description { get; private set; } =
            string.Empty;

        /// <summary>
        /// Unidad de medida que describe la magnitud del valor.
        /// </summary>
        /// <remarks>Se inicializa durante la construcción y tiene un setter privado. Garantiza que no sea
        /// nulo en tiempo de ejecución.</remarks>
        public MeasurementUnit Unit { get; private set; } =
            default!;

        public int UnitRevision { get; private set; }

        /// <summary>
        /// Responsable de indicar si se ha activado un fallback para el componente, proporcionando información sobre la fuente o el motivo del fallback.
        /// </summary>
        /// <remarks>Se establece internamente; el formato y los valores concretos dependen de la
        /// implementación (por ejemplo, códigos predefinidos o descriptores). Será nulo si no se ha activado ningún
        /// fallback.</remarks>
        public string? FallbackIndicator { get; private set; }


        /// <summary>
        /// Añade un registro de precios al historial del componente.
        /// </summary>
        /// <param name="pricing">Registro de precios del componente que se va a añadir.</param>
        public Result AddPricing(APUComponentPricing pricing)
        {
            ArgumentNullException.ThrowIfNull(pricing);

            if (pricing.TenantId != TenantId)
            {
                return ResultFactory.Failure(
                    ApplicationError.Conflict(
                        "PROJ_APU_PRICING_TENANT_MISMATCH",
                        "The pricing belongs to another tenant."));
            }

            if (pricing.APUComponentId != Id)
            {
                return ResultFactory.Failure(
                    ApplicationError.Conflict(
                        "PROJ_APU_PRICING_COMPONENT_MISMATCH",
                        "The pricing belongs to another component."));
            }

            if (pricing.QuotedUnit != Unit ||
                pricing.UnitRevision != UnitRevision)
            {
                return ResultFactory.Failure(
                    ApplicationError.Conflict(
                        "PROJ_APU_PRICING_UNIT_MISMATCH",
                        "The quoted unit and revision must match the component."));
            }

            _pricingHistory.Add(pricing);
            RegisterUpdate(pricing.UpdatedBy);

            return ResultFactory.Ok();

        }

        /// <summary>
        /// Obtiene la lista de precios históricos del componente APU como una colección de solo lectura.
        /// </summary>
        /// <remarks>La colección es una vista de solo lectura del almacenamiento interno; las
        /// modificaciones al listado interno se reflejarán en esta vista. No garantiza seguridad frente a
        /// concurrencia.</remarks>
        public IReadOnlyList<APUComponentPricing> PricingHistory =>
            _pricingHistory.AsReadOnly();

        /// <summary>
        /// Obtiene el tipo del componente APU.
        /// </summary>
        /// <remarks>Se utiliza para distinguir implementaciones y controlar la lógica según el tipo; los
        /// valores permitidos están definidos en APUComponentType.</remarks>
        public abstract APUComponentType Type { get; }

        /// <summary>
        /// Verifica que los identificadores proporcionados no sean Guid.Empty.
        /// </summary>
        /// <param name="tenantId">Identificador del tenant; debe ser distinto de Guid.Empty.</param>
        /// <param name="apuAssignmentId">Identificador de la asignación APU; su valor interno debe ser distinto de Guid.Empty.</param>
        /// <param name="sourceComponentId">Identificador del componente origen; debe ser distinto de Guid.Empty.</param>
        /// <returns>true si todos los identificadores son distintos de Guid.Empty; en caso contrario, false.</returns>
        protected static bool IsValidIdentity(
            Guid tenantId,
            APUAssignmentId apuAssignmentId,
            Guid sourceComponentId) =>
            tenantId != Guid.Empty &&
            apuAssignmentId.Value != Guid.Empty &&
            sourceComponentId != Guid.Empty;

        /// <summary>
        /// Determina si la cadena de descripción contiene al menos un carácter distinto de espacios en blanco.
        /// </summary>
        /// <param name="description">Descripción a evaluar.</param>
        /// <returns>true si la descripción no es null, no está vacía y no contiene solo espacios en blanco; de lo contrario,
        /// false.</returns>
        protected static bool IsValidDescription(
            string? description) =>
            !string.IsNullOrWhiteSpace(description);

        /// <summary>
        /// Determina si la unidad de medida proporcionada no es nula y su propiedad Code no está vacía ni formada solo
        /// por espacios.
        /// </summary>
        /// <param name="unit">Unidad de medida a validar.</param>
        /// <returns>true si la unidad no es nula y su Code contiene al menos un carácter distinto de espacio; en caso contrario,
        /// false.</returns>
        protected static bool IsValidUnit(
            MeasurementUnit? unit) =>
            unit is not null &&
            !string.IsNullOrWhiteSpace(unit.Code);

        /// <summary>
        /// Indica si el valor de rendimiento es mayor que cero.
        /// </summary>
        /// <param name="yield">Valor de rendimiento a validar.</param>
        /// <returns>true si el rendimiento es mayor que cero; de lo contrario, false.</returns>
        protected static bool IsValidYield(
            decimal yield) =>
            yield > 0;  

        /// <summary>
        /// Normaliza una cadena: devuelve null si es null o contiene solo espacios en blanco; en caso contrario
        /// devuelve la cadena recortada.
        /// </summary>
        /// <param name="value">Cadena de entrada que puede ser null o contener únicamente espacios en blanco.</param>
        /// <returns>La cadena recortada sin espacios iniciales ni finales, o null si la entrada es null o solo espacios en
        /// blanco.</returns>
        protected static string? Normalize(
            string? value) =>
            string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();

        private APUComponentPricing? SelectCurrentPricing()
        {

            var usablePrices = _pricingHistory
                .Where(pricing =>
                    !pricing.IsDeleted &&
                    pricing.HasPrice &&
                    pricing.QuotedUnit is not null &&
                    pricing.QuotedUnit == Unit &&
                    pricing.UnitRevision == UnitRevision)
                .ToList();

            return usablePrices
                       .Where(pricing =>
                           pricing.Source == PricingSource.McpProvider)
                       .OrderByDescending(pricing => pricing.QueriedAt)
                       .FirstOrDefault()
                   ?? usablePrices
                       .Where(pricing =>
                           pricing.Source == PricingSource.CachedPrice)
                       .OrderByDescending(pricing => pricing.QueriedAt)
                       .FirstOrDefault()
                   ?? usablePrices
                       .Where(pricing =>
                           pricing.Source == PricingSource.AlternativeSource)
                       .OrderByDescending(pricing => pricing.QueriedAt)
                       .FirstOrDefault();

        }

        internal Result UpdateDescription(
            string description,
            Guid? updatedBy = null)
        {
            if (!IsValidDescription(description))
            {
                return ResultFactory.Failure(
                    ApplicationError.Validation(
                        "PROJ_APU_COMPONENT_DESCRIPTION_REQUIRED",
                        "The component description is required."));
            }

            var normalizedDescription = description.Trim();

            if (Description == normalizedDescription)
            {
                return ResultFactory.Ok();
            }

            Description = normalizedDescription;
            RegisterUpdate(updatedBy);

            return ResultFactory.Ok();
        }

        internal Result UpdateUnit(
            MeasurementUnit unit,
            Guid? updatedBy = null)
        {
            if (!IsValidUnit(unit))
            {
                return ResultFactory.Failure(
                    ApplicationError.Validation(
                        "PROJ_APU_COMPONENT_UNIT_REQUIRED",
                        "The component measurement unit is required."));
            }

            if (Unit == unit)
            {
                return ResultFactory.Ok();
            }

            if (UnitRevision == int.MaxValue)
            {
                return ResultFactory.Failure(
                    ApplicationError.Conflict(
                        "PROJ_APU_COMPONENT_UNIT_REVISION_EXHAUSTED",
                        "The component unit cannot be revised again."));
            }

            Unit = unit;
            UnitRevision++;
            RegisterUpdate(updatedBy);

            return ResultFactory.Ok();
        }


    }
}

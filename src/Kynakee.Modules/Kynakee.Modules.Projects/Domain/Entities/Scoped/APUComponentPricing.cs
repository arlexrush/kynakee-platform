using Kynakee.Modules.Projects.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;
using Kynakee.Modules.SharedKernel.Domain;

namespace Kynakee.Modules.Projects.Domain.Entities.Scoped
{
    /// <summary>
    /// Representa el resultado de la consulta de precio para un componente APU, incluyendo estado, precio unitario,
    /// proveedor, origen, marca temporal, nivel de confianza, indicador de fallback y motivo de fallo cuando procede.
    /// </summary>
    /// <remarks>Las instancias se crean mediante los métodos de fábrica CreateSuccessful y CreateUnavailable,
    /// que realizan validaciones y normalizan valores. La propiedad HasPrice señala si existe un precio válido (Status
    /// == PricingStatus.Found y UnitPrice != null). Hereda de BaseEntity<APUComponentPricingId>.</remarks>
    public sealed class APUComponentPricing :
    BaseEntity<APUComponentPricingId>
    {
        private APUComponentPricing()
        {
        }

        private APUComponentPricing(
            APUComponentPricingId id,
            Guid tenantId,
            APUComponentId componentId,
            PricingStatus status,
            Money? unitPrice,
            MeasurementUnit? quotedUnit,
            int? unitRevision,
            string? providerName,
            PricingSource source,
            DateTime queriedAt,
            Confidence confidence,
            bool isFallback,
            string? failureReason,
            Guid? createdBy)
            : base(id, tenantId, createdBy)
        {
            APUComponentId = componentId;
            Status = status;
            UnitPrice = unitPrice;
            QuotedUnit = quotedUnit;
            UnitRevision = unitRevision;
            ProviderName = providerName;
            Source = source;
            QueriedAt = queriedAt;
            Confidence = confidence;
            IsFallback = isFallback;
            FailureReason = failureReason;
        }

        /// <summary>
        /// Identificador único del componente APU asociado al agregado.
        /// </summary>
        /// <remarks>Se asigna internamente y solo puede modificarse desde la lógica del agregado a través
        /// de sus métodos.</remarks>
        public APUComponentId APUComponentId { get; private set; }

        /// <summary>
        /// Indica el estado actual de la tarificación.
        /// </summary>
        /// <remarks>Tiene un setter privado; solo puede modificarse internamente por la clase que lo
        /// expone.</remarks>
        public PricingStatus Status { get; private set; }

        /// <summary>
        /// Precio unitario del artículo representado como un objeto Money; puede ser null si no se ha establecido.
        /// </summary>
        /// <remarks>Contiene tanto la cantidad como la moneda. El setter es privado; modifique el valor
        /// mediante las operaciones de dominio correspondientes.</remarks>
        public Money? UnitPrice { get; private set; }

        public MeasurementUnit? QuotedUnit { get; private set; }

        public int? UnitRevision { get; private set; }



        /// <summary>
        /// Nombre del proveedor asociado a la entidad o configuración.
        /// </summary>
        /// <remarks>Es nulo si no se ha establecido. Se asigna internamente y tiene un setter privado,
        /// por lo que no puede modificarse desde fuera.</remarks>
        public string? ProviderName { get; private set; }

        /// <summary>
        /// Fuente de los datos de tarificación que determina el origen usado para calcular o recuperar precios.
        /// </summary>
        /// <remarks>Propiedad de solo lectura pública con setter privado; se establece durante
        /// operaciones de creación o actualización y condiciona la lógica de cálculo o consulta de precios.</remarks>
        public PricingSource Source { get; private set; }

        /// <summary>
        /// Marca temporal que indica el instante en que se realizó la consulta.
        /// </summary>
        /// <remarks>Se establece de forma privada por la entidad; puede usarse para auditoría, control de
        /// caché y ordenación temporal.</remarks>
        public DateTime QueriedAt { get; private set; }

        /// <summary>
        /// Nivel de confianza asociado a la entidad, representado por una instancia de Confidence.
        /// </summary>
        /// <remarks>Inicializada por defecto y modificable únicamente desde dentro del tipo (setter
        /// privado). Se garantiza no nula en tiempo de ejecución.</remarks>
        public Confidence Confidence { get; private set; } =
            default!;

        /// <summary>
        /// Indica si se utilizó el mecanismo de fallback.
        /// </summary>
        /// <remarks>Se establece de forma interna (setter privado) para reflejar que se recurrió a una
        /// alternativa de reserva y no debe modificarse desde fuera.</remarks>
        public bool IsFallback { get; private set; }

        /// <summary>
        /// Motivo del fallo de la operación, si está disponible.
        /// </summary>
        /// <remarks>Se establece internamente cuando una operación falla; será null si no hay fallo o no
        /// se ha proporcionado un motivo.</remarks>
        public string? FailureReason { get; private set; }

        /// <summary>
        /// Indica si hay un precio disponible y válido.
        /// </summary>
        /// <remarks>Devuelve <c>true</c> cuando <c>PricingStatus.Found</c> y <c>UnitPrice</c> no es
        /// <c>null</c>; en caso contrario, <c>false</c>.</remarks>
        public bool HasPrice =>
            Status == PricingStatus.Found &&
            UnitPrice is not null;

        /// <summary>
        /// Crea un Result exitoso que encapsula un APUComponentPricing con precio, proveedor, fuente, confianza y
        /// metadatos de creación.
        /// </summary>
        /// <remarks>Valida tenantId y componentId frente a Guid.Empty, comprueba unitPrice y confidence
        /// no nulos y exige una fuente válida. Los errores de validación se retornan como Result.Failure; no se lanzan
        /// excepciones para la lógica de negocio.</remarks>
        /// <param name="tenantId">Identificador del inquilino asociado al precio; no puede ser Guid.Empty.</param>
        /// <param name="componentId">Identificador del componente APU; no puede tener el valor Guid.Empty.</param>
        /// <param name="unitPrice">Precio unitario del componente; no puede ser null.</param>
        /// <param name="providerName">Nombre del proveedor opcional; se normaliza o queda null si no se suministra.</param>
        /// <param name="source">Origen de la cotización; debe ser distinto de PricingSource.None.</param>
        /// <param name="confidence">Nivel de confianza asociado al precio; no puede ser null.</param>
        /// <param name="isFallback">Indica si el precio procede de una fuente de fallback.</param>
        /// <param name="createdBy">Identificador opcional del usuario que creó el registro.</param>
        /// <param name="queriedAt">Fecha y hora en que se obtuvo la cotización; si es null se usa DateTime.UtcNow.</param>
        /// <returns>Result que contiene la instancia creada de APUComponentPricing en caso de éxito, o un Result de fallo con
        /// errores de validación en caso contrario.</returns>
        public static Result<APUComponentPricing> CreateSuccessful(
            Guid tenantId,
            APUComponentId componentId,
            Money unitPrice,
            MeasurementUnit quotedUnit,
            int unitRevision,
            string? providerName,
            PricingSource source,
            Confidence confidence,
            bool isFallback,
            Guid? createdBy = null,
            DateTime? queriedAt = null)
        {
            if (tenantId == Guid.Empty)
            {
                return ResultFactory.Failure<APUComponentPricing>(
                    ApplicationError.Validation(
                        "PROJ_APU_PRICING_TENANT_REQUIRED",
                        "The pricing tenant is required."));
            }

            if (componentId.Value == Guid.Empty)
            {
                return ResultFactory.Failure<APUComponentPricing>(
                    ApplicationError.Validation(
                        "PROJ_APU_PRICING_COMPONENT_REQUIRED",
                        "The priced component is required."));
            }

            ArgumentNullException.ThrowIfNull(unitPrice);
            ArgumentNullException.ThrowIfNull(confidence);

            if (source == PricingSource.None)
            {
                return ResultFactory.Failure<APUComponentPricing>(
                    ApplicationError.Validation(
                        "PROJ_APU_PRICING_SOURCE_REQUIRED",
                        "The pricing source is required."));
            }

            if (quotedUnit is null ||
                string.IsNullOrWhiteSpace(quotedUnit.Code) ||
                unitRevision < 0)
            {
                return ResultFactory.Failure<APUComponentPricing>(
                    ApplicationError.Validation(
                        "PROJ_APU_PRICING_UNIT_INVALID",
                        "A quoted unit and a valid unit revision are required."));
            }

            return ResultFactory.Success(
                new APUComponentPricing(
                    APUComponentPricingId.New(),
                    tenantId,
                    componentId,
                    PricingStatus.Found,
                    unitPrice,
                    quotedUnit,
                    unitRevision,
                    Normalize(providerName),
                    source,
                    queriedAt ?? DateTime.UtcNow,
                    confidence,
                    isFallback,
                    null,
                    createdBy));
        }

        /// <summary>
        /// Crea un Result que representa el precio de un componente con estado Unavailable tras una consulta fallida.
        /// </summary>
        /// <remarks>Valida tenantId, componentId y failureReason; genera un nuevo APUComponentPricingId.
        /// Asigna Confidence a 0; marca IsEstimated como true si la fuente no es McpProvider; queriedAt se normaliza a
        /// UtcNow si no se proporciona.</remarks>
        /// <param name="tenantId">Identificador del tenant propietario del precio; obligatorio (no Guid.Empty).</param>
        /// <param name="componentId">Identificador del componente APU; obligatorio (no Guid.Empty).</param>
        /// <param name="source">Origen de la consulta de precios (PricingSource).</param>
        /// <param name="failureReason">Descripción del motivo del fallo; obligatoria y se recorta de espacios en blanco.</param>
        /// <param name="createdBy">Identificador opcional del usuario que creó el registro.</param>
        /// <param name="queriedAt">Fecha y hora opcional de la consulta; si es null se utiliza DateTime.UtcNow.</param>
        /// <returns>Result que, en caso de éxito, contiene una instancia de APUComponentPricing con estado Unavailable; en caso
        /// de error devuelve Result con errores de validación.</returns>
        public static Result<APUComponentPricing> CreateUnavailable(
            Guid tenantId,
            APUComponentId componentId,
            PricingSource source,
            string failureReason,
            MeasurementUnit quotedUnit,
            int unitRevision,
            Guid? createdBy = null,
            DateTime? queriedAt = null)
        {
            if (tenantId == Guid.Empty)
            {
                return ResultFactory.Failure<APUComponentPricing>(
                    ApplicationError.Validation(
                        "PROJ_APU_PRICING_TENANT_REQUIRED",
                        "The pricing tenant is required."));
            }

            if (componentId.Value == Guid.Empty)
            {
                return ResultFactory.Failure<APUComponentPricing>(
                    ApplicationError.Validation(
                        "PROJ_APU_PRICING_COMPONENT_REQUIRED",
                        "The priced component is required."));
            }

            if (string.IsNullOrWhiteSpace(failureReason))
            {
                return ResultFactory.Failure<APUComponentPricing>(
                    ApplicationError.Validation(
                        "PROJ_APU_PRICING_FAILURE_REASON_REQUIRED",
                        "The pricing failure reason is required."));
            }

            if (source == PricingSource.None)
            {
                return ResultFactory.Failure<APUComponentPricing>(
                    ApplicationError.Validation(
                        "PROJ_APU_PRICING_SOURCE_REQUIRED",
                        "The pricing source is required."));
            }

            if (quotedUnit is null ||
                string.IsNullOrWhiteSpace(quotedUnit.Code) ||
                unitRevision < 0)
            {
                return ResultFactory.Failure<APUComponentPricing>(
                    ApplicationError.Validation(
                        "PROJ_APU_PRICING_UNIT_INVALID",
                        "A quoted unit and a valid unit revision are required."));
            }

            return ResultFactory.Success(
                new APUComponentPricing(
                    APUComponentPricingId.New(),
                    tenantId,
                    componentId,
                    PricingStatus.Unavailable,
                    null,
                    quotedUnit,
                    unitRevision,
                    null,
                    source,
                    queriedAt ?? DateTime.UtcNow,
                    new Confidence(0m),
                    source != PricingSource.McpProvider,
                    failureReason.Trim(),
                    createdBy));
        }

        public static Result<APUComponentPricing> AuxiliaryCostApply(Money pricing, APUComponentPricing componentPricing)
        {

            if (pricing is null)
            {
                return ResultFactory.Failure<APUComponentPricing>(
                    ApplicationError.Validation(
                        "PROJ_APU_PRICING_AUXILIARY_COST_REQUIRED",
                        "The auxiliary cost is required."));
            }

            if (componentPricing is null)
            {
                return ResultFactory.Failure<APUComponentPricing>(
                    ApplicationError.Validation(
                        "PROJ_APU_PRICING_COMPONENT_REQUIRED",
                        "The component pricing is required."));
            }

            componentPricing.UnitPrice = pricing;

            return ResultFactory.Success(componentPricing);


        }

        /// <summary>
        /// Normaliza una cadena eliminando los espacios iniciales y finales; devuelve null si la cadena es nula o
        /// consta únicamente de espacios en blanco.
        /// </summary>
        /// <param name="value">Cadena de entrada que se recortará; puede ser null o contener solo espacios.</param>
        /// <returns>La cadena recortada, o null si la entrada es null o está compuesta únicamente por espacios en blanco.</returns>
        private static string? Normalize(string? value) =>
            string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
    }
}

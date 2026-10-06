using Kynakee.Modules.Projects.Domain.Entities.Scoped.ConcreteComponent;
using Kynakee.Modules.Projects.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;
using Kynakee.Modules.SharedKernel.Domain;

namespace Kynakee.Modules.Projects.Domain.Entities.Scoped
{
    /// <summary>
    /// Snapshot del precio y el importe de una partida valorada.
    /// </summary>
    public sealed record ValuedWorkItem(
        WorkItemId WorkItemId,
        APUAssignmentId APUAssignmentId,
        decimal Quantity,
        Money DirectUnitCost,
        Money AuxiliaryUnitCost,
        Money TotalUnitPrice,
        Money TotalAmount);

    /// <summary>
    /// Valoración económica de un proyecto a partir del precio unitario de cada APU
    /// y de la medición de su WorkItem.
    /// </summary>
    public sealed class Valuation : BaseEntity<ValuationId>
    {
        private readonly List<ValuedComponent> _valuedComponents = [];
        private readonly List<ValuedWorkItem> _valuedWorkItems = [];

        private Valuation()
        {
        }

        private Valuation(
            ValuationId id,
            Guid tenantId,
            ProjectId projectId,
            IEnumerable<ValuedComponent> valuedComponents,
            IEnumerable<ValuedWorkItem> valuedWorkItems,
            Money directCost,
            Money auxiliaryCost,
            Confidence confidenceLevel,
            Guid? createdBy)
            : base(id, tenantId, createdBy)
        {
            _valuedComponents.AddRange(valuedComponents);
            _valuedWorkItems.AddRange(valuedWorkItems);

            ProjectId = projectId;
            DirectCost = directCost;
            AuxiliaryCost = auxiliaryCost;
            ConfidenceLevel = confidenceLevel;

            TotalCost = DirectCost.Add(AuxiliaryCost);
            ValuatedAt = DateTime.UtcNow;
        }

        public ProjectId ProjectId { get; private set; }

        /// <summary>
        /// Snapshots unitarios de los componentes directos que forman las APU.
        /// </summary>
        public IReadOnlyList<ValuedComponent> ValuedComponents =>
            _valuedComponents.AsReadOnly();

        /// <summary>
        /// Precio e importe calculados para cada partida del proyecto.
        /// </summary>
        public IReadOnlyList<ValuedWorkItem> ValuedWorkItems =>
            _valuedWorkItems.AsReadOnly();

        /// <summary>
        /// Coste directo total de las partidas, después de aplicar sus mediciones.
        /// </summary>
        public Money DirectCost { get; private set; } = Money.Zero;

        /// <summary>
        /// Coste auxiliar total de las partidas, después de aplicar sus mediciones.
        /// </summary>
        public Money AuxiliaryCost { get; private set; } = Money.Zero;

        public Money IndirectCost { get; private set; } = Money.Zero;

        public Money Administration { get; private set; } = Money.Zero;

        public Money Quality { get; private set; } = Money.Zero;

        public Money SafetyHealth { get; private set; } = Money.Zero;

        public Money Environment { get; private set; } = Money.Zero;

        public Money Contingency { get; private set; } = Money.Zero;

        public Money Profit { get; private set; } = Money.Zero;

        public Money VAT { get; private set; } = Money.Zero;

        public Money TotalCost { get; private set; } = Money.Zero;

        public Confidence ConfidenceLevel { get; private set; } = Confidence.Low;

        public bool IsComplete =>
            _valuedWorkItems.Count > 0 &&
            _valuedWorkItems.All(item => item.TotalAmount.Amount >= 0);

        public DateTime ValuatedAt { get; private set; }


        public static Result<Valuation> Create(
            Guid tenantId,
            ProjectId projectId,
            IEnumerable<APUAssignment> assignments,
            IReadOnlyDictionary<WorkItemId, decimal> workItemQuantities,
            Guid? createdBy = null)
        {
            if (tenantId == Guid.Empty)
            {
                return ResultFactory.Failure<Valuation>(
                    ApplicationError.Validation(
                        "PROJ_VALUATION_TENANT_REQUIRED",
                        "The valuation tenant is required."));
            }

            ArgumentNullException.ThrowIfNull(assignments);
            ArgumentNullException.ThrowIfNull(workItemQuantities);

            var assignmentList = assignments.ToArray();

            if (projectId.Value == Guid.Empty)
            {
                return ResultFactory.Failure<Valuation>(
                    ApplicationError.Validation(
                        "PROJ_VALUATION_PROJECT_REQUIRED",
                        "The valuation project identifier is required."));
            }

            if (assignmentList.Length == 0)
            {
                return ResultFactory.Failure<Valuation>(
                    ApplicationError.Validation(
                        "PROJ_VALUATION_ASSIGNMENTS_REQUIRED",
                        "At least one APU assignment is required to value the project."));
            }

            foreach (var assignment in assignmentList)
            {
                if (assignment.TenantId != tenantId)
                {
                    return ResultFactory.Failure<Valuation>(
                        ApplicationError.Conflict(
                            "PROJ_VALUATION_ASSIGNMENT_TENANT_MISMATCH",
                            "An APU assignment belongs to another tenant."));
                }

                if (assignment.ProjectId != projectId)
                {
                    return ResultFactory.Failure<Valuation>(
                        ApplicationError.Conflict(
                            "PROJ_VALUATION_ASSIGNMENT_PROJECT_MISMATCH",
                            "An APU assignment belongs to another project."));
                }
            }

            var assignedWorkItemIds = assignmentList
                .Select(assignment => assignment.WorkItemId)
                .ToHashSet(); // Use a HashSet to ensure uniqueness of work item IDs

            if (assignedWorkItemIds.Count != assignmentList.Length)
            {
                return ResultFactory.Failure<Valuation>(
                    ApplicationError.Conflict(
                        "PROJ_VALUATION_DUPLICATE_WORKITEM_ASSIGNMENT",
                        "A work item cannot have multiple APU assignments in one valuation."));
            }

            if (workItemQuantities.Count != assignedWorkItemIds.Count ||
                workItemQuantities.Keys.Any(
                    workItemId => !assignedWorkItemIds.Contains(workItemId)))
            {
                return ResultFactory.Failure<Valuation>(
                    ApplicationError.Validation(
                        "PROJ_VALUATION_WORKITEM_SET_MISMATCH",
                        "The work item quantities must match the APU assignments."));
            }

            var valuedComponents = new List<ValuedComponent>();
            var valuedWorkItems = new List<ValuedWorkItem>();

            Money? directCost = null;
            Money? auxiliaryCost = null;

            foreach (var assignment in assignmentList)
            {
                if (!workItemQuantities.TryGetValue(
                        assignment.WorkItemId,
                        out var workItemQuantity) ||
                    workItemQuantity <= 0)
                {
                    return ResultFactory.Failure<Valuation>(
                        ApplicationError.Validation(
                            "PROJ_VALUATION_WORKITEM_QUANTITY_REQUIRED",
                            $"No valid quantity exists for work item '{assignment.WorkItemId}'."));
                }

                var calculationResult = assignment.CalculateUnitPrice();

                if (!calculationResult.IsSuccess)
                {
                    return ResultFactory.Failure<Valuation>(
                        calculationResult.Error!);
                }

                var calculation = calculationResult.Value!;
                var directUnitCost = calculation.DirectUnitCost;
                var auxiliaryUnitCost = calculation.AuxiliaryUnitCost;

                if (!HasSameCurrency(directUnitCost, auxiliaryUnitCost))
                {
                    return ResultFactory.Failure<Valuation>(
                        ApplicationError.Validation(
                            "PROJ_VALUATION_APU_CURRENCY_MISMATCH",
                            "Direct and auxiliary APU costs must use the same currency."));
                }

                var totalUnitPrice = directUnitCost.Add(auxiliaryUnitCost); // Total unit price is the sum of direct and auxiliary costs
                
                var lineDirectCost = directUnitCost.Multiply(workItemQuantity);
                var lineAuxiliaryCost = auxiliaryUnitCost.Multiply(workItemQuantity);
                var lineTotalAmount = totalUnitPrice.Multiply(workItemQuantity);

                if (directCost is null)
                {
                    directCost = lineDirectCost;
                    auxiliaryCost = lineAuxiliaryCost;
                }
                else
                {
                    if (!HasSameCurrency(directCost, lineDirectCost) ||
                        auxiliaryCost is null ||
                        !HasSameCurrency(auxiliaryCost, lineAuxiliaryCost))
                    {
                        return ResultFactory.Failure<Valuation>(
                            ApplicationError.Validation(
                                "PROJ_VALUATION_CURRENCY_MISMATCH",
                                "All valued APU amounts must use the same currency."));
                    }

                    directCost = directCost.Add(lineDirectCost);
                    auxiliaryCost = auxiliaryCost.Add(lineAuxiliaryCost);
                }

                valuedComponents.AddRange(calculation.ComponentSnapshots);

                valuedWorkItems.Add(
                    new ValuedWorkItem(
                        assignment.WorkItemId,
                        assignment.Id,
                        workItemQuantity,
                        directUnitCost,
                        auxiliaryUnitCost,
                        totalUnitPrice,
                        lineTotalAmount));
            }

            if (directCost is null || auxiliaryCost is null)
            {
                return ResultFactory.Failure<Valuation>(
                    ApplicationError.Conflict(
                        "PROJ_VALUATION_COSTS_NOT_CALCULATED",
                        "The project costs could not be calculated."));
            }

            return ResultFactory.Success(
                new Valuation(
                    ValuationId.New(),
                    tenantId,
                    projectId,
                    valuedComponents,
                    valuedWorkItems,
                    directCost,
                    auxiliaryCost,
                    CalculateConfidence(valuedComponents),
                    createdBy));
        }

        internal Result ApplyAdjustments(
            Money indirectCost,
            Money administration,
            Money quality,
            Money safetyHealth,
            Money environment,
            Money contingency,
            Money profit,
            Money vat,
            Guid? updatedBy = null)
        {
            ArgumentNullException.ThrowIfNull(indirectCost);
            ArgumentNullException.ThrowIfNull(administration);
            ArgumentNullException.ThrowIfNull(quality);
            ArgumentNullException.ThrowIfNull(safetyHealth);
            ArgumentNullException.ThrowIfNull(environment);
            ArgumentNullException.ThrowIfNull(contingency);
            ArgumentNullException.ThrowIfNull(profit);
            ArgumentNullException.ThrowIfNull(vat);

            var currency = DirectCost.Currency;

            if (!AllSameCurrency(
                    currency,
                    AuxiliaryCost,
                    indirectCost,
                    administration,
                    quality,
                    safetyHealth,
                    environment,
                    contingency,
                    profit,
                    vat))
            {
                return ResultFactory.Failure(
                    ApplicationError.Validation(
                        "PROJ_VALUATION_CURRENCY_MISMATCH",
                        "All valuation amounts must use the same currency."));
            }

            IndirectCost = indirectCost;
            Administration = administration;
            Quality = quality;
            SafetyHealth = safetyHealth;
            Environment = environment;
            Contingency = contingency;
            Profit = profit;
            VAT = vat;

            TotalCost = DirectCost
                .Add(AuxiliaryCost)
                .Add(IndirectCost)
                .Add(Administration)
                .Add(Quality)
                .Add(SafetyHealth)
                .Add(Environment)
                .Add(Contingency)
                .Add(Profit)
                .Add(VAT);

            RegisterUpdate(updatedBy);

            return ResultFactory.Ok();
        }

        private static Confidence CalculateConfidence(
            IEnumerable<ValuedComponent> valuedComponents)
        {
            var confidenceValues = valuedComponents
                .Select(component => component.Confidence.Value)
                .ToArray();

            return confidenceValues.Length == 0
                ? Confidence.Low
                : new Confidence(confidenceValues.Average());
        }

        private static bool HasSameCurrency(
            Money first,
            Money second) =>
            string.Equals(
                first.Currency,
                second.Currency,
                StringComparison.OrdinalIgnoreCase);

        private static bool AllSameCurrency(
            string currency,
            params Money[] amounts) =>
            amounts.All(amount =>
                string.Equals(
                    currency,
                    amount.Currency,
                    StringComparison.OrdinalIgnoreCase));
    }
}

using Kynakee.Modules.Projects.Domain.Entities.Scoped;
using Kynakee.Modules.Projects.Domain.Entities.Scoped.ConcreteComponent;

namespace Kynakee.Modules.Projects.Domain.ValueObjects
{
    /// <summary>
    /// Resultado del cálculo unitario de una APU, con subtotales por grupo
    /// y snapshots de las contribuciones de los componentes directos.
    /// </summary>
    public sealed record APUUnitPriceCalculation(
        Money DirectUnitCost,
        Money AuxiliaryUnitCost,
        Money MaterialSubtotal,
        Money LaborSubtotal,
        Money EquipmentSubtotal,
        Money SubcontractSubtotal,
        Money TransportSubtotal,
        IReadOnlyCollection<ValuedComponent> ComponentSnapshots,
        IReadOnlyCollection<AuxiliaryMeansComponent> AuxiliaryMeansComponents)
    {
        /// <summary>
        /// Precio unitario completo de la APU, incluidos los costes auxiliares.
        /// </summary>
        public Money TotalUnitPrice =>
            DirectUnitCost.Add(AuxiliaryUnitCost);
    }
}

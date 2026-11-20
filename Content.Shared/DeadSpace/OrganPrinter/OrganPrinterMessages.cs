// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.OrganPrinter;

[Serializable, NetSerializable]
public enum OrganPrinterUiKey : byte
{
    Key,
}

/// <summary>
/// Запись о печатаемом органе для UI.
/// </summary>
[Serializable, NetSerializable]
public sealed record OrganPrinterProduct(EntProtoId Product, int Cost, OrganSpeciesGroup Group);

[Serializable, NetSerializable]
public sealed class OrganPrinterUpdateState : BoundUserInterfaceState
{
    public List<OrganPrinterProduct> Products;

    /// <summary>
    /// Доступно биомассы в хранилище машины.
    /// </summary>
    public int Biomass;

    public bool Printing;

    public OrganPrinterUpdateState(List<OrganPrinterProduct> products, int biomass, bool printing)
    {
        Products = products;
        Biomass = biomass;
        Printing = printing;
    }
}

/// <summary>
/// Клиент просит напечатать выбранный орган.
/// </summary>
[Serializable, NetSerializable]
public sealed class OrganPrinterPrintMessage : BoundUserInterfaceMessage
{
    public readonly EntProtoId Product;
    public readonly OrganSpeciesGroup Group;

    public OrganPrinterPrintMessage(EntProtoId product, OrganSpeciesGroup group)
    {
        Product = product;
        Group = group;
    }
}

/// <summary>
/// Запрос на извлечение всей биомассы из хранилища принтера.
/// </summary>
[Serializable, NetSerializable]
public sealed class OrganPrinterEjectMessage : BoundUserInterfaceMessage
{
}


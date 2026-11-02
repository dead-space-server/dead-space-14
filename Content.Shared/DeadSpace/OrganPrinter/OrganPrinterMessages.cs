// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Chemistry.Reagent;
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
/// NeedsParticles - тратит ли изделие частицу вида.
/// </summary>
[Serializable, NetSerializable]
public sealed record OrganPrinterProduct(EntProtoId Product, int Cost, OrganSpeciesGroup Group, bool NeedsParticles);

/// <summary>
/// Залитые в машину частицы вида и их количество.
/// </summary>
[Serializable, NetSerializable]
public sealed record OrganPrinterSpecies(OrganSpeciesGroup Group, ProtoId<ReagentPrototype> Reagent, float Amount);

[Serializable, NetSerializable]
public sealed class OrganPrinterUpdateState : BoundUserInterfaceState
{
    public List<OrganPrinterProduct> Products;

    /// <summary>
    /// Доступно биомассы в хранилище машины.
    /// </summary>
    public int Biomass;

    /// <summary>
    /// Частицы вида, залитые в машину.
    /// </summary>
    public List<OrganPrinterSpecies> Species;

    /// <summary>
    /// Сколько единиц частиц уходит на одну попытку печати.
    /// </summary>
    public float ParticlesPerPrint;

    public OrganPrinterUpdateState(List<OrganPrinterProduct> products, int biomass, List<OrganPrinterSpecies> species, float particlesPerPrint)
    {
        Products = products;
        Biomass = biomass;
        Species = species;
        ParticlesPerPrint = particlesPerPrint;
    }
}

/// <summary>
/// Запрос на печать органа после мини-игры.
/// Качество (0-100) - итог сплайсинга, отходы - лишние кубы вне силуэта органа.
/// </summary>
[Serializable, NetSerializable]
public sealed class OrganPrinterPrintRequestMessage : BoundUserInterfaceMessage
{
    public readonly EntProtoId Product;
    public readonly OrganSpeciesGroup Group;
    public readonly int Quality;
    public readonly int Waste;

    public OrganPrinterPrintRequestMessage(EntProtoId product, OrganSpeciesGroup group, int quality, int waste)
    {
        Product = product;
        Group = group;
        Quality = quality;
        Waste = waste;
    }
}

/// <summary>
/// Сообщение о провале мини-игры: принтер повреждается. Быстрая печать бьёт сильнее.
/// </summary>
[Serializable, NetSerializable]
public sealed class OrganPrinterGameFailedMessage : BoundUserInterfaceMessage
{
    public readonly EntProtoId Product;
    public readonly OrganSpeciesGroup Group;
    public readonly bool FastMode;

    public OrganPrinterGameFailedMessage(EntProtoId product, OrganSpeciesGroup group, bool fastMode)
    {
        Product = product;
        Group = group;
        FastMode = fastMode;
    }
}

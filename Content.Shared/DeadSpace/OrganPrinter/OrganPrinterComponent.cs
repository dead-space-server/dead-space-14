// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Materials;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.OrganPrinter;

[Serializable, NetSerializable]
public enum OrganPrinterVisuals : byte
{
    Working,
}

/// <summary>
/// Группы рас для печати органов: деление используется в интерфейсе принтера и хирургии.
/// Группы собраны по факту использования органов в прототипах тел рас.
/// </summary>
[Serializable, NetSerializable]
public enum OrganSpeciesGroup : byte
{
    Humanoid = 0,
    Feline = 1,
    Canine = 2,
    Reptilian = 3,
    Moth = 4,
    Kobolt = 5,
    Shark = 6,
    Demon = 7,
    Felinid = 8,
    Vox = 9,
    Arachnid = 10,
}

/// <summary>
/// Орган, доступный для печати на ОрганоМехе, и его стоимость в биомассе.
/// </summary>
[DataDefinition]
public sealed partial class OrganPrinterEntry
{
    /// <summary>
    /// Прототип печатаемого органа.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Product;

    /// <summary>
    /// Сколько единиц биомассы уходит на один орган.
    /// </summary>
    [DataField(required: true)]
    public int Cost;

    /// <summary>
    /// Группа рас, для чьих тел подходит орган.
    /// Один орган может быть указан в нескольких группах отдельными записями.
    /// </summary>
    [DataField]
    public OrganSpeciesGroup Group = OrganSpeciesGroup.Humanoid;
}

/// <summary>
/// Принтер органов, печатающий их из биомассы (материал в MaterialStorage).
/// </summary>
[RegisterComponent]
public sealed partial class OrganPrinterComponent : Component
{
    /// <summary>
    /// Список органов, которые можно напечатать.
    /// </summary>
    [DataField]
    public List<OrganPrinterEntry> Entries = new();

    /// <summary>
    /// Материал, из которого печатаются органы.
    /// </summary>
    [DataField]
    public ProtoId<MaterialPrototype> Material = "Biomass";

    /// <summary>
    /// Сколько длится печать одного органа.
    /// </summary>
    [DataField]
    public TimeSpan PrintDuration = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Орган, печатаемый прямо сейчас. null, если принтер свободен.
    /// </summary>
    [ViewVariables]
    public EntProtoId? PrintingProduct;

    /// <summary>
    /// Момент окончания текущей печати.
    /// </summary>
    [ViewVariables]
    public TimeSpan PrintEndTime;

    [ViewVariables]
    public OrganSpeciesGroup PrintGroup;

    [DataField]
    public Dictionary<OrganSpeciesGroup, string> GroupSpecies = new()
    {
        [OrganSpeciesGroup.Humanoid] = "Human",
        [OrganSpeciesGroup.Feline] = "Tajaran",
        [OrganSpeciesGroup.Canine] = "Vulpkanin",
        [OrganSpeciesGroup.Reptilian] = "Reptilian",
        [OrganSpeciesGroup.Moth] = "Moth",
        [OrganSpeciesGroup.Kobolt] = "Kobolt",
        [OrganSpeciesGroup.Shark] = "Shark",
        [OrganSpeciesGroup.Demon] = "Demon",
        [OrganSpeciesGroup.Felinid] = "Felinid",
        [OrganSpeciesGroup.Vox] = "Vox",
        [OrganSpeciesGroup.Arachnid] = "Arachnid",
    };

    /// <summary>
    /// Звук печати органа.
    /// </summary>
    [DataField]
    public SoundSpecifier? PrintSound = new SoundPathSpecifier("/Audio/Machines/diagnoser_printing.ogg");
}

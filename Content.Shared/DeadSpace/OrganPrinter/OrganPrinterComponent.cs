// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Materials;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.OrganPrinter;

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
    /// Сколько биомассы стоит один поглощённый куб вне силуэта органа.
    /// </summary>
    [DataField]
    public float WasteCost = 0.1f;

    /// <summary>
    /// Звук печати органа.
    /// </summary>
    [DataField]
    public SoundSpecifier? PrintSound = new SoundPathSpecifier("/Audio/Machines/diagnoser_printing.ogg");
}

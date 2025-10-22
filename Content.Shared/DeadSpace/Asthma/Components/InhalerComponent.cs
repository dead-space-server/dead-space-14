// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameStates;
using Robust.Shared.Audio;
using Content.Shared.FixedPoint;

namespace Content.Shared.DeadSpace.Asthma.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class InhalerComponent : Component
{
    [DataField]
    public SoundSpecifier? Sound;

    [DataField]
    public float UseDuration = 1f;

    [DataField]
    public string SolutionName = "inhaler";

    /// <summary>
    ///     Потребление реагента.
    /// </summary>
    [DataField]
    public FixedPoint2 Quantity = FixedPoint2.New(5f);

    [DataField]
    public List<string> AsthmaMedicineWhitelist { get; set; } = new()
    {
        "Dexalin",
        "DexalinPlus"
    };
}

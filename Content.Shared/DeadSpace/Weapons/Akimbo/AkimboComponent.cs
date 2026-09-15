// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Hands.Components;
using Content.Shared.Tag;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Weapons.Akimbo;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AkimboComponent : Component
{
    /// <summary>
    /// Multiplier applied to the delay between alternating shots while both guns have ammunition.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float FireDelayMultiplier = 0.7f;

    /// <summary>
    /// Accuracy retained by both weapons while akimbo is active. 0.6 means 60% accuracy.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float AkimboAccuracy = 0.6f;

    /// <summary>
    /// Optional tag that permits different prototypes to form a pair.
    /// Both weapons must have the same configured tag and the actual entity tag.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<TagPrototype>? MatchTag;

    [AutoNetworkedField]
    public HandLocation NextHand = HandLocation.Left;

    [AutoNetworkedField]
    public TimeSpan NextPairFire;
}

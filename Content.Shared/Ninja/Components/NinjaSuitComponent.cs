using Content.Shared.Actions;
using Content.Shared.Ninja.Systems;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Ninja.Components;

/// <summary>
/// Component for ninja suit abilities and power consumption.
/// As an implementation detail, dashing with katana is a suit action which isn't ideal.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedNinjaSuitSystem))]
public sealed partial class NinjaSuitComponent : Component
{
    /// <summary>
    /// Sound played when a ninja is hit while cloaked.
    /// </summary>
    [DataField]
    public SoundSpecifier RevealSound = new SoundPathSpecifier("/Audio/Effects/chime.ogg");

    /// <summary>
    /// ID of the use delay to disable all ninja abilities.
    /// </summary>
    [DataField]
    public string DisableDelayId = "suit_powers";

    /// <summary>
    /// The action id for recalling a bound energy katana
    /// </summary>
    [DataField]
    public EntProtoId RecallKatanaAction = "ActionRecallKatana";

    [DataField, AutoNetworkedField]
    public EntityUid? RecallKatanaActionEntity;

    //DS14-start
    [DataField]
    public EntProtoId OpenSpiderOSAction = "SpiderOSAction";

    [DataField, AutoNetworkedField]
    public EntityUid? OpenSpiderOSActionEntity;
    //DS14-end

    /// <summary>
    /// Battery charge used per tile the katana teleported.
    /// Uses 1% of a default battery per tile.
    /// </summary>
    [DataField]
    public float RecallCharge = 3.6f;

    // DS14-start
    /// <summary>
    /// If the katana recall cost would be equal to or higher than the current battery capacity,
    /// recalling it costs this fraction of the current battery capacity instead.
    /// </summary>
    [DataField]
    public float RecallOverMaxChargeRatio = 0.9f;
    // DS14-end
}

public sealed partial class RecallKatanaEvent : InstantActionEvent;
public sealed partial class OpenSpiderOSEvent : InstantActionEvent;

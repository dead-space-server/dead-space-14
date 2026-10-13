using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Smokables;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ShishaHoseComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Base;

    /// <summary>Server-side pending puff; never persist an in-progress interaction.</summary>
    public DoAfterId? Puff;
}

[Serializable, NetSerializable]
public sealed partial class ShishaPuffDoAfterEvent : SimpleDoAfterEvent;

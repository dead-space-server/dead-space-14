using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Ninja.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NinjaHolographicClonesComponent : Component
{
    [DataField]
    public EntProtoId HolographicClonesAction = "NinjaHolographicClonesAction";

    [DataField, AutoNetworkedField]
    public EntityUid? HolographicClonesActionEntity;

    [DataField]
    public EntProtoId CloneProto = "HolographicNinjaClone";

    [DataField]
    public float EnergyCost = 50f;

    [DataField]
    public int CloneAmount = 3;

    [DataField]
    public float MinSpawnRadius = 1f;

    [DataField]
    public float MaxSpawnRadius = 3f;
}

public sealed partial class NinjaHolographicClonesActionEvent : InstantActionEvent;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Ninja.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NinjaSuitHeatComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Heat = 0f;

    [DataField]
    public float MaxHeat = 400;

    [DataField]
    public float HeatRate = 5f;

    [DataField]
    public float CoolRate = 10f;

    [DataField]
    public float EffectsThreshold = 200f;

    [DataField]
    public float DangerThreshold = 300f;

    [DataField]
    public EntProtoId EffectPrototype = "EffectSparks";

    [ViewVariables]
    public float LastSentHeat = 0f;
}
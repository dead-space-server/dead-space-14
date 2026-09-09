using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.AshWalkers;

[RegisterComponent, NetworkedComponent]
public sealed partial class AshWalkerRitualEffectComponent : Component
{
    [DataField]
    public float HuntMultiplier = 1;

    [DataField]
    public FixedPoint2 HealingBudget;

    [DataField]
    public FixedPoint2 HealingPerTick = 1;

    [DataField]
    public TimeSpan HealingInterval = TimeSpan.FromSeconds(5);

    [DataField]
    public float Elapsed;
}

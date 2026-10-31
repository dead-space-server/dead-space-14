namespace Content.Shared.DeadSpace.Ninja.Components;

[RegisterComponent]
public sealed partial class NinjaStealMoneyConditionComponent : Component
{
    [DataField] public int CurrentCount;
    [DataField] public int TargetCount;
    [DataField] public float PercentageOfMaximumBalance = 0.7f;
}

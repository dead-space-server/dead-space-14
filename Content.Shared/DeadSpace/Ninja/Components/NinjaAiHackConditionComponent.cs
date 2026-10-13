namespace Content.Shared.DeadSpace.Ninja.Components;

[RegisterComponent]
public sealed partial class NinjaAiHackConditionComponent : Component
{
    [DataField]
    public bool Hacked;

    [DataField]
    public EntityUid? Mind;
}

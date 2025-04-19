namespace Content.Server.DeadSpace.Armutant.Objectives.SocialInteractObjective;

[RegisterComponent]
public sealed partial class ObjectiveAlivePersonComponent : Component
{
    [DataField]
    public bool RequireAlive = false;
}

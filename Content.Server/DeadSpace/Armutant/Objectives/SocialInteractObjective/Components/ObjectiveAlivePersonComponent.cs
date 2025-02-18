namespace Content.Server.DeadSpace.Armutant.Objectives.SocialInteractObjective;

[RegisterComponent, Access(typeof(ObjectiveAliveRandomPersonSystem))]
public sealed partial class ObjectiveAlivePersonComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool RequireAlive = false;
}

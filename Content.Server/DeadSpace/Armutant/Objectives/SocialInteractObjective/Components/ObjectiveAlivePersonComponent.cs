using Content.Server.DeadSpace.Armutant.Objectives.SocialInteractObjective;

namespace Content.Server.DeadSpace.Armutant.Objectives;

[RegisterComponent, Access(typeof(ObjectiveAliveRandomPersonSystem))]
public sealed partial class ObjectiveAlivePersonComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool RequireAlive = false;
}

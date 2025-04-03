using Content.Server.DeadSpace.Armutant.Objectives.SocialInteractObjective;

namespace Content.Server.DeadSpace.Armutant.Objectives;

[RegisterComponent, Access(typeof(ObjectiveKillOneTrySystem))]
public sealed partial class ObjectiveKillOneTryComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool RequireDie = false;

    public bool OneTry = false;
}

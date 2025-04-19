namespace Content.Server.DeadSpace.Armutant.Objectives.SocialInteractObjective;

[RegisterComponent]
public sealed partial class ObjectiveKillOneTryComponent : Component
{
    [DataField]
    public bool RequireDie = false;

    public bool OneTry = false;
}

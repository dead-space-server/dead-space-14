namespace Content.Server.Silicons.AiBorgControl;

[RegisterComponent]
public sealed partial class AiControlledBorgComponent : Component
{
    public EntityUid Ai;
    public EntityUid? ReturnActionEntity;
}

[RegisterComponent]
public sealed partial class AiDeployedInBorgComponent : Component
{
    public EntityUid Borg;
}

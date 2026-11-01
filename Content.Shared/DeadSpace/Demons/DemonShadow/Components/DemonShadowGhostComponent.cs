using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Demons.DemonShadow.Components;

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class DemonShadowGhostComponent : Component
{
    [DataField]
    public string ActionShadowSpawn = "ActionShadowSpawn";

    [DataField, AutoNetworkedField]
    public EntityUid? ActionShadowSpawnEntity;

    [DataField]
    public string ShadowMobSpawnId = "MobDemonShadow";
}

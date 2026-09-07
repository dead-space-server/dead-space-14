using Content.Shared.Whitelist;
using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.AshWalkers;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AshWalkerComponent : Component
{
    [DataField, AutoNetworkedField]
    public float FaunaDamageMultiplier = 2.5f;

    [DataField, AutoNetworkedField]
    public EntityWhitelist GunWhitelist = new();

    [DataField, AutoNetworkedField]
    public EntityWhitelist FootwearWhitelist = new();
}

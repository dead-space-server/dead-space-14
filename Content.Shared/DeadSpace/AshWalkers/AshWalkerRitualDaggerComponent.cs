using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.AshWalkers;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AshWalkerRitualDaggerComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? HuntAction;

    [DataField, AutoNetworkedField]
    public EntityUid? MendingAction;

    [DataField, AutoNetworkedField]
    public EntityUid? ReturnAction;

    [DataField]
    public TimeSpan DrawTime = TimeSpan.FromSeconds(4);
}

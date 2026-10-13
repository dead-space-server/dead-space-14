using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Ninja.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SelfHealthAnalyzerComponent : Component
{
    [DataField]
    public EntProtoId Action = "SelfAnalyzeAction";

    [DataField, AutoNetworkedField]
    public EntityUid? ActionEntity;
}
public sealed partial class SelfAnalyzeActionEvent : InstantActionEvent;
using Content.Shared.Body.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Changeling.Components;

/// <summary>
/// Grants a metabolizer type to the owner's organs on map initialization.
/// </summary>
[RegisterComponent, ComponentProtoName("AddMetabolism"), NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AddMetabolismComponent : Component
{
    [DataField, AutoNetworkedField]
    public ProtoId<MetabolizerTypePrototype>? AddedMetabolizer;
}

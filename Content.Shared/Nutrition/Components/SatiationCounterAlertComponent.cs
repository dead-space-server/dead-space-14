using Content.Shared.Alert.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Nutrition.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Nutrition.Components;

/// <summary>
/// Selects the satiation type displayed by a generic counter alert.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SatiationCounterAlertComponent : Component
{
    [DataField]
    public ProtoId<SatiationTypePrototype> SatiationType = SatiationSystem.Hunger;
}

using Content.Shared.FixedPoint;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Nutrition.Prototypes;
using Content.Shared.Popups;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Nutrition.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SatiationSystem))]
public sealed partial class ActionRequireSatiationComponent : Component
{
    [DataField(required: true)]
    public ProtoId<SatiationTypePrototype> Satiation = SatiationSystem.Hunger;

    [DataField, AutoNetworkedField]
    public FixedPoint2 Amount = 10f;

    [DataField, AutoNetworkedField]
    public bool Spend = true;

    [DataField, AutoNetworkedField]
    public LocId? FailReason = "satiation-not-enough-hunger";

    [DataField, AutoNetworkedField]
    public PopupType FailReasonType = PopupType.SmallCaution;
}

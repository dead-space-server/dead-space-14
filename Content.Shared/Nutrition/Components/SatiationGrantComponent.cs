using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Nutrition.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Nutrition.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(SatiationGrantSystem))]
public sealed partial class SatiationGrantComponent : Component
{
    [DataField(required: true), AutoNetworkedField, AlwaysPushInheritance]
    public Dictionary<ProtoId<SatiationTypePrototype>, Satiation> Satiation = new();

    [DataField, AutoNetworkedField]
    public bool RemoveOnShutdown = true;
}

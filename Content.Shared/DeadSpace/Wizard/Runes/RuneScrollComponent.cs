// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Wizard.Runes;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RuneScrollComponent : Component
{
    [DataField, AutoNetworkedField]
    public List<ProtoId<RunePrototype>> Runes = new();

    [DataField, AutoNetworkedField]
    public EntityUid? BoundTo;

    [DataField, AutoNetworkedField]
    public EntityUid? Action;

    [DataField]
    public EntProtoId ActionPrototype = "ActionWizardRuneScroll";

    [DataField]
    public float MisfireChance = 0.6f;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RuneScrollActionComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Scroll;
}

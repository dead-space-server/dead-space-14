// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Alert;
using Content.Shared.DeadSpace.Changeling.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Changeling.Components;

/// <summary>
///     Marks a changeling that has evolved the Stasis Cocoon ability.
///     While in regenerative stasis, the changeling is wrapped in a cocoon
///     of leftover flesh that prevents other people from stripping or searching them.
///     The cocoon provides no damage protection, but the body can still be dragged around.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(ChangelingCocoonAbilitySystem))]
public sealed partial class ChangelingCocoonAbilityComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Enabled = true;

    [DataField]
    public ProtoId<AlertPrototype> AlertId = "ChangelingCocoon";
}

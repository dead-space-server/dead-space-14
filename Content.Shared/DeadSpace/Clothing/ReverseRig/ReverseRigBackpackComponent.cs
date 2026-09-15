// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Clothing.ReverseRig;

/// <summary>
///     Placed on a Reverse RIG backpack. Marks the backpack as permanently attached to a suit: manual unequip
///     attempts are cancelled, and the backpack is only ever removed by the suit it is attached to.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ReverseRigBackpackComponent : Component
{
    /// <summary>
    ///     The suit this backpack is attached to.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? AttachedUid;

    /// <summary>
    ///     Tank that supplied the gas currently held in the backpack buffer.
    ///     This is server-side bookkeeping used to return the reserve to the correct tank when it is removed.
    /// </summary>
    [ViewVariables]
    public EntityUid? BufferSourceUid;
}

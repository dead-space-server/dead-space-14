// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.TheCircle.Geist;

/// <summary>
/// An item a geist's thread is tied to. Only the one who tied it gets this component's state (and sees the thread);
/// to everyone else the item is just an item.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GeistThreadComponent : Component
{
    /// <summary>The spool the thread runs from.</summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Spool;

    /// <summary>Who tied it: the only one who sees the thread.</summary>
    [DataField, AutoNetworkedField]
    public EntityUid? TiedBy;

    public override bool SessionSpecific => true;
}

/// <summary>
/// The geist's thread spool: used on an item it ties a thread to it. The item stays where it is left; with the use
/// key held down and the spool in hand, it is reeled in. The thread snaps past its reach or behind a wall.
/// </summary>
[RegisterComponent]
public sealed partial class GeistThreadSpoolComponent : Component
{
    /// <summary>The item the thread is tied to.</summary>
    [ViewVariables]
    public EntityUid? Target;

    /// <summary>Who is reeling it in right now, holding the use key.</summary>
    [ViewVariables]
    public EntityUid? Reeler;

    /// <summary>Past this, or with a wall between, the thread snaps.</summary>
    [DataField]
    public float MaxRange = 10f;

    /// <summary>How fast the reeled item is drawn in.</summary>
    [DataField]
    public float ReelSpeed = 5f;

    /// <summary>Reeled in this close, it stops.</summary>
    [DataField]
    public float ReelStop = 1f;
}

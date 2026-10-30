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
    public EntityUid? Owner;

    public override bool SessionSpecific => true;
}

/// <summary>
/// The geist's thread spool: used on an item it ties a thread to it. Held in a hand, the item is dragged after its
/// holder once further away than the thread's slack; used in the hand, it reels the item in. The thread snaps past
/// its reach or behind a wall.
/// </summary>
[RegisterComponent]
public sealed partial class GeistThreadSpoolComponent : Component
{
    /// <summary>The item the thread is tied to.</summary>
    [ViewVariables]
    public EntityUid? Target;

    /// <summary>Being reeled in, until it is close.</summary>
    [ViewVariables]
    public bool Reeling;

    /// <summary>Slack: no farther than this the item lies still.</summary>
    [DataField]
    public float Length = 2.5f;

    /// <summary>Past this, or with a wall between, the thread snaps.</summary>
    [DataField]
    public float MaxRange = 10f;

    [DataField]
    public float DragSpeed = 5f;

    [DataField]
    public float ReelSpeed = 9f;

    /// <summary>Reeled in this close, it stops.</summary>
    [DataField]
    public float ReelStop = 1f;
}

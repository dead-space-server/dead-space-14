using Content.Shared.Alert;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Lavaland.Components;

/// <summary>
/// The Hierophant staff. Alt + right click: on the floor - the Hierophant blink; on a creature - the blessing that
/// turns its tile-by-tile walk on or off. Both show their cooldowns as alerts while the staff is held.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class LavalandHierophantStaffComponent : Component
{
    // DS14: the blink lives on Alt + right click instead of an action button.

    [DataField]
    public LavalandHierophantStaffBlinkActionEvent Blink = new();

    [DataField]
    public TimeSpan BlinkCooldown = TimeSpan.FromSeconds(14);

    [DataField]
    public float BlinkRange = 9f;

    [DataField]
    public ProtoId<AlertPrototype> BlinkAlert = "LavalandHierophantStaffBlink";

    /// <summary>Casting the blessing.</summary>
    [DataField]
    public TimeSpan BlessTime = TimeSpan.FromSeconds(5);

    /// <summary>A new blessing (turning the walk on) is ready this long after the last one.</summary>
    [DataField]
    public TimeSpan BlessCooldown = TimeSpan.FromSeconds(200);

    [DataField]
    public float BlessRange = 1.5f;

    [ViewVariables]
    public TimeSpan NextBlink;

    [ViewVariables]
    public TimeSpan NextBless;
}

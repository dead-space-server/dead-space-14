// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Lavaland.AshWolf;

[Serializable, NetSerializable]
public enum LavalandAshWolfVisuals : byte
{
    State,
}

[Serializable, NetSerializable]
public enum LavalandAshWolfLayers : byte
{
    Body,
    Eyes,
}

/// <summary>How the Ash Wolf looks right now.</summary>
[Serializable, NetSerializable]
public enum LavalandAshWolfVisualState : byte
{
    /// <summary>Lying on its side, eyes dark: the abandoned borg the storm brought.</summary>
    Dormant,
    Normal,

    /// <summary>Curled up into a rolling blade wheel (the rush, the pounce).</summary>
    Roll,
    Shield,

    /// <summary>Not seen at all (up in the sky).</summary>
    Hidden,

    /// <summary>Only its eyes glow out of the dust.</summary>
    EyesOnly,
}

/// <summary>
/// The eyes of the Ash Wolf (its <see cref="LavalandAshWolfLayers.Eyes"/> layer) are drawn once more above the
/// weather, so they are seen through the ash of the strong storm.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class LavalandAshWolfEyesComponent : Component;

/// <summary>
/// The tamed Ash Wolf borg: its eyes (the borg's light layer) follow its body, rolled up during the attacks.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class LavalandAshWolfBorgComponent : Component
{
    [DataField]
    public string EyesState = "eyes-bladewolfmk2";

    [DataField]
    public string RollEyesState = "eyes-bladewolfmk2-roll";
}

/// <summary>
/// Server to the pinned player: the Ash Wolf is on top of you, a quick time event starts. The client plays it on its
/// own clock - a needle goes round a dial <see cref="Checks"/> times, and LMB, RMB or Space must be pressed while it is
/// in the bright zone each time; a press outside the zone or a full turn without one fails it.
/// </summary>
[Serializable, NetSerializable]
public sealed class LavalandAshWolfQteStartEvent : EntityEventArgs
{
    public int Id;
    public float Duration;

    /// <summary>Seconds before the first turn of the needle.</summary>
    public float FirstDelay;

    /// <summary>One full turn of the needle.</summary>
    public float TurnTime;

    /// <summary>Seconds between the end of one check and the start of the next.</summary>
    public float Gap;

    /// <summary>Where each check's zone starts, as a fraction of a turn.</summary>
    public List<float> ZoneStarts = new();

    /// <summary>Width of the zone, as a fraction of a turn.</summary>
    public float ZoneWidth;

    public int Checks => ZoneStarts.Count;
}

/// <summary>Client to server: how the quick time event went. Taken only from the pinned player and in time.</summary>
[Serializable, NetSerializable]
public sealed class LavalandAshWolfQteResultEvent(int id, bool success) : EntityEventArgs
{
    public int Id = id;
    public bool Success = success;
}

/// <summary>Server to the pinned player: the quick time event is over (the server decided first), hide it.</summary>
[Serializable, NetSerializable]
public sealed class LavalandAshWolfQteStopEvent(int id) : EntityEventArgs
{
    public int Id = id;
}

/// <summary>The rush of the tamed Ash Wolf (a borg now): a blade wheel at where it looks.</summary>
public sealed partial class LavalandAshWolfRushActionEvent : WorldTargetActionEvent;

/// <summary>The leap of the tamed Ash Wolf: up and down on the spot, saws spiral away from the landing.</summary>
public sealed partial class LavalandAshWolfLeapActionEvent : WorldTargetActionEvent;

/// <summary>The tamed Ash Wolf jumps back and throws three saws at where it looks.</summary>
public sealed partial class LavalandAshWolfRetreatActionEvent : WorldTargetActionEvent;

/// <summary>The tamed Ash Wolf pounces and pins whoever it lands on.</summary>
public sealed partial class LavalandAshWolfPounceActionEvent : WorldTargetActionEvent;

/// <summary>The tamed Ash Wolf switches how it fights: its blades up close, or now and then a saw shot from afar.</summary>
public sealed partial class LavalandAshWolfModeActionEvent : InstantActionEvent;

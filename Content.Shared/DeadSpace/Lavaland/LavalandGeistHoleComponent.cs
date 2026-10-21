// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Lavaland;

[Serializable, NetSerializable]
public enum LavalandGeistHolePhase : byte
{
    /// <summary>Climbing half out of the hole.</summary>
    Rising,
    /// <summary>Half out, waist-deep in it.</summary>
    Holding,
    /// <summary>Sinking back in, fading away.</summary>
    Sinking,
    /// <summary>Not drawn at all: about to be deleted, so nothing of it (or of its clothes) is seen going.</summary>
    Hidden,
}

/// <summary>A geist in its hole: drawn sunk to the waist - rising out of it, standing in it or sinking back.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LavalandGeistHoleComponent : Component
{
    [DataField, AutoNetworkedField]
    public LavalandGeistHolePhase Phase;

    [DataField, AutoNetworkedField]
    public TimeSpan PhaseStart;

    [DataField, AutoNetworkedField]
    public float Duration = 1f;

    /// <summary>
    /// Shot down: while sinking it is thrown back this way (tiles) and tips over, away from the shooter.
    /// Zero - it just sinks.
    /// </summary>
    [DataField, AutoNetworkedField]
    public System.Numerics.Vector2 Knock;
}

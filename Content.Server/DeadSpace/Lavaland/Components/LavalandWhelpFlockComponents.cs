// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Lavaland.Components;

/// <summary>
/// Invisible trigger on lavaland: a player who walks up to it gets a flock of little whelps just beyond the edge of
/// his screen. Once he sees them, the whole flock scatters away.
/// </summary>
[RegisterComponent]
public sealed partial class LavalandWhelpFlockTriggerComponent : Component
{
    [DataField]
    public EntProtoId Prototype = "MobLavalandWhelpFlockling";

    [DataField]
    public int MinCount = 6;

    [DataField]
    public int MaxCount = 11;

    /// <summary>The flock sits within this radius of the trigger.</summary>
    [DataField]
    public float FlockRadius = 3f;

    /// <summary>Kept for old maps; the flock now appears when a player comes within <see cref="TriggerRadius"/>.</summary>
    [DataField]
    public float SpawnRange = 22f;

    /// <summary>A player this close to the trigger makes the flock appear off his screen.</summary>
    [DataField]
    public float TriggerRadius = 5f;

    /// <summary>Half size of what a player sees around himself, in tiles: inside it the flock is noticed.</summary>
    [DataField]
    public Vector2 ViewHalfSize = new(11f, 8f);

    /// <summary>Extra margin beyond the view: the flock appears only while the trigger is at least this far off screen.</summary>
    [DataField]
    public float HiddenMargin = 3f;

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromMinutes(8);

    [ViewVariables]
    public TimeSpan NextTrigger;

    [ViewVariables]
    public readonly List<EntityUid> Flock = new();
}

public enum LavalandWhelpFlockStage : byte
{
    /// <summary>Milling about the spot.</summary>
    Idle,
    /// <summary>Noticed, about to take off.</summary>
    Startled,
    /// <summary>Running away across the ground.</summary>
    Scatter,
    /// <summary>Flying up out of sight.</summary>
    Rising,
}

/// <summary>A little whelp of a flock: wanders about the spot, scatters when seen, then flies away.</summary>
[RegisterComponent]
public sealed partial class LavalandWhelpFlockComponent : Component
{
    [DataField]
    public float WanderSpeed = 0.8f;

    [DataField]
    public float ScatterSpeedMin = 5f;

    [DataField]
    public float ScatterSpeedMax = 8f;

    /// <summary>Spread of the escape directions around "straight away from the player", degrees.</summary>
    [DataField]
    public float ScatterSpread = 70f;

    [DataField]
    public TimeSpan ScatterTime = TimeSpan.FromSeconds(1.3);

    /// <summary>Not everyone takes off at once.</summary>
    [DataField]
    public TimeSpan MaxStartleDelay = TimeSpan.FromSeconds(0.35);

    [DataField]
    public float RiseTime = 0.7f;

    [DataField]
    public float RiseHeight = 8f;

    /// <summary>With nobody around for this long the flock quietly disappears.</summary>
    [DataField]
    public TimeSpan IdleLifetime = TimeSpan.FromMinutes(2);

    [DataField]
    public SoundSpecifier? ScatterSound = new SoundPathSpecifier("/Audio/Effects/thudswoosh.ogg");

    [ViewVariables]
    public EntityUid? Trigger;

    [ViewVariables]
    public Vector2 Home;

    [ViewVariables]
    public float HomeRadius = 3f;

    [ViewVariables]
    public LavalandWhelpFlockStage Stage;

    [ViewVariables]
    public TimeSpan StageEnd;

    [ViewVariables]
    public Vector2 WanderTarget;

    [ViewVariables]
    public Vector2 Velocity;

    [ViewVariables]
    public TimeSpan LastSeenNearby;
}

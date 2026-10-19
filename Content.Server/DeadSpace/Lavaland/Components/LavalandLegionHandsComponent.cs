// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Lavaland.Components;

/// <summary>
/// The Legion's two hands and their attacks:
/// the slam - both fists hit the floor, screens shake, basalt rains down by the Legion and by every player;
/// the throw - when the player runs off, a hand reaches for the nearest fauna (or basalt boulder, or tears out a
/// rock) and hurls it at him. Every landing spot is marked like the storm staff's bolt.
/// The hands float by the Legion's sides, trailing behind its head; each has its own health and every blow at a hand
/// hurts the Legion too. A destroyed hand is gone until the fight resets.
/// </summary>
[RegisterComponent]
public sealed partial class LavalandLegionHandsComponent : Component
{
    [DataField]
    public EntProtoId LeftHand = "LavalandLegionHandLeft";

    /// <summary>Hands up over the head (the Legion fleeing a raid in fright) until then, shaking.</summary>
    [ViewVariables]
    public TimeSpan? HandsUpUntil;

    [DataField]
    public EntProtoId RightHand = "LavalandLegionHandRight";

    /// <summary>Where the hands rest beside the Legion.</summary>
    [DataField]
    public Vector2 HandOffset = new(1.6f, 0.2f);

    /// <summary>How quickly a hand catches up with its place: lower trails further behind.</summary>
    [DataField]
    public float FollowSharpness = 3.5f;

    /// <summary>The hands bob up and down this much while floating.</summary>
    [DataField]
    public float BobHeight = 0.18f;

    [DataField]
    public EntProtoId Mark = "LavalandLegionRockMark";

    [DataField]
    public EntProtoId FallingRock = "LavalandLegionFallingRock";

    /// <summary>What a missed rock leaves on the ground: basalt rock to be torn out and thrown later.</summary>
    [DataField]
    public EntProtoId Boulder = "WallRockBasalt";

    /// <summary>A missed rock this close to the Legion just breaks: it must not wall itself in.</summary>
    [DataField]
    public float NoWallNearLegion = 2.6f;

    [DataField]
    public EntProtoId RockParticle = "LavalandIntroRockParticle";

    [DataField]
    public DamageSpecifier RockDamage = new()
    {
        DamageDict = new() { { "Blunt", 30 } },
    };

    [DataField]
    public EntProtoId SlowEffect = "LavalandLegionRockSlowdownStatusEffect";

    [DataField]
    public float SlowMultiplier = 0.9f;

    [DataField]
    public TimeSpan SlowTime = TimeSpan.FromSeconds(4);

    #region Slam

    [DataField]
    public float SlamChance = 0.25f;

    /// <summary>Fists raised before they come down.</summary>
    [DataField]
    public TimeSpan SlamWindup = TimeSpan.FromSeconds(0.8);

    [DataField]
    public TimeSpan ShakeTime = TimeSpan.FromSeconds(2);

    [DataField]
    public float ShakeStrength = 0.35f;

    [DataField]
    public float ShakeRange = 16f;

    [DataField]
    public int RocksByLegion = 6;

    [DataField]
    public float RockRadiusByLegion = 4.5f;

    [DataField]
    public int RocksByPlayer = 4;

    /// <summary>One of them always falls right on the player.</summary>
    [DataField]
    public float RockRadiusByPlayer = 3f;

    [DataField]
    public TimeSpan RockFallTime = TimeSpan.FromSeconds(1.2);

    /// <summary>The rocks don't all start falling at once.</summary>
    [DataField]
    public TimeSpan RockSpread = TimeSpan.FromSeconds(0.9);

    [DataField]
    public float RockHitRadius = 0.65f;

    [DataField]
    public SoundSpecifier? SlamSound = new SoundPathSpecifier("/Audio/_DeadSpace/Lavaland/Bubblegum/meteorimpact.ogg");

    [DataField]
    public SoundSpecifier? RockLandSound = new SoundPathSpecifier("/Audio/Effects/break_stone.ogg");

    #endregion

    #region Punch

    /// <summary>A player this close gets a fist instead of a slam or a throw.</summary>
    [DataField]
    public float PunchRange = 3.5f;

    [DataField]
    public float PunchChance = 0.7f;

    /// <summary>The fist clenches and draws back; the mark lies on the target meanwhile.</summary>
    [DataField]
    public TimeSpan PunchWindup = TimeSpan.FromSeconds(0.5);

    [DataField]
    public TimeSpan PunchStrike = TimeSpan.FromSeconds(0.12);

    /// <summary>The knuckles stay out this long before the hand opens and floats back.</summary>
    [DataField]
    public TimeSpan PunchHold = TimeSpan.FromSeconds(0.3);

    [DataField]
    public float PunchHitRadius = 1.1f;

    [DataField]
    public DamageSpecifier PunchDamage = new()
    {
        DamageDict = new() { { "Blunt", 25 } },
    };

    /// <summary>The punched player is knocked back this hard.</summary>
    [DataField]
    public float PunchKnockback = 8f;

    [DataField]
    public SoundSpecifier? PunchSound = new SoundPathSpecifier("/Audio/Weapons/genhit3.ogg");

    #endregion

    #region Throw

    [DataField]
    public float ThrowChance = 0.6f;

    /// <summary>Thrown at players farther than this: they are running off.</summary>
    [DataField]
    public float ThrowMinDistance = 6f;

    [DataField]
    public float GrabRange = 10f;

    [DataField]
    public TimeSpan ReachTime = TimeSpan.FromSeconds(0.6);

    [DataField]
    public TimeSpan PullTime = TimeSpan.FromSeconds(0.4);

    /// <summary>The mark lies on the landing spot this long before the throw.</summary>
    [DataField]
    public TimeSpan AimTime = TimeSpan.FromSeconds(0.8);

    [DataField]
    public TimeSpan FlightTime = TimeSpan.FromSeconds(0.6);

    [DataField]
    public float ThrowHitRadius = 1f;

    [DataField]
    public SoundSpecifier? ThrowSound = new SoundPathSpecifier("/Audio/Effects/thudswoosh.ogg");

    #endregion

    #region State

    [ViewVariables]
    public TimeSpan BusyUntil;

    [ViewVariables]
    public EntityUid? Left;

    [ViewVariables]
    public EntityUid? Right;

    [ViewVariables]
    public readonly List<LavalandLegionHandMove> Moves = new();

    [ViewVariables]
    public readonly List<LavalandLegionFallingRockState> Rocks = new();

    [ViewVariables]
    public readonly List<(EntityUid Player, TimeSpan Until)> Shaking = new();

    [ViewVariables]
    public TimeSpan NextShake;

    [ViewVariables]
    public LavalandLegionThrowState? Throw;

    [ViewVariables]
    public LavalandLegionPunchState? Punch;

    /// <summary>When the raised fists hit the floor.</summary>
    [ViewVariables]
    public TimeSpan? SlamAt;

    /// <summary>When the fists of the slam open back into hands.</summary>
    [ViewVariables]
    public TimeSpan? OpenHandsAt;

    /// <summary>Destroyed hands stay gone until the fight resets.</summary>
    [ViewVariables]
    public bool LeftLost;

    [ViewVariables]
    public bool RightLost;

    #endregion
}

/// <summary>One of the Legion's hands: its blows go to the Legion's health too.</summary>
[RegisterComponent]
public sealed partial class LavalandLegionHandComponent : Component
{
    [ViewVariables]
    public EntityUid Legion;

    [ViewVariables]
    public bool IsLeft;
}

/// <summary>A hand gliding from one spot to another, relative to the world.</summary>
public sealed class LavalandLegionHandMove
{
    public EntityUid Hand;
    public Vector2 From;
    public Vector2 To;
    public TimeSpan Start;
    public TimeSpan End;
}

public sealed class LavalandLegionFallingRockState
{
    public MapCoordinates Spot;
    public TimeSpan StartAt;
    public TimeSpan LandAt;
    public EntityUid? Rock;
    public bool Started;
}

public enum LavalandLegionPunchStage : byte
{
    Windup,
    Strike,
    Hold,
}

public sealed class LavalandLegionPunchState
{
    public LavalandLegionPunchStage Stage;
    public TimeSpan StageEnd;
    public EntityUid Hand;
    public MapCoordinates Spot;
}

public enum LavalandLegionThrowStage : byte
{
    Reach,
    Pull,
    Aim,
    Flight,
}

public sealed class LavalandLegionThrowState
{
    public LavalandLegionThrowStage Stage;
    public TimeSpan StageEnd;
    public EntityUid Hand;
    public EntityUid Target;

    /// <summary>What is thrown: a fauna mob, a boulder or a rock torn out of the ground.</summary>
    public EntityUid? Held;

    public bool HeldIsMob;
    public Vector2 HandFrom;
    public Vector2 HandTo;
    public MapCoordinates AimSpot;
    public Vector2 FlightFrom;
}

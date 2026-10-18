// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.ActionBlocker;
using Content.Shared.Chasm;
using Content.Shared.Eui;
using Content.Shared.Movement.Events;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.DeadSpace.Lavaland.ChromiteCaves;

/// <summary>Light of the caves, set by the lavaland day cycle.</summary>
public enum LavalandCavesMode : byte
{
    /// <summary>Lavaland night: an ordinary night.</summary>
    Night,
    /// <summary>Lavaland day: a deep night in the caves.</summary>
    DeepDark,
    /// <summary>Lavaland deep night: the caves glow like the milky way, fireflies fly about.</summary>
    Glow,
}

/// <summary>The chromite caves map. Lives on the caves map entity.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LavalandChromiteCavesComponent : Component
{
    [DataField, AutoNetworkedField]
    public LavalandCavesMode Mode = LavalandCavesMode.Night;

    /// <summary>Server: the lavaland map the caves are under.</summary>
    [ViewVariables]
    public EntityUid LavalandMap;

    /// <summary>Server: where fallers land, under the beam of light.</summary>
    [ViewVariables]
    public MapCoordinates Landing;

    /// <summary>Server: a tiny out-of-sight grid where falling and climbing players wait.</summary>
    [ViewVariables]
    public MapCoordinates Holding;

    [ViewVariables]
    public EntityUid? Exit;

    /// <summary>
    /// Server: chunks of the boundary whose walls are not put up yet - they go up only when somebody comes near,
    /// instead of thousands of walls at once when the caves are generated.
    /// </summary>
    [ViewVariables]
    public HashSet<Vector2i> PendingBoundaryChunks = new();

    [ViewVariables]
    public bool ModeInitialized;

    [ViewVariables]
    public Color FromColor;

    [ViewVariables]
    public Color ToColor;

    [ViewVariables]
    public TimeSpan FadeStart;

    [ViewVariables]
    public TimeSpan NextLightUpdate;

    [ViewVariables]
    public bool Fading;
}

/// <summary>One tile of the shimmering pit leading down to the caves. Jaunters don't save from it.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class LavalandChromitePitComponent : Component
{
    [DataField]
    public Color ColorA = Color.FromHex("#58c8ff");

    [DataField]
    public Color ColorB = Color.FromHex("#b05cff");

    /// <summary>Seconds for a full blue - purple - blue cycle.</summary>
    [DataField]
    public float Period = 4f;

    /// <summary>How strongly the pit sprite itself is tinted (the light is always fully tinted).</summary>
    [DataField]
    public float SpriteTint = 0.55f;

    /// <summary>Server: the lavaland map the pit is on.</summary>
    [ViewVariables]
    public EntityUid LavalandMap;
}

/// <summary>The beam of light from above in the caves: the only way out, for those with a hook.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class LavalandChromiteExitComponent : Component
{
    /// <summary>Seconds of one slow pulse of the beam.</summary>
    [DataField]
    public float PulsePeriod = 3.5f;

    [DataField]
    public float PulseDepth = 0.25f;

    /// <summary>Server: the caves map.</summary>
    [ViewVariables]
    public EntityUid CavesMap;
}

/// <summary>How a doll hits the ground after a fall.</summary>
public enum LavalandLandingStyle : byte
{
    /// <summary>Flat on the back.</summary>
    Back,
    /// <summary>Head down, legs up.</summary>
    Head,
    /// <summary>Face into the flowers.</summary>
    Face,
    /// <summary>On the side, sliding a bit.</summary>
    Side,
    /// <summary>On the backside, squashed.</summary>
    Seat,
}

/// <summary>
/// Landing: the doll drops from the top of its owner's screen, hits the ground in some silly pose, gets up and
/// stands in the middle of the screen. With <see cref="ClimbOut"/> it climbs out of a pit instead.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class LavalandPitLandingComponent : Component
{
    [DataField, AutoNetworkedField]
    public LavalandLandingStyle Style;

    [DataField, AutoNetworkedField]
    public float Duration = 2.4f;

    [DataField, AutoNetworkedField]
    public bool ClimbOut;

    /// <summary>Climbing out: from the doll to the pit center, world space.</summary>
    [DataField, AutoNetworkedField]
    public Vector2 From;

    [ViewVariables]
    public TimeSpan EndTime;
}

/// <summary>Jumping on the spot under the beam without a hook.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class LavalandCaveHopComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Duration = 2.1f;

    [DataField, AutoNetworkedField]
    public int Hops = 3;

    [ViewVariables]
    public TimeSpan EndTime;
}

public enum LavalandCaveClimbStage : byte
{
    /// <summary>The hook flies up with the rope.</summary>
    Rope,
    /// <summary>The doll is pulled up out of sight.</summary>
    FlyUp,
    /// <summary>The owner sees the doll hanging on the rope on the way up.</summary>
    Hanging,
}

/// <summary>Climbing out of the caves on the hook.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class LavalandCaveClimbComponent : Component
{
    [DataField, AutoNetworkedField]
    public LavalandCaveClimbStage Stage;

    /// <summary>Rope of the thing that pulls him up (a grappling gun, a sticky hand...).</summary>
    [DataField, AutoNetworkedField]
    public SpriteSpecifier? RopeSprite;

    /// <summary>What flies at the end of the rope: the projectile of that thing.</summary>
    [DataField, AutoNetworkedField]
    public string? TipPrototype;

    [DataField, AutoNetworkedField]
    public float Duration = 1f;

    /// <summary>Client clock when the current stage started, for animation.</summary>
    [ViewVariables]
    public float Elapsed;

    [ViewVariables]
    public LavalandCaveClimbStage? SeenStage;
}

/// <summary>What a <see cref="LavalandParticleEmitterComponent"/> throws out.</summary>
public enum LavalandParticleKind : byte
{
    /// <summary>Sparks rising with a flicker (lava).</summary>
    Ember,
    /// <summary>Grey flakes drifting up and sideways (lava).</summary>
    Ash,
    /// <summary>Petals fluttering down in a sway.</summary>
    Petal,
    /// <summary>Soft glowing motes slowly rising.</summary>
    Mote,
    /// <summary>Fireflies wandering around and blinking.</summary>
    Firefly,
    /// <summary>Tiny twinkling specks hanging in the air (the milky way of the glowing caves).</summary>
    Star,
    /// <summary>White puffs of a little cloud under the feet.</summary>
    Cloud,
}

/// <summary>
/// Deep night above a chasm: the shadows below are counting. Only the owner sees the countdown over his head.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LavalandShadowTimerComponent : Component
{
    public override bool SendOnlyToOwner => true;

    /// <summary>Server time when the shadow grabs him.</summary>
    [DataField, AutoNetworkedField]
    public TimeSpan EndTime;
}

/// <summary>A shadow rises from the chasm and drags its victim down; everyone around sees it.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class LavalandShadowGrabComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Duration = 1.4f;

    [ViewVariables]
    public TimeSpan EndTime;

    /// <summary>Client clock of the animation.</summary>
    [ViewVariables]
    public float Elapsed;
}

/// <summary>A pit jumper of the caves has spotted somebody: an exclamation mark pops up over it.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class LavalandPitJumperAlertComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Duration = 0.8f;

    /// <summary>Client clock of the pop-up.</summary>
    [ViewVariables]
    public float Elapsed;
}

/// <summary>
/// The flight potion: a little white cloud forms under the feet and carries its owner over chasms and lava.
/// Drawn by the client.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class LavalandCloudWalkerComponent : Component
{
}

/// <summary>
/// Client-side particles around the entity, like the ash and sparks over lava in minecraft.
/// Data only: the client reads it from the prototype.
/// </summary>
[RegisterComponent]
public sealed partial class LavalandParticleEmitterComponent : Component
{
    [DataField]
    public LavalandParticleKind Kind = LavalandParticleKind.Ember;

    /// <summary>Particles per second.</summary>
    [DataField]
    public float Rate = 0.5f;

    [DataField]
    public List<Color> Colors = new() { Color.FromHex("#ff8a2a") };

    [DataField]
    public float LifetimeMin = 1.2f;

    [DataField]
    public float LifetimeMax = 2.4f;

    /// <summary>Size in world units.</summary>
    [DataField]
    public float SizeMin = 0.04f;

    [DataField]
    public float SizeMax = 0.09f;

    [DataField]
    public float Speed = 0.6f;

    /// <summary>Half size of the spawn area around the entity.</summary>
    [DataField]
    public float Spread = 0.45f;

    /// <summary>Rate multiplier while the caves glow.</summary>
    [DataField]
    public float GlowBoost = 1f;

    /// <summary>A second kind thrown out alongside (ash with the lava sparks), with its own rate and colors.</summary>
    [DataField]
    public LavalandParticleKind? AltKind;

    [DataField]
    public float AltRate;

    [DataField]
    public List<Color> AltColors = new();
}

[Serializable, NetSerializable]
public sealed class LavalandCavePitEuiState : EuiStateBase
{
}

[Serializable, NetSerializable]
public sealed class LavalandCavePitResponseMessage(bool accepted) : EuiMessageBase
{
    public readonly bool Accepted = accepted;
}

[Serializable, NetSerializable]
public sealed class LavalandCaveExitEuiState : EuiStateBase
{
}

[Serializable, NetSerializable]
public sealed class LavalandCaveExitResponseMessage(bool accepted) : EuiMessageBase
{
    public readonly bool Accepted = accepted;
}

/// <summary>Movement blocking of the landing, hopping and climbing dolls; helpers for other systems.</summary>
public sealed class LavalandChromiteCavesSharedSystem : EntitySystem
{
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ChasmFallingAttemptEvent>(OnChasmFallingAttempt);
        SubscribeLocalEvent<LavalandPitLandingComponent, UpdateCanMoveEvent>(OnBlockMove);
        SubscribeLocalEvent<LavalandPitLandingComponent, ComponentStartup>(OnStateChanged);
        SubscribeLocalEvent<LavalandPitLandingComponent, ComponentShutdown>(OnStateChanged);
        SubscribeLocalEvent<LavalandCaveHopComponent, UpdateCanMoveEvent>(OnBlockMove);
        SubscribeLocalEvent<LavalandCaveHopComponent, ComponentStartup>(OnStateChanged);
        SubscribeLocalEvent<LavalandCaveHopComponent, ComponentShutdown>(OnStateChanged);
        SubscribeLocalEvent<LavalandShadowGrabComponent, UpdateCanMoveEvent>(OnBlockMove);
        SubscribeLocalEvent<LavalandShadowGrabComponent, ComponentStartup>(OnStateChanged);
        SubscribeLocalEvent<LavalandShadowGrabComponent, ComponentShutdown>(OnStateChanged);
        SubscribeLocalEvent<LavalandCaveClimbComponent, UpdateCanMoveEvent>(OnBlockMove);
        SubscribeLocalEvent<LavalandCaveClimbComponent, ComponentStartup>(OnStateChanged);
        SubscribeLocalEvent<LavalandCaveClimbComponent, ComponentShutdown>(OnStateChanged);
    }

    // The server turns stepping into the pit into a jump; the client must not predict a plain fall.
    private void OnChasmFallingAttempt(ChasmFallingAttemptEvent args)
    {
        if (_net.IsClient && !args.Cancelled && HasComp<LavalandChromitePitComponent>(args.Chasm))
            args.Cancel();
    }

    private void OnBlockMove<T>(Entity<T> ent, ref UpdateCanMoveEvent args) where T : IComponent
    {
        if (ent.Comp.LifeStage <= ComponentLifeStage.Running)
            args.Cancel();
    }

    private void OnStateChanged<T, TEvent>(Entity<T> ent, ref TEvent args) where T : IComponent
    {
        _blocker.UpdateCanMove(ent);
    }

    public bool IsCaves(EntityUid? map)
    {
        return HasComp<LavalandChromiteCavesComponent>(map);
    }

    /// <summary>The caves are in their deep night (it is day on lavaland).</summary>
    public bool IsDeepDark(EntityUid? map)
    {
        return TryComp<LavalandChromiteCavesComponent>(map, out var caves) && caves.Mode == LavalandCavesMode.DeepDark;
    }
}

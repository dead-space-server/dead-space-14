// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.ActionBlocker;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Eui;
using Content.Shared.Movement.Events;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Lavaland.SecretBoss;

/// <summary>How a player dives into the Celestial pit. Picked at random.</summary>
public enum LavalandPitJumpStyle : byte
{
    HeadFirst,
    Somersault,
    Backflip,
    Corkscrew,
    Starfish,
}

/// <summary>Jumping into the pit: the client plays the dive animation, movement is blocked.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class LavalandPitJumpingComponent : Component
{
    [DataField, AutoNetworkedField]
    public LavalandPitJumpStyle Style;

    /// <summary>From the jumper to the pit center, world space.</summary>
    [DataField, AutoNetworkedField]
    public Vector2 Target;

    [DataField, AutoNetworkedField]
    public float Duration = 1.2f;

    [ViewVariables]
    public TimeSpan EndTime;
}

/// <summary>Falling down the pit: the owner sees the dark falling screen, movement is blocked.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LavalandPitFallingComponent : Component
{
    [DataField, AutoNetworkedField]
    public LavalandPitJumpStyle Style;

    [DataField, AutoNetworkedField]
    public float Duration = 10f;

    /// <summary>Client clock when the fall began, for animation.</summary>
    [ViewVariables]
    public float Elapsed;

    [ViewVariables]
    public TimeSpan EndTime;
}

/// <summary>
/// Temporarily a doll (jumping, falling, asleep before and after the Celestial): no movement, no damage.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class LavalandPitFrozenComponent : Component
{
    /// <summary>Server: the freeze added respirator immunity and must take it away again.</summary>
    [ViewVariables]
    public bool AddedRespiratorImmunity;
}

/// <summary>Hides the arena behind darkness and purple fire while the Celestial intro plays.</summary>
[Serializable, NetSerializable]
public sealed class LavalandCelestialBackdropEvent(float duration) : EntityEventArgs
{
    public float Duration = duration;
}

/// <summary>Map component: jaunters, warp cubes and other teleports don't work here.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class LavalandNoTeleportZoneComponent : Component
{
}

/// <summary>Celestial blessing: innate armor.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LavalandBlessingArmorComponent : Component
{
    [DataField, AutoNetworkedField]
    public DamageModifierSet Modifiers = new();
}

/// <summary>Fade the screen to black and back (falling asleep before the Celestial).</summary>
[Serializable, NetSerializable]
public sealed class LavalandSecretBossFadeEvent(float fadeIn, float hold, float fadeOut) : EntityEventArgs
{
    public float FadeIn = fadeIn;
    public float Hold = hold;
    public float FadeOut = fadeOut;
}

[Serializable, NetSerializable]
public sealed class LavalandCelestialPitEuiState : EuiStateBase
{
}

[Serializable, NetSerializable]
public sealed class LavalandCelestialPitResponseMessage(bool accepted) : EuiMessageBase
{
    public readonly bool Accepted = accepted;
}

public sealed class LavalandSecretBossSharedSystem : EntitySystem
{
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LavalandPitJumpingComponent, UpdateCanMoveEvent>(OnBlockMove);
        SubscribeLocalEvent<LavalandPitJumpingComponent, ComponentStartup>(OnStateChanged);
        SubscribeLocalEvent<LavalandPitJumpingComponent, ComponentShutdown>(OnStateChanged);
        SubscribeLocalEvent<LavalandPitFallingComponent, UpdateCanMoveEvent>(OnBlockMove);
        SubscribeLocalEvent<LavalandPitFallingComponent, ComponentStartup>(OnStateChanged);
        SubscribeLocalEvent<LavalandPitFallingComponent, ComponentShutdown>(OnStateChanged);
        SubscribeLocalEvent<LavalandBlessingArmorComponent, DamageModifyEvent>(OnArmorDamageModify);
        SubscribeLocalEvent<LavalandPitFrozenComponent, UpdateCanMoveEvent>(OnBlockMove);
        SubscribeLocalEvent<LavalandPitFrozenComponent, ComponentStartup>(OnStateChanged);
        SubscribeLocalEvent<LavalandPitFrozenComponent, ComponentShutdown>(OnStateChanged);
        SubscribeLocalEvent<LavalandPitFrozenComponent, BeforeDamageChangedEvent>(OnFrozenBeforeDamage);
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

    private void OnFrozenBeforeDamage(Entity<LavalandPitFrozenComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (args.Damage.AnyPositive())
            args.Cancelled = true;
    }

    private void OnArmorDamageModify(Entity<LavalandBlessingArmorComponent> ent, ref DamageModifyEvent args)
    {
        args.Damage = DamageSpecifier.ApplyModifierSet(args.Damage, ent.Comp.Modifiers);
    }

    /// <summary>True when teleports are blocked for this entity (it is in the Celestial zone).</summary>
    public bool IsTeleportBlocked(EntityUid uid)
    {
        return HasComp<LavalandNoTeleportZoneComponent>(Transform(uid).MapUid);
    }
}

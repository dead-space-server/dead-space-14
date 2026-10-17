// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Throwing;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.Shared.DeadSpace.Attachments;

/// <summary>What an attachment effect is applied to. Effects only run on the server.</summary>
public readonly record struct AttachmentEffectArgs(
    EntityUid User,
    EntityUid Target,
    EntityUid Weapon,
    EntityUid Attachment,
    IEntityManager EntityManager);

/// <summary>
/// Something an attachment does on a melee hit or a crusher mark detonation. Written in YAML with <c>!type:</c>.
/// </summary>
[ImplicitDataDefinitionForInheritors]
public abstract partial class AttachmentEffect
{
    /// <summary>Chance for the effect to happen at all.</summary>
    [DataField]
    public float Chance = 1f;

    /// <summary>Applies the effect. Damage added to <paramref name="bonusDamage"/> is dealt with the hit.</summary>
    public abstract void Apply(in AttachmentEffectArgs args, DamageSpecifier bonusDamage);

    /// <summary>Adds <paramref name="add"/> into <paramref name="target"/> in place, including new damage types.</summary>
    public static void AddInto(DamageSpecifier target, DamageSpecifier add)
    {
        foreach (var (type, value) in add.DamageDict)
        {
            target.DamageDict[type] = target.DamageDict.GetValueOrDefault(type) + value;
        }
    }
}

/// <summary>Heals (negative damage) or hurts the user. Demon claws, flesh glob.</summary>
public sealed partial class AttachmentHealUser : AttachmentEffect
{
    [DataField(required: true)]
    public DamageSpecifier Damage = new();

    public override void Apply(in AttachmentEffectArgs args, DamageSpecifier bonusDamage)
    {
        args.EntityManager.System<DamageableSystem>()
            .TryChangeDamage(args.User, Damage, true, false, origin: args.Weapon);
    }
}

/// <summary>Extra damage for every <see cref="PerMissing"/> points of damage the user has taken. Goliath tentacle.</summary>
public sealed partial class AttachmentMissingHealthDamage : AttachmentEffect
{
    [DataField(required: true)]
    public DamageSpecifier Damage = new();

    [DataField]
    public float PerMissing = 10f;

    public override void Apply(in AttachmentEffectArgs args, DamageSpecifier bonusDamage)
    {
        if (!args.EntityManager.TryGetComponent<DamageableComponent>(args.User, out var damageable) || PerMissing <= 0f)
            return;

        var steps = damageable.TotalDamage.Float() / PerMissing;
        if (steps <= 0f)
            return;

        AddInto(bonusDamage, Damage * steps);
    }
}

/// <summary>Flat extra damage to the target. Demon claws detonation bonus.</summary>
public sealed partial class AttachmentBonusDamage : AttachmentEffect
{
    [DataField(required: true)]
    public DamageSpecifier Damage = new();

    public override void Apply(in AttachmentEffectArgs args, DamageSpecifier bonusDamage)
    {
        AddInto(bonusDamage, Damage);
    }
}

/// <summary>Damages and pushes back living things around the user. Ash drake tail spike.</summary>
public sealed partial class AttachmentAreaBlast : AttachmentEffect
{
    [DataField]
    public float Radius = 2f;

    [DataField(required: true)]
    public DamageSpecifier Damage = new();

    /// <summary>How far victims are pushed away from the user, in tiles.</summary>
    [DataField]
    public float Push = 1f;

    [DataField]
    public float PushSpeed = 5f;

    [DataField]
    public EntProtoId? Effect;

    [DataField]
    public SoundSpecifier? Sound;

    public override void Apply(in AttachmentEffectArgs args, DamageSpecifier bonusDamage)
    {
        var entMan = args.EntityManager;
        var lookup = entMan.System<EntityLookupSystem>();
        var transform = entMan.System<SharedTransformSystem>();
        var mobState = entMan.System<MobStateSystem>();
        var damageable = entMan.System<DamageableSystem>();
        var throwing = entMan.System<ThrowingSystem>();
        var audio = entMan.System<SharedAudioSystem>();

        var center = transform.GetMapCoordinates(args.User);
        foreach (var victim in lookup.GetEntitiesInRange<MobStateComponent>(center, Radius))
        {
            if (victim.Owner == args.User || mobState.IsDead(victim, victim.Comp))
                continue;

            var coords = transform.GetMapCoordinates(victim);
            damageable.TryChangeDamage(victim.Owner, Damage, true, origin: args.User);
            if (Effect != null)
                entMan.SpawnEntity(Effect, coords);
            if (Sound != null)
                audio.PlayPvs(Sound, victim);

            var away = coords.Position - center.Position;
            if (Push > 0f && away.LengthSquared() > 0.0001f)
                throwing.TryThrow(victim, Vector2.Normalize(away) * Push, PushSpeed, args.User, recoil: false, doSpin: false);
        }
    }
}

/// <summary>For a moment the user takes only a fraction of damage. Blood-drunk miner eye.</summary>
public sealed partial class AttachmentDamageShield : AttachmentEffect
{
    /// <summary>Damage taken is multiplied by this.</summary>
    [DataField]
    public float Coefficient = 0.1f;

    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(1);

    public override void Apply(in AttachmentEffectArgs args, DamageSpecifier bonusDamage)
    {
        var entMan = args.EntityManager;
        var shield = entMan.EnsureComponent<AttachmentDamageShieldComponent>(args.User);
        shield.Coefficient = Math.Min(shield.Coefficient, Coefficient);
        var end = IoCManager.Resolve<IGameTiming>().CurTime + Duration;
        if (end > shield.EndTime)
            shield.EndTime = end;
        entMan.Dirty(args.User, shield);
    }
}

/// <summary>Spawns an entity on and around the target. Broodmother tongue tentacles.</summary>
public sealed partial class AttachmentSpawnAtTarget : AttachmentEffect
{
    [DataField(required: true)]
    public EntProtoId Prototype;

    /// <summary>Tile offsets from the target to spawn at.</summary>
    [DataField]
    public List<Vector2> Offsets = new() { Vector2.Zero };

    public override void Apply(in AttachmentEffectArgs args, DamageSpecifier bonusDamage)
    {
        var entMan = args.EntityManager;
        var coords = entMan.System<SharedTransformSystem>().GetMapCoordinates(args.Target);
        foreach (var offset in Offsets)
        {
            entMan.SpawnEntity(Prototype, new MapCoordinates(coords.Position + offset, coords.MapId));
        }
    }
}

/// <summary>
/// Summons a creature at the user's side, fighting for the user's factions for a while. Legionnaire spine skulls.
/// </summary>
public sealed partial class AttachmentSummonAlly : AttachmentEffect
{
    [DataField(required: true)]
    public EntProtoId Prototype;

    /// <summary>Seconds before it is gone; 0 - it stays.</summary>
    [DataField]
    public float Lifetime = 20f;

    public override void Apply(in AttachmentEffectArgs args, DamageSpecifier bonusDamage)
    {
        var entMan = args.EntityManager;
        var transform = entMan.System<SharedTransformSystem>();
        var user = transform.GetMapCoordinates(args.User);
        var target = transform.GetMapCoordinates(args.Target);
        var toward = target.Position - user.Position;
        var offset = toward.LengthSquared() > 0.0001f ? Vector2.Normalize(toward) * 0.5f : Vector2.Zero;
        var ally = entMan.SpawnEntity(Prototype, new MapCoordinates(user.Position + offset, user.MapId));

        var faction = entMan.System<NpcFactionSystem>();
        if (entMan.HasComponent<NpcFactionMemberComponent>(ally))
        {
            faction.ClearFactions(ally, false);
            if (entMan.TryGetComponent<NpcFactionMemberComponent>(args.User, out var member))
                faction.AddFactions(ally, member.Factions);
        }

        if (Lifetime > 0f)
            entMan.EnsureComponent<TimedDespawnComponent>(ally).Lifetime = Lifetime;
    }
}

/// <summary>
/// Blasts the tiles on and around the target: every living thing there except the user is hurt once. Pandora's hope.
/// </summary>
public sealed partial class AttachmentTargetBlast : AttachmentEffect
{
    [DataField(required: true)]
    public DamageSpecifier Damage = new();

    /// <summary>Tile offsets from the target to blast.</summary>
    [DataField]
    public List<Vector2> Offsets = new() { Vector2.Zero };

    [DataField]
    public EntProtoId? Effect;

    [DataField]
    public SoundSpecifier? Sound;

    public override void Apply(in AttachmentEffectArgs args, DamageSpecifier bonusDamage)
    {
        var entMan = args.EntityManager;
        var lookup = entMan.System<EntityLookupSystem>();
        var transform = entMan.System<SharedTransformSystem>();
        var mobState = entMan.System<MobStateSystem>();
        var damageable = entMan.System<DamageableSystem>();

        var center = transform.GetMapCoordinates(args.Target);
        var hit = new HashSet<EntityUid>();
        foreach (var offset in Offsets)
        {
            var coords = new MapCoordinates(center.Position + offset, center.MapId);
            if (Effect != null)
                entMan.SpawnEntity(Effect, coords);

            foreach (var victim in lookup.GetEntitiesInRange<MobStateComponent>(coords, 0.5f))
            {
                if (victim.Owner == args.User || mobState.IsDead(victim, victim.Comp) || !hit.Add(victim))
                    continue;

                damageable.TryChangeDamage(victim.Owner, Damage, true, origin: args.User);
            }
        }

        if (Sound != null)
            entMan.System<SharedAudioSystem>().PlayPvs(Sound, args.Target);
    }
}

/// <summary>Sends a hierophant chaser after the target. Vortex talisman. Handled by the lavaland server code.</summary>
public sealed partial class AttachmentHierophantChaser : AttachmentEffect
{
    [DataField(required: true)]
    public DamageSpecifier Damage = new();

    [DataField]
    public int MaxSteps = 7;

    public override void Apply(in AttachmentEffectArgs args, DamageSpecifier bonusDamage)
    {
        var ev = new AttachmentHierophantChaserEvent(args.User, args.Target, Damage, MaxSteps);
        args.EntityManager.EventBus.RaiseEvent(EventSource.Local, ref ev);
    }
}

[ByRefEvent]
public record struct AttachmentHierophantChaserEvent(EntityUid User, EntityUid Target, DamageSpecifier Damage, int MaxSteps);

/// <summary>Raised on a weapon when a crusher mark it placed is detonated by a melee hit.</summary>
[ByRefEvent]
public record struct DamageMarkerDetonatedEvent(EntityUid Target, EntityUid User, DamageSpecifier BonusDamage);

/// <summary>Temporary damage reduction from <see cref="AttachmentDamageShield"/>.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class AttachmentDamageShieldComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Coefficient = 1f;

    [DataField(customTypeSerializer: typeof(Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.TimeOffsetSerializer))]
    [AutoNetworkedField, AutoPausedField]
    public TimeSpan EndTime;
}

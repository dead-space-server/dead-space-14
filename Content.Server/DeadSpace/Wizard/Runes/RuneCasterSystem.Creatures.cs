// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using System.Numerics;
using Content.Server.NPC;
using Content.Server.NPC.Systems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.DeadSpace.Wizard.Runes;
using Content.Shared.FixedPoint;
using Content.Shared.Item;
using Content.Shared.Magic.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Temperature.Components;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.DeadSpace.Wizard.Runes;

[RegisterComponent]
public sealed partial class RuneAnimatedComponent : Component
{
    public TimeSpan End;

    public List<string> Added = new();

    public List<string> Restore = new();

    public bool Anchored;

    public BodyType? BodyType;

    public BodyStatus BodyStatus;

    public bool FixedRotation;

    public bool CanCollide;

    public string? Fixture;

    public int Mask;

    public int Layer;

    public bool Hard;
}

[RegisterComponent]
public sealed partial class RuneAnimateStrikeComponent : Component
{
    [DataField]
    public float MinTemperature;

    [DataField]
    public float MaxTemperature;

    [DataField]
    public float IgniteChance;

    [DataField]
    public float IgniteStacks = 1f;

    [DataField]
    public float SlowChance;

    [DataField]
    public float SlowTime = 3f;

    [DataField]
    public float SlowMultiplier = 0.5f;

    [DataField]
    public float HealFraction;

    [DataField]
    public EntProtoId? Puddle;

    [DataField]
    public float DisarmChance;

    [DataField]
    public float PushChance;

    [DataField]
    public float PushDistance = 2.5f;

    [DataField]
    public float BloodFraction;

    [DataField]
    public ProtoId<ReagentPrototype>? Reagent;

    [DataField]
    public float ReagentAmount;

    [DataField]
    public List<ProtoId<DamageTypePrototype>> RandomTypes = new();

    [DataField]
    public float RandomMin = 2f;

    [DataField]
    public float RandomMax = 6f;

    public EntityUid? Caster;
}

public enum RuneSeekerMode : byte
{
    Slip,
    Throw,
}

[RegisterComponent]
public sealed partial class RuneSeekerComponent : Component
{
    [DataField]
    public RuneSeekerMode Mode = RuneSeekerMode.Slip;

    [DataField]
    public float Speed = 4f;

    [DataField]
    public float Range = 8f;

    [DataField]
    public float Reach = 0.7f;

    [DataField]
    public float Knockdown = 2f;

    [DataField]
    public SoundSpecifier? Sound;

    [DataField]
    public float Rest;

    public EntityUid? Caster;

    public EntityUid? Victim;

    public TimeSpan NextSearch;

    public TimeSpan RestUntil;
}

[RegisterComponent]
public sealed partial class RuneMedicComponent : Component
{
    [DataField(required: true)]
    public DamageSpecifier Heal = new();

    [DataField]
    public float Interval = 2f;

    [DataField]
    public float Range = 2.5f;

    public EntityUid? Owner;

    public TimeSpan Next;
}

[RegisterComponent]
public sealed partial class RuneHealOrbComponent : Component
{
    [DataField]
    public float Speed = 6f;

    public EntityUid? Target;

    public float Amount;
}

public sealed partial class RuneCasterSystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly NPCSystem _npc = default!;

    private const float AnimateMinDamage = 0.5f;
    private const float AnimateMaxDamage = 1.5f;
    private const float SeekerSearchInterval = 0.25f;
    private const float HealOrbReach = 0.4f;
    private const float HealOrbSight = 12f;
    private static readonly EntProtoId HealOrbPrototype = "RuneLifeOrb";
    private static readonly ProtoId<DamageGroupPrototype> BurnGroup = "Burn";
    private static readonly string[] AnimateScaled = { "Destructible", "MeleeWeapon" };

    private void InitializeCreatures()
    {
        SubscribeLocalEvent<RuneAnimateStrikeComponent, MeleeHitEvent>(OnAnimateStrike);
    }

    private void UpdateCreatures(float frameTime)
    {
        var now = Timing.CurTime;

        var expired = new List<(EntityUid, RuneAnimatedComponent)>();
        var animated = EntityQueryEnumerator<RuneAnimatedComponent>();
        while (animated.MoveNext(out var uid, out var comp))
        {
            if (now >= comp.End)
                expired.Add((uid, comp));
        }

        foreach (var (uid, comp) in expired)
            RevertAnimated(uid, comp);

        var seekers = EntityQueryEnumerator<RuneSeekerComponent, PhysicsComponent>();
        while (seekers.MoveNext(out var uid, out var seeker, out var physics))
            UpdateSeeker(uid, seeker, physics, now);

        var medics = EntityQueryEnumerator<RuneMedicComponent>();
        while (medics.MoveNext(out var uid, out var medic))
        {
            if (now < medic.Next || medic.Owner is not { } owner || TerminatingOrDeleted(owner) || _mobState.IsDead(uid) || _mobState.IsDead(owner))
                continue;

            medic.Next = now + TimeSpan.FromSeconds(medic.Interval);
            if (!_interaction.InRangeUnobstructed(uid, owner, medic.Range))
                continue;

            _damageable.TryChangeDamage(owner, medic.Heal, true, interruptsDoAfters: false, origin: uid);
        }

        var orbs = EntityQueryEnumerator<RuneHealOrbComponent, TransformComponent>();
        while (orbs.MoveNext(out var uid, out var orb, out var xform))
            UpdateHealOrb(uid, orb, xform, frameTime);
    }

    private void BindSpawned(RuneCastContext ctx, EntityUid uid)
    {
        if (TryComp<RuneSeekerComponent>(uid, out var seeker))
            seeker.Caster = ctx.Caster;

        if (TryComp<RuneMedicComponent>(uid, out var medic))
        {
            medic.Owner = ctx.Caster;
            _npc.SetBlackboard(uid, NPCBlackboard.FollowTarget, new EntityCoordinates(ctx.Caster, Vector2.Zero));
        }
    }

    private RuneAnimatedComponent SnapshotAnimated(EntityUid uid, ChangeComponentsSpellEvent? change, EntityPrototype template)
    {
        var names = new HashSet<string>(template.Components.Keys);
        if (change != null)
            names.UnionWith(change.ToAdd.Keys);

        var animated = new RuneAnimatedComponent();
        foreach (var name in names)
        {
            if (!Factory.TryGetRegistration(name, out var registration))
                continue;

            if (HasComp(uid, registration.Type))
                animated.Restore.Add(name);
            else
                animated.Added.Add(name);
        }

        if (change != null)
        {
            foreach (var name in change.ToRemove)
            {
                if (Factory.TryGetRegistration(name, out var registration) && HasComp(uid, registration.Type))
                    animated.Restore.Add(name);
            }
        }

        foreach (var name in AnimateScaled)
        {
            if (!animated.Restore.Contains(name) && !animated.Added.Contains(name) &&
                Factory.TryGetRegistration(name, out var registration) && HasComp(uid, registration.Type))
                animated.Restore.Add(name);
        }

        animated.Anchored = Transform(uid).Anchored;
        if (TryComp<PhysicsComponent>(uid, out var physics))
        {
            animated.BodyType = physics.BodyType;
            animated.BodyStatus = physics.BodyStatus;
            animated.FixedRotation = physics.FixedRotation;
            animated.CanCollide = physics.CanCollide;
        }

        if (TryComp<FixturesComponent>(uid, out var fixtures) && fixtures.Fixtures.Count > 0)
        {
            var (key, fixture) = fixtures.Fixtures.First();
            animated.Fixture = key;
            animated.Mask = fixture.CollisionMask;
            animated.Layer = fixture.CollisionLayer;
            animated.Hard = fixture.Hard;
        }

        return animated;
    }

    private void RevertAnimated(EntityUid uid, RuneAnimatedComponent animated)
    {
        if (TerminatingOrDeleted(uid))
            return;

        foreach (var held in _hands.EnumerateHeld(uid).ToList())
            _hands.TryDrop(uid, held, checkActionBlocker: false);

        foreach (var name in animated.Added)
        {
            if (Factory.TryGetRegistration(name, out var registration))
                RemComp(uid, registration.Type);
        }

        if (MetaData(uid).EntityPrototype is { } prototype)
        {
            var restore = new ComponentRegistry();
            foreach (var name in animated.Restore)
            {
                if (prototype.Components.TryGetValue(name, out var entry))
                    restore[name] = entry;
            }

            EntityManager.AddComponents(uid, restore, true);
        }

        RemComp<RuneTintComponent>(uid);
        RemComp<RuneAnimatedComponent>(uid);

        var xform = Transform(uid);
        if (TryComp<FixturesComponent>(uid, out var fixtures) && TryComp<PhysicsComponent>(uid, out var physics) && animated.BodyType is { } bodyType)
        {
            if (animated.Fixture is { } key && fixtures.Fixtures.TryGetValue(key, out var fixture))
            {
                _physics.SetCollisionMask(uid, key, fixture, animated.Mask, fixtures, physics);
                _physics.SetCollisionLayer(uid, key, fixture, animated.Layer, fixtures, physics);
                _physics.SetHard(uid, fixture, animated.Hard, fixtures);
            }

            _physics.SetBodyType(uid, bodyType, fixtures, physics, xform);
            _physics.SetBodyStatus(uid, physics, animated.BodyStatus, true);
            _physics.SetFixedRotation(uid, animated.FixedRotation, true, fixtures, physics);
            _physics.SetCanCollide(uid, animated.CanCollide, true, false, fixtures, physics);
        }

        if (animated.Anchored)
            _transform.AnchorEntity(uid, xform);

        _popup.PopupEntity(Loc.GetString("rune-animate-revert", ("item", uid)), uid, PopupType.Small);
    }

    private float AnimateSize(EntityUid uid)
    {
        return TryComp<ItemComponent>(uid, out var item)
            ? Math.Clamp(_item.GetSizePrototype(item.Size).Weight / AnimateNormalWeight, AnimateMinSize, AnimateLargeSize)
            : AnimateLargeSize;
    }

    private void ScaleAnimateDamage(EntityUid uid, float size)
    {
        if (!TryComp<MeleeWeaponComponent>(uid, out var melee))
            return;

        melee.Damage *= Math.Clamp(size, AnimateMinDamage, AnimateMaxDamage);
        Dirty(uid, melee);
    }

    private void OnAnimateStrike(Entity<RuneAnimateStrikeComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit || args.HitEntities.Count == 0)
            return;

        var strike = ent.Comp;
        var dealt = 0f;
        foreach (var target in args.HitEntities)
        {
            if (target == ent.Owner || target == strike.Caster || !HasComp<MobStateComponent>(target))
                continue;

            dealt += args.BaseDamage.GetTotal().Float();
            StrikeTarget(ent, target);
        }

        if (dealt <= 0f)
            return;

        if (strike.Puddle is { } puddle)
            Spawn(puddle, Transform(ent).Coordinates);

        if (strike.HealFraction > 0f && strike.Caster is { } caster && !TerminatingOrDeleted(caster) &&
            _interaction.InRangeUnobstructed(ent.Owner, caster, HealOrbSight))
        {
            var orb = Spawn(HealOrbPrototype, _transform.GetMapCoordinates(ent));
            var comp = EnsureComp<RuneHealOrbComponent>(orb);
            comp.Target = caster;
            comp.Amount = dealt * strike.HealFraction;
        }
    }

    private void StrikeTarget(Entity<RuneAnimateStrikeComponent> ent, EntityUid target)
    {
        var strike = ent.Comp;
        if (strike.MaxTemperature != 0f || strike.MinTemperature != 0f)
            ShiftTemperature(target, _random.NextFloat(strike.MinTemperature, strike.MaxTemperature));

        if (strike.IgniteChance > 0f && _random.Prob(strike.IgniteChance))
            _flammable.AdjustFireStacks(target, strike.IgniteStacks, ignite: true);

        if (strike.SlowChance > 0f && _random.Prob(strike.SlowChance))
            _movementMod.TryUpdateMovementSpeedModDuration(target, SlowStatus, TimeSpan.FromSeconds(strike.SlowTime), strike.SlowMultiplier);

        if (strike.Reagent is { } reagent)
            Inject(target, reagent, strike.ReagentAmount);

        if (strike.BloodFraction > 0f && strike.Caster is { } caster)
            DrainBlood(caster, target, strike.BloodFraction, 1f);

        if (strike.RandomTypes.Count > 0 && Proto.TryIndex(_random.Pick(strike.RandomTypes), out var type))
            _damageable.TryChangeDamage(target, new DamageSpecifier(type, FixedPoint2.New(_random.NextFloat(strike.RandomMin, strike.RandomMax))), origin: ent.Owner);

        if (strike.DisarmChance > 0f && _random.Prob(strike.DisarmChance))
        {
            foreach (var held in _hands.EnumerateHeld(target).ToList())
                _hands.TryDrop(target, held, checkActionBlocker: false);
        }
        else if (strike.PushChance > 0f && _random.Prob(strike.PushChance))
        {
            var direction = _transform.GetWorldPosition(target) - _transform.GetWorldPosition(ent);
            if (direction.LengthSquared() > 0.001f)
                _throwing.TryThrow(target, Vector2.Normalize(direction) * strike.PushDistance, 8f, ent.Owner, recoil: false, compensateFriction: true);
        }
    }

    private void ShiftTemperature(EntityUid target, float delta)
    {
        if (TryComp<TemperatureComponent>(target, out var temperature))
            _temperature.ForceChangeTemperature(target, MathF.Max(0f, temperature.CurrentTemperature + delta), temperature);
    }

    private void UpdateSeeker(EntityUid uid, RuneSeekerComponent seeker, PhysicsComponent physics, TimeSpan now)
    {
        if (_container.IsEntityInContainer(uid))
        {
            RemCompDeferred<RuneSeekerComponent>(uid);
            return;
        }

        if (now < seeker.RestUntil)
            return;

        var position = _transform.GetMapCoordinates(uid);
        if (now >= seeker.NextSearch)
        {
            seeker.NextSearch = now + TimeSpan.FromSeconds(SeekerSearchInterval);
            seeker.Victim = null;
            var best = float.MaxValue;
            foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(position, seeker.Range))
            {
                if (mob.Owner == seeker.Caster || _mobState.IsDead(mob) ||
                    seeker.Mode == RuneSeekerMode.Slip && HasComp<KnockedDownComponent>(mob))
                    continue;

                var distance = (_transform.GetWorldPosition(mob) - position.Position).LengthSquared();
                if (distance >= best)
                    continue;

                best = distance;
                seeker.Victim = mob;
            }
        }

        if (seeker.Victim is not { } victim || TerminatingOrDeleted(victim))
            return;

        var delta = _transform.GetWorldPosition(victim) - position.Position;
        var length = delta.Length();
        if (length > seeker.Reach)
        {
            _physics.SetLinearVelocity(uid, delta / length * seeker.Speed, body: physics);
            return;
        }

        _physics.SetLinearVelocity(uid, Vector2.Zero, body: physics);
        seeker.Victim = null;
        if (seeker.Rest > 0f)
            seeker.RestUntil = now + TimeSpan.FromSeconds(seeker.Rest);
        else
            RemCompDeferred<RuneSeekerComponent>(uid);

        if (seeker.Sound != null)
            _audio.PlayPvs(seeker.Sound, uid);

        switch (seeker.Mode)
        {
            case RuneSeekerMode.Slip:
                _stun.TryKnockdown(victim, TimeSpan.FromSeconds(seeker.Knockdown));
                _popup.PopupEntity(Loc.GetString("rune-seeker-slip"), victim, victim, PopupType.SmallCaution);
                break;
            case RuneSeekerMode.Throw:
                _throwing.TryThrow(uid, Transform(victim).Coordinates, 10f, seeker.Caster, recoil: false);
                break;
        }
    }

    private void UpdateHealOrb(EntityUid uid, RuneHealOrbComponent orb, TransformComponent xform, float frameTime)
    {
        if (orb.Target is not { } target || TerminatingOrDeleted(target) || Transform(target).MapID != xform.MapID)
        {
            QueueDel(uid);
            return;
        }

        var from = _transform.GetWorldPosition(xform);
        var to = _transform.GetWorldPosition(target);
        var delta = to - from;
        var length = delta.Length();
        var step = orb.Speed * frameTime;
        if (length > HealOrbReach && length > step)
        {
            _transform.SetWorldPosition(uid, from + delta / length * step);
            return;
        }

        if (Proto.TryIndex<DamageGroupPrototype>(BruteGroup, out var brute) && Proto.TryIndex(BurnGroup, out var burn))
        {
            var heal = new DamageSpecifier(brute, FixedPoint2.New(-orb.Amount * 0.6f)) + new DamageSpecifier(burn, FixedPoint2.New(-orb.Amount * 0.4f));
            _damageable.TryChangeDamage(target, heal, true, interruptsDoAfters: false);
        }

        QueueDel(uid);
    }
}

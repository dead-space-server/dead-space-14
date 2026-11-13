// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using System.Numerics;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Administration.Systems;
using Content.Shared.Charges.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Charges.Systems;
using Content.Shared.Mind;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Containers;
using Content.Server.Destructible;
using Content.Server.Emp;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Lightning;
using Content.Server.Lightning.Components;
using Content.Server.Magic;
using Content.Server.Polymorph.Systems;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.Actions.Components;
using Content.Shared.Chat;
using Content.Shared.Magic.Events;
using Content.Shared.Damage.Systems;
using Content.Shared.Destructible.Thresholds.Triggers;
using Content.Shared.DeadSpace.Wizard.Runes;
using Content.Shared.Gibbing;
using Content.Shared.Magic.Components;
using Content.Shared.Maps;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.RepulseAttract;
using Content.Shared.Stunnable;
using Content.Shared.Tag;
using Content.Shared.Whitelist;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Spawners;

namespace Content.Server.DeadSpace.Wizard.Runes;

public sealed class RuneCastContext
{
    public readonly EntityUid Caster;
    public readonly EntityCoordinates Point;
    public readonly EntityUid? Target;
    public readonly float Power;
    public readonly int Extra;
    public readonly float RangeScale;
    public readonly float DurationScale;
    public bool KeepUltimateReady;
    public readonly RuneEffectTarget AutoTarget;
    public readonly RuneSpawnPattern AutoPattern;

    public RuneCastContext(EntityUid caster, EntityCoordinates point, EntityUid? target, RuneComposition? composition, float powerScale = 1f)
    {
        AutoTarget = composition?.Form.AutoTarget ?? RuneEffectTarget.AroundCaster;
        AutoPattern = composition?.Form.AutoPattern ?? RuneSpawnPattern.AtCaster;
        Caster = caster;
        Point = point;
        Target = target;
        Power = (composition?.Power ?? 1f) * powerScale;
        Extra = composition?.Extra ?? 0;
        RangeScale = composition?.RangeScale ?? 1f;
        DurationScale = composition?.DurationScale ?? 1f;
    }
}

[RegisterComponent]
public sealed partial class RuneTimedComponentsComponent : Component
{
    public readonly List<RuneTimedEntry> Entries = new();
}

public sealed class RuneTimedEntry
{
    public readonly List<string> Added;
    public readonly TimeSpan End;

    public RuneTimedEntry(List<string> added, TimeSpan end)
    {
        Added = added;
        End = end;
    }
}

[RegisterComponent]
public sealed partial class RuneImmortalityComponent : Component
{
    public TimeSpan End;

    public bool Granted;
}

public sealed partial class RuneCasterSystem
{
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly GunSystem _gun = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly MovementModStatusSystem _movementMod = default!;
    [Dependency] private readonly FlammableSystem _flammable = default!;
    [Dependency] private readonly RepulseAttractSystem _repulse = default!;
    [Dependency] private readonly PolymorphSystem _polymorph = default!;
    [Dependency] private readonly GibbingSystem _gibbing = default!;
    [Dependency] private readonly MagicSystem _magic = default!;
    [Dependency] private readonly LightningSystem _lightning = default!;
    [Dependency] private readonly EmpSystem _emp = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedChargesSystem _charges = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly RejuvenateSystem _rejuvenate = default!;
    [Dependency] private readonly SharedGodmodeSystem _godmode = default!;

    private const float AnimateHealthScale = 0.6f;
    private EntityUid? _lightningOwner;

    private static readonly EntProtoId SlowStatus = "RuneSlowStatusEffect";
    private static readonly ProtoId<TagPrototype> AnimateBlacklistTag = "AnimateSpellBlacklist";

    private static readonly string[] AnimateBlacklist =
    {
        "NukeDisk",
        "GravityGenerator",
        "AnomalyGenerator",
        "TegGenerator",
        "TegCirculator",
        "XenoArtifact",
    };

    private static readonly EntProtoId AnimateAction = "ActionAnimateSpell";
    private static readonly ProtoId<TagPrototype> WandTag = "WizardWand";

    private EntityWhitelist _pushWhitelist = new();

    private void InitializeEffects()
    {
        _pushWhitelist = new EntityWhitelist { Components = new[] { "MobMover", "Item" } };
        SubscribeLocalEvent<LightningComponent, ComponentInit>(OnWizardLightningInit);
    }

    private void OnWizardLightningInit(EntityUid uid, LightningComponent component, ComponentInit args)
    {
        if (_lightningOwner is not { } owner)
            return;

        EnsureComp<PreventCollideComponent>(uid).Uid = owner;
    }

    private void Apply(RuneCastContext ctx, RuneSpellEffect effect)
    {
        switch (effect)
        {
            case RuneProjectileEffect projectile:
                ApplyProjectile(ctx, projectile);
                break;
            case RuneSpawnEffect spawn:
                ApplySpawn(ctx, spawn);
                break;
            case RuneSpawnRandomEffect spawnRandom:
                ApplySpawnRandom(ctx, spawnRandom);
                break;
            case RuneDamageEffect damage:
                ApplyDamage(ctx, damage);
                break;
            case RuneStaminaEffect stamina:
                foreach (var target in Targets(ctx, stamina))
                    _stamina.TakeStaminaDamage(target, stamina.Amount * ctx.Power, source: ctx.Caster);
                break;
            case RuneKnockdownEffect knockdown:
                foreach (var target in Targets(ctx, knockdown))
                    _stun.TryKnockdown(target, TimeSpan.FromSeconds(knockdown.Time * ctx.DurationScale));
                break;
            case RuneSlowEffect slow:
                foreach (var target in Targets(ctx, slow))
                    _movementMod.TryUpdateMovementSpeedModDuration(target, SlowStatus, TimeSpan.FromSeconds(slow.Time * ctx.DurationScale), slow.Multiplier);
                break;
            case RuneIgniteEffect ignite:
                foreach (var target in Targets(ctx, ignite))
                    _flammable.AdjustFireStacks(target, ignite.FireStacks * ctx.Power, ignite: true);
                break;
            case RuneExtinguishEffect extinguish:
                foreach (var target in Targets(ctx, extinguish))
                    _flammable.Extinguish(target);
                break;
            case RunePushEffect push:
                ApplyPush(ctx, push);
                break;
            case RuneTeleportEffect teleport:
                ApplyTeleport(ctx, teleport);
                break;
            case RunePolymorphEffect polymorph:
                foreach (var target in Targets(ctx, polymorph))
                    _polymorph.PolymorphEntity(target, target == ctx.Caster && polymorph.OnCaster is { } own ? own : polymorph.Polymorph);
                break;
            case RuneComponentsEffect components:
                foreach (var target in Targets(ctx, components))
                    ApplyComponents(target, components, ctx.DurationScale);
                break;
            case RuneParalyzeEffect paralyze:
                foreach (var target in Targets(ctx, paralyze))
                    _stun.TryUpdateParalyzeDuration(target, TimeSpan.FromSeconds(paralyze.Time * ctx.DurationScale));
                break;
            case RuneOrbitShieldEffect shield:
                ApplyOrbitShield(ctx, shield);
                break;
            case RuneRegenerationEffect regeneration:
                foreach (var target in Targets(ctx, regeneration))
                    StartRegeneration(target, regeneration.PerSecond * ctx.Power, regeneration.Duration * ctx.DurationScale);
                break;
            case RuneStatusEffect status:
                foreach (var target in Targets(ctx, status))
                    _status.TryAddStatusEffectDuration(target, status.Status, TimeSpan.FromSeconds(status.Time * ctx.DurationScale));
                break;
            case RuneTelekinesisEffect telekinesis:
                ApplyTelekinesis(ctx, telekinesis);
                break;
            case RuneRecallEffect:
                ApplyRecall(ctx);
                break;
            case RuneChargeEffect charge:
                ApplyCharge(ctx, charge);
                break;
            case RuneMindSwapEffect swap:
                ApplyMindSwap(ctx, swap);
                break;
            case RuneDecayEffect decay:
                ApplyDecay(ctx, decay);
                break;
            case RuneBarrageEffect barrage:
                ApplyBarrage(ctx, barrage);
                break;
            case RuneReviveEffect revive:
                foreach (var target in Targets(ctx, revive))
                {
                    RestoreFully(target);
                    GrantImmortality(target, revive.Immortality * ctx.DurationScale);
                }
                break;
            case RuneTimeStopEffect timeStop:
                StartTimeStop(ctx, timeStop);
                break;
            case RunePortalEffect portal:
                ApplyPortal(ctx, portal);
                break;
            case RuneArenaEffect arena:
                StartArena(ctx, arena);
                break;
            case RuneAnimateEffect animate:
                ApplyAnimate(ctx, animate);
                break;
            case RuneKnockEffect knock:
                _magic.Knock(ctx.Caster, knock.Range * ctx.RangeScale);
                break;
            case RuneGibEffect gib:
                foreach (var target in Targets(ctx, gib))
                    _gibbing.Gib(target, user: ctx.Caster);
                break;
            case RuneLightningEffect lightning:
                ApplyLightning(ctx, lightning);
                break;
            case RuneEmpEffect emp:
                var at = AtPoint(ctx, emp.AtPoint, emp.Auto) ? _transform.ToMapCoordinates(ctx.Point) : _transform.GetMapCoordinates(ctx.Caster);
                _emp.EmpPulse(at, emp.Range * ctx.RangeScale, emp.Energy, TimeSpan.FromSeconds(emp.Duration * ctx.DurationScale), ctx.Caster);
                break;
            case RuneSpeakEffect speak:
                ApplySpeak(ctx, speak);
                break;
            case RuneRandomEffect random when random.Options.Count > 0:
                Apply(ctx, _random.Pick(random.Options));
                break;
        }
    }

    private List<EntityUid> Targets(RuneCastContext ctx, RuneTargetedEffect effect)
    {
        var found = FindTargets(ctx, effect);
        if (effect.OnlyDead)
            found.RemoveAll(uid => !_mobState.IsDead(uid));

        return found;
    }

    private void ApplyDecay(RuneCastContext ctx, RuneDecayEffect effect)
    {
        var center = effect.AroundCaster ? _transform.GetMapCoordinates(ctx.Caster) : _transform.ToMapCoordinates(ctx.Point);
        foreach (var target in _lookup.GetEntitiesInRange<DamageableComponent>(center, effect.Radius * ctx.RangeScale))
        {
            if (target.Owner == ctx.Caster || HasComp<MobStateComponent>(target))
                continue;

            _damageable.TryChangeDamage(target.Owner, effect.Damage * ctx.Power, origin: ctx.Caster);
        }
    }

    private void ApplyBarrage(RuneCastContext ctx, RuneBarrageEffect effect)
    {
        var center = _transform.ToMapCoordinates(ctx.Point);
        var count = effect.Count + ctx.Extra * 2;
        var radius = effect.Radius * ctx.RangeScale;
        var prototype = effect.Prototype;

        for (var i = 0; i < count; i++)
        {
            var offset = _random.NextVector2(radius);
            var at = new MapCoordinates(center.Position + offset, center.MapId);
            Robust.Shared.Timing.Timer.Spawn(TimeSpan.FromSeconds(effect.Interval * i), () =>
            {
                if (_map.MapExists(at.MapId))
                    Spawn(prototype, at);
            });
        }
    }

    private List<EntityUid> FindTargets(RuneCastContext ctx, RuneTargetedEffect effect)
    {
        var result = new List<EntityUid>();
        var mode = effect.Target == RuneEffectTarget.Auto ? ctx.AutoTarget : effect.Target;
        switch (mode)
        {
            case RuneEffectTarget.Caster:
                result.Add(ctx.Caster);
                return result;
            case RuneEffectTarget.Target:
                if (ctx.Target is { } target && !TerminatingOrDeleted(target))
                    result.Add(target);
                return result;
        }

        var center = mode == RuneEffectTarget.AroundCaster
            ? _transform.GetMapCoordinates(ctx.Caster)
            : _transform.ToMapCoordinates(ctx.Point);

        foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(center, effect.Radius * ctx.RangeScale))
        {
            if (mob.Owner == ctx.Caster && !effect.IncludeCaster)
                continue;

            result.Add(mob);
        }

        if (effect.IncludeCaster && !result.Contains(ctx.Caster) && mode == RuneEffectTarget.AroundCaster)
            result.Add(ctx.Caster);

        return result;
    }

    private void ApplyProjectile(RuneCastContext ctx, RuneProjectileEffect effect)
    {
        var from = _transform.GetMapCoordinates(ctx.Caster);
        var to = _transform.ToMapCoordinates(ctx.Point);
        var direction = to.Position - from.Position;
        if (direction.LengthSquared() < 0.01f)
            direction = Transform(ctx.Caster).LocalRotation.ToWorldVec();

        var count = Math.Max(1, effect.Count + ctx.Extra);
        var velocity = _physics.GetMapLinearVelocity(ctx.Caster);
        var spread = MathHelper.DegreesToRadians(effect.Spread);
        var power = count > 1 && ctx.Extra > 0 ? ctx.Power * 0.6f : ctx.Power;

        for (var i = 0; i < count; i++)
        {
            var angle = count == 1 ? 0f : spread * (i - (count - 1) / 2f);
            var shot = new Angle(angle).RotateVec(direction);
            var ent = Spawn(effect.Prototype, from);

            if (TryComp<ProjectileComponent>(ent, out var projectile) && MathF.Abs(power - 1f) > 0.01f)
                projectile.Damage *= power;

            _gun.ShootProjectile(ent, shot, velocity, ctx.Caster, ctx.Caster, effect.Speed);
        }
    }

    private void ApplySpawn(RuneCastContext ctx, RuneSpawnEffect effect)
    {
        var pattern = effect.Pattern == RuneSpawnPattern.Auto ? ctx.AutoPattern : effect.Pattern;
        var size = pattern is RuneSpawnPattern.LineInFront or RuneSpawnPattern.LineAtPoint
            ? effect.Size + ctx.Extra * 2
            : effect.Size + ctx.Extra;

        foreach (var (coords, blocked) in Positions(ctx, pattern, size))
        {
            if (blocked && effect.SkipBlocked)
                continue;

            var ent = Spawn(effect.Prototype, coords);
            if (effect.Lifetime is { } lifetime)
                EnsureComp<TimedDespawnComponent>(ent).Lifetime = lifetime * ctx.DurationScale;

            if (effect.PassableByCaster)
                EnsureComp<PreventCollideComponent>(ent).Uid = ctx.Caster;
        }
    }

    private void ApplySpawnRandom(RuneCastContext ctx, RuneSpawnRandomEffect effect)
    {
        var positions = Positions(ctx, effect.Pattern, effect.Size + ctx.Extra);
        if (positions.Count == 0 || effect.Prototypes.Count == 0)
            return;

        var count = effect.Count + ctx.Extra;
        for (var i = 0; i < count; i++)
        {
            var (coords, _) = _random.Pick(positions);
            var ent = Spawn(_random.Pick(effect.Prototypes), coords);
            if (effect.Lifetime is { } lifetime)
                EnsureComp<TimedDespawnComponent>(ent).Lifetime = lifetime * ctx.DurationScale;

            if (effect.NoGhostRole)
            {
                RemComp<GhostRoleComponent>(ent);
                RemComp<GhostTakeoverAvailableComponent>(ent);
            }
        }
    }

    private List<(EntityCoordinates Coords, bool Blocked)> Positions(RuneCastContext ctx, RuneSpawnPattern pattern, int size)
    {
        if (pattern == RuneSpawnPattern.Auto)
            pattern = ctx.AutoPattern;

        var caster = _transform.GetMapCoordinates(ctx.Caster);
        var point = _transform.ToMapCoordinates(ctx.Point);
        if (point.MapId != caster.MapId)
            point = caster;

        var direction = point.Position - caster.Position;
        if (direction.LengthSquared() < 0.01f)
            direction = _transform.GetWorldRotation(ctx.Caster).ToWorldVec();

        var forward = MathF.Abs(direction.X) > MathF.Abs(direction.Y)
            ? new Vector2(MathF.Sign(direction.X), 0f)
            : new Vector2(0f, MathF.Sign(direction.Y));
        var side = new Vector2(-forward.Y, forward.X);
        var offsets = new List<Vector2>();

        switch (pattern)
        {
            case RuneSpawnPattern.AtCaster:
                offsets.Add(caster.Position);
                break;
            case RuneSpawnPattern.AtPoint:
                offsets.Add(point.Position);
                break;
            case RuneSpawnPattern.LineInFront:
            case RuneSpawnPattern.LineAtPoint:
            {
                var center = pattern == RuneSpawnPattern.LineInFront ? caster.Position + forward : point.Position;
                var half = Math.Max(0, size - 1) / 2;
                for (var i = -half; i <= half; i++)
                    offsets.Add(center + side * i);
                break;
            }
            case RuneSpawnPattern.RingAroundCaster:
            {
                var radius = Math.Max(1, size);
                for (var x = -radius; x <= radius; x++)
                for (var y = -radius; y <= radius; y++)
                {
                    if (Math.Max(Math.Abs(x), Math.Abs(y)) == radius)
                        offsets.Add(caster.Position + new Vector2(x, y));
                }
                break;
            }
            case RuneSpawnPattern.RingAtPoint:
            {
                var radius = Math.Max(1, size);
                for (var x = -radius; x <= radius; x++)
                for (var y = -radius; y <= radius; y++)
                {
                    if (Math.Max(Math.Abs(x), Math.Abs(y)) == radius)
                        offsets.Add(point.Position + new Vector2(x, y));
                }
                break;
            }
            case RuneSpawnPattern.AreaAtPoint:
            {
                var radius = Math.Max(0, size - 1);
                for (var x = -radius; x <= radius; x++)
                for (var y = -radius; y <= radius; y++)
                    offsets.Add(point.Position + new Vector2(x, y));
                break;
            }
        }

        var result = new List<(EntityCoordinates, bool)>(offsets.Count);
        foreach (var world in offsets)
        {
            var map = new MapCoordinates(world, caster.MapId);
            if (_mapManager.TryFindGridAt(map, out var gridUid, out var grid))
            {
                var tile = _map.CoordinatesToTile(gridUid, grid, map);
                var blocked = _turf.IsTileBlocked(gridUid, tile, CollisionGroup.Impassable, grid);
                result.Add((_map.GridTileToLocal(gridUid, grid, tile), blocked));
                continue;
            }

            result.Add((_transform.ToCoordinates(map), false));
        }

        return result;
    }

    private void ApplyPush(RuneCastContext ctx, RunePushEffect effect)
    {
        var center = AtPoint(ctx, effect.AtPoint, effect.Auto) ? _transform.ToMapCoordinates(ctx.Point) : _transform.GetMapCoordinates(ctx.Caster);
        _repulse.TryRepulseAttract(center, ctx.Caster, effect.Speed * ctx.Power, effect.Radius * ctx.RangeScale, _pushWhitelist, CollisionGroup.MobMask);
    }

    private static bool AtPoint(RuneCastContext ctx, bool atPoint, bool auto)
    {
        if (!auto)
            return atPoint;

        return ctx.AutoTarget is not (RuneEffectTarget.AroundCaster or RuneEffectTarget.Caster);
    }

    private void ApplyTeleport(RuneCastContext ctx, RuneTeleportEffect effect)
    {
        switch (effect.Mode)
        {
            case RuneTeleportMode.CasterToPoint:
            {
                var xform = Transform(ctx.Caster);
                if (xform.MapID != _transform.GetMapId(ctx.Point) || !_interaction.InRangeUnobstructed(ctx.Caster, ctx.Point, 1000f, CollisionGroup.Opaque, popup: true))
                    return;

                _transform.SetCoordinates(ctx.Caster, ctx.Point);
                _transform.AttachToGridOrMap(ctx.Caster, xform);
                break;
            }
            case RuneTeleportMode.SwapWithTarget:
                if (ctx.Target is { } target && !TerminatingOrDeleted(target))
                    _transform.SwapPositions(ctx.Caster, target);
                break;
            case RuneTeleportMode.ScatterCaster:
            {
                var center = _transform.GetMapCoordinates(ctx.Caster);
                for (var attempt = 0; attempt < 8; attempt++)
                {
                    var destination = new MapCoordinates(center.Position + _random.NextVector2(effect.Radius * 0.5f, effect.Radius), center.MapId);
                    if (!_mapManager.TryFindGridAt(destination, out var gridUid, out var grid))
                        continue;

                    var tile = _map.CoordinatesToTile(gridUid, grid, destination);
                    if (_turf.IsTileBlocked(gridUid, tile, CollisionGroup.Impassable, grid))
                        continue;

                    _transform.SetCoordinates(ctx.Caster, _map.GridTileToLocal(gridUid, grid, tile));
                    break;
                }
                break;
            }
            case RuneTeleportMode.ScatterTargets:
            {
                var center = _transform.ToMapCoordinates(ctx.Point);
                var radius = effect.Radius * ctx.RangeScale;
                foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(center, effect.Reach * ctx.RangeScale))
                {
                    if (mob.Owner == ctx.Caster)
                        continue;

                    var offset = _random.NextVector2(radius * 0.5f, radius);
                    var destination = new MapCoordinates(center.Position + offset, center.MapId);
                    if (!_mapManager.TryFindGridAt(destination, out var gridUid, out var grid))
                        continue;

                    var tile = _map.CoordinatesToTile(gridUid, grid, destination);
                    if (_turf.IsTileBlocked(gridUid, tile, CollisionGroup.Impassable, grid))
                        continue;

                    _transform.SetCoordinates(mob, _map.GridTileToLocal(gridUid, grid, tile));
                }
                break;
            }
        }
    }

    private ComponentRegistry? ComponentSource(RuneComponentsEffect effect)
    {
        if (effect.Template is { } template && Proto.TryIndex(template, out var proto))
            return proto.Components;

        if (effect.FromAction is { } action &&
            Proto.TryIndex(action, out var actionProto) &&
            actionProto.TryGetComponent<EntityTargetActionComponent>(out var targeted, Factory) &&
            targeted.Event is ChangeComponentsSpellEvent change)
            return change.ToAdd;

        return null;
    }

    private void ApplyRecall(RuneCastContext ctx)
    {
        if (!TryComp<RuneCasterComponent>(ctx.Caster, out var caster))
            return;

        if (_hands.GetActiveItem(ctx.Caster) is { } held)
        {
            caster.Recalled = caster.Recalled == held ? null : held;
            _popup.PopupEntity(Loc.GetString(caster.Recalled == null ? "rune-recall-unmarked" : "rune-recall-marked", ("item", held)), ctx.Caster, ctx.Caster);
            return;
        }

        if (caster.Recalled is not { } item || TerminatingOrDeleted(item))
        {
            caster.Recalled = null;
            _popup.PopupEntity(Loc.GetString("rune-recall-none"), ctx.Caster, ctx.Caster, PopupType.SmallCaution);
            return;
        }

        _container.TryRemoveFromContainer(item, true);
        _transform.SetCoordinates(item, Transform(ctx.Caster).Coordinates);
        _hands.PickupOrDrop(ctx.Caster, item, checkActionBlocker: false);
    }

    private void ApplyCharge(RuneCastContext ctx, RuneChargeEffect effect)
    {
        foreach (var held in _hands.EnumerateHeld(ctx.Caster))
        {
            if (!_tag.HasTag(held, WandTag))
                continue;

            if (TryComp<BasicEntityAmmoProviderComponent>(held, out var ammo) && ammo.Count != null)
                _gun.UpdateBasicEntityAmmoCount((held, ammo), ammo.Count.Value + effect.Charges);
            else if (TryComp<LimitedChargesComponent>(held, out var charges))
                _charges.AddCharges((held, charges, null), effect.Charges);
        }
    }

    private void ApplyMindSwap(RuneCastContext ctx, RuneMindSwapEffect effect)
    {
        if (ctx.Target is not { } target || target == ctx.Caster || TerminatingOrDeleted(target) ||
            !_mind.TryGetMind(ctx.Caster, out var casterMind, out _))
            return;

        var targetHasMind = _mind.TryGetMind(target, out var targetMind, out _);
        MoveCaster(ctx.Caster, target);

        _mind.TransferTo(casterMind, target);
        if (targetHasMind)
            _mind.TransferTo(targetMind, ctx.Caster);

        var stun = TimeSpan.FromSeconds(effect.Stun);
        _stun.TryUpdateParalyzeDuration(target, stun);
        _stun.TryUpdateParalyzeDuration(ctx.Caster, stun);
    }

    private void MoveCaster(EntityUid from, EntityUid to)
    {
        if (!TryComp<RuneCasterComponent>(from, out var source))
            return;

        foreach (var scroll in source.Scrolls.ToArray())
            Unbind(scroll);

        var copy = EnsureComp<RuneCasterComponent>(to);
        copy.Known = new HashSet<ProtoId<RunePrototype>>(source.Known);
        copy.Affinity = source.Affinity;
        copy.Weakness = source.Weakness;
        copy.Strain = source.Strain;
        copy.StrainChanged = source.StrainChanged;
        copy.Home = source.Home;
        copy.UltimateReady = source.UltimateReady;
        Dirty(to, copy);
        RemComp<RuneCasterComponent>(from);
    }

    private void ApplyComponents(EntityUid target, RuneComponentsEffect effect, float durationScale)
    {
        if (ComponentSource(effect) is not { } source)
            return;

        var added = new List<string>();
        var registry = new ComponentRegistry();
        foreach (var (name, entry) in source)
        {
            if (!Factory.TryGetRegistration(name, out var registration) || HasComp(target, registration.Type))
                continue;

            registry.Add(name, entry);
            added.Add(name);
        }

        if (registry.Count == 0)
            return;

        EntityManager.AddComponents(target, registry, false);

        if (effect.Duration is not { } duration)
            return;

        var timed = EnsureComp<RuneTimedComponentsComponent>(target);
        timed.Entries.Add(new RuneTimedEntry(added, Timing.CurTime + TimeSpan.FromSeconds(duration * durationScale)));
    }

    private void UpdateTimedComponents()
    {
        var query = EntityQueryEnumerator<RuneTimedComponentsComponent>();
        while (query.MoveNext(out var uid, out var timed))
        {
            for (var i = timed.Entries.Count - 1; i >= 0; i--)
            {
                var entry = timed.Entries[i];
                if (Timing.CurTime < entry.End)
                    continue;

                foreach (var name in entry.Added)
                {
                    if (Factory.TryGetRegistration(name, out var registration))
                        RemComp(uid, registration.Type);
                }

                timed.Entries.RemoveAt(i);
            }

            if (timed.Entries.Count == 0)
                RemCompDeferred<RuneTimedComponentsComponent>(uid);
        }
    }

    private void ApplyAnimate(RuneCastContext ctx, RuneAnimateEffect effect)
    {
        if (!Proto.TryIndex(effect.Template, out var template))
            return;

        var center = _transform.ToMapCoordinates(ctx.Point);
        var candidates = new List<(EntityUid Uid, float Distance)>();
        foreach (var item in _lookup.GetEntitiesInRange<AnimateableComponent>(center, effect.Radius * ctx.RangeScale))
        {
            if (!CanAnimate(item))
                continue;

            candidates.Add((item, (_transform.GetWorldPosition(item) - center.Position).LengthSquared()));
        }

        candidates.Sort((a, b) => a.Distance.CompareTo(b.Distance));
        var count = Math.Min(candidates.Count, effect.Count + ctx.Extra);
        var change = AnimateBase();
        for (var i = 0; i < count; i++)
        {
            var uid = candidates[i].Uid;
            if (change != null)
            {
                foreach (var name in change.ToRemove)
                {
                    if (Factory.TryGetRegistration(name, out var registration))
                        RemComp(uid, registration.Type);
                }

                EntityManager.AddComponents(uid, change.ToAdd, false);
            }

            EntityManager.AddComponents(uid, template.Components, true);
            ScaleAnimateHealth(uid);
        }

        if (count == 0)
            _popup.PopupEntity(Loc.GetString("rune-animate-nothing"), ctx.Caster, ctx.Caster, PopupType.SmallCaution);
    }

    private ChangeComponentsSpellEvent? AnimateBase()
    {
        if (!Proto.TryIndex(AnimateAction, out var action) ||
            !action.TryGetComponent<EntityTargetActionComponent>(out var target, Factory))
            return null;

        return target.Event as ChangeComponentsSpellEvent;
    }

    private bool CanAnimate(EntityUid uid)
    {
        if (HasComp<MindContainerComponent>(uid) || HasComp<AnimateComponent>(uid) || _tag.HasTag(uid, AnimateBlacklistTag))
            return false;

        foreach (var name in AnimateBlacklist)
        {
            if (Factory.TryGetRegistration(name, out var registration) && HasComp(uid, registration.Type))
                return false;
        }

        return !HasComp<MovementSpeedModifierComponent>(uid);
    }

    private void ScaleAnimateHealth(EntityUid uid)
    {
        if (!TryComp<DestructibleComponent>(uid, out var destructible))
            return;

        foreach (var threshold in destructible.Thresholds)
        {
            if (threshold.Trigger is DamageTrigger trigger)
                trigger.Damage *= AnimateHealthScale;
        }
    }

    private void GrantImmortality(EntityUid target, float seconds)
    {
        if (seconds <= 0f)
            return;

        var granted = !HasComp<GodmodeComponent>(target);
        var immortality = EnsureComp<RuneImmortalityComponent>(target);
        if (granted)
            immortality.Granted = true;

        immortality.End = Timing.CurTime + TimeSpan.FromSeconds(seconds);
        if (immortality.Granted)
            _godmode.EnableGodmode(target);
    }

    private void UpdateImmortality()
    {
        var query = EntityQueryEnumerator<RuneImmortalityComponent>();
        while (query.MoveNext(out var uid, out var immortality))
        {
            if (Timing.CurTime < immortality.End)
                continue;

            if (immortality.Granted)
                _godmode.DisableGodmode(uid);

            RemCompDeferred<RuneImmortalityComponent>(uid);
        }
    }

    private void ApplyLightning(RuneCastContext ctx, RuneLightningEffect effect)
    {
        var origin = Spawn(null, _transform.ToMapCoordinates(ctx.Point));
        EnsureComp<TimedDespawnComponent>(origin).Lifetime = 2f;
        _lightningOwner = ctx.Caster;
        try
        {
            _lightning.ShootRandomLightnings(origin, effect.Range * ctx.RangeScale, effect.Bolts + ctx.Extra, effect.Prototype);
        }
        finally
        {
            _lightningOwner = null;
        }
    }

    private void ApplySpeak(RuneCastContext ctx, RuneSpeakEffect effect)
    {
        if (!Proto.TryIndex(effect.Lines, out var lines) || lines.Values.Count == 0)
            return;

        foreach (var target in Targets(ctx, effect))
        {
            var line = Loc.GetString(_random.Pick(lines.Values));
            _chat.TrySendInGameICMessage(target, line, InGameICChatType.Speak, false, ignoreActionBlocker: true);
        }
    }
}

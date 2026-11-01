using Content.Server.Access.Systems;
using Content.Server.Cargo.Components;
using Content.Server.Damage.Systems;
using Content.Shared.Access.Components;
using Content.Shared.Access;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.DeadSpace.Halloween;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Nutrition;
using Content.Shared.Stacks;
using Content.Server.Speech.Components;
using Content.Shared.Tag;
using Content.Shared.StatusEffect;
using Content.Shared.Throwing;
using OldStatusEffects = Content.Shared.StatusEffect.StatusEffectsSystem;
using NewStatusEffects = Content.Shared.StatusEffectNew.StatusEffectsSystem;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using System.Numerics;
using System.Linq;
using Robust.Server.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Halloween;

/// <summary>
/// Applies a random candy effect when a Halloween candy is eaten,
/// announcing the effect name to the eater's screen with a sound.
/// </summary>
public sealed class CandySystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly AccessSystem _access = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly GodmodeSystem _godmode = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly TagSystem _tags = default!;
    [Dependency] private readonly SharedHumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MobThresholdSystem _threshold = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    [Dependency] private readonly SharedPointLightSystem _light = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly OldStatusEffects _status = default!;
    [Dependency] private readonly NewStatusEffects _newStatus = default!;
    [Dependency] private readonly SharedContentEyeSystem _eye = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;

    private static readonly ProtoId<TagPrototype> HighRiskItemTag = "HighRiskItem";

    private readonly Dictionary<EntityUid, string> _lastCandy = new();
    private TimeSpan _nextLastCandyPrune;

    private List<EntProtoId>? _cornSticksPool;

    private static readonly string[] CornSticksExcludedComponents =
    {
        "Explosive",
        "ExplodeOnTrigger",
        "NukeDisk",
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CandyComponent, FullyEatenEvent>(OnFullyEaten);

        SubscribeLocalEvent<CandySpeedBoostComponent, RefreshMovementSpeedModifiersEvent>(OnSpeedBoostRefresh);
        SubscribeLocalEvent<CandyTimedSpeedComponent, RefreshMovementSpeedModifiersEvent>(OnTimedSpeedRefresh);
    }

    private void OnFullyEaten(Entity<CandyComponent> ent, ref FullyEatenEvent args)
    {
        var user = args.User;

        // Only mobs get candy effects.
        if (!HasComp<MobStateComponent>(user) || !HasComp<DamageableComponent>(user))
            return;

        var protoId = MetaData(ent).EntityPrototype?.ID;

        // Announce the effect name on top of the eater's screen with a sound.
        var effect = ent.Comp.Effect ?? _random.Pick(Enum.GetValues<CandyEffectKind>());

        if (TryComp<ActorComponent>(user, out var actor))
            RaiseNetworkEvent(new CandyAnnounceEvent(effect), actor.PlayerSession.Channel);

        switch (effect)
        {
            case CandyEffectKind.GoodAftertaste:
                GoodAftertaste(user, 0.5f);
                break;
            case CandyEffectKind.PerfectAftertaste:
                GoodAftertaste(user, 0f);
                break;
            case CandyEffectKind.ChocolateCoin:
                ChocolateCoin(user);
                break;
            case CandyEffectKind.Cinnamon:
                AddSpeedBoost(user, 0.1f);
                break;
            case CandyEffectKind.Duplicate:
                Duplicate(user);
                break;
            case CandyEffectKind.Glowstick:
                Glowstick(user);
                break;
            case CandyEffectKind.MoneyWalk:
                MoneyWalk(user);
                break;
            case CandyEffectKind.CornSticks:
                CornSticks(user);
                break;
            case CandyEffectKind.Regurgitation:
                Regurgitation(user, protoId);
                break;
            case CandyEffectKind.ToothBreaker:
                ToothBreaker(user);
                break;
            case CandyEffectKind.ForAmateurs:
                ScaleThresholds(user, 0.5f);
                break;
            case CandyEffectKind.Fragility:
                AddSpeedBoost(user, 0.5f);
                ScaleThresholds(user, 0.8f);
                break;
            case CandyEffectKind.YourElement:
                YourElement(user);
                break;
            case CandyEffectKind.Nothing:
                Nothing(user);
                break;
            case CandyEffectKind.Nausea:
                Nausea(user);
                break;
            case CandyEffectKind.Oops:
                Oops(user);
                break;
            case CandyEffectKind.CrispyToffee:
                CrispyToffee(user);
                break;
            case CandyEffectKind.ControlInvert:
                InvertControls(user);
                break;
            case CandyEffectKind.TooSweet:
                TooSweet(user);
                break;
            case CandyEffectKind.Earplugs:
                if (TryComp<ActorComponent>(user, out var earActor))
                    RaiseNetworkEvent(new CandyMuteEvent(TimeSpan.FromSeconds(30)), earActor.PlayerSession.Channel);
                break;
            case CandyEffectKind.Butterfingers:
                Butterfingers(user);
                break;
            case CandyEffectKind.Jinx:
                _status.TryAddStatusEffect<TemporaryBlindnessComponent>(user, "TemporaryBlindness",
                    TimeSpan.FromSeconds(30), true);
                break;
            case CandyEffectKind.VulpEater:
                VulpEater(user);
                break;
            case CandyEffectKind.ImAdequate:
                _newStatus.TryAddStatusEffectDuration(user, "StatusEffectParacusia", TimeSpan.FromSeconds(30));
                break;
        }

        // Record this candy as the "last eaten one" (used by future regurgitation).
        if (protoId != null)
            _lastCandy[user] = protoId;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;

        if (now >= _nextLastCandyPrune)
        {
            _nextLastCandyPrune = now + TimeSpan.FromMinutes(1);

            foreach (var key in _lastCandy.Keys.Where(k => !Exists(k)).ToList())
                _lastCandy.Remove(key);
        }

        // Money walk: drop one credit behind the player for 30 seconds.
        var trailQuery = EntityQueryEnumerator<CandyMoneyTrailComponent>();
        while (trailQuery.MoveNext(out var uid, out var trail))
        {
            if (now >= trail.EndTime)
            {
                RemComp<CandyMoneyTrailComponent>(uid);
                continue;
            }

            if (now < trail.NextDrop)
                continue;

            trail.NextDrop = now + TimeSpan.FromSeconds(1);
            var cash = Spawn("SpaceCash", _transform.GetMoverCoordinates(uid));
            _stack.SetCount(cash, 1);
        }

        // Expire temporary speed modifiers.
        var speedQuery = EntityQueryEnumerator<CandyTimedSpeedComponent>();
        while (speedQuery.MoveNext(out var uid, out var timed))
        {
            if (now < timed.EndTime)
                continue;

            RemComp<CandyTimedSpeedComponent>(uid);
            _movement.RefreshMovementSpeedModifiers(uid);
        }
    }

    // Good aftertaste: heals a fraction of total damage (0 = full heal) and removes all status effects.
    private void GoodAftertaste(EntityUid user, float fraction)
    {
        if (TryComp<DamageableComponent>(user, out var damageable))
        {
            if (fraction > 0f && damageable.TotalDamage > 0)
                _damageable.SetDamage((user, damageable), damageable.Damage * fraction);
            else
                _damageable.SetAllDamage((user, damageable), 0);
        }

        RemoveAllStatusEffects(user);
    }

    private void RemoveAllStatusEffects(EntityUid user)
    {
        if (!TryComp<StatusEffectsComponent>(user, out var status))
            return;

        foreach (var key in status.ActiveEffects.Keys.ToList())
            _status.TryRemoveStatusEffect(user, key);
    }

    // Chocolate coin: random map access on the first access card found and a random stack of credits.
    private void ChocolateCoin(EntityUid user)
    {
        var card = FindAccessCard(user);
        if (card != null)
        {
            var currentTags = _access.TryGetTags(card.Value);
            if (currentTags != null)
            {
                var levels = new List<ProtoId<AccessLevelPrototype>>();
                foreach (var level in _proto.EnumeratePrototypes<AccessLevelPrototype>())
                    levels.Add(new ProtoId<AccessLevelPrototype>(level.ID));

                var pick = _random.Pick(levels);
                var newTags = new HashSet<ProtoId<AccessLevelPrototype>>(currentTags) { pick };
                _access.TrySetTags(card.Value, newTags);
            }
        }

        var amount = _random.Next(100, 10001);
        var cash = Spawn("SpaceCash", _transform.GetMoverCoordinates(user));
        _stack.SetCount(cash, amount);

        if (TryComp<StaticPriceComponent>(cash, out var price))
            price.Price = amount;

        _hands.TryPickupAnyHand(user, cash);
    }

    private EntityUid? FindAccessCard(EntityUid user)
    {
        if (HasComp<AccessComponent>(user))
            return user;

        return SearchAccessCard(user, 0);
    }

    private EntityUid? SearchAccessCard(EntityUid uid, int depth)
    {
        if (depth > 3 || !TryComp<ContainerManagerComponent>(uid, out var manager))
            return null;

        foreach (var container in manager.Containers.Values)
        {
            foreach (var ent in container.ContainedEntities)
            {
                if (HasComp<AccessComponent>(ent))
                    return ent;

                var nested = SearchAccessCard(ent, depth + 1);
                if (nested != null)
                    return nested;
            }
        }

        return null;
    }

    // Cinnamon: small but permanent speed boost, stacks with itself.
    private void AddSpeedBoost(EntityUid user, float bonus)
    {
        var comp = EnsureComp<CandySpeedBoostComponent>(user);
        comp.Bonus += bonus;
        _movement.RefreshMovementSpeedModifiers(user);
    }

    // Duplicate: copies every item from hands, pockets and belt to the floor.
    private void Duplicate(EntityUid user)
    {
        var coords = _transform.GetMoverCoordinates(user);

        foreach (var held in _hands.EnumerateHeld(user))
            CloneItem(held, coords);

        foreach (var slot in new[] { "belt", "pocket1", "pocket2" })
        {
            if (_inventory.TryGetSlotEntity(user, slot, out var item))
                CloneItem(item.Value, coords);
        }
    }

    private void TooSweet(EntityUid user)
    {
        if (!_mobState.IsAlive(user))
            return;

        // Pin the total damage exactly at the PreCritical threshold, otherwise the state
        // gets recalculated back to Alive a moment later. A single Blunt entry keeps the
        // total equal to the threshold (SetAllDamage would multiply it across every type).
        if (_threshold.TryGetThresholdForState(user, MobState.PreCritical, out var threshold) &&
            threshold is { } thresholdValue &&
            TryComp<DamageableComponent>(user, out var damageable))
        {
            var spec = new DamageSpecifier { DamageDict = { ["Blunt"] = thresholdValue } };
            _damageable.SetDamage((user, damageable), spec);
        }

        _mobState.ChangeMobState(user, MobState.PreCritical, origin: user);
    }

    private void CloneItem(EntityUid item, EntityCoordinates coords)
    {
        if (_tags.HasTag(item, HighRiskItemTag))
            return;

        // The eaten candy itself must not duplicate.
        if (HasComp<CandyComponent>(item))
            return;

        var proto = MetaData(item).EntityPrototype;
        if (proto == null || proto.Abstract || proto.HideSpawnMenu)
            return;

        Spawn(proto.ID, coords);
    }

    // Glowstick: strong purple light around the player for 30 seconds.
    private void Glowstick(EntityUid user)
    {
        if (HasComp<PointLightComponent>(user))
            return;

        var light = EnsureComp<PointLightComponent>(user);
        _light.SetColor(user, Color.FromHex("#8a2be2"), light);
        _light.SetRadius(user, 12f, light);
        _light.SetEnergy(user, 8f, light);
        _light.SetEnabled(user, true, light);

        Timer.Spawn(TimeSpan.FromSeconds(30), () =>
        {
            if (!Exists(user) || TerminatingOrDeleted(user))
                return;

            RemComp<PointLightComponent>(user);
        });
    }

    // Money walk: leaves one credit behind the player for 30 seconds.
    private void MoneyWalk(EntityUid user)
    {
        if (HasComp<CandyMoneyTrailComponent>(user))
            return;

        var comp = EnsureComp<CandyMoneyTrailComponent>(user);
        comp.EndTime = _timing.CurTime + TimeSpan.FromSeconds(30);
        comp.NextDrop = _timing.CurTime + TimeSpan.FromSeconds(1);
    }

    // Corn sticks: spits out a random item from the unsafe gift pool.
    private List<EntProtoId> BuildCornSticksPool()
    {
        var itemCompName = Factory.GetComponentName<ItemComponent>();
        var tagCompName = Factory.GetComponentName<TagComponent>();
        var pool = new List<EntProtoId>();

        foreach (var proto in _proto.EnumeratePrototypes<EntityPrototype>())
        {
            if (proto.Abstract || proto.HideSpawnMenu)
                continue;

            if (!proto.Components.ContainsKey(itemCompName))
                continue;

            if (CornSticksExcludedComponents.Any(proto.Components.ContainsKey))
                continue;

            if (proto.Components.TryGetValue(tagCompName, out var tagEntry) &&
                tagEntry.Component is TagComponent tags &&
                _tags.HasTag(tags, HighRiskItemTag))
            {
                continue;
            }

            pool.Add(new EntProtoId(proto.ID));
        }

        return pool;
    }

    private void CornSticks(EntityUid user)
    {
        _cornSticksPool ??= BuildCornSticksPool();

        if (_cornSticksPool.Count == 0)
            return;

        var pick = _random.Pick(_cornSticksPool);
        var item = Spawn(pick, _transform.GetMoverCoordinates(user).Offset(new Vector2(
            _random.NextFloat(-0.5f, 0.5f),
            _random.NextFloat(-0.5f, 0.5f))));
        _throwing.TryThrow(item, _random.NextVector2Box(), 8f, user);
    }

    // Regurgitation: the player spits out the last candy they ate.
    private void Regurgitation(EntityUid user, string? current)
    {
        _lastCandy.TryGetValue(user, out var previous);
        var candy = !string.IsNullOrEmpty(previous) ? previous : current;
        if (string.IsNullOrEmpty(candy))
            return;

        var coords = _transform.GetMoverCoordinates(user).Offset(new Vector2(0f, 0.5f));
        var candyEnt = Spawn(candy, coords);
        _throwing.TryThrow(candyEnt, _transform.GetWorldRotation(user).ToWorldVec(), 5f, user);
    }

    // Tooth breaker: the player becomes invulnerable for two seconds.
    private void ToothBreaker(EntityUid user)
    {
        if (HasComp<GodmodeComponent>(user))
            return;

        _godmode.EnableGodmode(user);

        Timer.Spawn(TimeSpan.FromSeconds(2), () =>
        {
            if (!Exists(user) || TerminatingOrDeleted(user))
                return;

            _godmode.DisableGodmode(user);
        });
    }

    // Scales all mob state thresholds (max health) by a factor.
    private void ScaleThresholds(EntityUid user, float factor)
    {
        if (!TryComp<MobThresholdsComponent>(user, out var thresholds))
            return;

        // SetMobStateThreshold mutates the dictionary, so iterate over a snapshot.
        var snapshot = thresholds.Thresholds.ToList();
        foreach (var (damage, state) in snapshot)
            _threshold.SetMobStateThreshold(user, damage * factor, state);
    }

    // Your element: the player turns green for others and their own screen slightly greens.
    private void YourElement(EntityUid user)
    {
        // Networked marker: the client visualizer tints the entity and everything on it.
        // Stays on the player permanently.
        EnsureComp<CandyGreenComponent>(user);

        if (TryComp<ActorComponent>(user, out var actor))
            RaiseNetworkEvent(new CandyScreenTintEvent(TimeSpan.FromSeconds(20)), actor.PlayerSession.Channel);
    }

    private void VulpEater(EntityUid user)
    {
        _humanoid.SetSpecies(user, "Vulpkanin");
        EnsureComp<RoarAccentComponent>(user);
    }

    // Nothing?: a random item disappears from the player's bag.
    private void Nothing(EntityUid user)
    {
        if (!_inventory.TryGetSlotEntity(user, "back", out var bag) || bag == null)
            return;

        if (!TryComp<ContainerManagerComponent>(bag, out var manager))
            return;

        var items = new List<EntityUid>();
        foreach (var container in manager.Containers.Values)
            items.AddRange(container.ContainedEntities.Where(e => !_tags.HasTag(e, HighRiskItemTag)));

        if (items.Count == 0)
            return;

        Del(_random.Pick(items));
    }

    // Nausea: doubles the player's speed but maxes the camera zoom in for 20 seconds.
    private void Nausea(EntityUid user)
    {
        var timed = EnsureComp<CandyTimedSpeedComponent>(user);
        timed.Modifier = 2f;
        timed.EndTime = _timing.CurTime + TimeSpan.FromSeconds(20);
        _movement.RefreshMovementSpeedModifiers(user);

        if (TryComp<EyeComponent>(user, out var eye))
        {
            var original = eye.Zoom;
            _eye.SetZoom(user, new Vector2(5f, 5f), ignoreLimits: true);

            Timer.Spawn(TimeSpan.FromSeconds(20), () =>
            {
                if (!Exists(user) || TerminatingOrDeleted(user))
                    return;

                _eye.SetZoom(user, original, ignoreLimits: true);
            });
        }

        // The player also feels queasy: slightly green screen.
        if (TryComp<ActorComponent>(user, out var actor))
            RaiseNetworkEvent(new CandyScreenTintEvent(TimeSpan.FromSeconds(20)), actor.PlayerSession.Channel);
    }

    // Oops: throws the player forward with huge force.
    private void Oops(EntityUid user)
    {
        var rotation = _transform.GetWorldRotation(user);
        _throwing.TryThrow(user, rotation.ToWorldVec(), 40f, user);
    }

    // Crispy toffee: five times speed for one second, then slowed for five seconds.
    private void CrispyToffee(EntityUid user)
    {
        var timed = EnsureComp<CandyTimedSpeedComponent>(user);
        timed.Modifier = 5f;
        timed.EndTime = _timing.CurTime + TimeSpan.FromSeconds(1);
        _movement.RefreshMovementSpeedModifiers(user);

        Timer.Spawn(TimeSpan.FromSeconds(1), () =>
        {
            if (!Exists(user) || TerminatingOrDeleted(user))
                return;

            var slow = EnsureComp<CandyTimedSpeedComponent>(user);
            slow.Modifier = 0.25f;
            slow.EndTime = _timing.CurTime + TimeSpan.FromSeconds(5);
            _movement.RefreshMovementSpeedModifiers(user);
        });
    }

    // Body swap: swaps the player's position with a random other player's.
    private void InvertControls(EntityUid user)
    {
        if (HasComp<CandyInvertControlsComponent>(user))
            return;

        EnsureComp<CandyInvertControlsComponent>(user);

        Timer.Spawn(TimeSpan.FromSeconds(20), () =>
        {
            if (!Exists(user) || TerminatingOrDeleted(user))
                return;

            RemComp<CandyInvertControlsComponent>(user);
        });
    }

    // Butterfingers: everything on the player falls to the floor.
    private void Butterfingers(EntityUid user)
    {
        foreach (var held in _hands.EnumerateHeld(user).ToList())
            _hands.TryDrop(user, held);

        if (TryComp<InventoryComponent>(user, out var inventory))
        {
            foreach (var slot in inventory.Slots)
                _inventory.TryUnequip(user, slot.Name, force: true);
        }
    }

    private void OnSpeedBoostRefresh(Entity<CandySpeedBoostComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(1f + ent.Comp.Bonus);
    }

    private void OnTimedSpeedRefresh(Entity<CandyTimedSpeedComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(ent.Comp.Modifier);
    }
}
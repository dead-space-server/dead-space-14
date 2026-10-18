// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using System.Numerics;
using Content.Server.DeadSpace.Lavaland.Components;
using Content.Server.Station.Systems;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.Administration.Logs;
using Content.Shared.Administration.Systems;
using Content.Shared.Body.Systems;
using Content.Shared.Chat;
using Content.Shared.Chemistry.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.Examine;
using Content.Shared.Ghost;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory.Events;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Lavaland;

/// <summary>
/// Megafauna loot ported from tgstation: mayhem in a bottle, anomalous crystals,
/// the voice of god and the H.E.C.K. suit curse.
/// </summary>
public sealed class LavalandBossLootSystem : EntitySystem
{
    [Dependency] private readonly BloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly GunSystem _gun = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLog = default!;
    [Dependency] private readonly ITileDefinitionManager _tiles = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly RejuvenateSystem _rejuvenate = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedHumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly StationSpawningSystem _spawning = default!;

    private readonly HashSet<Entity<MobStateComponent>> _mobs = new();
    private readonly List<EntityUid> _expired = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LavalandMayhemBottleComponent, UseInHandEvent>(OnMayhem);
        SubscribeLocalEvent<LavalandAnomalousCrystalComponent, InteractHandEvent>(OnCrystalTouch);
        SubscribeLocalEvent<LavalandAnomalousCrystalComponent, ExaminedEvent>(OnCrystalExamined);
        SubscribeLocalEvent<LavalandVoiceOfGodItemComponent, UseInHandEvent>(OnVoiceItemUse);
        SubscribeLocalEvent<LavalandVoiceOfGodComponent, EntitySpokeEvent>(OnVoiceSpoke);
        SubscribeLocalEvent<LavalandCursedWhispersComponent, GotEquippedEvent>(OnCursedEquipped);
        SubscribeLocalEvent<LavalandCursedWhispersComponent, GotUnequippedEvent>(OnCursedUnequipped);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;

        _expired.Clear();
        var mayhem = EntityQueryEnumerator<LavalandMayhemAffectedComponent>();
        while (mayhem.MoveNext(out var uid, out var affected))
        {
            if (now >= affected.EndsAt)
                _expired.Add(uid);
        }
        foreach (var uid in _expired)
            EndMayhem(uid);

        var crystals = EntityQueryEnumerator<LavalandAnomalousCrystalComponent>();
        while (crystals.MoveNext(out var uid, out var crystal))
        {
            if (crystal.FireAt is not { } fireAt || now < fireAt)
                continue;

            crystal.FireAt = null;
            _appearance.SetData(uid, Shared.DeadSpace.Lavaland.LavalandArtifactVisuals.Active, false);
            TriggerCrystal((uid, crystal));
        }

        var cursed = EntityQueryEnumerator<LavalandCursedWhispersComponent>();
        while (cursed.MoveNext(out _, out var curse))
        {
            if (curse.Wearer is not { } wearer || now < curse.NextWhisper || curse.Messages.Count == 0)
                continue;

            curse.NextWhisper = now + RandomInterval(curse);
            _popup.PopupEntity(Loc.GetString(_random.Pick(curse.Messages)), wearer, wearer, PopupType.SmallCaution);
        }
    }

    #region Mayhem

    private void OnMayhem(Entity<LavalandMayhemBottleComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;

        var user = args.User;
        _adminLog.Add(LogType.Action, LogImpact.High, $"{ToPrettyString(user):user} shattered a bottle of mayhem");
        if (ent.Comp.Sound != null)
            _audio.PlayPvs(ent.Comp.Sound, user);

        var coordinates = Transform(user).Coordinates;
        QueueDel(ent);

        _mobs.Clear();
        _lookup.GetEntitiesInRange(coordinates, ent.Comp.Range, _mobs);
        foreach (var mob in _mobs)
        {
            if (!HasComp<HumanoidAppearanceComponent>(mob) || _mobState.IsDead(mob, mob.Comp) ||
                HasComp<LavalandMayhemAffectedComponent>(mob))
            {
                continue;
            }

            StartMayhem(mob, ent.Comp);
        }
    }

    private void StartMayhem(EntityUid target, LavalandMayhemBottleComponent bottle)
    {
        foreach (var held in _hands.EnumerateHeld(target).ToList())
            _hands.TryDrop(target, held, checkActionBlocker: false);

        var weapon = Spawn(bottle.Weapon, Transform(target).Coordinates);
        EnsureComp<UnremoveableComponent>(weapon);
        _hands.PickupOrDrop(target, weapon);

        if (bottle.Reagent is { } reagent)
            _bloodstream.TryAddToBloodstream(target, new Solution(reagent, bottle.ReagentAmount));

        var affected = EnsureComp<LavalandMayhemAffectedComponent>(target);
        affected.Weapon = weapon;
        affected.EndsAt = _timing.CurTime + bottle.Duration;
        _popup.PopupEntity(Loc.GetString("lavaland-mayhem-start"), target, target, PopupType.LargeCaution);
        _adminLog.Add(LogType.Action, LogImpact.High, $"{ToPrettyString(target):target} entered a mayhem frenzy");
    }

    private void EndMayhem(EntityUid target)
    {
        if (TryComp<LavalandMayhemAffectedComponent>(target, out var affected) && affected.Weapon is { } weapon)
            QueueDel(weapon);
        RemComp<LavalandMayhemAffectedComponent>(target);
        _popup.PopupEntity(Loc.GetString("lavaland-mayhem-end"), target, target);
    }

    #endregion

    #region Anomalous crystals

    private void OnCrystalExamined(Entity<LavalandAnomalousCrystalComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.ObserverDescription is { } description && HasComp<GhostComponent>(args.Examiner))
            args.PushMarkup(Loc.GetString(description));
    }

    private void OnCrystalTouch(Entity<LavalandAnomalousCrystalComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;

        var now = _timing.CurTime;
        if (now < ent.Comp.NextUse || ent.Comp.FireAt != null)
            return;

        ent.Comp.NextUse = now + ent.Comp.ChargeTime + ent.Comp.Cooldown;
        ent.Comp.FireAt = now + ent.Comp.ChargeTime;
        ent.Comp.Activator = args.User;
        _appearance.SetData(ent, Shared.DeadSpace.Lavaland.LavalandArtifactVisuals.Active, true);
        if (ent.Comp.Sound != null)
            _audio.PlayPvs(ent.Comp.Sound, ent);
    }

    private void TriggerCrystal(Entity<LavalandAnomalousCrystalComponent> ent)
    {
        switch (ent.Comp.Effect)
        {
            case LavalandCrystalEffect.Emitter:
                FireCrystal(ent);
                break;
            case LavalandCrystalEffect.Revive:
                ReviveAround(ent);
                break;
            case LavalandCrystalEffect.ThemeWarp:
                WarpTheme(ent);
                break;
        }
    }

    private void FireCrystal(Entity<LavalandAnomalousCrystalComponent> ent)
    {
        if (ent.Comp.Projectiles.Count == 0)
            return;

        var origin = _transform.GetWorldPosition(ent);
        var direction = ent.Comp.Activator is { } activator && Exists(activator)
            ? origin - _transform.GetWorldPosition(activator)
            : _random.NextAngle().ToVec();
        if (direction.LengthSquared() < 0.01f)
            direction = _random.NextAngle().ToVec();

        var projectile = Spawn(_random.Pick(ent.Comp.Projectiles), Transform(ent).Coordinates);
        _gun.ShootProjectile(projectile, Vector2.Normalize(direction), Vector2.Zero, ent, ent, ent.Comp.ProjectileSpeed);
    }

    private void ReviveAround(Entity<LavalandAnomalousCrystalComponent> ent)
    {
        _mobs.Clear();
        _lookup.GetEntitiesInRange(Transform(ent).Coordinates, ent.Comp.Range, _mobs);
        foreach (var mob in _mobs)
        {
            if (!HasComp<HumanoidAppearanceComponent>(mob) || !_mobState.IsDead(mob, mob.Comp))
                continue;

            if (ent.Comp.ReviveSpecies is { } species)
                _humanoid.SetSpecies(mob, species);
            _rejuvenate.PerformRejuvenate(mob);
            if (ent.Comp.ReviveGear is { } gear)
                _spawning.EquipStartingGear(mob, gear);
            _adminLog.Add(LogType.Action, LogImpact.Medium, $"{ToPrettyString(ent.Owner):crystal} revived {ToPrettyString(mob):target}");
        }
    }

    private void WarpTheme(Entity<LavalandAnomalousCrystalComponent> ent)
    {
        var xform = Transform(ent);
        if (ent.Comp.ThemeTiles.Count == 0 || xform.GridUid is not { } gridUid ||
            !TryComp<MapGridComponent>(gridUid, out var grid))
        {
            return;
        }

        var tileDef = (ContentTileDefinition) _tiles[_random.Pick(ent.Comp.ThemeTiles)];
        var center = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        var radius = (int) MathF.Ceiling(ent.Comp.Range);
        for (var x = -radius; x <= radius; x++)
        {
            for (var y = -radius; y <= radius; y++)
            {
                if (x * x + y * y > ent.Comp.Range * ent.Comp.Range)
                    continue;

                var indices = center + new Vector2i(x, y);
                if (!_map.TryGetTileRef(gridUid, grid, indices, out var tileRef) || tileRef.Tile.IsEmpty)
                    continue;

                _map.SetTile(gridUid, grid, indices, new Tile(tileDef.TileId));
            }
        }
    }

    #endregion

    #region Voice of god

    private void OnVoiceItemUse(Entity<LavalandVoiceOfGodItemComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;

        var voice = EnsureComp<LavalandVoiceOfGodComponent>(args.User);
        voice.Commands = new List<LavalandVoiceCommand>(ent.Comp.Commands);
        voice.Range = ent.Comp.Range;
        voice.Sound = ent.Comp.Sound;
        _popup.PopupEntity(Loc.GetString("lavaland-voice-of-god-gained"), args.User, args.User, PopupType.Large);
        _adminLog.Add(LogType.Action, LogImpact.Medium, $"{ToPrettyString(args.User):user} gained the voice of god");
        QueueDel(ent);
    }

    private void OnVoiceSpoke(Entity<LavalandVoiceOfGodComponent> ent, ref EntitySpokeEvent args)
    {
        var now = _timing.CurTime;
        if (now < ent.Comp.NextCommand || args.ObfuscatedMessage != null)
            return;

        // What was said, before any accent mangled it: a unathi hissing an order still gives it.
        var message = (string.IsNullOrEmpty(args.OriginalMessage) ? args.Message : args.OriginalMessage).ToLowerInvariant();
        LavalandVoiceCommand? command = null;
        foreach (var candidate in ent.Comp.Commands)
        {
            if (candidate.Words.Any(word => message.Contains(word)))
            {
                command = candidate;
                break;
            }
        }

        if (command == null)
            return;

        ent.Comp.NextCommand = now + command.Cooldown;
        if (ent.Comp.Sound != null)
            _audio.PlayPvs(ent.Comp.Sound, ent);

        var origin = _transform.GetWorldPosition(ent);
        _mobs.Clear();
        _lookup.GetEntitiesInRange(Transform(ent).Coordinates, ent.Comp.Range, _mobs);
        foreach (var mob in _mobs)
        {
            if (mob.Owner == ent.Owner || _mobState.IsDead(mob, mob.Comp))
                continue;

            if (command.Stun > TimeSpan.Zero)
                _stun.TryUpdateParalyzeDuration(mob, command.Stun);
            if (command.Knockdown > TimeSpan.Zero)
                _stun.TryKnockdown(mob.Owner, command.Knockdown);
            if (command.Damage is { } damage)
                _damageable.TryChangeDamage(mob.Owner, damage, true, origin: ent);
            if (command.Repel > 0f && TryComp<PhysicsComponent>(mob, out var physics))
            {
                var away = _transform.GetWorldPosition(mob) - origin;
                if (away.LengthSquared() > 0.01f)
                    _physics.SetLinearVelocity(mob, Vector2.Normalize(away) * command.Repel, body: physics);
            }
        }

        _adminLog.Add(LogType.Action, LogImpact.Medium, $"{ToPrettyString(ent.Owner):user} used the voice of god: {args.Message}");
    }

    #endregion

    #region H.E.C.K. suit

    private void OnCursedEquipped(Entity<LavalandCursedWhispersComponent> ent, ref GotEquippedEvent args)
    {
        ent.Comp.Wearer = args.Equipee;
        ent.Comp.NextWhisper = _timing.CurTime + RandomInterval(ent.Comp);
    }

    private void OnCursedUnequipped(Entity<LavalandCursedWhispersComponent> ent, ref GotUnequippedEvent args)
    {
        if (ent.Comp.Wearer == args.Equipee)
            ent.Comp.Wearer = null;
    }

    private TimeSpan RandomInterval(LavalandCursedWhispersComponent curse)
    {
        var min = curse.MinInterval.TotalSeconds;
        var max = Math.Max(min, curse.MaxInterval.TotalSeconds);
        return TimeSpan.FromSeconds(_random.NextDouble(min, max));
    }

    #endregion
}

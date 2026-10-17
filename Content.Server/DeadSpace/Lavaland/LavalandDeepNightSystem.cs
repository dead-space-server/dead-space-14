// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using Content.Shared.Stacks;
using Content.Shared.Temperature.Components;
using Content.Server.DeadSpace.Attachments;
using Content.Server.DeadSpace.Components.NightVision;
using Content.Server.DeadSpace.Lavaland.ChromiteCaves;
using Content.Server.DeadSpace.Lavaland.Components;
using Content.Server.Light.Components;
using Content.Server.Light.EntitySystems;
using Content.Shared.Clothing.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.Attachments;
using Content.Shared.DeadSpace.Lavaland;
using Content.Shared.Inventory;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Lavaland;

/// <summary>
/// What the deep night does (<see cref="LavalandDeepNightConfig"/>): night vision does not turn on, PDA and helmet
/// lights die quickly and go on cooldown, flashlights drain their battery, and players out of any light get burnt.
/// Released lantern wisps are the exception: they shine brightly and lead home.
/// </summary>
public sealed class LavalandDeepNightSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly AttachmentSystem _attachment = default!;
    [Dependency] private readonly LavalandChromiteCavesSystem _caves = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly HandheldLightSystem _handheldLight = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] private readonly SharedBatterySystem _battery = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedPointLightSystem _pointLight = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly UnpoweredFlashlightSystem _unpoweredFlashlight = default!;

    private static readonly TimeSpan EquipmentInterval = TimeSpan.FromSeconds(0.2);
    private static readonly TimeSpan DarknessCheckInterval = TimeSpan.FromSeconds(1);

    private TimeSpan _nextEquipmentCheck;
    private TimeSpan _nextDarknessCheck;

    private readonly Dictionary<EntityUid, LavalandDeepNightConfig> _deepMaps = new();
    private readonly Dictionary<(EntityUid Uid, LightKind Kind), LightState> _lights = new();
    private readonly HashSet<EntityUid> _blocked = new();
    private readonly HashSet<EntityUid> _darkened = new();
    private readonly Dictionary<EntityUid, TimeSpan> _bonfires = new();
    private readonly Dictionary<EntityUid, DarkState> _darkness = new();
    private readonly Dictionary<EntityUid, (float Radius, float Energy)> _wisps = new();
    private readonly HashSet<Entity<PointLightComponent>> _nearbyLights = new();

    private sealed class LightState
    {
        /// <summary>When it was switched on this time.</summary>
        public TimeSpan? OnSince;

        /// <summary>
        /// Time it shone before, adding up across switching it off and on: flicking it does not get a fresh
        /// allowance. Forgotten only after it stays off as long as its cooldown.
        /// </summary>
        public TimeSpan Used;

        public TimeSpan OffSince;
        public TimeSpan CooldownUntil;
    }

    private sealed class DarkState
    {
        public TimeSpan DarkSince;
        public TimeSpan NextBurn;
        public bool Warned;
    }

    private enum LightKind : byte
    {
        Pda,
        Helmet,
        Flashlight,
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextEquipmentCheck)
            return;
        _nextEquipmentCheck = now + EquipmentInterval;

        CollectDeepMaps();
        if (_deepMaps.Count == 0)
        {
            // The moon is back: forget everything, cooldowns included.
            _lights.Clear();
            _darkness.Clear();
            RestoreWisps(null);
            ClearBlocks();
            RestoreDarkened(true);
            _bonfires.Clear();
            return;
        }

        UpdateNightVision();
        UpdateHandheldLights(now);
        UpdatePdaLights(now);
        UpdateWeaponLights(now);
        UpdateFlares();
        UpdateBonfires(now);
        UpdateWisps();
        UpdateBlocks(now);
        UpdateDarkened();

        if (now >= _nextDarknessCheck)
        {
            _nextDarknessCheck = now + DarknessCheckInterval;
            UpdateDarkness(now);
        }
    }

    private void CollectDeepMaps()
    {
        _deepMaps.Clear();
        var query = EntityQueryEnumerator<LavalandDayCycleComponent, LavalandMapComponent>();
        while (query.MoveNext(out var uid, out var cycle, out var lavaland))
        {
            if (!cycle.DeepNight || lavaland.Planet.Id == null ||
                !_prototype.TryIndex(lavaland.Planet, out var planet) ||
                cycle.Phase < 0 || cycle.Phase >= planet.DayCycle.Phases.Count ||
                planet.DayCycle.Phases[cycle.Phase].DeepNight is not { } deep)
            {
                continue;
            }

            _deepMaps[uid] = deep;
        }

        // While it is day on lavaland the chromite caves are in their own deep night.
        var caves = EntityQueryEnumerator<Content.Shared.DeadSpace.Lavaland.ChromiteCaves.LavalandChromiteCavesComponent>();
        while (caves.MoveNext(out var uid, out _))
        {
            if (_caves.TryGetDeepNight(uid, out var deep))
                _deepMaps[uid] = deep;
        }
    }

    /// <summary>The deep night the entity is in right now (lavaland, or the chromite caves while it is day above).</summary>
    public bool TryGetDeepNight(EntityUid uid, out LavalandDeepNightConfig deep)
    {
        return TryGetDeep(uid, out deep);
    }

    private bool TryGetDeep(EntityUid uid, out LavalandDeepNightConfig deep)
    {
        deep = default!;
        return Transform(uid).MapUid is { } map && _deepMaps.TryGetValue(map, out deep!);
    }

    #region Equipment

    private void UpdateNightVision()
    {
        var query = EntityQueryEnumerator<NightVisionComponent>();
        while (query.MoveNext(out var uid, out var vision))
        {
            if (!vision.IsNightVision || !TryGetDeep(uid, out var deep) || !deep.BlockNightVision)
                continue;

            vision.IsNightVision = false;
            vision.RemainingTime = null;
            Dirty(uid, vision);
            _popup.PopupEntity(Loc.GetString("lavaland-deep-night-nvg-blocked"), uid, uid, PopupType.MediumCaution);
        }
    }

    private void UpdateHandheldLights(TimeSpan now)
    {
        var query = EntityQueryEnumerator<HandheldLightComponent>();
        while (query.MoveNext(out var uid, out var light))
        {
            if (HasComp<LavalandWispLanternComponent>(uid) || !TryGetDeep(uid, out var deep))
                continue;

            var kind = IsHelmet(uid) ? LightKind.Helmet : LightKind.Flashlight;
            if (!light.Activated)
            {
                ResetOnTime(uid, kind);
                continue;
            }

            if (!CheckLight(uid, kind, deep, now))
                continue;

            if (kind == LightKind.Flashlight &&
                _powerCell.TryGetBatteryFromSlotOrEntity(uid, out var battery))
            {
                _battery.SetCharge((battery.Value.Owner, battery.Value.Comp), 0f);
            }

            _handheldLight.TurnOff((uid, light));
        }
    }

    private void UpdatePdaLights(TimeSpan now)
    {
        var query = EntityQueryEnumerator<UnpoweredFlashlightComponent>();
        while (query.MoveNext(out var uid, out var light))
        {
            if (!TryGetDeep(uid, out var deep))
                continue;

            if (!light.LightOn)
            {
                ResetOnTime(uid, LightKind.Pda);
                continue;
            }

            if (CheckLight(uid, LightKind.Pda, deep, now))
                _unpoweredFlashlight.SetLight((uid, light), false, quiet: true);
        }
    }

    /// <summary>A bonfire in the deep night - lit there or carried in - falls apart into firewood a few seconds later.</summary>
    private void UpdateBonfires(TimeSpan now)
    {
        var query = EntityQueryEnumerator<AlwaysHotComponent, MetaDataComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var meta, out var xform))
        {
            if (meta.EntityPrototype is not { } proto || !BonfirePrototypes.Contains(proto.ID) ||
                xform.MapUid is not { } map || !_deepMaps.TryGetValue(map, out var deep))
            {
                continue;
            }

            if (!_bonfires.TryGetValue(uid, out var since))
            {
                _bonfires[uid] = now;
                continue;
            }

            if (now - since < deep.BonfireTime)
                continue;

            _bonfires.Remove(uid);
            var wood = Spawn(BonfireWood, xform.Coordinates);
            _stack.SetCount(wood, BonfireWoodCount);
            _popup.PopupCoordinates(Loc.GetString("lavaland-deep-night-bonfire-out"), xform.Coordinates, PopupType.MediumCaution);
            QueueDel(uid);
        }

        foreach (var uid in _bonfires.Keys.ToArray())
        {
            if (TerminatingOrDeleted(uid))
                _bonfires.Remove(uid);
        }
    }

    private static readonly HashSet<string> BonfirePrototypes = new() { "Bonfire", "BonfireStake" };
    private static readonly EntProtoId BonfireWood = "MaterialWoodPlank1";
    private const int BonfireWoodCount = 5;

    /// <summary>A flare lit in the deep night: it burns out in seconds instead of a quarter of an hour.</summary>
    private void UpdateFlares()
    {
        var query = EntityQueryEnumerator<ExpendableLightComponent>();
        while (query.MoveNext(out var uid, out var flare))
        {
            if (!flare.Activated || !TryGetDeep(uid, out var deep))
                continue;

            var limit = flare.CurrentState == ExpendableLightState.Lit ? deep.FlareTime : deep.FlareFadeTime;
            if (flare.StateExpiryTime > (float) limit.TotalSeconds)
                flare.StateExpiryTime = (float) limit.TotalSeconds;
        }
    }

    /// <summary>A flashlight in a weapon's light slot (the weapon shines with it): same as a flashlight.</summary>
    private void UpdateWeaponLights(TimeSpan now)
    {
        var query = EntityQueryEnumerator<AttachmentHolderComponent>();
        while (query.MoveNext(out var uid, out var holder))
        {
            if (!TryGetDeep(uid, out var deep))
                continue;

            if (!holder.LightOn)
            {
                ResetOnTime(uid, LightKind.Flashlight);
                continue;
            }

            // The weapon does not check the battery itself: a dead flashlight gives no light.
            Entity<BatteryComponent>? battery = null;
            if (_attachment.GetSlotItem(uid, holder.LightSlot) is { } flashlight &&
                _powerCell.TryGetBatteryFromSlotOrEntity(flashlight, out var cell))
            {
                battery = cell;
            }

            var dead = battery is not { } empty || _battery.GetCharge((empty.Owner, empty.Comp)) <= 0f;
            if (!dead && !CheckLight(uid, LightKind.Flashlight, deep, now))
                continue;

            if (battery is { } drained)
                _battery.SetCharge((drained.Owner, drained.Comp), 0f);

            _attachment.SetLight((uid, holder), false);
        }
    }

    /// <summary>A light is on; true if it has to go out now.</summary>
    private bool CheckLight(EntityUid uid, LightKind kind, LavalandDeepNightConfig deep, TimeSpan now)
    {
        if (!_lights.TryGetValue((uid, kind), out var state))
        {
            state = new LightState();
            _lights[(uid, kind)] = state;
        }

        var holder = GetHolder(uid);
        if (now < state.CooldownUntil)
        {
            var seconds = (int) Math.Ceiling((state.CooldownUntil - now).TotalSeconds);
            if (holder != null)
                _popup.PopupEntity(Loc.GetString("lavaland-deep-night-light-cooldown", ("seconds", seconds)), holder.Value, holder.Value);
            return true;
        }

        var (time, cooldown) = GetLimits(kind, deep);
        if (state.OnSince == null)
        {
            // Off as long as its cooldown: the allowance is whole again. A flashlight has none - its battery is it.
            if (cooldown > TimeSpan.Zero && now - state.OffSince >= cooldown)
                state.Used = TimeSpan.Zero;
            state.OnSince = now;
            return false;
        }

        if (state.Used + (now - state.OnSince.Value) < time)
            return false;

        state.OnSince = null;
        state.Used = TimeSpan.Zero;
        state.OffSince = now;
        state.CooldownUntil = now + cooldown;
        if (holder != null)
        {
            var message = kind == LightKind.Flashlight ? "lavaland-deep-night-battery-dead" : "lavaland-deep-night-light-out";
            _popup.PopupEntity(Loc.GetString(message), holder.Value, holder.Value, PopupType.MediumCaution);
        }

        return true;
    }

    private static (TimeSpan Time, TimeSpan Cooldown) GetLimits(LightKind kind, LavalandDeepNightConfig deep)
    {
        return kind switch
        {
            LightKind.Pda => (deep.PdaLightTime, deep.PdaLightCooldown),
            LightKind.Helmet => (deep.HelmetLightTime, deep.HelmetLightCooldown),
            _ => (deep.FlashlightTime, TimeSpan.Zero),
        };
    }

    /// <summary>Switched off: the time it shone is kept, not thrown away.</summary>
    private void ResetOnTime(EntityUid uid, LightKind kind)
    {
        if (!_lights.TryGetValue((uid, kind), out var state) || state.OnSince is not { } since)
            return;

        state.Used += _timing.CurTime - since;
        state.OnSince = null;
        state.OffSince = _timing.CurTime;
    }

    /// <summary>
    /// Lights on cooldown and night vision in the deep night get the networked block: their toggles refuse to turn
    /// them on at all, so spamming the button gives no flashes of light.
    /// </summary>
    private void UpdateBlocks(TimeSpan now)
    {
        _blocked.Clear();
        foreach (var ((uid, _), state) in _lights)
        {
            if (now < state.CooldownUntil && !TerminatingOrDeleted(uid) && TryGetDeep(uid, out _))
                _blocked.Add(uid);
        }

        var visions = EntityQueryEnumerator<NightVisionComponent>();
        while (visions.MoveNext(out var uid, out _))
        {
            if (TryGetDeep(uid, out var deep) && deep.BlockNightVision)
                _blocked.Add(uid);
        }

        var current = EntityQueryEnumerator<LavalandLightBlockedComponent>();
        while (current.MoveNext(out var uid, out _))
        {
            if (!_blocked.Remove(uid))
                RemCompDeferred<LavalandLightBlockedComponent>(uid);
        }

        foreach (var uid in _blocked)
        {
            AddComp<LavalandLightBlockedComponent>(uid);
        }
    }

    private void ClearBlocks()
    {
        var current = EntityQueryEnumerator<LavalandLightBlockedComponent>();
        while (current.MoveNext(out var uid, out _))
        {
            RemCompDeferred<LavalandLightBlockedComponent>(uid);
        }
    }

    private bool IsHelmet(EntityUid uid)
    {
        return TryComp<ClothingComponent>(uid, out var clothing) && (clothing.Slots & SlotFlags.HEAD) != 0;
    }

    /// <summary>The mob carrying or wearing the item, if any.</summary>
    private EntityUid? GetHolder(EntityUid uid)
    {
        var parent = Transform(uid).ParentUid;
        while (parent.IsValid() && !HasComp<MapGridComponent>(parent) && !HasComp<MapComponent>(parent))
        {
            if (HasComp<ActorComponent>(parent))
                return parent;
            parent = Transform(parent).ParentUid;
        }

        return null;
    }

    #endregion

    #region Lights that go out

    /// <summary>Lights of <see cref="LavalandDeepNightDarkComponent"/> things in a deep night: off.</summary>
    private void UpdateDarkened()
    {
        var query = EntityQueryEnumerator<LavalandDeepNightDarkComponent, PointLightComponent>();
        while (query.MoveNext(out var uid, out _, out var light))
        {
            if (!light.Enabled || !TryGetDeep(uid, out _))
                continue;

            _pointLight.SetEnabled(uid, false, light);
            _darkened.Add(uid);
        }

        RestoreDarkened(false);
    }

    /// <summary>Gives the light back to what left the deep night (everything if <paramref name="all"/>).</summary>
    private void RestoreDarkened(bool all)
    {
        foreach (var uid in _darkened.ToArray())
        {
            if (TerminatingOrDeleted(uid))
            {
                _darkened.Remove(uid);
                continue;
            }

            if (!all && TryGetDeep(uid, out _))
                continue;

            if (_pointLight.TryGetLight(uid, out var light))
                _pointLight.SetEnabled(uid, true, light);
            _darkened.Remove(uid);
        }
    }

    #endregion

    #region Wisps

    private void UpdateWisps()
    {
        var query = EntityQueryEnumerator<LavalandWispLanternComponent>();
        while (query.MoveNext(out _, out var lantern))
        {
            if (lantern.Wisp is not { } wisp || TerminatingOrDeleted(wisp) ||
                !TryGetDeep(wisp, out var deep) || _wisps.ContainsKey(wisp) ||
                !_pointLight.TryGetLight(wisp, out var light))
            {
                continue;
            }

            _wisps[wisp] = (light.Radius, light.Energy);
            _pointLight.SetRadius(wisp, deep.WispLightRadius, light);
            _pointLight.SetEnergy(wisp, deep.WispLightEnergy, light);
        }

        RestoreWisps(_deepMaps);
    }

    /// <summary>Gives the wisps outside a deep night (all of them if null) their own light back.</summary>
    private void RestoreWisps(Dictionary<EntityUid, LavalandDeepNightConfig>? deepMaps)
    {
        foreach (var (wisp, original) in _wisps.ToArray())
        {
            if (TerminatingOrDeleted(wisp))
            {
                _wisps.Remove(wisp);
                continue;
            }

            if (deepMaps != null && Transform(wisp).MapUid is { } map && deepMaps.ContainsKey(map))
                continue;

            _wisps.Remove(wisp);
            if (_pointLight.TryGetLight(wisp, out var light))
            {
                _pointLight.SetRadius(wisp, original.Radius, light);
                _pointLight.SetEnergy(wisp, original.Energy, light);
            }
        }
    }

    #endregion

    #region Darkness

    private void UpdateDarkness(TimeSpan now)
    {
        var query = EntityQueryEnumerator<ActorComponent, MobStateComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var mobState, out var xform))
        {
            if (xform.MapUid is not { } map || !_deepMaps.TryGetValue(map, out var deep) ||
                !_mobState.IsAlive(uid, mobState) || IsLit(uid, xform, deep))
            {
                _darkness.Remove(uid);
                continue;
            }

            if (!_darkness.TryGetValue(uid, out var dark))
            {
                dark = new DarkState { DarkSince = now, NextBurn = now + deep.DarknessGrace };
                _darkness[uid] = dark;
            }

            if (!dark.Warned && now - dark.DarkSince >= deep.DarknessGrace / 2)
            {
                dark.Warned = true;
                _popup.PopupEntity(Loc.GetString("lavaland-deep-night-darkness-warning"), uid, uid, PopupType.MediumCaution);
            }

            if (now < dark.NextBurn)
                continue;

            // Only until critical: it is the dark, not an execution.
            dark.NextBurn = now + deep.DarknessInterval;
            _damageable.TryChangeDamage(uid, deep.DarknessDamage, true);
            _popup.PopupEntity(Loc.GetString("lavaland-deep-night-darkness"), uid, uid, PopupType.LargeCaution);
        }
    }

    /// <summary>Is there a light shining on this player (the lava's glow does not count, it is gone)?</summary>
    private bool IsLit(EntityUid uid, TransformComponent xform, LavalandDeepNightConfig deep)
    {
        // A light he carries himself - a flashlight in hand (directional ones too), a helmet lamp, a lit gun -
        // always counts: the lookup does not always reach items in hands and slots.
        foreach (var item in _inventory.GetHandOrInventoryEntities(uid))
        {
            if (TryComp<PointLightComponent>(item, out var carried) &&
                carried.Enabled && carried.Radius > 0f && carried.Energy > 0f)
            {
                return true;
            }
        }

        var position = _transform.GetMapCoordinates(uid, xform);
        _nearbyLights.Clear();
        _lookup.GetEntitiesInRange(position, deep.LightSearchRange, _nearbyLights, LookupFlags.All);

        foreach (var light in _nearbyLights)
        {
            if (!light.Comp.Enabled || light.Comp.Radius <= 0f || light.Comp.Energy <= 0f)
                continue;

            // Lights packed away in a bag do not shine; ones in hands and on clothes do.
            if (_container.IsEntityInContainer(light) &&
                (GetHolder(light) is not { } holder || Transform(light).ParentUid != holder))
            {
                continue;
            }

            var lightPosition = _transform.GetMapCoordinates(light);
            if ((lightPosition.Position - position.Position).Length() <= light.Comp.Radius)
                return true;
        }

        return false;
    }

    #endregion
}

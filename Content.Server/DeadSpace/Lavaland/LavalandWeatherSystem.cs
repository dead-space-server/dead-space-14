using Content.Server.DeadSpace.Lavaland.Components;
using Content.Server.Chat.Systems;
using Content.Server.Temperature.Systems;
using Content.Shared.Temperature.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.Lavaland;
using Content.Shared.DeadSpace.Lavaland.Components;
using Content.Shared.Inventory;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Weather;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Lavaland;

public sealed class LavalandWeatherSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedWeatherSystem _weather = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly TemperatureSystem _temperature = default!;

    private EntityQuery<LavalandWeatherImmuneComponent> _immuneQuery;
    private EntityQuery<MapGridComponent> _gridQuery;
    private EntityQuery<TransformComponent> _xformQuery;
    private readonly HashSet<EntityUid> _weatherEntities = new();

    public override void Initialize()
    {
        base.Initialize();

        _immuneQuery = GetEntityQuery<LavalandWeatherImmuneComponent>();
        _gridQuery = GetEntityQuery<MapGridComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<LavalandMapComponent>();
        while (query.MoveNext(out var uid, out var lavaland))
        {
            if (lavaland.Planet.Id == null || !_prototype.TryIndex(lavaland.Planet, out var planet))
                continue;

            if (!planet.AshStormEnabled)
            {
                ResetWeatherState(uid, lavaland);
                continue;
            }

            if (!lavaland.WeatherInitialized)
                ScheduleNextStorm(lavaland, planet, curTime);

            switch (lavaland.WeatherStage)
            {
                case LavalandWeatherStage.Calm:
                    if (curTime >= lavaland.NextWeatherTime)
                        StartWarning(uid, lavaland, planet, curTime);
                    break;

                case LavalandWeatherStage.Warning:
                    if (curTime >= lavaland.WeatherStageEndTime)
                        StartStorm(uid, lavaland, planet, curTime);
                    break;

                case LavalandWeatherStage.Active:
                    if (curTime >= lavaland.WeatherStageEndTime)
                    {
                        EndStorm(uid, lavaland, planet, curTime);
                        break;
                    }

                    if (curTime >= lavaland.NextWeatherDamageTime)
                        ApplyStormDamage(uid, lavaland, planet, curTime);
                    break;
            }
        }
    }

    public bool IsStormActive(EntityUid mapUid)
    {
        return TryComp<LavalandMapComponent>(mapUid, out var lavaland) &&
               lavaland.WeatherStage == LavalandWeatherStage.Active;
    }

    /// <summary>
    /// Ends an ongoing or upcoming ash storm on the map. Returns false if there is nothing to dispel.
    /// </summary>
    public bool TryDispelStorm(EntityUid mapUid)
    {
        if (!TryComp<LavalandMapComponent>(mapUid, out var lavaland) ||
            lavaland.WeatherStage == LavalandWeatherStage.Calm ||
            lavaland.Planet.Id == null || !_prototype.TryIndex(lavaland.Planet, out var planet))
        {
            return false;
        }

        EndStorm(mapUid, lavaland, planet, _timing.CurTime);
        return true;
    }

    private void StartWarning(
        EntityUid mapUid,
        LavalandMapComponent lavaland,
        LavalandPlanetPrototype planet,
        TimeSpan curTime)
    {
        var duration = ClampDuration(planet.AshStormWarningDuration);
        lavaland.SnowStorm = planet.SnowStormChance > 0f && _random.Prob(Math.Clamp(planet.SnowStormChance, 0f, 1f));
        if (lavaland.SnowStorm)
            StartSnowCover(mapUid, planet, curTime);

        if (duration == TimeSpan.Zero)
        {
            StartStorm(mapUid, lavaland, planet, curTime);
            return;
        }

        var warningWeather = lavaland.SnowStorm ? planet.SnowStormWarningWeather : planet.AshStormWarningWeather;
        if (_prototype.Resolve(warningWeather, out var weather))
            _weather.SetWeather(Transform(mapUid).MapID, weather, curTime + duration);

        WarnPlayers(mapUid, lavaland.SnowStorm ? planet : null);

        lavaland.WeatherStage = LavalandWeatherStage.Warning;
        lavaland.WeatherStageEndTime = curTime + duration;
    }

    private void StartStorm(
        EntityUid mapUid,
        LavalandMapComponent lavaland,
        LavalandPlanetPrototype planet,
        TimeSpan curTime)
    {
        var duration = PickDuration(planet.AshStormDurationMin, planet.AshStormDurationMax);

        var stormWeather = lavaland.SnowStorm ? planet.SnowStormWeather : planet.AshStormWeather;
        if (_prototype.Resolve(stormWeather, out var weather))
            _weather.SetWeather(Transform(mapUid).MapID, weather, curTime + duration);

        lavaland.WeatherStage = LavalandWeatherStage.Active;
        lavaland.WeatherStageEndTime = curTime + duration;
        lavaland.NextWeatherDamageTime = curTime + ClampDamageInterval(planet);
    }

    private void EndStorm(
        EntityUid mapUid,
        LavalandMapComponent lavaland,
        LavalandPlanetPrototype planet,
        TimeSpan curTime)
    {
        lavaland.WeatherStage = LavalandWeatherStage.Calm;
        lavaland.WeatherStageEndTime = TimeSpan.Zero;
        lavaland.NextWeatherDamageTime = TimeSpan.Zero;
        lavaland.SnowStorm = false;
        RemComp<LavalandSnowCoverComponent>(mapUid);
        // Ended early (the staff of storms): the ash, the snow and their sound go with it, not when they were due.
        _weather.SetWeather(Transform(mapUid).MapID, null, null);
        ScheduleNextStorm(lavaland, planet, curTime);
    }

    /// <summary>The blizzard starts whitening the map from its borders inward.</summary>
    private void StartSnowCover(EntityUid mapUid, LavalandPlanetPrototype planet, TimeSpan curTime)
    {
        var cover = EnsureComp<LavalandSnowCoverComponent>(mapUid);
        cover.Start = curTime;
        cover.GrowSeconds = (float) Math.Max(1, planet.SnowCoverGrowTime.TotalSeconds);
        cover.Depth = Math.Max(0f, planet.SnowCoverDepth);
        cover.HalfSize = Math.Max(1, planet.MapHalfSize);
        cover.Color = planet.SnowCoverColor;
        Dirty(mapUid, cover);
    }

    /// <param name="snow">The planet when the coming storm is a blizzard (blue warning), null for ash (red).</param>
    private void WarnPlayers(EntityUid mapUid, LavalandPlanetPrototype? snow = null)
    {
        var filter = Filter.Empty().AddInMap(Transform(mapUid).MapID);
        _chat.DispatchFilteredAnnouncement(
            filter,
            Loc.GetString(snow?.SnowStormWarning ?? "lavaland-ash-storm-warning"),
            sender: Loc.GetString("lavaland-ash-storm-sender"),
            playSound: false,
            colorOverride: snow?.SnowStormWarningColor ?? Color.OrangeRed);
    }

    private void ResetWeatherState(EntityUid mapUid, LavalandMapComponent lavaland)
    {
        if (lavaland.WeatherStage == LavalandWeatherStage.Calm && !lavaland.WeatherInitialized)
            return;

        lavaland.WeatherStage = LavalandWeatherStage.Calm;
        lavaland.WeatherInitialized = false;
        lavaland.SnowStorm = false;
        RemComp<LavalandSnowCoverComponent>(mapUid);
        lavaland.NextWeatherTime = TimeSpan.Zero;
        lavaland.WeatherStageEndTime = TimeSpan.Zero;
        lavaland.NextWeatherDamageTime = TimeSpan.Zero;
        _weather.SetWeather(Transform(mapUid).MapID, null, null);
    }

    private void ScheduleNextStorm(
        LavalandMapComponent lavaland,
        LavalandPlanetPrototype planet,
        TimeSpan curTime)
    {
        lavaland.WeatherInitialized = true;
        lavaland.NextWeatherTime = curTime + PickDuration(planet.AshStormCooldownMin, planet.AshStormCooldownMax);
    }

    private void ApplyStormDamage(
        EntityUid mapUid,
        LavalandMapComponent lavaland,
        LavalandPlanetPrototype planet,
        TimeSpan curTime)
    {
        lavaland.NextWeatherDamageTime = curTime + ClampDamageInterval(planet);

        var stormDamage = lavaland.SnowStorm ? planet.SnowStormDamage : planet.AshStormDamage;
        var cooling = lavaland.SnowStorm ? planet.SnowStormCooling : 0f;
        if (stormDamage.Empty && cooling <= 0f)
            return;

        var damage = new DamageSpecifier(stormDamage);
        var mapId = Transform(mapUid).MapID;
        var halfSize = GetWeatherLookupHalfSize(planet);
        var bounds = new Box2(-halfSize, -halfSize, halfSize, halfSize);

        _weatherEntities.Clear();
        _lookup.GetEntitiesIntersecting(
            mapId,
            bounds,
            _weatherEntities,
            LookupFlags.Dynamic | LookupFlags.Sundries);

        foreach (var uid in _weatherEntities)
        {
            if (!TryComp(uid, out DamageableComponent? damageable) ||
                !TryComp(uid, out MobStateComponent? mobState) ||
                !TryComp(uid, out TransformComponent? xform) ||
                xform.MapUid != mapUid ||
                xform.GridUid is not { } gridUid ||
                !_gridQuery.TryGetComponent(gridUid, out var grid) ||
                !_map.TryGetTileRef(gridUid, grid, xform.Coordinates, out var tile) ||
                tile.Tile.IsEmpty)
            {
                continue;
            }

            if (_mobState.IsDead(uid, mobState) ||
                IsWeatherImmune(uid, xform) ||
                !_weather.CanWeatherAffect(gridUid, grid, tile))
            {
                continue;
            }

            if (!stormDamage.Empty)
                _damageable.TryChangeDamage((uid, damageable), damage, interruptsDoAfters: false, origin: mapUid);

            // The blizzard chills the body; cold protection of the clothes takes its share off.
            if (cooling > 0f &&
                TryComp<TemperatureComponent>(uid, out var temperature) &&
                temperature.CurrentTemperature > planet.SnowStormMinTemperature)
            {
                var heat = -cooling * _temperature.GetHeatCapacity(uid, temperature);
                _temperature.ChangeHeat(uid, heat, false, temperature);
            }
        }

        _weatherEntities.Clear();
    }

    private bool IsWeatherImmune(EntityUid uid, TransformComponent xform)
    {
        while (true)
        {
            if (_immuneQuery.HasComponent(uid) || HasCompleteEquippedWeatherProtection(uid))
                return true;

            var parent = xform.ParentUid;
            if (!parent.IsValid() || parent == uid || !_xformQuery.TryGetComponent(parent, out var parentXform))
                return false;

            uid = parent;
            xform = parentXform;
        }
    }

    private bool HasCompleteEquippedWeatherProtection(EntityUid uid)
    {
        var requiredSlots = SlotFlags.OUTERCLOTHING | SlotFlags.HEAD;
        var protectedSlots = SlotFlags.NONE;
        var enumerator = _inventory.GetSlotEnumerator(uid, requiredSlots);

        while (enumerator.NextItem(out var item, out var slot))
        {
            if (!_immuneQuery.HasComponent(item))
                continue;

            protectedSlots |= slot.SlotFlags & requiredSlots;
        }

        return (protectedSlots & requiredSlots) == requiredSlots;
    }

    private TimeSpan PickDuration(TimeSpan min, TimeSpan max)
    {
        min = ClampDuration(min);
        max = ClampDuration(max);

        if (max < min)
            max = min;

        if (min == max)
            return min;

        return TimeSpan.FromSeconds(_random.NextFloat((float) min.TotalSeconds, (float) max.TotalSeconds));
    }

    private static TimeSpan ClampDamageInterval(LavalandPlanetPrototype planet)
    {
        var interval = ClampDuration(planet.AshStormDamageInterval);
        return interval == TimeSpan.Zero ? TimeSpan.FromSeconds(1) : interval;
    }

    private static float GetWeatherLookupHalfSize(LavalandPlanetPrototype planet)
    {
        const float MinExtraMargin = 64f;
        var halfSize = Math.Max(1, planet.MapHalfSize);
        var extraMargin = Math.Max(MinExtraMargin, planet.FtlFallbackMaxOffset + MinExtraMargin);

        return halfSize + extraMargin;
    }

    private static TimeSpan ClampDuration(TimeSpan duration)
    {
        return duration < TimeSpan.Zero ? TimeSpan.Zero : duration;
    }
}

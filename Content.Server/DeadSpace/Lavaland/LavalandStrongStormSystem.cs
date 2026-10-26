// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Server.Chat.Systems;
using Content.Server.DeadSpace.Lavaland.Components;
using Content.Server.GameTicking;
using Content.Shared.DeadSpace.Lavaland;
using Content.Shared.DeadSpace.Lavaland.Components;
using Content.Shared.DeadSpace.Lavaland.SecretBoss;
using Content.Shared.Clothing;
using Content.Shared.Ghost;
using Content.Shared.Inventory;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Mobs.Systems;
using Content.Shared.Throwing;
using Content.Shared.Weather;
using Robust.Server.Player;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Lavaland;

/// <summary>
/// The strong storm (<see cref="LavalandStrongStormConfig"/>): late in the round, with an alarm like the deep night's,
/// a wind rises that no staff of storms dispels. Every few seconds every player out in the open is thrown several
/// tiles - forward, back, to the left or to the right; switched on magboots hold them closer. Its first time it brings the Ash Wolf to the miners' outpost.
/// </summary>
public sealed class LavalandStrongStormSystem : EntitySystem
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly ItemToggleSystem _toggle = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly LavalandFaunaRaidSystem _raids = default!;
    [Dependency] private readonly LavalandWeatherSystem _lavalandWeather = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedWeatherSystem _weather = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly LavalandAshWolfSystem _ashWolf = default!;

    /// <summary>The ordinary ash storm waits this long after the strong one.</summary>
    private static readonly TimeSpan AshStormPause = TimeSpan.FromMinutes(2);

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<LavalandMapComponent>();
        while (query.MoveNext(out var uid, out var lavaland))
        {
            if (!TryGetConfig(lavaland, out var config))
                continue;

            var storm = EnsureComp<LavalandStrongStormComponent>(uid);
            switch (storm.Stage)
            {
                case LavalandStrongStormStage.Idle:
                    TryRoll((uid, storm), lavaland, config, now);
                    break;

                case LavalandStrongStormStage.Warning when now >= storm.StageEnd:
                    Begin((uid, storm), lavaland, config, now);
                    break;

                case LavalandStrongStormStage.Active when now >= storm.StageEnd:
                    End((uid, storm), lavaland, config, now);
                    break;

                case LavalandStrongStormStage.Active when now >= storm.NextThrow:
                    storm.NextThrow = now + config.ThrowInterval;
                    Gust(uid, config);
                    break;
            }
        }
    }

    private bool TryGetConfig(LavalandMapComponent lavaland, out LavalandStrongStormConfig config)
    {
        config = default!;
        if (lavaland.Planet.Id == null || !_prototype.TryIndex(lavaland.Planet, out var planet) || !planet.StrongStorm.Enabled)
            return false;

        config = planet.StrongStorm;
        return true;
    }

    /// <summary>Only late in the round and only when the sky is calm; a miss makes the next roll likelier.</summary>
    private void TryRoll(Entity<LavalandStrongStormComponent> ent, LavalandMapComponent lavaland, LavalandStrongStormConfig config, TimeSpan now)
    {
        if (now < ent.Comp.NextCheck)
            return;

        ent.Comp.NextCheck = now + config.CheckInterval;
        if (_ticker.RoundDuration() < config.EarliestStart || lavaland.WeatherStage != LavalandWeatherStage.Calm)
            return;

        if (ent.Comp.Chance <= 0f)
            ent.Comp.Chance = config.BaseChance;

        if (!_random.Prob(Math.Clamp(ent.Comp.Chance, 0f, 1f)))
        {
            ent.Comp.Chance = Math.Min(1f, ent.Comp.Chance + config.ChanceIncrease);
            return;
        }

        StartWarning(ent, lavaland, config, now);
    }

    /// <summary>Starts the strong storm's alarm on the lavaland right now (the admin command). False if it is already coming.</summary>
    public bool Force(EntityUid mapUid)
    {
        if (!TryComp<LavalandMapComponent>(mapUid, out var lavaland) || !TryGetConfig(lavaland, out var config))
            return false;

        var storm = EnsureComp<LavalandStrongStormComponent>(mapUid);
        if (storm.Stage != LavalandStrongStormStage.Idle)
            return false;

        StartWarning((mapUid, storm), lavaland, config, _timing.CurTime);
        return true;
    }

    private void StartWarning(Entity<LavalandStrongStormComponent> ent, LavalandMapComponent lavaland, LavalandStrongStormConfig config, TimeSpan now)
    {
        ent.Comp.Stage = LavalandStrongStormStage.Warning;
        ent.Comp.StageEnd = now + config.WarningDuration;
        ent.Comp.Chance = config.BaseChance;
        HoldAshStorm(lavaland, ent.Comp.StageEnd + config.Duration);

        if (config.WarningWeather is { } warningId && _prototype.TryIndex(warningId, out var warning))
            _weather.SetWeather(Transform(ent).MapID, warning, ent.Comp.StageEnd);

        Announce(ent, config.WarningMessage, config, playSound: true);
    }

    private void Begin(Entity<LavalandStrongStormComponent> ent, LavalandMapComponent lavaland, LavalandStrongStormConfig config, TimeSpan now)
    {
        ent.Comp.Stage = LavalandStrongStormStage.Active;
        ent.Comp.StageEnd = now + config.Duration;
        ent.Comp.NextThrow = now + config.ThrowInterval;
        HoldAshStorm(lavaland, ent.Comp.StageEnd);

        if (config.Weather is { } weatherId && _prototype.TryIndex(weatherId, out var weather))
            _weather.SetWeather(Transform(ent).MapID, weather, ent.Comp.StageEnd);

        Announce(ent, config.StartMessage, config, playSound: true);

        if (!ent.Comp.BossBrought && config.Boss is { } boss && TryPickBossSpot(ent, config, out var spot))
        {
            ent.Comp.BossBrought = true;
            Spawn(boss, spot);
        }
    }

    private void End(Entity<LavalandStrongStormComponent> ent, LavalandMapComponent lavaland, LavalandStrongStormConfig config, TimeSpan now)
    {
        ent.Comp.Stage = LavalandStrongStormStage.Idle;
        ent.Comp.NextCheck = now + config.CheckInterval;
        _weather.SetWeather(Transform(ent).MapID, null, null);
        Announce(ent, config.EndMessage, config, playSound: false);

        // The storm's boss is not calmed by the calm: it goes mad.
        var mapId = Transform(ent).MapID;
        var wolves = EntityQueryEnumerator<LavalandAshWolfComponent, TransformComponent>();
        while (wolves.MoveNext(out var wolf, out var comp, out var xform))
        {
            if (xform.MapID == mapId)
                _ashWolf.Frenzy((wolf, comp));
        }
    }

    /// <summary>The ordinary ash storm does not start while the strong one is coming or blowing.</summary>
    private static void HoldAshStorm(LavalandMapComponent lavaland, TimeSpan until)
    {
        var after = until + AshStormPause;
        if (lavaland.NextWeatherTime < after)
            lavaland.NextWeatherTime = after;
    }

    private void Announce(EntityUid map, LocId message, LavalandStrongStormConfig config, bool playSound)
    {
        var filter = Filter.Empty().AddInMap(Transform(map).MapID);
        _chat.DispatchFilteredAnnouncement(filter, Loc.GetString(message),
            sender: Loc.GetString("lavaland-day-cycle-sender"), playSound: false, colorOverride: config.MessageColor);
        if (playSound && config.WarningSound != null)
            _audio.PlayGlobal(config.WarningSound, filter, true);
    }

    /// <summary>A gust: every living player out in the open is thrown forward, back, left or right.</summary>
    private void Gust(EntityUid map, LavalandStrongStormConfig config)
    {
        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity is not { Valid: true } player ||
                HasComp<GhostComponent>(player) ||
                // Watching a boss scene: the wind waits.
                HasComp<LavalandPitFrozenComponent>(player) ||
                HasComp<LavalandWeatherImmuneComponent>(player) ||
                !_mobState.IsAlive(player) ||
                !TryComp(player, out TransformComponent? xform) ||
                xform.MapUid != map ||
                !_lavalandWeather.IsUnderOpenSky(player, xform))
            {
                continue;
            }

            var facing = _transform.GetWorldRotation(xform);
            var turn = Angle.FromDegrees(90 * _random.Next(4));
            var direction = (facing + turn).ToWorldVec();
            var distance = HasActiveMagboots(player) ? config.MagbootsThrowDistance : config.ThrowDistance;
            _throwing.TryThrow(player, direction * distance, config.ThrowSpeed, recoil: false, playSound: false);
        }
    }

    private bool HasActiveMagboots(EntityUid player)
    {
        return _inventory.TryGetSlotEntity(player, "shoes", out var boots) &&
               HasComp<MagbootsComponent>(boots) &&
               _toggle.IsActivated(boots.Value);
    }

    /// <summary>Open ground a little way out from the outpost's edge, on a random side.</summary>
    private bool TryPickBossSpot(EntityUid map, LavalandStrongStormConfig config, out MapCoordinates spot)
    {
        spot = MapCoordinates.Nullspace;
        if (!_raids.TryGetOutpostBounds(map, out var bounds) || !TryComp<MapGridComponent>(map, out var grid))
            return false;

        var mapId = Transform(map).MapID;
        var reach = MathF.Max(bounds.Width, bounds.Height) / 2f + config.BossDistance;
        for (var attempt = 0; attempt < 40; attempt++)
        {
            var candidate = bounds.Center + _random.NextAngle().ToVec() * reach;
            if (!IsOpenGround(map, grid, candidate))
                continue;

            spot = new MapCoordinates(candidate, mapId);
            return true;
        }

        return false;
    }

    private bool IsOpenGround(EntityUid map, MapGridComponent grid, Vector2 position)
    {
        var tile = _map.WorldToTile(map, grid, position);
        if (!_map.TryGetTileRef(map, grid, tile, out var tileRef) || tileRef.Tile.IsEmpty)
            return false;

        var anchored = _map.GetAnchoredEntitiesEnumerator(map, grid, tile);
        return !anchored.MoveNext(out _);
    }
}

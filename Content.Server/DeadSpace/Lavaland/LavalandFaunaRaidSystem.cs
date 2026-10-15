// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Chat.Systems;
using Content.Server.DeadSpace.Lavaland.Components;
using Content.Shared.DeadSpace.Lavaland;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Lavaland;

/// <summary>
/// Periodic fauna raids on the salvage outpost, independent of any machine. Settings live in the lavaland
/// planet prototype (<see cref="LavalandFaunaRaidConfig"/>).
/// </summary>
public sealed class LavalandFaunaRaidSystem : EntitySystem
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LavalandFaunaRaidComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnShutdown(Entity<LavalandFaunaRaidComponent> ent, ref ComponentShutdown args)
    {
        EndRaid(ent, TerminatingOrDeleted(ent) ? MapId.Nullspace : Transform(ent).MapID);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var maps = EntityQueryEnumerator<LavalandMapComponent, TransformComponent>();
        while (maps.MoveNext(out var mapUid, out var lavaland, out var xform))
        {
            if (lavaland.Planet.Id == null || !_prototype.TryIndex(lavaland.Planet, out var planet) || !planet.FaunaRaid.Enabled)
                continue;

            var config = planet.FaunaRaid;
            if (!TryComp<LavalandFaunaRaidComponent>(mapUid, out var raid))
            {
                raid = AddComp<LavalandFaunaRaidComponent>(mapUid);
                raid.Chance = config.BaseChance;
                raid.NextCheck = now + config.CheckInterval;
            }

            UpdateRaid((mapUid, raid), config, xform.MapID, now);
        }
    }

    private void UpdateRaid(Entity<LavalandFaunaRaidComponent> ent, LavalandFaunaRaidConfig config, MapId mapId, TimeSpan now)
    {
        var raid = ent.Comp;
        switch (raid.Stage)
        {
            case LavalandFaunaRaidStage.Idle:
                if (now < raid.NextCheck)
                    return;

                raid.NextCheck = now + config.CheckInterval;
                if (!TryFindOutpost(ent, out _))
                    return;

                if (!_random.Prob(Math.Clamp(raid.Chance, 0f, 1f)))
                {
                    // Every miss makes the next raid more likely.
                    var min = config.ChanceIncreaseMin;
                    var max = Math.Max(min, config.ChanceIncreaseMax);
                    raid.Chance = Math.Min(1f, raid.Chance + _random.NextFloat(min, max));
                    return;
                }

                StartWarning(ent, config, mapId, now);
                break;

            case LavalandFaunaRaidStage.Warning when now >= raid.StageEnd:
                raid.Stage = LavalandFaunaRaidStage.Active;
                raid.StageEnd = now + config.Duration;
                raid.NextTimerUpdate = TimeSpan.Zero;
                SpawnTendrils(ent, config);
                var filter = Filter.BroadcastMap(mapId);
                _chat.DispatchFilteredAnnouncement(filter, Loc.GetString("lavaland-raid-active"),
                    playSound: false,
                    colorOverride: Color.CornflowerBlue);
                if (config.Music.Count > 0)
                    raid.MusicStream = _audio.PlayGlobal(_random.Pick(config.Music), filter, false)?.Entity;
                break;

            case LavalandFaunaRaidStage.Active when now >= raid.StageEnd:
                EndRaid(ent, mapId);
                return;
        }

        if (raid.Stage == LavalandFaunaRaidStage.Idle || now < raid.NextTimerUpdate)
            return;

        raid.NextTimerUpdate = now + TimeSpan.FromSeconds(1);
        var seconds = Math.Max(0, (int) Math.Ceiling((raid.StageEnd - now).TotalSeconds));
        RaiseNetworkEvent(new LavalandFaunaRaidTimerEvent(seconds, raid.Stage == LavalandFaunaRaidStage.Warning),
            Filter.BroadcastMap(mapId));
    }

    private void StartWarning(Entity<LavalandFaunaRaidComponent> ent, LavalandFaunaRaidConfig config, MapId mapId, TimeSpan now)
    {
        var raid = ent.Comp;
        raid.Chance = config.BaseChance;
        raid.Stage = LavalandFaunaRaidStage.Warning;
        raid.StageEnd = now + config.WarningDuration;
        raid.NextTimerUpdate = TimeSpan.Zero;
        _chat.DispatchFilteredAnnouncement(Filter.BroadcastMap(mapId),
            Loc.GetString("lavaland-raid-warning"),
            playSound: true,
            announcementSound: config.WarningSound,
            colorOverride: Color.Red);
    }

    /// <summary>
    /// Starts a raid on the lavaland map right now (guaranteed raids of the bluespace well).
    /// Does nothing if a raid is already coming or going on. The regular schedule is not touched.
    /// </summary>
    public void ForceRaid(EntityUid mapUid)
    {
        if (!TryComp<LavalandMapComponent>(mapUid, out var lavaland) ||
            lavaland.Planet.Id == null || !_prototype.TryIndex(lavaland.Planet, out var planet) ||
            !planet.FaunaRaid.Enabled || !TryFindOutpost(mapUid, out _))
        {
            return;
        }

        var raid = EnsureComp<LavalandFaunaRaidComponent>(mapUid);
        if (raid.Stage != LavalandFaunaRaidStage.Idle)
            return;

        var now = _timing.CurTime;
        if (raid.NextCheck == TimeSpan.Zero)
            raid.NextCheck = now + planet.FaunaRaid.CheckInterval;
        StartWarning((mapUid, raid), planet.FaunaRaid, Transform(mapUid).MapID, now);
    }

    private void EndRaid(Entity<LavalandFaunaRaidComponent> ent, MapId mapId)
    {
        foreach (var spawned in ent.Comp.Entities)
        {
            if (Exists(spawned))
                QueueDel(spawned);
        }

        ent.Comp.Entities.Clear();
        ent.Comp.MusicStream = _audio.Stop(ent.Comp.MusicStream);
        var wasRunning = ent.Comp.Stage != LavalandFaunaRaidStage.Idle;
        ent.Comp.Stage = LavalandFaunaRaidStage.Idle;
        if (wasRunning && mapId != MapId.Nullspace)
            RaiseNetworkEvent(new LavalandFaunaRaidTimerHideEvent(), Filter.BroadcastMap(mapId));
    }

    private void SpawnTendrils(Entity<LavalandFaunaRaidComponent> ent, LavalandFaunaRaidConfig config)
    {
        if (!TryFindOutpost(ent, out var outpost))
            return;

        var center = _transform.GetMapCoordinates(outpost);
        var min = config.MinDistance;
        var max = Math.Max(min, config.MaxDistance);
        foreach (var prototype in config.Tendrils)
        {
            var offset = _random.NextAngle().ToWorldVec() * _random.NextFloat(min, max);
            var tendril = Spawn(prototype, new EntityCoordinates(ent, center.Position + offset));
            EnsureComp<TimedDespawnComponent>(tendril).Lifetime = (float) config.Duration.TotalSeconds;
            ent.Comp.Entities.Add(tendril);
        }
    }

    private bool TryFindOutpost(EntityUid mapUid, out EntityUid outpost)
    {
        var query = EntityQueryEnumerator<LavalandOutpostComponent>();
        while (query.MoveNext(out var uid, out var marker))
        {
            if (marker.Map != mapUid)
                continue;
            outpost = uid;
            return true;
        }

        outpost = default;
        return false;
    }
}

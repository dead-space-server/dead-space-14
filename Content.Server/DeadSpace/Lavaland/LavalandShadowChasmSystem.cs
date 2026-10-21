// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.DeadSpace.Lavaland.ChromiteCaves;
using Content.Shared.Chasm;
using Content.Shared.DeadSpace.Lavaland.ChromiteCaves;
using Content.Shared.DeadSpace.Lavaland.Components;
using Content.Shared.DeadSpace.Lavaland.SecretBoss;
using Content.Shared.Ghost;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Popups;
using Robust.Server.Player;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Lavaland;

/// <summary>
/// In the deep night nothing keeps a body above a chasm for long. Whoever hangs over one - on the flight potion's
/// cloud, in moon boots or the like - has <see cref="Content.Shared.DeadSpace.Lavaland.LavalandDeepNightConfig.ChasmHoverTime"/>
/// (the countdown over his head is shown to him only); then a shadow rises, drags him down, and he dies in the chasm.
/// </summary>
public sealed class LavalandShadowChasmSystem : EntitySystem
{
    [Dependency] private readonly ChasmSystem _chasm = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly LavalandChromiteCavesSystem _caves = default!;
    [Dependency] private readonly LavalandDeepNightSystem _deepNight = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(0.2);

    private readonly List<EntityUid> _toProcess = new();
    private TimeSpan _nextCheck;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        UpdateGrabs(now);

        if (now < _nextCheck)
            return;
        _nextCheck = now + CheckInterval;

        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity is not { Valid: true } mob)
                continue;

            UpdateHover(mob, now);
        }

        // Timers of those who are no longer controlled by a player (left the body and the like).
        _toProcess.Clear();
        var timers = EntityQueryEnumerator<LavalandShadowTimerComponent>();
        while (timers.MoveNext(out var uid, out _))
        {
            if (!_players.TryGetSessionByEntity(uid, out _))
                _toProcess.Add(uid);
        }

        foreach (var uid in _toProcess)
        {
            RemComp<LavalandShadowTimerComponent>(uid);
        }
    }

    private void UpdateHover(EntityUid mob, TimeSpan now)
    {
        if (HasComp<LavalandShadowGrabComponent>(mob))
            return;

        var hovering = _deepNight.TryGetDeepNight(mob, out var deep) &&
                       deep.ChasmHoverTime > TimeSpan.Zero &&
                       CanBeTaken(mob) &&
                       TryGetChasmUnder(mob, out _);

        if (!hovering)
        {
            // Back on solid ground: the shadows lose count.
            RemComp<LavalandShadowTimerComponent>(mob);
            return;
        }

        if (!TryComp<LavalandShadowTimerComponent>(mob, out var timer))
        {
            timer = AddComp<LavalandShadowTimerComponent>(mob);
            timer.EndTime = now + deep.ChasmHoverTime;
            Dirty(mob, timer);
            return;
        }

        if (now < timer.EndTime)
            return;

        RemComp<LavalandShadowTimerComponent>(mob);
        var grab = AddComp<LavalandShadowGrabComponent>(mob);
        grab.Duration = (float) deep.ShadowGrabTime.TotalSeconds;
        grab.EndTime = now + deep.ShadowGrabTime;
        Dirty(mob, grab);
        _popup.PopupEntity(Loc.GetString(deep.ShadowGrabMessage), mob, mob, PopupType.LargeCaution);
    }

    private bool CanBeTaken(EntityUid mob)
    {
        return !HasComp<GhostComponent>(mob) &&
               HasComp<MobStateComponent>(mob) && !_mobState.IsDead(mob) &&
               !HasComp<ChasmFallingComponent>(mob) &&
               !HasComp<LavalandPitFrozenComponent>(mob) &&
               !_caves.IsTravelling(mob);
    }

    private bool TryGetChasmUnder(EntityUid mob, out Entity<ChasmComponent> chasm)
    {
        chasm = default;
        var xform = Transform(mob);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return false;

        var tile = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
        while (anchored.MoveNext(out var uid))
        {
            if (!TryComp<ChasmComponent>(uid, out var comp))
                continue;

            chasm = (uid.Value, comp);
            return true;
        }

        // A chasm that is not anchored to the tile (spawned by hand and the like) is still a chasm under him.
        foreach (var found in _lookup.GetEntitiesInRange<ChasmComponent>(_transform.GetMapCoordinates(mob, xform), 0.3f))
        {
            chasm = found;
            return true;
        }

        return false;
    }

    private void UpdateGrabs(TimeSpan now)
    {
        _toProcess.Clear();
        var query = EntityQueryEnumerator<LavalandShadowGrabComponent>();
        while (query.MoveNext(out var uid, out var grab))
        {
            if (now >= grab.EndTime)
                _toProcess.Add(uid);
        }

        foreach (var uid in _toProcess)
        {
            RemComp<LavalandShadowGrabComponent>(uid);
            TakeDown(uid);
        }
    }

    /// <summary>Whatever held him up lets go, he dies and the chasm takes the body.</summary>
    private void TakeDown(EntityUid mob)
    {
        RemComp<LavalandChasmImmuneComponent>(mob);
        RemComp<CanMoveInAirComponent>(mob);
        RemComp<LavalandCloudWalkerComponent>(mob);

        if (!_mobState.IsDead(mob))
            _mobState.ChangeMobState(mob, MobState.Dead);

        if (TryGetChasmUnder(mob, out var chasm))
            _chasm.StartFalling(chasm, chasm.Comp, mob);
    }
}

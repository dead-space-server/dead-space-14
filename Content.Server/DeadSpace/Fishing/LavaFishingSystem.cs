using System.Linq;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.DeadSpace.Fishing;
using Content.Shared.EntityTable;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Maps;
using Content.Shared.Popups;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Fishing;

public sealed class LavaFishingSystem : SharedLavaFishingSystem
{
    private const int AreaSize = 16;
    private const int AreaCapacity = 6;
    private const double RestockSeconds = 120;

    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly EntityTableSystem _tables = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLog = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LavaFishingRodComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<LavaFishingPoolComponent, EntityUnpausedEvent>(OnPoolUnpaused);
    }

    protected override void BeginFishing(Entity<LavaFishingRodComponent> ent, EntityUid fisher, EntityCoordinates spot)
    {
        if (ent.Comp.Rewards.Count == 0 || _turf.GetTileRef(spot) is not { } tile ||
            !HasComp<MapGridComponent>(tile.GridUid))
            return;

        var gridUid = tile.GridUid;
        var rods = EntityQueryEnumerator<LavaFishingRodComponent>();
        while (rods.MoveNext(out var rod))
        {
            if (rod.Fisher == fisher)
                return;
        }

        var container = _containers.EnsureContainer<ContainerSlot>(ent.Owner, ent.Comp.CatchContainer);
        if (container.ContainedEntity != null)
        {
            ent.Comp.Phase = LavaFishingPhase.Caught;
            Dirty(ent);
            RetrieveCatch(ent, fisher);
            return;
        }

        var area = new Vector2i((int) Math.Floor(tile.GridIndices.X / (double) AreaSize),
            (int) Math.Floor(tile.GridIndices.Y / (double) AreaSize));
        var pool = EnsureComp<LavaFishingPoolComponent>(gridUid);
        if (!pool.Areas.TryGetValue(area, out var stock))
        {
            stock = new LavaFishingStock { UpdatedAt = _timing.CurTime };
            pool.Areas.Add(area, stock);
        }
        var replenished = (int) ((_timing.CurTime - stock.UpdatedAt).TotalSeconds / RestockSeconds);
        if (replenished > 0)
        {
            stock.Available = Math.Min(AreaCapacity, stock.Available + replenished);
            stock.UpdatedAt += TimeSpan.FromSeconds(replenished * RestockSeconds);
        }
        if (stock.Available <= 0)
        {
            _popup.PopupEntity(Loc.GetString("lava-fishing-depleted"), ent, fisher);
            return;
        }
        var reward = PickReward(ent.Comp);
        var catches = _tables.GetSpawns(reward.Table).ToArray();
        if (catches.Length != 1)
        {
            Log.Error($"Fishing table for {ToPrettyString(ent)} must produce exactly one catch.");
            return;
        }
        var session = EnsureComp<LavaFishingSessionComponent>(ent);
        session.Grid = gridUid;
        session.Area = area;
        session.Reward = catches[0];
        stock.Available--;
        ent.Comp.Bait--;
        ent.Comp.Fisher = fisher;
        ent.Comp.Origin = _transform.ToCoordinates(gridUid, _transform.GetMapCoordinates(fisher));
        ent.Comp.Spot = _transform.ToCoordinates(gridUid, _transform.ToMapCoordinates(spot));
        ent.Comp.Phase = LavaFishingPhase.Waiting;
        ent.Comp.NextPhase = _timing.CurTime + TimeSpan.FromSeconds(_random.Next(5, 11));
        ent.Comp.Difficulty = reward.Difficulty;
        ent.Comp.IsFish = reward.Fish;
        ent.Comp.Rhythm = _random.NextFloat() * MathF.Tau;
        ent.Comp.Reeling = false;
        ent.Comp.ZonePosition = 0.5f;
        ent.Comp.FishPosition = 0.5f;
        ent.Comp.Progress = 0.25f;
        ent.Comp.Elapsed = 0;
        ent.Comp.Float = Spawn("LavaFishingFloat", ent.Comp.Spot);
        Dirty(ent);
    }

    private LavaFishingReward PickReward(LavaFishingRodComponent rod)
    {
        var remaining = _random.NextFloat() * rod.Rewards.Sum(reward => reward.Weight);
        foreach (var reward in rod.Rewards)
        {
            remaining -= reward.Weight;
            if (remaining <= 0)
                return reward;
        }
        return rod.Rewards[^1];
    }

    protected override void FinishFishing(Entity<LavaFishingRodComponent> ent, bool success)
    {
        if (ent.Comp.Phase is LavaFishingPhase.Idle or LavaFishingPhase.Caught)
            return;

        var fisher = ent.Comp.Fisher;
        ent.Comp.Fisher = null;
        ent.Comp.Reeling = false;
        ent.Comp.Phase = LavaFishingPhase.Idle;
        if (ent.Comp.Float is { } floatUid)
        {
            QueueDel(floatUid);
            ent.Comp.Float = null;
        }

        if (TryComp<LavaFishingSessionComponent>(ent, out var session))
        {
            if (success && fisher is { } user && !TerminatingOrDeleted(user))
            {
                var container = _containers.EnsureContainer<ContainerSlot>(ent.Owner, ent.Comp.CatchContainer);
                var reward = Spawn(session.Reward);
                if (_containers.Insert(reward, container))
                {
                    ent.Comp.Phase = LavaFishingPhase.Caught;
                    _adminLog.Add(LogType.Action, LogImpact.Low,
                        $"{ToPrettyString(user):user} caught {ToPrettyString(reward):reward} with {ToPrettyString(ent):rod}.");
                    _popup.PopupEntity(Loc.GetString("lava-fishing-caught", ("catch", reward)), ent, user);
                    RetrieveCatch(ent, user);
                }
                else
                {
                    QueueDel(reward);
                    ReleaseStock(session);
                }
            }
            else
            {
                ReleaseStock(session);
                if (fisher is { } unsuccessfulFisher && !TerminatingOrDeleted(unsuccessfulFisher))
                    _popup.PopupEntity(Loc.GetString("lava-fishing-escaped"), ent, unsuccessfulFisher);
            }
            RemComp<LavaFishingSessionComponent>(ent);
        }
        Dirty(ent);
    }

    protected override void RetrieveCatch(Entity<LavaFishingRodComponent> ent, EntityUid user)
    {
        if (!_hands.IsHolding(user, ent.Owner) || !_containers.TryGetContainer(ent.Owner, ent.Comp.CatchContainer, out var container) ||
            container.ContainedEntities.FirstOrDefault() is not { Valid: true } caught)
            return;

        if (!_hands.TryPickupAnyHand(user, caught))
        {
            _popup.PopupEntity(Loc.GetString("lava-fishing-free-hand"), ent, user);
            return;
        }
        ent.Comp.Phase = LavaFishingPhase.Idle;
        Dirty(ent);
    }

    private void ReleaseStock(LavaFishingSessionComponent session)
    {
        if (TryComp<LavaFishingPoolComponent>(session.Grid, out var pool) && pool.Areas.TryGetValue(session.Area, out var stock))
            stock.Available = Math.Min(AreaCapacity, stock.Available + 1);
    }

    private void OnPoolUnpaused(Entity<LavaFishingPoolComponent> ent, ref EntityUnpausedEvent args)
    {
        foreach (var stock in ent.Comp.Areas.Values)
            stock.UpdatedAt += args.PausedTime;
    }

    private void OnShutdown(Entity<LavaFishingRodComponent> ent, ref ComponentShutdown args)
    {
        if (TryComp<LavaFishingSessionComponent>(ent, out var session))
            ReleaseStock(session);
        if (ent.Comp.Float is { } floatUid && !TerminatingOrDeleted(floatUid))
            QueueDel(floatUid);
    }
}

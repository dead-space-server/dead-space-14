// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using System.Linq;
using Content.Server.DeadSpace.Lavaland.Components;
using Content.Shared.Examine;
using Content.Shared.GameTicking;
using Content.Server.Radio.EntitySystems;
using Content.Server.Station.Systems;
using Content.Shared.Cargo;
using Content.Shared.Cargo.Components;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Storage;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Timing;
using Robust.Shared.Audio.Systems;
using Content.Shared.DeadSpace.Lavaland;
using Content.Shared.DeadSpace.Lavaland.Components;
using Robust.Shared.Map;

namespace Content.Server.DeadSpace.Lavaland;

public sealed class LavalandBluespaceWellSystem : EntitySystem
{
    private static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(5);

    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly LavalandMorkiteMissionSystem _morkite = default!;
    [Dependency] private readonly LavalandFaunaRaidSystem _raids = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly LavalandMiningSystem _mining = default!;
    [Dependency] private readonly SharedCargoSystem _cargo = default!;
    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private TimeSpan _nextScan;

    // Ore lying loose on a lavaland map -> when it was first seen there.
    private readonly Dictionary<EntityUid, TimeSpan> _groundSince = new();
    private readonly HashSet<EntityUid> _seenOre = new();
    private readonly Dictionary<EntityUid, List<Entity<LavalandBluespaceWellComponent>>> _wellsByMap = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<LavalandBluespaceWellComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<LavalandBluespaceWellComponent, InteractUsingEvent>(OnInsert);
        SubscribeLocalEvent<LavalandBluespaceWellComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<LavalandBluespaceWellComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<LavalandBluespaceWellComponent, GetVerbsEvent<AlternativeVerb>>(OnUnloadVerb);
        SubscribeLocalEvent<LavalandBluespaceWellComponent, GetVerbsEvent<Verb>>(OnSendVerb);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnInit(Entity<LavalandBluespaceWellComponent> ent, ref ComponentInit args)
    {
        ent.Comp.Ore = _containers.EnsureContainer<Container>(ent, "lavaland-well-ore");
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        _groundSince.Clear();
    }

    private int Count(LavalandBluespaceWellComponent comp)
    {
        var count = 0;
        foreach (var uid in comp.Ore.ContainedEntities)
        {
            if (TryComp<StackComponent>(uid, out var stack))
                count += stack.Count;
        }
        return count;
    }

    private bool CanSend(LavalandBluespaceWellComponent comp)
    {
        return comp.ReturnAt == null && (Count(comp) >= comp.RequiredOre || comp.Morkite >= 100f);
    }

    private bool Insert(Entity<LavalandBluespaceWellComponent> ent, EntityUid ore)
    {
        return ent.Comp.ReturnAt == null && TryComp<StackComponent>(ore, out var stack) &&
               ent.Comp.OreTypes.Contains(stack.StackTypeId) && _containers.Insert(ore, ent.Comp.Ore);
    }

    private void OnInsert(Entity<LavalandBluespaceWellComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || ent.Comp.ReturnAt != null)
            return;
        if (TryComp<StorageComponent>(args.Used, out var storage))
            args.Handled = EmptyStorage(ent, storage, args.User) > 0;
        else
            args.Handled = Insert(ent, args.Used);
    }

    /// <summary>
    /// An empty hand on the well: at once all the ore from every bag the miner carries (the satchel on the belt
    /// too) goes in, no bag in hand and no waiting.
    /// </summary>
    private void OnInteractHand(Entity<LavalandBluespaceWellComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled || ent.Comp.ReturnAt != null)
            return;

        var moved = 0;
        foreach (var item in _inventory.GetHandOrInventoryEntities(args.User))
        {
            if (TryComp<StorageComponent>(item, out var storage))
                moved += EmptyStorage(ent, storage, null);
        }

        if (moved == 0)
            return;

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("lavaland-well-inserted", ("count", moved)), ent, args.User);
    }

    /// <summary>All the ore the well takes from the storage; how much went in.</summary>
    private int EmptyStorage(Entity<LavalandBluespaceWellComponent> ent, StorageComponent storage, EntityUid? user)
    {
        var moved = 0;
        foreach (var ore in storage.Container.ContainedEntities.ToArray())
        {
            var amount = TryComp<StackComponent>(ore, out var stack) ? stack.Count : 1;
            if (Insert(ent, ore))
                moved += amount;
        }

        if (moved > 0 && user != null)
            _popup.PopupEntity(Loc.GetString("lavaland-well-inserted", ("count", moved)), ent, user.Value);
        return moved;
    }

    private void OnExamine(Entity<LavalandBluespaceWellComponent> ent, ref ExaminedEvent args)
    {
        var count = Count(ent.Comp);
        var percent = Math.Clamp(count * 100L / Math.Max(1, ent.Comp.RequiredOre), 0, 100);
        args.PushMarkup(Loc.GetString("lavaland-well-progress", ("count", count),
            ("required", ent.Comp.RequiredOre), ("percent", percent)));
        args.PushMarkup(Loc.GetString("lavaland-well-morkite-progress",
            ("percent", (int) Math.Floor(ent.Comp.Morkite))));
    }

    private void OnUnloadVerb(Entity<LavalandBluespaceWellComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || ent.Comp.ReturnAt != null)
            return;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("lavaland-well-unload"),
            Act = () => Unload(ent),
        });
    }

    private void Unload(Entity<LavalandBluespaceWellComponent> ent)
    {
        if (Deleted(ent) || ent.Comp.ReturnAt != null)
            return;
        foreach (var ore in ent.Comp.Ore.ContainedEntities.ToArray())
            _containers.Remove(ore, ent.Comp.Ore);
    }

    private void OnSendVerb(Entity<LavalandBluespaceWellComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !CanSend(ent.Comp))
            return;
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("lavaland-well-send"),
            Act = () =>
            {
                if (Deleted(ent) || !CanSend(ent.Comp))
                    return;
                foreach (var ore in ent.Comp.Ore.ContainedEntities.ToArray())
                    QueueDel(ore);
                ent.Comp.Morkite = 0f;
                ent.Comp.RaidsTriggered.Clear();
                ent.Comp.WasConnected = false;
                ent.Comp.RewardPending = true;
                ent.Comp.ReturnAt = _timing.CurTime + ent.Comp.ReturnDelay;
                ent.Comp.ReturnCoordinates = Transform(ent).Coordinates;
                TeleportEffect(ent);
                _transform.DetachEntity(ent, Transform(ent));
            },
        });
    }

    /// <summary>The well is back in place: the salvagers' mission is done.</summary>
    private void PayReward(Entity<LavalandBluespaceWellComponent> ent)
    {
        if (!ent.Comp.RewardPending)
            return;

        ent.Comp.RewardPending = false;
        if (ent.Comp.RewardCredits > 0 &&
            Transform(ent).MapUid is { } map &&
            TryComp<LavalandMapComponent>(map, out var lavaland) &&
            TryComp<StationBankAccountComponent>(lavaland.Station, out var bank))
        {
            _cargo.UpdateBankAccount((lavaland.Station, bank), ent.Comp.RewardCredits, bank.PrimaryAccount);

            // Said from the station itself: a radio on the lavaland map does not reach the station's telecoms.
            if (_station.GetLargestGrid(lavaland.Station) is { } stationGrid)
            {
                _radio.SendRadioMessage(ent, Loc.GetString("lavaland-well-reward-radio", ("credits", ent.Comp.RewardCredits)),
                    ent.Comp.RewardChannel, stationGrid);
            }
        }

        if (ent.Comp.RewardPoints > 0)
            _mining.AwardMiningPoints(ent.Comp.RewardPoints, ent.Comp.RewardAccess);
    }

    private void TeleportEffect(Entity<LavalandBluespaceWellComponent> ent)
    {
        Spawn(ent.Comp.TeleportEffect, Transform(ent).Coordinates);
        _audio.PlayPvs(ent.Comp.TeleportSound, Transform(ent).Coordinates);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var wells = EntityQueryEnumerator<LavalandBluespaceWellComponent, TransformComponent>();
        while (wells.MoveNext(out var uid, out var well, out var transform))
        {
            if (well.ReturnAt is { } returnAt)
            {
                if (now < returnAt)
                    continue;
                well.ReturnAt = null;
                if (!Exists(well.ReturnCoordinates.EntityId))
                {
                    QueueDel(uid);
                    continue;
                }
                _transform.SetCoordinates(uid, well.ReturnCoordinates);
                _transform.AnchorEntity((uid, transform));
                TeleportEffect((uid, well));
                PayReward((uid, well));
            }
        }

        if (now < _nextScan)
            return;
        _nextScan = now + ScanInterval;
        UpdateMorkite((float) ScanInterval.TotalSeconds);
        ScanGroundOre(now);
        CheckGuaranteedRaids();
    }

    /// <summary>
    /// Guaranteed fauna raids at fill thresholds: the ore bar on ore delivery,
    /// the morkite bar on morkite mining (when the map has morkite extractors).
    /// </summary>
    private void CheckGuaranteedRaids()
    {
        var wells = EntityQueryEnumerator<LavalandBluespaceWellComponent, TransformComponent>();
        while (wells.MoveNext(out _, out var well, out var xform))
        {
            if (well.ReturnAt != null || !xform.Anchored ||
                xform.MapUid is not { } map || !HasComp<LavalandMapComponent>(map))
            {
                continue;
            }

            var morkite = _morkite.HasExtractorsOnMap(map);
            var thresholds = morkite ? well.MorkiteRaidPercents : well.OreRaidPercents;
            if (thresholds.Count == 0)
                continue;

            var percent = morkite
                ? well.Morkite
                : Count(well) * 100f / Math.Max(1, well.RequiredOre);

            foreach (var threshold in thresholds)
            {
                if (percent < threshold || !well.RaidsTriggered.Add(threshold))
                    continue;

                _raids.ForceRaid(map);
            }
        }
    }

    private void UpdateMorkite(float elapsed)
    {
        if (elapsed <= 0f)
            return;
        var wells = EntityQueryEnumerator<LavalandBluespaceWellComponent, TransformComponent>();
        while (wells.MoveNext(out var uid, out var well, out var xform))
        {
            if (well.ReturnAt != null || !xform.Anchored || well.Morkite >= 100f)
                continue;
            var rate = _morkite.GetMorkiteRate(uid);
            if (rate > 0f)
            {
                well.WasConnected = true;
                well.Morkite = Math.Min(100f, well.Morkite + rate * elapsed);
            }
            else if (well.WasConnected && well.Morkite > 0f)
            {
                // A broken pipe: nothing flows and some morkite leaks out.
                well.Morkite = Math.Max(0f, well.Morkite - well.MorkiteLeakRate * elapsed);
            }
        }
    }

    /// <summary>
    /// One pass over the ore for every well: loose ore left on a lavaland map long enough is pulled into a well there.
    /// </summary>
    private void ScanGroundOre(TimeSpan now)
    {
        foreach (var list in _wellsByMap.Values)
            list.Clear();

        var any = false;
        var wells = EntityQueryEnumerator<LavalandBluespaceWellComponent, TransformComponent>();
        while (wells.MoveNext(out var uid, out var well, out var xform))
        {
            if (well.ReturnAt != null || !xform.Anchored ||
                xform.MapUid is not { } map || !HasComp<LavalandMapComponent>(map))
            {
                continue;
            }

            if (!_wellsByMap.TryGetValue(map, out var list))
                _wellsByMap[map] = list = new List<Entity<LavalandBluespaceWellComponent>>();
            list.Add((uid, well));
            any = true;
        }

        if (!any)
        {
            _groundSince.Clear();
            return;
        }

        _seenOre.Clear();
        // Only marked ore, not every stack on the station.
        var ores = EntityQueryEnumerator<LavalandWellOreComponent, StackComponent, TransformComponent>();
        while (ores.MoveNext(out var ore, out _, out var stack, out var oreTransform))
        {
            if (oreTransform.MapUid is not { } map ||
                !_wellsByMap.TryGetValue(map, out var mapWells) || mapWells.Count == 0 ||
                _containers.IsEntityInContainer(ore))
            {
                continue;
            }

            Entity<LavalandBluespaceWellComponent>? target = null;
            foreach (var well in mapWells)
            {
                if (!well.Comp.OreTypes.Contains(stack.StackTypeId))
                    continue;
                target = well;
                break;
            }

            if (target is not { } accepting)
                continue;

            _seenOre.Add(ore);
            if (!_groundSince.TryGetValue(ore, out var since))
                _groundSince[ore] = now;
            else if (now - since >= accepting.Comp.CollectionDelay && Insert(accepting, ore))
                _groundSince.Remove(ore);
        }

        foreach (var ore in _groundSince.Keys.ToArray())
        {
            if (!_seenOre.Contains(ore))
                _groundSince.Remove(ore);
        }
    }
}

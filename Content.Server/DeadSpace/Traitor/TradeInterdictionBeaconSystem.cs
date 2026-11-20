// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Cargo.Components;
using Content.Server.Cargo.Systems;
using Content.Server.Chat.Systems;
using Content.Server.DeadSpace.Taipan.Components;
using Content.Server.DeadSpace.Traitor.Objectives;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Shuttles.Components;
using Content.Server.Station.Systems;
using Content.Shared.Examine;
using Content.Shared.Foldable;
using Content.Shared.Popups;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Traitor;

public sealed partial class TradeInterdictionBeaconSystem : EntitySystem
{
    private static readonly Color DangerColor = Color.Red;
    private static readonly Color NoticeColor = Color.LightSkyBlue;
    private const string TradeAnchorExplosionType = "HardBomb";
    private const float TradeAnchorExplosionTotalIntensity = 6000f;
    private const float TradeAnchorExplosionIntensitySlope = 10f;
    private const float TradeAnchorExplosionMaxIntensity = 75f;

    [Dependency] private CargoSystem _cargo = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private ExplosionSystem _explosion = default!;
    [Dependency] private FoldableSystem _foldable = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private TraitorUltraHijackTradeConditionSystem _objectives = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TradeInterdictionBeaconComponent, FoldAttemptEvent>(OnFoldAttempt);
        SubscribeLocalEvent<TradeInterdictionBeaconComponent, FoldedEvent>(OnFolded);
        SubscribeLocalEvent<TradeInterdictionBeaconComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<TradeInterdictionBeaconComponent, ComponentShutdown>(OnShutdown);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<TradeInterdictionBeaconComponent>();
        while (query.MoveNext(out var uid, out var beacon))
        {
            if (!beacon.Active ||
                beacon.Completed ||
                beacon.CompletionTime == null ||
                _timing.CurTime < beacon.CompletionTime.Value)
            {
                continue;
            }

            CompleteHijack(uid, beacon);
        }
    }

    private void OnFoldAttempt(Entity<TradeInterdictionBeaconComponent> ent, ref FoldAttemptEvent args)
    {
        if (ent.Comp.Active || ent.Comp.Completed)
        {
            args.Cancelled = true;
            return;
        }

        if (args.Comp.IsFolded && !TryGetValidTradeStation(ent.Owner, out _, out _, out _))
            args.Cancelled = true;
    }

    private void OnFolded(Entity<TradeInterdictionBeaconComponent> ent, ref FoldedEvent args)
    {
        if (args.IsFolded)
        {
            if (ent.Comp.Active && !ent.Comp.Completed)
                CancelHijack(ent.Owner, ent.Comp);

            return;
        }

        if (args.User is not { } user || !TryStartHijack(ent.Owner, ent.Comp, user))
            Refold(ent.Owner, args.User);
    }

    private void OnExamined(Entity<TradeInterdictionBeaconComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Completed)
        {
            args.PushMarkup(Loc.GetString("trade-interdiction-beacon-examine-complete"));
            return;
        }

        if (!ent.Comp.Active || ent.Comp.CompletionTime == null)
        {
            args.PushMarkup(Loc.GetString("trade-interdiction-beacon-examine-idle"));
            return;
        }

        var elapsed = ent.Comp.Duration - (ent.Comp.CompletionTime.Value - _timing.CurTime);
        var percentage = Math.Clamp((float) (elapsed.TotalSeconds / ent.Comp.Duration.TotalSeconds * 100), 0f, 100f);
        args.PushMarkup(Loc.GetString("trade-interdiction-beacon-examine-active", ("percentage", MathF.Round(percentage))));
    }

    private void OnShutdown(Entity<TradeInterdictionBeaconComponent> ent, ref ComponentShutdown args)
    {
        if (!ent.Comp.Active || ent.Comp.Completed)
            return;

        if (ent.Comp.CompletionTime is { } completionTime && completionTime <= _timing.CurTime)
        {
            CompleteHijack(ent.Owner, ent.Comp);
            return;
        }

        CancelHijack(ent.Owner, ent.Comp);
    }

    private bool TryStartHijack(EntityUid uid, TradeInterdictionBeaconComponent beacon, EntityUid user)
    {
        if (!TryGetValidTradeStation(uid, out var station, out var database, out var tradeGrid))
        {
            Popup(user, "trade-interdiction-beacon-popup-invalid-location");
            return false;
        }

        if (!_objectives.HasObjectiveForStation(user, station, out var mindId))
        {
            Popup(user, "trade-interdiction-beacon-popup-no-objective");
            return false;
        }

        if (HasActiveOrCompletedHijack(uid, station, database))
        {
            Popup(user, database.TradeHijacked
                ? "trade-interdiction-beacon-popup-already-complete"
                : "trade-interdiction-beacon-popup-already-active");
            return false;
        }

        beacon.Active = true;
        beacon.Completed = false;
        beacon.TargetStation = station;
        beacon.TargetTradeGrid = tradeGrid;
        beacon.HijackerMind = mindId;
        beacon.CompletionTime = _timing.CurTime + beacon.Duration;
        database.TradeHijackActiveBeacon = uid;

        DispatchAnnouncement(station, "trade-interdiction-announcement-started", beacon, DangerColor);
        _cargo.RefreshOrderConsoles(station);
        return true;
    }

    private void CompleteHijack(EntityUid uid, TradeInterdictionBeaconComponent beacon)
    {
        if (beacon.TargetStation is not { } station ||
            !TryComp<StationCargoOrderDatabaseComponent>(station, out var database))
        {
            beacon.Active = false;
            beacon.TargetStation = null;
            beacon.TargetTradeGrid = null;
            beacon.CompletionTime = null;
            return;
        }

        beacon.Active = false;
        beacon.Completed = true;
        beacon.CompletionTime = null;

        if (database.TradeHijackActiveBeacon == uid)
            database.TradeHijackActiveBeacon = null;

        database.TradeHijacked = true;

        if (beacon.HijackerMind is { } mind)
            _objectives.TryCompleteObjective(mind, station);

        DispatchAnnouncement(station, "trade-interdiction-announcement-completed", beacon, DangerColor);
        _cargo.RefreshOrderConsoles(station);
        ExplodeTradeAnchor(uid, beacon);
    }

    private void CancelHijack(EntityUid uid, TradeInterdictionBeaconComponent beacon)
    {
        if (beacon.TargetStation is { } station &&
            TryComp<StationCargoOrderDatabaseComponent>(station, out var database) &&
            !database.TradeHijacked)
        {
            if (database.TradeHijackActiveBeacon == uid)
                database.TradeHijackActiveBeacon = null;

            DispatchAnnouncement(station, "trade-interdiction-announcement-cancelled", beacon, NoticeColor);
            _cargo.RefreshOrderConsoles(station);
        }

        beacon.Active = false;
        beacon.TargetStation = null;
        beacon.TargetTradeGrid = null;
        beacon.HijackerMind = null;
        beacon.CompletionTime = null;
    }

    private bool TryGetValidTradeStation(
        EntityUid beacon,
        out EntityUid station,
        out StationCargoOrderDatabaseComponent database,
        out EntityUid tradeGrid)
    {
        station = default;
        database = default!;
        tradeGrid = default;

        var xform = Transform(beacon);
        if (xform.GridUid is not { } grid || !HasComp<TradeStationComponent>(grid))
            return false;

        if (_station.GetOwningStation(beacon, xform) is not { } owningStation ||
            HasComp<StationTaipanComponent>(owningStation) ||
            !TryComp<StationCargoOrderDatabaseComponent>(owningStation, out var databaseComp))
        {
            return false;
        }

        station = owningStation;
        database = databaseComp;
        tradeGrid = grid;
        return true;
    }

    private bool HasActiveOrCompletedHijack(EntityUid beacon, EntityUid station, StationCargoOrderDatabaseComponent database)
    {
        if (database.TradeHijacked || HasComp<StationTaipanComponent>(station))
            return true;

        if (database.TradeHijackActiveBeacon is not { } active)
            return false;

        if (active == beacon)
            return false;

        if (Exists(active) && !TerminatingOrDeleted(active))
            return true;

        database.TradeHijackActiveBeacon = null;
        return false;
    }

    private void ExplodeTradeAnchor(EntityUid beaconUid, TradeInterdictionBeaconComponent beacon)
    {
        var explosionTarget = beaconUid;

        if (beacon.TargetTradeGrid is { } tradeGrid && Exists(tradeGrid))
        {
            explosionTarget = tradeGrid;

            var anchorQuery = EntityQueryEnumerator<StationAnchorComponent>();
            while (anchorQuery.MoveNext(out var anchorUid, out _))
            {
                if (Transform(anchorUid).GridUid != tradeGrid)
                    continue;

                explosionTarget = anchorUid;
                break;
            }
        }

        _explosion.QueueExplosion(
            explosionTarget,
            TradeAnchorExplosionType,
            TradeAnchorExplosionTotalIntensity,
            TradeAnchorExplosionIntensitySlope,
            TradeAnchorExplosionMaxIntensity,
            deleteEntities: true,
            destroyTiles: true,
            ignoreTileBlockers: true);
    }

    private void Refold(EntityUid uid, EntityUid? user)
    {
        if (TryComp<FoldableComponent>(uid, out var foldable) && !foldable.IsFolded)
            _foldable.SetFolded(uid, foldable, true, user);
    }

    private void Popup(EntityUid user, string locId)
    {
        _popup.PopupEntity(Loc.GetString(locId), user, user, PopupType.MediumCaution);
    }

    private void DispatchAnnouncement(EntityUid station, string locId, TradeInterdictionBeaconComponent beacon, Color color)
    {
        var message = Loc.GetString(locId);
        _chat.DispatchStationAnnouncement(
            station,
            message,
            Loc.GetString("trade-interdiction-announcer"),
            playDefaultSound: true,
            announcementSound: beacon.AlertSound,
            colorOverride: color,
            voice: "Rita");
    }
}

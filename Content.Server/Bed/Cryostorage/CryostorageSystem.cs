using System.Globalization;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.Ghost;
using Content.Server.Hands.Systems;
using Content.Server.Inventory;
using Content.Server.Popups;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Server.StationRecords;
using Content.Server.StationRecords.Systems;
using Content.Shared.Access.Systems;
using Content.Shared.Bed.Cryostorage;
using Content.Shared.Chat;
using Content.Shared.Climbing.Systems;
using Content.Shared.Database;
using Content.Shared.DeadSpace.Virus;
using Content.Shared.GameTicking;
using Content.Shared.Hands.Components;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Mind.Components;
using Content.Shared.StationRecords;
using Content.Shared.UserInterface;
using Robust.Server.Audio;
using Robust.Server.Containers;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Containers;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server.Bed.Cryostorage;

/// <inheritdoc/>
public sealed partial class CryostorageSystem : SharedCryostorageSystem
{
    [Dependency] private IChatManager _chatManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private AccessReaderSystem _accessReader = default!;
    [Dependency] private ChatSystem _chatSystem = default!;
    [Dependency] private ClimbSystem _climb = default!;
    [Dependency] private ContainerSystem _container = default!;
    [Dependency] private GhostSystem _ghostSystem = default!;
    [Dependency] private HandsSystem _hands = default!;
    [Dependency] private ServerInventorySystem _inventory = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private StationJobsSystem _stationJobs = default!;
    [Dependency] private StationRecordsSystem _stationRecords = default!;
    [Dependency] private TransformSystem _transform = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CryostorageComponent, BeforeActivatableUIOpenEvent>(OnBeforeUIOpened);
        SubscribeLocalEvent<CryostorageComponent, CryostorageRemoveItemBuiMessage>(OnRemoveItemBuiMessage);
        SubscribeLocalEvent<CryostorageStoredGearComponent, EntityTerminatingEvent>(OnGearTerminating); //DS-14

        SubscribeLocalEvent<CryostorageContainedComponent, PlayerSpawnCompleteEvent>(OnPlayerSpawned);
        SubscribeLocalEvent<CryostorageContainedComponent, MindRemovedMessage>(OnMindRemoved);

        _playerManager.PlayerStatusChanged += PlayerStatusChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _playerManager.PlayerStatusChanged -= PlayerStatusChanged;
    }

    private void OnBeforeUIOpened(Entity<CryostorageComponent> ent, ref BeforeActivatableUIOpenEvent args)
    {
        UpdateCryostorageUIState(ent);
    }

    private void OnRemoveItemBuiMessage(Entity<CryostorageComponent> ent, ref CryostorageRemoveItemBuiMessage args)
    {
        var attachedEntity = args.Actor;

        //DS-14 start
        if (!TryComp<CryostorageStoredGearComponent>(ent, out var gear))
            return;

        CryostorageGearEntry? entry = null;
        foreach (var candidate in gear.Entries)
        {
            if (candidate.Id != args.EntryId)
                continue;

            entry = candidate;
            break;
        }

        if (entry == null)
            return;
        //DS-14 end

        if (!HasComp<HandsComponent>(attachedEntity))
            return;

        if (!_accessReader.IsAllowed(attachedEntity, ent))
        {
            _popup.PopupEntity(Loc.GetString("cryostorage-popup-access-denied"), attachedEntity, attachedEntity);
            return;
        }

        //DS-14 start
        var stored = args.Type == CryostorageRemoveItemBuiMessage.RemovalType.Hand
            ? entry.Hands
            : entry.Inventory;

        if (!stored.TryGetValue(args.Key, out var entity) || TerminatingOrDeleted(entity))
        {
            stored.Remove(args.Key);
            if (entry.Inventory.Count == 0 && entry.Hands.Count == 0)
                gear.Entries.Remove(entry);

            UpdateCryostorageUIState(ent);
            return;
        }

        AdminLog.Add(LogType.Action, LogImpact.High,
            $"{ToPrettyString(attachedEntity):player} removed item {ToPrettyString(entity)} from cryostorage {ToPrettyString(ent)} ({entry.Name})");

        _container.TryRemoveFromContainer(entity);
        _transform.SetCoordinates(entity, Transform(attachedEntity).Coordinates);
        _hands.PickupOrDrop(attachedEntity, entity);
        stored.Remove(args.Key);

        if (entry.Inventory.Count == 0 && entry.Hands.Count == 0)
            gear.Entries.Remove(entry);
        //DS-14 end

        UpdateCryostorageUIState(ent);
    }

    //DS-14 start
    private void OnGearTerminating(Entity<CryostorageStoredGearComponent> ent, ref EntityTerminatingEvent args)
    {
        if (!_container.TryGetContainer(ent, CryostorageStoredGearComponent.ContainerId, out var container))
            return;

        var coords = Transform(ent).Coordinates;
        var items = new List<EntityUid>(container.ContainedEntities);
        foreach (var item in items)
            _container.Remove(item, container, force: true, destination: coords);

        ent.Comp.Entries.Clear();
    }
    //DS-14 end

    private void UpdateCryostorageUIState(Entity<CryostorageComponent> ent)
    {
        var state = new CryostorageBuiState(GetAllContainedData(ent));
        _ui.SetUiState(ent.Owner, CryostorageUIKey.Key, state);
    }

    private void OnPlayerSpawned(Entity<CryostorageContainedComponent> ent, ref PlayerSpawnCompleteEvent args)
    {
        // if you spawned into cryostorage, we're not gonna round-remove you.
        ent.Comp.GracePeriodEndTime = null;
    }

    private void OnMindRemoved(Entity<CryostorageContainedComponent> ent, ref MindRemovedMessage args)
    {
        var comp = ent.Comp;

        if (!TryComp<CryostorageComponent>(comp.Cryostorage, out var cryostorageComponent))
            return;

        if (comp.GracePeriodEndTime != null)
            comp.GracePeriodEndTime = Timing.CurTime + cryostorageComponent.NoMindGracePeriod;
        comp.AllowReEnteringBody = false;
        comp.UserId = args.Mind.Comp.UserId;
    }

    private void PlayerStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        if (args.Session.AttachedEntity is not { } entity)
            return;

        if (!TryComp<CryostorageContainedComponent>(entity, out var containedComponent))
            return;

        if (args.NewStatus is SessionStatus.Disconnected or SessionStatus.Zombie)
        {
            containedComponent.AllowReEnteringBody = true;
            var delay = CompOrNull<CryostorageComponent>(containedComponent.Cryostorage)?.NoMindGracePeriod ?? TimeSpan.Zero;
            containedComponent.GracePeriodEndTime = Timing.CurTime + delay;
            containedComponent.UserId = args.Session.UserId;
        }
        else if (args.NewStatus == SessionStatus.InGame)
        {
            HandleCryostorageReconnection((entity, containedComponent));
        }
    }

    public void HandleEnterCryostorage(Entity<CryostorageContainedComponent> ent, NetUserId? userId)
    {
        var comp = ent.Comp;
        var cryostorageEnt = ent.Comp.Cryostorage;

        var station = _station.GetOwningStation(ent);
        var name = Name(ent.Owner);

        if (!TryComp<CryostorageComponent>(cryostorageEnt, out var cryostorageComponent))
            return;

        // if we have a session, we use that to add back in all the job slots the player had.
        if (userId != null)
        {
            foreach (var uniqueStation in _station.GetStationsSet())
            {
                if (!TryComp<StationJobsComponent>(uniqueStation, out var stationJobs))
                    continue;

                if (!_stationJobs.TryGetPlayerJobs(uniqueStation, userId.Value, out var jobs, stationJobs))
                    continue;

                foreach (var job in jobs)
                {
                    _stationJobs.TryAdjustJobSlot(uniqueStation, job, 1, clamp: true);
                }

                _stationJobs.TryRemovePlayerJobs(uniqueStation, userId.Value, stationJobs);
            }
        }

        _audio.PlayPvs(cryostorageComponent.RemoveSound, ent);

        if (!CryoSleepRejoiningEnabled || !comp.AllowReEnteringBody)
        {
            if (userId != null && Mind.TryGetMind(userId.Value, out var mind) &&
                HasComp<CryostorageContainedComponent>(mind.Value.Comp.CurrentEntity))
            {
                _ghostSystem.OnGhostAttempt(mind.Value, false);
            }
        }

        comp.AllowReEnteringBody = false;
        //DS-14 start
        comp.GracePeriodEndTime = null;
        StoreGear(ent.Owner, cryostorageEnt.Value, name);
        _container.TryRemoveFromContainer(ent.Owner);
        QueueDel(ent.Owner);
        //DS-14 end
        UpdateCryostorageUIState((cryostorageEnt.Value, cryostorageComponent));
        AdminLog.Add(LogType.Action, LogImpact.High, $"{ToPrettyString(ent):player} was entered into cryostorage inside of {ToPrettyString(cryostorageEnt.Value)}");

        if (!TryComp<StationRecordsComponent>(station, out var stationRecords))
            return;

        var jobName = Loc.GetString("earlyleave-cryo-job-unknown");
        var recordId = _stationRecords.GetRecordByName(station.Value, name);
        if (recordId != null)
        {
            var key = new StationRecordKey(recordId.Value, station.Value);
            if (_stationRecords.TryGetRecord<GeneralStationRecord>(key, out var entry, stationRecords))
                jobName = entry.JobTitle;

            _stationRecords.RemoveRecord(key, stationRecords);
        }

        _chatSystem.DispatchStationAnnouncement(station.Value,
            Loc.GetString(
                "earlyleave-cryo-announcement",
                ("character", name),
                ("entity", ent.Owner), // gender things for supporting downstreams with other languages
                ("job", CultureInfo.CurrentCulture.TextInfo.ToTitleCase(jobName))
            ), Loc.GetString("earlyleave-cryo-sender"),
            playDefaultSound: false
        );

        // DS14-Start
        var ev = new EnterCryostorageEvent();
        RaiseLocalEvent(ent.Owner, ev);
        // DS14-End
    }

    private void HandleCryostorageReconnection(Entity<CryostorageContainedComponent> entity)
    {
        var (uid, comp) = entity;
        if (!CryoSleepRejoiningEnabled || !IsInPausedMap(uid))
            return;

        // how did you destroy these? they're indestructible.
        if (comp.Cryostorage is not { } cryostorage ||
            TerminatingOrDeleted(cryostorage) ||
            !TryComp<CryostorageComponent>(cryostorage, out var cryostorageComponent))
        {
            QueueDel(entity);
            return;
        }

        var cryoXform = Transform(cryostorage);
        _transform.SetParent(uid, cryoXform.ParentUid);
        _transform.SetCoordinates(uid, cryoXform.Coordinates);
        if (!_container.TryGetContainer(cryostorage, cryostorageComponent.ContainerId, out var container) ||
            !_container.Insert(uid, container, cryoXform))
        {
            _climb.ForciblySetClimbing(uid, cryostorage);
        }

        comp.GracePeriodEndTime = null;
        cryostorageComponent.StoredPlayers.Remove(uid);
        AdminLog.Add(LogType.Action, LogImpact.High, $"{ToPrettyString(entity):player} re-entered the game from cryostorage {ToPrettyString(cryostorage)}");
        UpdateCryostorageUIState((cryostorage, cryostorageComponent));
    }

    protected override void OnInsertedContainer(Entity<CryostorageComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        var (uid, comp) = ent;
        if (args.Container.ID != comp.ContainerId)
            return;

        base.OnInsertedContainer(ent, ref args);

        var locKey = CryoSleepRejoiningEnabled
            ? "cryostorage-insert-message-temp"
            : "cryostorage-insert-message-permanent";

        var msg = Loc.GetString(locKey, ("time", comp.GracePeriod.TotalMinutes));
        if (TryComp<ActorComponent>(args.Entity, out var actor))
            _chatManager.ChatMessageToOne(ChatChannel.Server, msg, msg, uid, false, actor.PlayerSession.Channel);
    }

    private List<CryostorageContainedPlayerData> GetAllContainedData(Entity<CryostorageComponent> ent)
    {
        var data = new List<CryostorageContainedPlayerData>();
        //DS-14 start
        if (!TryComp<CryostorageStoredGearComponent>(ent, out var gear))
            return data;

        for (var i = gear.Entries.Count - 1; i >= 0; i--)
        {
            var entry = gear.Entries[i];
            PruneMissingGear(entry);
            if (entry.Inventory.Count == 0 && entry.Hands.Count == 0)
            {
                gear.Entries.RemoveAt(i);
                continue;
            }

            var contained = new CryostorageContainedPlayerData
            {
                PlayerName = entry.Name,
                EntryId = entry.Id,
            };

            foreach (var (slot, item) in entry.Inventory)
                contained.ItemSlots.Add(slot, Name(item));

            foreach (var (hand, item) in entry.Hands)
                contained.HeldItems.Add(hand, Name(item));

            data.Add(contained);
        }
        //DS-14 end

        return data;
    }

    //DS-14 start
    private void StoreGear(EntityUid body, EntityUid cryo, string playerName)
    {
        var gear = EnsureComp<CryostorageStoredGearComponent>(cryo);
        var container = _container.EnsureContainer<Container>(cryo, CryostorageStoredGearComponent.ContainerId);
        var entry = new CryostorageGearEntry
        {
            Id = gear.NextId++,
            Name = playerName,
        };

        var handItems = new List<(string Hand, EntityUid Item)>();
        foreach (var hand in _hands.EnumerateHands(body))
        {
            if (_hands.TryGetHeldItem(body, hand, out var held))
                handItems.Add((hand, held.Value));
        }

        foreach (var (hand, item) in handItems)
        {
            if (TerminatingOrDeleted(item))
                continue;

            if (HasComp<VirtualItemComponent>(item))
            {
                _hands.TryDrop((body, null), hand, checkActionBlocker: false, doDropInteraction: false);
                continue;
            }

            _hands.TryDrop((body, null), hand, checkActionBlocker: false, doDropInteraction: false);
            if (!TryStoreItem(item, container))
                continue;

            entry.Hands[hand] = item;
        }

        var worn = new List<(string Slot, EntityUid Item)>();
        var slots = _inventory.GetSlotEnumerator(body);
        while (slots.NextItem(out var item, out var slotDef))
            worn.Add((slotDef.Name, item));

        foreach (var (slot, item) in worn)
        {
            if (TerminatingOrDeleted(item))
                continue;

            if (_container.TryGetContainingContainer((item, null, null), out var current) && current.Owner == body)
                _inventory.TryUnequip(body, slot, silent: true, force: true);

            if (!TryStoreItem(item, container))
                continue;

            entry.Inventory[slot] = item;
        }

        if (entry.Inventory.Count == 0 && entry.Hands.Count == 0)
            return;

        gear.Entries.Add(entry);
    }

    private bool TryStoreItem(EntityUid item, BaseContainer container)
    {
        if (TerminatingOrDeleted(item))
            return false;

        if (container.Contains(item))
            return true;

        if (_container.Insert(item, container))
            return true;

        if (_container.IsEntityInContainer(item))
            _container.TryRemoveFromContainer(item, force: true);

        return !TerminatingOrDeleted(item) && _container.Insert(item, container, force: true);
    }

    private void PruneMissingGear(CryostorageGearEntry entry)
    {
        var slots = new List<string>();
        foreach (var (slot, item) in entry.Inventory)
        {
            if (TerminatingOrDeleted(item))
                slots.Add(slot);
        }

        foreach (var slot in slots)
            entry.Inventory.Remove(slot);

        var hands = new List<string>();
        foreach (var (hand, item) in entry.Hands)
        {
            if (TerminatingOrDeleted(item))
                hands.Add(hand);
        }

        foreach (var hand in hands)
            entry.Hands.Remove(hand);
    }
    //DS-14 end

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<CryostorageContainedComponent>();
        while (query.MoveNext(out var uid, out var containedComp))
        {
            if (containedComp.GracePeriodEndTime == null)
                continue;

            if (Timing.CurTime < containedComp.GracePeriodEndTime)
                continue;

            Mind.TryGetMind(uid, out _, out var mindComp);
            var id = mindComp?.UserId ?? containedComp.UserId;
            HandleEnterCryostorage((uid, containedComp), id);
        }
    }
}

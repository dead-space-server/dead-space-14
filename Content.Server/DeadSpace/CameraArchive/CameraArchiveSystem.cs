// Dead Space 14, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.IO;
using System.Numerics;
using Content.Server.GameTicking;
using Content.Server.Power.Components;
using Content.Server.SurveillanceCamera;
using Content.Server.NPC.HTN;
using Content.Shared.CCVar;
using Content.Server.DeadSpace.CentComm;
using Content.Server.Station.Systems;
using Content.Shared.DeadSpace.CameraArchives;
using Content.Shared.DeadSpace.SignBoard;
using Content.Shared.DeadSpace.UniformAccessories.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Clothing;
using Content.Shared.Chat;
using Content.Shared.Emag.Systems;
using Content.Shared.Emp;
using Content.Shared.Examine;
using Content.Shared.Fluids.Components;
using Content.Shared.Ghost;
using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.Labels.Components;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Components;
using Content.Shared.Projectiles;
using Content.Shared.Power.Components;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Content.Shared.Verbs;
using Content.Shared.Tools.Systems;
using Content.Shared.GameTicking;
using Content.Shared.SSDIndicator;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.SurveillanceCamera.Components;
using Content.Shared.TextScreen;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.DeadSpace.CameraArchives;

public sealed class CameraArchiveSystem : EntitySystem
{
    public readonly record struct CameraFrame(TimeSpan RoundTime, CameraArchive Still, bool Saved = false);

    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IRobustSerializer _serializer = default!;
    [Dependency] private readonly IResourceManager _resources = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SurveillanceCameraSystem _cameras = default!;
    [Dependency] private readonly SharedToolSystem _tools = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly EmagSystem _emag = default!;
    [Dependency] private readonly StationSystem _station = default!;

    private static readonly string Welding = "Welding";
    private const string AudioEntity = "Audio";

    private static readonly ResPath StoreDir = new("/camera-archive");
    private const SlotFlags WornSlots = SlotFlags.HEAD | SlotFlags.EYES | SlotFlags.EARS | SlotFlags.MASK
        | SlotFlags.OUTERCLOTHING | SlotFlags.INNERCLOTHING | SlotFlags.NECK | SlotFlags.GLOVES
        | SlotFlags.LEGS | SlotFlags.FEET | SlotFlags.UNDERWEART | SlotFlags.UNDERWEARB | SlotFlags.SOCKS;

    private readonly HashSet<EntityUid> _seen = new();
    private readonly List<EntityUid> _order = new();
    private readonly Dictionary<EntityUid, Queue<CameraFrame>> _history = new();
    private readonly Dictionary<EntityUid, (int Stamp, CameraArchive Still)> _reuse = new();
    private readonly Dictionary<EntityUid, BuiltView> _built = new();
    private readonly Dictionary<EntityUid, List<string>> _chat = new();
    private readonly Dictionary<EntityUid, CameraArchive> _latest = new();
    private readonly Queue<EntityUid> _soon = new();
    private readonly HashSet<EntityUid> _soonSet = new();
    private readonly Dictionary<EntityUid, TimeSpan> _turnDue = new();
    private readonly List<EntityUid> _turnReady = new();
    private readonly Dictionary<EntityUid, List<(EntityUid Camera, int Index)>> _spriteOf = new();
    private readonly Dictionary<EntityUid, HashSet<EntityUid>> _spriteCams = new();
    private readonly Dictionary<EntityUid, HashSet<EntityUid>> _spriteDirty = new();
    private readonly Dictionary<EntityUid, TimeSpan> _spriteDue = new();
    private readonly List<EntityUid> _spriteReady = new();
    private readonly Dictionary<EntityUid, GameTick> _spriteTick = new();
    private readonly List<EntityUid> _spriteGone = new();
    private float _spriteTimer;
    private static readonly TimeSpan SpriteGap = TimeSpan.FromSeconds(0.5);
    private const string BodyCameraTag = "BodyCamera";
    private const int ChatLineCap = 40;
    private const int SavedMinutes = 5;
    private readonly Dictionary<EntityUid, (GameTick Tick, byte[] Bytes)> _appearanceCache = new();
    private readonly Dictionary<EntityUid, (GameTick Tick, byte[]? Bytes)> _humanoidCache = new();
    private int _cursor;
    private bool _recording;

    public override void Initialize()
    {
        Wipe();
        SubscribeLocalEvent<RoundStartedEvent>(_ =>
        {
            Wipe();
            _recording = true;
        });
        SubscribeLocalEvent<RoundEndedEvent>(_ => Wipe());
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => Wipe());
        SubscribeLocalEvent<EntitySpokeEvent>(OnSpoke);
        SubscribeLocalEvent<SurveillanceCameraComponent, InteractUsingEvent>(OnLabel);
        SubscribeLocalEvent<SurveillanceCameraComponent, GetVerbsEvent<Verb>>(OnBodyCameraVerb);
        SubscribeLocalEvent<SurveillanceCameraComponent, InteractHandEvent>(OnPeelLabel);
        SubscribeLocalEvent<SurveillanceCameraComponent, GotEmaggedEvent>(OnEmagged);
        SubscribeLocalEvent<CameraArchiveBurnedComponent, InteractUsingEvent>(OnRepair);
        SubscribeLocalEvent<CameraArchiveSealedComponent, InteractUsingEvent>(OnBurnLabel);
        SubscribeLocalEvent<CameraArchiveBurnedComponent, SurveillanceCameraSetActiveAttemptEvent>(OnBurnedFeed);
        SubscribeLocalEvent<CameraArchiveSealedComponent, SurveillanceCameraSetActiveAttemptEvent>(OnSealedFeed);
        SubscribeLocalEvent<CameraConcealCloakComponent, ClothingGotEquippedEvent>(OnCloakEquipped);
        SubscribeLocalEvent<CameraConcealCloakComponent, ClothingGotUnequippedEvent>(OnCloakUnequipped);
        _xform.OnGlobalMoveEvent += OnSceneMove;
    }

    public override void Shutdown()
    {
        _xform.OnGlobalMoveEvent -= OnSceneMove;
        base.Shutdown();
    }

    private void Wipe()
    {
        _recording = false;
        _history.Clear();
        _reuse.Clear();
        _built.Clear();
        _chat.Clear();
        _latest.Clear();
        _soon.Clear();
        _soonSet.Clear();
        _turnDue.Clear();
        _turnReady.Clear();
        _spriteOf.Clear();
        _spriteCams.Clear();
        _spriteDirty.Clear();
        _spriteDue.Clear();
        _spriteReady.Clear();
        _spriteTick.Clear();
        _spriteGone.Clear();
        _spriteTimer = 0f;
        _appearanceCache.Clear();
        _humanoidCache.Clear();
        _order.Clear();
        _cursor = 0;
        if (_resources.UserData.Exists(StoreDir))
            _resources.UserData.Delete(StoreDir);
    }

    public override void Update(float frameTime)
    {
        if (!_recording)
            return;

        var now = _ticker.RoundDuration();
        PromoteTurns(now);
        var shot = FlushSoon(now);
        _spriteTimer += frameTime;
        if (_spriteTimer >= 1f)
        {
            _spriteTimer = 0f;
            PollSprites(now);
        }

        FlushSprites(now);
        if (!shot)
            BaselineOne(now);
    }

    public bool IsOnline(EntityUid camera)
    {
        if (!Exists(camera) || HasComp<EmpDisabledComponent>(camera))
            return false;

        return !TryComp<ApcPowerReceiverComponent>(camera, out var power) || power.Powered;
    }

    public bool ShouldRecord(EntityUid camera)
    {
        if (!Exists(camera))
            return false;

        if (OnCentComm(camera))
            return false;

        if (!_cfg.GetCVar(CCVars.CameraArchiveActive))
            return false;

        if (HasComp<CameraArchiveSealedComponent>(camera) || HasComp<CameraArchiveBurnedComponent>(camera))
            return false;

        if (!IsOnline(camera))
            return false;

        return !_tag.HasTag(camera, BodyCameraTag) || BodyCameraLive(camera);
    }

    public bool CountsAsPerson(EntityUid uid)
    {
        if (HasComp<CameraArchiveIgnoreComponent>(uid) || HasComp<GhostComponent>(uid))
            return false;

        return HasComp<HumanoidAppearanceComponent>(uid);
    }

    public bool Listed(EntityUid camera)
    {
        if (!HasComp<SurveillanceCameraComponent>(camera) || OnCentComm(camera))
            return false;

        if (!_tag.HasTag(camera, BodyCameraTag))
            return true;

        return BodyCameraLive(camera) || Count(camera) > 0;
    }

    public bool IsBodyCamera(EntityUid camera)
    {
        return _tag.HasTag(camera, BodyCameraTag);
    }

    private bool BodyCameraLive(EntityUid camera)
    {
        if (_cameras.GetConfiguredName(camera) == null)
            return false;

        return TryGetBodyCameraCarrier(camera, out _);
    }

    private bool OnCentComm(EntityUid uid)
    {
        if (!TryComp(uid, out TransformComponent? _))
            return false;

        return _station.GetOwningStation(uid) is { } station && HasComp<CentCommStationComponent>(station);
    }

    private bool TryGetBodyCameraCarrier(EntityUid camera, out EntityUid carrier)
    {
        carrier = default;
        var parent = Transform(camera).ParentUid;
        if (parent.IsValid() && _hands.IsHolding(parent, camera))
        {
            carrier = parent;
            return true;
        }

        if (_inventory.TryGetContainingSlot(camera, out _) && parent.IsValid())
        {
            carrier = parent;
            return true;
        }

        if (!_containers.TryGetContainingContainer(camera, out var container)
            || container.ID != UniformAccessoryHolderComponent.ContainerId
            || !HasComp<UniformAccessoryHolderComponent>(container.Owner)
            || !_inventory.TryGetContainingSlot(container.Owner, out _))
            return false;

        carrier = Transform(container.Owner).ParentUid;
        return carrier.IsValid();
    }

    public void Forget(EntityUid camera)
    {
        _history.Remove(camera);
        _chat.Remove(camera);
        _reuse.Remove(camera);
        _latest.Remove(camera);
        _turnDue.Remove(camera);
        _soonSet.Remove(camera);
        UnwatchSprites(camera);
    }

    private void OnEmagged(Entity<SurveillanceCameraComponent> ent, ref GotEmaggedEvent args)
    {
        if (HasComp<BorgChassisComponent>(ent))
            return;

        if (!_emag.CompareFlag(args.Type, EmagType.Interaction) || HasComp<CameraArchiveBurnedComponent>(ent))
            return;

        Forget(ent);
        EnsureComp<CameraArchiveBurnedComponent>(ent);
        _cameras.SetActive(ent, false, ent.Comp);
        var popup = _tag.HasTag(ent, BodyCameraTag)
            ? "camera-archive-body-burned-popup"
            : "camera-archive-burned-popup";
        _popup.PopupEntity(Loc.GetString(popup), ent, args.UserUid);

        args.Handled = true;
    }

    private void OnRepair(Entity<CameraArchiveBurnedComponent> ent, ref InteractUsingEvent args)
    {
        if (!_tools.HasQuality(args.Used, Welding))
            return;

        if (_tag.HasTag(ent.Owner, BodyCameraTag))
            return;

        RemComp<CameraArchiveBurnedComponent>(ent);
        RestoreFeed(ent.Owner);
        args.Handled = true;
    }

    private void OnBurnLabel(Entity<CameraArchiveSealedComponent> ent, ref InteractUsingEvent args)
    {
        if (!_tools.HasQuality(args.Used, Welding))
            return;

        RemComp<CameraArchiveSealedComponent>(ent);
        RestoreFeed(ent.Owner);
        _popup.PopupEntity(Loc.GetString("camera-archive-label-burned-popup"), ent, args.User);
        args.Handled = true;
    }

    private void OnBurnedFeed(Entity<CameraArchiveBurnedComponent> ent, ref SurveillanceCameraSetActiveAttemptEvent args)
    {
        args.Cancelled = true;
    }

    private void OnSealedFeed(Entity<CameraArchiveSealedComponent> ent, ref SurveillanceCameraSetActiveAttemptEvent args)
    {
        args.Cancelled = true;
    }

    private void RestoreFeed(EntityUid camera)
    {
        if (!TryComp<SurveillanceCameraComponent>(camera, out var cam))
            return;

        if (HasComp<CameraArchiveSealedComponent>(camera) || HasComp<CameraArchiveBurnedComponent>(camera))
            return;

        if (TryComp<ApcPowerReceiverComponent>(camera, out var power) && !power.Powered)
            return;

        _cameras.SetActive(camera, true, cam);
    }

    private void OnCloakEquipped(Entity<CameraConcealCloakComponent> ent, ref ClothingGotEquippedEvent args)
    {
        EnsureComp<CameraConcealedComponent>(args.Wearer);
    }

    private void OnCloakUnequipped(Entity<CameraConcealCloakComponent> ent, ref ClothingGotUnequippedEvent args)
    {
        RemComp<CameraConcealedComponent>(args.Wearer);
    }

    private bool WearsCloak(EntityUid wearer)
    {
        var slots = _inventory.GetSlotEnumerator(wearer, WornSlots);
        while (slots.MoveNext(out var container))
        {
            if (container.ContainedEntity is { } item && HasComp<CameraConcealCloakComponent>(item))
                return true;
        }

        return false;
    }

    public bool Sees(EntityUid camera, EntityUid person)
    {
        return SeesPoint(camera, _xform.GetMapCoordinates(person), person);
    }

    private bool SeesPoint(EntityUid camera, MapCoordinates target, EntityUid ignore)
    {
        var map = _xform.GetMapCoordinates(camera);
        if (map.MapId != target.MapId || map.MapId == MapId.Nullspace)
            return false;

        var eye = map.Position + _xform.GetWorldRotation(camera).RotateVec(new Vector2(CameraArchive.EyeLead, 0f));
        return _examine.InRangeUnOccluded(
            new MapCoordinates(eye, map.MapId),
            target,
            CameraArchive.ViewRadius,
            uid => uid == ignore);
    }

    private void OnSceneMove(ref MoveEvent ev)
    {
        if (!_recording)
            return;

        var uid = ev.Sender;
        if (MetaData(uid).EntityLifeStage >= EntityLifeStage.Terminating)
            return;

        if (IsAmbientMob(uid))
            return;

        var person = CountsAsPerson(uid);
        if (!person && !IsTrackedItem(uid))
            return;

        var place = ev.ParentChanged;
        var oldTile = TryTile(ev.OldPosition, out var oldIndices);
        var newTile = TryTile(ev.NewPosition, out var newIndices);
        if (oldTile && newTile && oldIndices != newIndices)
            place = true;
        else if (oldTile != newTile)
            place = true;

        var turned = SteadyBucket(ev.OldRotation) != SteadyBucket(ev.NewRotation);
        if (!place && !turned)
            return;

        MarkVisible(uid, ev.OldPosition, ev.NewPosition, place);
        if (person)
            MarkBodyCamera(uid, place);
    }

    private static int SteadyBucket(Angle angle)
    {
        return (int) Math.Round(Steady(angle).Degrees);
    }

    private bool IsTrackedItem(EntityUid uid)
    {
        if (!HasComp<ItemComponent>(uid)
            || HasComp<MobStateComponent>(uid)
            || HasComp<ProjectileComponent>(uid))
            return false;

        var parent = Transform(uid).ParentUid;
        return !parent.IsValid() || !HasComp<MobStateComponent>(parent);
    }

    private bool TryTile(EntityCoordinates coords, out Vector2i tile)
    {
        tile = default;
        if (!coords.IsValid(EntityManager) || !TryComp(coords.EntityId, out TransformComponent? parent))
            return false;

        var grid = parent.GridUid;
        if (grid == null && HasComp<MapGridComponent>(coords.EntityId))
            grid = coords.EntityId;
        if (grid is not { } gridUid || !TryComp(gridUid, out MapGridComponent? gridComp))
            return false;

        tile = _map.WorldToTile(gridUid, gridComp, _xform.ToMapCoordinates(coords).Position);
        return true;
    }

    private void MarkVisible(EntityUid subject, EntityCoordinates oldCoords, EntityCoordinates newCoords, bool place)
    {
        var oldOk = oldCoords.IsValid(EntityManager);
        var newOk = newCoords.IsValid(EntityManager);
        if (!oldOk && !newOk)
            return;

        var oldMap = oldOk ? _xform.ToMapCoordinates(oldCoords) : MapCoordinates.Nullspace;
        var newMap = newOk ? _xform.ToMapCoordinates(newCoords) : MapCoordinates.Nullspace;
        var rangeSq = CameraArchive.ViewRadius * CameraArchive.ViewRadius;
        var cameras = EntityQueryEnumerator<SurveillanceCameraComponent, TransformComponent>();
        while (cameras.MoveNext(out var uid, out _, out _))
        {
            if (_tag.HasTag(uid, BodyCameraTag) || !ShouldRecord(uid))
                continue;

            var cam = _xform.GetMapCoordinates(uid);
            var nearNew = cam.MapId == newMap.MapId && (cam.Position - newMap.Position).LengthSquared() <= rangeSq;
            var nearOld = cam.MapId == oldMap.MapId && (cam.Position - oldMap.Position).LengthSquared() <= rangeSq;
            if (nearNew && !SeesPoint(uid, newMap, subject))
                nearNew = false;
            if (nearOld && !SeesPoint(uid, oldMap, subject))
                nearOld = false;
            if (!nearNew && !nearOld)
                continue;

            Enqueue(uid, place);
        }
    }

    private void MarkBodyCamera(EntityUid person, bool place)
    {
        var slots = _inventory.GetSlotEnumerator(person, SlotFlags.All);
        while (slots.MoveNext(out var container))
        {
            if (container.ContainedEntity is not { } worn)
                continue;

            if (_tag.HasTag(worn, BodyCameraTag))
                Enqueue(worn, place);

            if (!TryComp<UniformAccessoryHolderComponent>(worn, out var holder) || holder.AccessoryContainer == null)
                continue;

            foreach (var accessory in holder.AccessoryContainer.ContainedEntities)
            {
                if (_tag.HasTag(accessory, BodyCameraTag))
                    Enqueue(accessory, place);
            }
        }

        foreach (var held in _hands.EnumerateHeld(person))
        {
            if (_tag.HasTag(held, BodyCameraTag))
                Enqueue(held, place);
        }
    }

    private void Enqueue(EntityUid camera, bool place)
    {
        if (place)
        {
            _turnDue.Remove(camera);
            if (_soonSet.Add(camera))
                _soon.Enqueue(camera);
            return;
        }

        if (_soonSet.Contains(camera))
            return;

        var settle = TimeSpan.FromSeconds(Math.Max(0.2f, _cfg.GetCVar(CCVars.CameraArchiveFocusSeconds)));
        _turnDue[camera] = _ticker.RoundDuration() + settle;
    }

    private void PollSprites(TimeSpan now)
    {
        if (_spriteOf.Count == 0)
            return;

        _spriteGone.Clear();
        foreach (var (uid, spots) in _spriteOf)
        {
            if (!Exists(uid))
            {
                _spriteGone.Add(uid);
                continue;
            }

            if (HasComp<MobStateComponent>(uid) && !HasComp<HumanoidAppearanceComponent>(uid))
                continue;

            if (IsAmbientMob(uid))
                continue;

            if (!TryComp<AppearanceComponent>(uid, out var appearance))
                continue;

            var tick = appearance.LastModifiedTick;
            if (_spriteTick.TryGetValue(uid, out var seen) && seen == tick)
                continue;

            _spriteTick[uid] = tick;
            _appearanceCache.Remove(uid);
            foreach (var (camera, _) in spots)
            {
                if (!_spriteDirty.TryGetValue(camera, out var dirty))
                {
                    dirty = new HashSet<EntityUid>();
                    _spriteDirty[camera] = dirty;
                    _spriteDue[camera] = now + SpriteGap;
                }

                dirty.Add(uid);
            }
        }

        foreach (var uid in _spriteGone)
        {
            _spriteTick.Remove(uid);
            _spriteOf.Remove(uid);
        }
    }

    private void FlushSprites(TimeSpan now)
    {
        if (_spriteDue.Count == 0)
            return;

        _spriteReady.Clear();
        foreach (var (camera, due) in _spriteDue)
        {
            if (due <= now)
                _spriteReady.Add(camera);
        }

        foreach (var camera in _spriteReady)
        {
            _spriteDue.Remove(camera);
            PatchSprites(camera, now);
        }
    }

    private void PatchSprites(EntityUid camera, TimeSpan now)
    {
        if (!_spriteDirty.Remove(camera, out var dirty) || dirty.Count == 0)
            return;

        if (!_built.TryGetValue(camera, out var cache) || !ShouldRecord(camera))
            return;

        var entities = new List<CameraArchiveEntity>(cache.Still.Entities);
        var changed = false;
        foreach (var uid in dirty)
        {
            if (!_spriteOf.TryGetValue(uid, out var spots))
                continue;

            var index = -1;
            foreach (var spot in spots)
            {
                if (spot.Camera != camera)
                    continue;

                index = spot.Index;
                break;
            }

            if (index < 0 || index >= entities.Count || !Exists(uid))
                continue;

            var bytes = SerializeAppearance(uid);
            var previous = entities[index];
            if (SameBytes(previous.Appearance, bytes))
                continue;

            var copy = CloneEntity(previous);
            copy.Appearance = bytes;
            if (TryComp(uid, out TransformComponent? xform))
            {
                var hold = DescribeHold(uid, xform);
                copy.SignText = hold.SignText;
                copy.ScreenText = hold.ScreenText;
            }

            entities[index] = copy;
            changed = true;
        }

        if (!changed)
            return;

        var still = new CameraArchive
        {
            GridRotation = cache.Still.GridRotation,
            CameraFacing = cache.Still.CameraFacing,
            CameraLocal = cache.Still.CameraLocal,
            Zoom = cache.Still.Zoom,
            DrawFov = cache.Still.DrawFov,
            DrawLight = cache.Still.DrawLight,
            Tiles = cache.Still.Tiles,
            Decals = cache.Still.Decals,
            Entities = entities,
            Chat = new List<string>(),
            Interference = cache.Still.Interference,
        };
        cache.Still = still;
        Remember(camera, still, now);
    }

    private void WatchSprites(EntityUid camera, Dictionary<EntityUid, int> placed)
    {
        UnwatchSprites(camera);
        var uids = new HashSet<EntityUid>();
        foreach (var (uid, index) in placed)
        {
            if (!_spriteOf.TryGetValue(uid, out var spots))
            {
                spots = new List<(EntityUid Camera, int Index)>();
                _spriteOf[uid] = spots;
            }

            spots.Add((camera, index));
            uids.Add(uid);
            if (!_spriteTick.ContainsKey(uid) && TryComp<AppearanceComponent>(uid, out var appearance))
                _spriteTick[uid] = appearance.LastModifiedTick;
        }

        _spriteCams[camera] = uids;
    }

    private void UnwatchSprites(EntityUid camera)
    {
        _spriteDirty.Remove(camera);
        _spriteDue.Remove(camera);
        if (!_spriteCams.Remove(camera, out var uids))
            return;

        foreach (var uid in uids)
        {
            if (!_spriteOf.TryGetValue(uid, out var spots))
                continue;

            spots.RemoveAll(spot => spot.Camera == camera);
            if (spots.Count == 0)
                _spriteOf.Remove(uid);
        }
    }

    private static bool SameBytes(byte[]? left, byte[]? right)
    {
        if (left == null || left.Length == 0)
            return right == null || right.Length == 0;

        return right != null && left.AsSpan().SequenceEqual(right);
    }

    private static CameraArchiveEntity CloneEntity(CameraArchiveEntity src)
    {
        return new CameraArchiveEntity
        {
            Prototype = src.Prototype,
            LocalPosition = src.LocalPosition,
            LocalRotation = src.LocalRotation,
            Anchored = src.Anchored,
            Parent = src.Parent,
            Container = src.Container,
            ShowContents = src.ShowContents,
            Loose = src.Loose,
            InHand = src.InHand,
            HandSide = src.HandSide,
            CopySsd = src.CopySsd,
            IsSsd = src.IsSsd,
            SignText = src.SignText,
            ScreenText = src.ScreenText,
            Humanoid = src.Humanoid,
            Conceal = src.Conceal,
            Appearance = src.Appearance,
        };
    }

    private void PromoteTurns(TimeSpan now)
    {
        if (_turnDue.Count == 0)
            return;

        _turnReady.Clear();
        foreach (var (camera, due) in _turnDue)
        {
            if (due <= now)
                _turnReady.Add(camera);
        }

        foreach (var camera in _turnReady)
        {
            _turnDue.Remove(camera);
            if (_soonSet.Add(camera))
                _soon.Enqueue(camera);
        }
    }

    private bool FlushSoon(TimeSpan now)
    {
        if (_soon.Count == 0)
            return false;

        var budget = TimeSpan.FromSeconds(Math.Max(0.01f, _cfg.GetCVar(CCVars.CameraArchiveTickBudgetSeconds)));
        var heavy = TickIsHeavy(budget);
        var watch = new Stopwatch();
        watch.Restart();
        var shot = false;
        var guard = _soon.Count;
        while (guard-- > 0 && _soon.Count > 0 && watch.Elapsed.TotalSeconds < 0.004)
        {
            var camera = _soon.Dequeue();
            _soonSet.Remove(camera);
            if (!ShouldRecord(camera))
                continue;

            if (!Shoot(camera, now, allowRebuild: !heavy))
            {
                if (_soonSet.Add(camera))
                    _soon.Enqueue(camera);
                return true;
            }

            shot = true;
            heavy = TickIsHeavy(budget);
        }

        return shot;
    }

    private void BaselineOne(TimeSpan now)
    {
        if (!_cfg.GetCVar(CCVars.CameraArchiveIdleEnabled))
            return;

        var budget = TimeSpan.FromSeconds(Math.Max(0.01f, _cfg.GetCVar(CCVars.CameraArchiveTickBudgetSeconds)));
        if (TickIsHeavy(budget))
            return;

        if (_order.Count == 0 || _cursor >= _order.Count)
        {
            RebuildOrder();
            _cursor = 0;
            if (_order.Count == 0)
                return;
        }

        var camera = _order[_cursor++];
        if (_history.ContainsKey(camera) || _soonSet.Contains(camera) || !ShouldRecord(camera))
            return;

        Shoot(camera, now, allowRebuild: true);
    }

    private bool Shoot(EntityUid camera, TimeSpan now, bool allowRebuild)
    {
        if (TryLoose(camera, now, allowRebuild))
            return true;

        if (!allowRebuild)
            return false;

        if (Capture(camera) is { } still)
            Remember(camera, still, now);

        return true;
    }

    private bool TickIsHeavy(TimeSpan budget)
    {
        var elapsed = _timing.RealTime - _timing.LastTick;
        if (elapsed < TimeSpan.Zero || elapsed > TimeSpan.FromSeconds(1))
            return false;

        return elapsed >= budget;
    }

    private void OnLabel(Entity<SurveillanceCameraComponent> ent, ref InteractUsingEvent args)
    {
        if (!HasComp<HandLabelerComponent>(args.Used) || HasComp<BorgChassisComponent>(ent))
            return;

        EnsureComp<CameraArchiveSealedComponent>(ent);
        _cameras.SetActive(ent, false, ent.Comp);
        _popup.PopupEntity(Loc.GetString("camera-archive-sealed-popup"), ent, args.User);
        args.Handled = true;
    }

    private void OnPeelLabel(Entity<SurveillanceCameraComponent> ent, ref InteractHandEvent args)
    {
        if (!HasComp<CameraArchiveSealedComponent>(ent))
            return;

        RemComp<CameraArchiveSealedComponent>(ent);
        RestoreFeed(ent);
        _popup.PopupEntity(Loc.GetString("camera-archive-peeled-popup"), ent, args.User);
        args.Handled = true;
    }

    private void OnSpoke(EntitySpokeEvent args)
    {
        if (args.IsRadioSpeech || args.Channel != null)
            return;

        var origin = _xform.GetWorldPosition(args.Source);
        var query = EntityQueryEnumerator<SurveillanceCameraComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (!_tag.HasTag(uid, BodyCameraTag) || !BodyCameraLive(uid))
                continue;

            var wornBySpeaker = TryGetBodyCameraCarrier(uid, out var carrier) && carrier == args.Source;
            var near = (_xform.GetWorldPosition(uid) - origin).LengthSquared() <= CameraArchive.ViewRadius * CameraArchive.ViewRadius;
            if (!wornBySpeaker && !near)
                continue;

            var spoken = wornBySpeaker || args.ObfuscatedMessage == null ? args.Message : args.ObfuscatedMessage;
            var line = $"{Plain(Name(args.Source))}: {Plain(spoken)}";

            if (_latest.TryGetValue(uid, out var still))
            {
                if (still.Chat.Count == ChatLineCap)
                    still.Chat.RemoveAt(0);
                still.Chat.Add(line);
                continue;
            }

            if (!_chat.TryGetValue(uid, out var lines))
            {
                lines = new List<string>();
                _chat[uid] = lines;
            }

            if (lines.Count == ChatLineCap)
                lines.RemoveAt(0);
            lines.Add(line);
        }
    }

    public TimeSpan HistoryWindow => TimeSpan.FromMinutes(_cfg.GetCVar(CCVars.CameraArchiveHistoryMinutes));

    public int Count(EntityUid camera)
    {
        return _history.TryGetValue(camera, out var ring) ? ring.Count : 0;
    }

    public bool TryGetFrame(EntityUid camera, int index, out CameraFrame frame)
    {
        frame = default;
        if (!_history.TryGetValue(camera, out var ring) || ring.Count == 0)
            return false;

        if (index < 0)
            index = ring.Count - 1;
        if (index >= ring.Count)
            index = ring.Count - 1;

        var i = 0;
        foreach (var item in ring)
        {
            if (i == index)
            {
                frame = item;
                return true;
            }

            i++;
        }

        return false;
    }

    public void Remember(EntityUid camera, CameraArchive still, TimeSpan time)
    {
        if (HasComp<CameraArchiveSealedComponent>(camera) || HasComp<CameraArchiveBurnedComponent>(camera))
            return;

        if (_chat.TryGetValue(camera, out var lines) && lines.Count > 0)
        {
            still = new CameraArchive
            {
                GridRotation = still.GridRotation,
                CameraLocal = still.CameraLocal,
                Zoom = still.Zoom,
                DrawFov = still.DrawFov,
                DrawLight = still.DrawLight,
                Tiles = still.Tiles,
                Entities = still.Entities,
                Decals = still.Decals,
                Chat = new List<string>(lines),
                Interference = still.Interference,
                CameraFacing = still.CameraFacing,
            };
            lines.Clear();
        }

        if (!_history.TryGetValue(camera, out var ring))
        {
            ring = new Queue<CameraFrame>();
            _history.Add(camera, ring);
        }

        ring.Enqueue(new CameraFrame(time, still));
        _latest[camera] = still;
        TrimOlderThan(ring, time, HistoryWindow);
    }

    public int CaptureAll(TimeSpan now)
    {
        RebuildOrder();
        var captured = 0;
        foreach (var camera in _order)
        {
            if (!ShouldRecord(camera))
                continue;

            if (Capture(camera) is not { } still)
                continue;

            Remember(camera, still, now);
            captured++;
        }

        return captured;
    }

    public static void TrimOlderThan(Queue<CameraFrame> frames, TimeSpan now, TimeSpan window)
    {
        if (frames.Count == 0)
            return;

        var keep = new List<CameraFrame>(frames.Count);
        foreach (var frame in frames)
        {
            if (frame.Saved || now - frame.RoundTime <= window)
                keep.Add(frame);
        }

        if (keep.Count == frames.Count)
            return;

        frames.Clear();
        foreach (var frame in keep)
            frames.Enqueue(frame);
    }

    private void OnBodyCameraVerb(Entity<SurveillanceCameraComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !_tag.HasTag(ent, BodyCameraTag))
            return;

        var user = args.User;
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("camera-archive-save-moment"),
            Act = () => SaveMoment(ent, user),
            Priority = 2,
        });
    }

    private void SaveMoment(EntityUid camera, EntityUid user)
    {
        if (!_history.TryGetValue(camera, out var ring) || ring.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("camera-archive-save-empty"), camera, user);
            return;
        }

        var frames = new List<CameraFrame>(ring);
        var newest = frames[^1].RoundTime;
        var window = TimeSpan.FromMinutes(SavedMinutes);
        var next = new Queue<CameraFrame>();
        var saved = 0;
        foreach (var frame in frames)
        {
            var keep = newest - frame.RoundTime <= window;
            if (keep)
                saved++;
            next.Enqueue(frame with { Saved = keep });
        }

        _history[camera] = next;
        _popup.PopupEntity(Loc.GetString("camera-archive-saved-popup", ("minutes", SavedMinutes), ("count", saved)), camera, user);
    }

    public CameraArchive? Latest(EntityUid camera)
    {
        return TryGetFrame(camera, -1, out var frame) ? frame.Still : Capture(camera);
    }

    private void RebuildOrder()
    {
        DropGone();
        _order.Clear();
        var query = EntityQueryEnumerator<SurveillanceCameraComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.GridUid == null)
                continue;

            _order.Add(uid);
        }
    }

    private void DropGone()
    {
        List<EntityUid>? gone = null;
        foreach (var uid in _history.Keys)
        {
            if (Exists(uid) && HasComp<SurveillanceCameraComponent>(uid))
                continue;

            gone ??= new List<EntityUid>();
            gone.Add(uid);
        }

        if (gone == null)
            return;

        foreach (var uid in gone)
        {
            _history.Remove(uid);
            _reuse.Remove(uid);
            _appearanceCache.Remove(uid);
            _humanoidCache.Remove(uid);
        }
    }

    public CameraArchive? Capture(EntityUid camera)
    {
        if (!TryComp(camera, out TransformComponent? cameraXform) || cameraXform.GridUid is not { } grid)
            return null;

        if (!TryComp<MapGridComponent>(grid, out var gridComp))
            return null;

        var still = new CameraArchive
        {
            GridRotation = _xform.GetWorldRotation(grid),
            CameraFacing = _xform.GetWorldRotation(camera) - _xform.GetWorldRotation(grid),
            CameraLocal = ToGridLocal(camera, cameraXform, grid, gridComp).Position,
        };

        if (TryComp<EyeComponent>(camera, out var eye))
        {
            still.Zoom = eye.Zoom;
            still.DrawFov = eye.DrawFov;
            still.DrawLight = eye.DrawLight;
        }

        var radius = CameraArchive.ViewRadius;
        var center = still.CameraLocal;
        var box = new Box2(center - new Vector2(radius, radius), center + new Vector2(radius, radius));

        foreach (var tile in _map.GetLocalTilesIntersecting(grid, gridComp, box))
        {
            still.Tiles.Add(new CameraArchiveTile
            {
                Indices = tile.GridIndices,
                TypeId = tile.Tile.TypeId,
                Flags = tile.Tile.Flags,
                Variant = tile.Tile.Variant,
                RotationMirroring = tile.Tile.RotationMirroring,
            });
        }

        _seen.Clear();

        var range = radius * MathF.Sqrt(2f) + 1f;
        var found = new List<(EntityUid Uid, TransformComponent Xform, string Prototype)>();
        if (TryComp(camera, out MetaDataComponent? cameraMeta) && cameraMeta.EntityPrototype is { } cameraProto)
        {
            _seen.Add(camera);
            found.Add((camera, cameraXform, cameraProto.ID));
        }

        foreach (var uid in _lookup.GetEntitiesInRange(camera, range))
        {
            if (!TryComp(uid, out TransformComponent? xform) || xform.GridUid != grid)
                continue;

            if (HasComp<MapGridComponent>(uid) || HasComp<MapComponent>(uid))
                continue;

            if (!TryComp(uid, out MetaDataComponent? meta) || meta.EntityPrototype is not { } proto)
                continue;

            if (proto.ID == AudioEntity || HiddenFromArchive(uid))
                continue;

            var local = ToGridLocal(uid, xform, grid, gridComp);
            var delta = local.Position - center;
            if (MathF.Abs(delta.X) > radius || MathF.Abs(delta.Y) > radius)
                continue;

            if (!_seen.Add(uid))
                continue;

            found.Add((uid, xform, proto.ID));
        }

        var placed = new Dictionary<EntityUid, int>();
        Append(found, still, placed, grid, gridComp, structuralPass: true);
        Append(found, still, placed, grid, gridComp, structuralPass: false);

        var structureCount = 0;
        while (structureCount < still.Entities.Count && !still.Entities[structureCount].Loose)
            structureCount++;

        var index = new Dictionary<EntityUid, int>();
        foreach (var (uid, entityIndex) in placed)
        {
            if (entityIndex < structureCount)
                index.Add(uid, entityIndex);
        }

        _built[camera] = new BuiltView
        {
            Anchored = AnchoredStamp(camera),
            Loose = LooseStamp(camera),
            StructureCount = structureCount,
            Index = index,
            Still = still,
        };
        _reuse[camera] = (WorldStamp(camera), still);
        WatchSprites(camera, placed);
        _spriteDirty.Remove(camera);
        _spriteDue.Remove(camera);
        return still;
    }

    private bool TryLoose(EntityUid camera, TimeSpan now, bool allowRebuild)
    {
        if (!_built.TryGetValue(camera, out var cache))
            return false;

        if (AnchoredStamp(camera) != cache.Anchored)
            return false;

        var loose = LooseStamp(camera);
        if (loose == cache.Loose)
            return true;

        if (!allowRebuild)
            return false;

        if (RebuildLoose(camera, cache) is not { } still)
            return false;

        cache.Still = still;
        cache.Loose = loose;
        Remember(camera, still, now);
        return true;
    }

    private CameraArchive? RebuildLoose(EntityUid camera, BuiltView cache)
    {
        if (!TryComp(camera, out TransformComponent? cameraXform) || cameraXform.GridUid is not { } grid)
            return null;

        if (!TryComp<MapGridComponent>(grid, out var gridComp))
            return null;

        var still = new CameraArchive
        {
            GridRotation = _xform.GetWorldRotation(grid),
            CameraFacing = cache.Still.CameraFacing,
            CameraLocal = ToGridLocal(camera, cameraXform, grid, gridComp).Position,
            Zoom = cache.Still.Zoom,
            DrawFov = cache.Still.DrawFov,
            DrawLight = cache.Still.DrawLight,
            Tiles = cache.Still.Tiles,
            Decals = cache.Still.Decals,
            Entities = cache.Still.Entities.GetRange(0, cache.StructureCount),
        };

        var center = still.CameraLocal;
        var radius = CameraArchive.ViewRadius;
        var range = radius * MathF.Sqrt(2f) + 1f;
        var found = new List<(EntityUid Uid, TransformComponent Xform, string Prototype)>();
        foreach (var uid in _lookup.GetEntitiesInRange(camera, range))
        {
            if (cache.Index.ContainsKey(uid))
                continue;

            if (!TryComp(uid, out TransformComponent? xform) || xform.GridUid != grid)
                continue;

            if (IsStructural(uid, xform))
                continue;

            if (HasComp<MapGridComponent>(uid) || HasComp<MapComponent>(uid))
                continue;

            if (!TryComp(uid, out MetaDataComponent? meta) || meta.EntityPrototype is not { } proto)
                continue;

            if (proto.ID == AudioEntity || HiddenFromArchive(uid))
                continue;

            var local = ToGridLocal(uid, xform, grid, gridComp);
            if (MathF.Abs(local.Position.X - center.X) > radius || MathF.Abs(local.Position.Y - center.Y) > radius)
                continue;

            found.Add((uid, xform, proto.ID));
        }

        var placed = new Dictionary<EntityUid, int>(cache.Index);
        Append(found, still, placed, grid, gridComp, structuralPass: false);
        WatchSprites(camera, placed);
        return still;
    }

    private void Append(
        List<(EntityUid Uid, TransformComponent Xform, string Prototype)> found,
        CameraArchive still,
        Dictionary<EntityUid, int> placed,
        EntityUid grid,
        MapGridComponent gridComp,
        bool structuralPass)
    {
        var guard = found.Count;
        while (found.Count > 0 && guard-- >= 0)
        {
            var added = false;
            for (var i = found.Count - 1; i >= 0; i--)
            {
                var item = found[i];
                var structural = IsStructural(item.Uid, item.Xform);
                if (structuralPass != structural)
                    continue;

                var parent = item.Xform.ParentUid;
                if (parent != grid && !placed.ContainsKey(parent))
                    continue;

                placed.Add(item.Uid, still.Entities.Count);
                var local = ToGridLocal(item.Uid, item.Xform, grid, gridComp);
                if (parent != grid)
                    local = (item.Xform.LocalPosition, item.Xform.LocalRotation);

                var hold = DescribeHold(item.Uid, item.Xform);
                var inHand = false;
                byte handSide = 0;
                if (hold.Container.Length > 0
                    && parent != grid
                    && _hands.TryGetHand(parent, hold.Container, out var hand))
                {
                    inHand = true;
                    handSide = (byte) hand.Value.Location;
                }

                var conceal = WearsCloak(item.Uid);
                still.Entities.Add(new CameraArchiveEntity
                {
                    Prototype = item.Prototype,
                    LocalPosition = local.Position,
                    LocalRotation = local.Rotation,
                    Anchored = parent == grid && item.Xform.Anchored && hold.Container.Length == 0,
                    Parent = parent == grid ? -1 : placed[parent],
                    Container = hold.Container,
                    ShowContents = hold.ShowContents,
                    Loose = !structural,
                    InHand = inHand,
                    HandSide = handSide,
                    Appearance = conceal ? null : SerializeAppearance(item.Uid),
                    CopySsd = hold.CopySsd,
                    IsSsd = hold.IsSsd,
                    SignText = hold.SignText,
                    ScreenText = hold.ScreenText,
                    Humanoid = conceal ? null : CopyHumanoid(item.Uid),
                    Conceal = conceal,
                });
                found.RemoveAt(i);
                added = true;
            }

            if (!added)
                break;
        }
    }

    private bool HiddenFromArchive(EntityUid uid)
    {
        return HasComp<GhostComponent>(uid) || HasComp<CameraArchiveIgnoreComponent>(uid);
    }

    private bool IgnoredMover(EntityUid uid)
    {
        return IsAmbientMob(uid);
    }

    private bool IsAmbientMob(EntityUid uid)
    {
        if (HasComp<HumanoidAppearanceComponent>(uid))
            return false;

        if (HasComp<HTNComponent>(uid) || HasComp<MobStateComponent>(uid) || HasComp<PuddleComponent>(uid))
            return true;

        var parent = Transform(uid).ParentUid;
        if (!parent.IsValid() || HasComp<HumanoidAppearanceComponent>(parent))
            return false;

        return HasComp<HTNComponent>(parent) || HasComp<MobStateComponent>(parent);
    }

    private bool IsStructural(EntityUid uid, TransformComponent xform)
    {
        var current = xform;
        for (var i = 0; i < 8; i++)
        {
            if (current.Anchored)
                return true;

            var parent = current.ParentUid;
            if (!parent.IsValid() || parent == current.GridUid)
                return false;

            if (!TryComp(parent, out current))
                return false;
        }

        return false;
    }

    private static Angle Steady(Angle angle)
    {
        var deg = angle.Degrees % 360d;
        if (deg < 0)
            deg += 360d;

        var nearest = Math.Round(deg / 90d) * 90d;
        var delta = Math.Abs(deg - nearest);
        if (delta > 180d)
            delta = 360d - delta;

        return delta <= 8d ? Angle.FromDegrees((float) nearest) : angle;
    }

    private int AnchoredStamp(EntityUid camera) => Stamp(camera, anchored: true);

    private int LooseStamp(EntityUid camera) => Stamp(camera, anchored: false);

    private int Stamp(EntityUid camera, bool anchored)
    {
        if (!TryComp(camera, out TransformComponent? cameraXform) || cameraXform.GridUid is not { } grid)
            return 0;

        var hash = new HashCode();
        var range = CameraArchive.ViewRadius * MathF.Sqrt(2f) + 1f;
        foreach (var uid in _lookup.GetEntitiesInRange(camera, range))
        {
            if (!TryComp(uid, out TransformComponent? xform) || xform.GridUid != grid)
                continue;

            if (IgnoredMover(uid) || HiddenFromArchive(uid))
                continue;

            if (IsStructural(uid, xform) != anchored)
                continue;

            if (!TryComp(uid, out MetaDataComponent? meta))
                continue;

            hash.Add(meta.EntityPrototype?.ID);
            hash.Add((int) MathF.Round(xform.LocalPosition.X * 4f));
            hash.Add((int) MathF.Round(xform.LocalPosition.Y * 4f));
            hash.Add((int) Math.Round(Steady(xform.LocalRotation).Degrees));
        }

        return hash.ToHashCode();
    }

    private sealed class BuiltView
    {
        public int Anchored;
        public int Loose;
        public int StructureCount;
        public Dictionary<EntityUid, int> Index = new();
        public CameraArchive Still = new();
    }

    public bool TryReuse(EntityUid camera, TimeSpan now)
    {
        if (!_reuse.TryGetValue(camera, out var cached))
            return false;

        if (WorldStamp(camera) != cached.Stamp)
            return false;

        Remember(camera, cached.Still, now);
        return true;
    }

    private int WorldStamp(EntityUid camera)
    {
        if (!TryComp(camera, out TransformComponent? cameraXform) || cameraXform.GridUid is not { } grid)
            return 0;

        var hash = new HashCode();
        hash.Add(GetNetEntity(camera));
        var range = CameraArchive.ViewRadius * MathF.Sqrt(2f) + 1f;
        foreach (var uid in _lookup.GetEntitiesInRange(camera, range))
        {
            if (!TryComp(uid, out TransformComponent? xform) || xform.GridUid != grid)
                continue;

            if (!TryComp(uid, out MetaDataComponent? meta))
                continue;

            hash.Add(meta.EntityPrototype?.ID);
            hash.Add((int) MathF.Round(xform.LocalPosition.X * 4f));
            hash.Add((int) MathF.Round(xform.LocalPosition.Y * 4f));
            hash.Add(xform.Anchored);
        }

        return hash.ToHashCode();
    }

    private (string Container, bool ShowContents, bool CopySsd, bool IsSsd, string SignText, string ScreenText) DescribeHold(EntityUid uid, TransformComponent xform)
    {
        var containerId = string.Empty;
        var show = false;
        if (_containers.TryGetContainingContainer((uid, xform, (MetaDataComponent?) null), out var container))
        {
            containerId = container.ID;
            show = container.ShowContents;
        }

        var copySsd = false;
        var isSsd = false;
        if (TryComp<SSDIndicatorComponent>(uid, out var ssd))
        {
            copySsd = true;
            isSsd = ssd.IsSSD;
        }

        var sign = TryComp<SignBoardComponent>(uid, out var signComp) ? signComp.Text : string.Empty;
        var screen = string.Empty;
        if (HasComp<AppearanceComponent>(uid)
            && _appearance.TryGetData(uid, TextScreenVisuals.ScreenText, out string? screenText)
            && screenText != null)
            screen = screenText;
        return (containerId, show, copySsd, isSsd, sign, screen);
    }

    private byte[]? CopyHumanoid(EntityUid uid)
    {
        if (!TryComp<HumanoidAppearanceComponent>(uid, out var humanoid))
            return null;

        return SerializeHumanoid(uid, humanoid);
    }

    private static string Plain(string text)
    {
        return text.Replace('[', '(').Replace(']', ')').Replace('\n', ' ');
    }

    private byte[]? SerializeHumanoid(EntityUid uid, HumanoidAppearanceComponent humanoid)
    {
        if (!humanoid.NetSyncEnabled || !TryComp(uid, out MetaDataComponent? meta))
            return null;

        if (_humanoidCache.TryGetValue(uid, out var cached) && cached.Tick == meta.EntityLastModifiedTick)
            return cached.Bytes;

        if (EntityManager.GetComponentState(EntityManager.EventBus, humanoid, null, GameTick.Zero) is not HumanoidAppearanceComponent.HumanoidAppearanceComponent_AutoState state)
            return null;

        using var stream = new MemoryStream();
        _serializer.SerializeDirect(stream, state);
        var bytes = stream.ToArray();
        _humanoidCache[uid] = (meta.EntityLastModifiedTick, bytes);
        return bytes;
    }

    private (Vector2 Position, Angle Rotation) ToGridLocal(EntityUid uid, TransformComponent xform, EntityUid grid, MapGridComponent gridComp)
    {
        if (xform.ParentUid == grid)
            return (xform.LocalPosition, xform.LocalRotation);

        var world = _xform.GetWorldPosition(uid);
        var rotation = _xform.GetWorldRotation(uid) - _xform.GetWorldRotation(grid);
        return (_map.WorldToLocal(grid, gridComp, world), rotation);
    }

    private byte[]? SerializeAppearance(EntityUid uid)
    {
        if (!TryComp<AppearanceComponent>(uid, out var appearance) || !appearance.NetSyncEnabled)
            return null;

        if (!TryComp(uid, out MetaDataComponent? meta))
            return null;

        if (_appearanceCache.TryGetValue(uid, out var cached) && cached.Tick == meta.EntityLastModifiedTick)
            return cached.Bytes;

        if (EntityManager.GetComponentState(EntityManager.EventBus, appearance, null, GameTick.Zero) is not AppearanceComponentState state)
            return null;

        using var stream = new MemoryStream();
        _serializer.SerializeDirect(stream, state);
        var bytes = stream.ToArray();
        _appearanceCache[uid] = (meta.EntityLastModifiedTick, bytes);
        return bytes;
    }
}

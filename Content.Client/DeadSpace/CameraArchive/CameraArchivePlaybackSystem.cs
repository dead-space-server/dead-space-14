// Dead Space 14, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.IO;
using System.Numerics;
using Content.Client.IconSmoothing;
using Content.Shared.DeadSpace.CameraArchives;
using Content.Shared.GameTicking;
using Content.Shared.Gravity;
using Content.Shared.DeadSpace.SignBoard;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.Maps;
using Content.Shared.SSDIndicator;
using Content.Shared.StatusEffectNew;
using Content.Client.Stealth;
using Content.Shared.Stealth.Components;
using Content.Shared.TextScreen;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Serialization;

namespace Content.Client.DeadSpace.CameraArchives;

public sealed class CameraArchivePlaybackSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly IMapManager _mapMan = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefs = default!;
    [Dependency] private readonly IRobustSerializer _serializer = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly StatusEffectsSystem _status = default!;
    [Dependency] private readonly StealthSystem _stealth = default!;

    private MapId? _mapId;
    private ushort _platingId;
    private bool _platingReady;

    private int _playId;
    private CameraArchive? _queued;
    private bool _busy;
    public override void Initialize()
    {
        SubscribeNetworkEvent<RoundRestartCleanupEvent>(_ => Clear());
        SubscribeNetworkEvent<TickerJoinLobbyEvent>(_ => Clear());
    }

    public CameraArchiveView? RequestPlay(CameraArchive still)
    {
        _queued = still;
        if (_busy)
            return null;

        CameraArchiveView? last = null;
        _busy = true;
        while (_queued != null)
        {
            var next = _queued;
            _queued = null;
            last = Play(next);
        }

        _busy = false;
        return last;
    }

    public CameraArchiveView Play(CameraArchive still)
    {
        var playId = ++_playId;
        Clear();

        var mapUid = _map.CreateMap(out var mapId, runMapInit: true);
        _map.SetPaused(mapId, false);
        _mapId = mapId;
        var mapLight = EnsureComp<MapLightComponent>(mapUid);
        mapLight.AmbientLightColor = Color.FromSrgb(new Color(0.85f, 0.85f, 0.85f));

        var grid = _mapMan.CreateGridEntity(mapId);
        var gravity = EnsureComp<GravityComponent>(grid.Owner);
        gravity.Enabled = true;
        gravity.Inherent = true;
        var origin = CameraArchive.PlaybackOrigin(still.CameraLocal);

        var plating = Plating();
        var under = new List<(Vector2i, Tile)>(still.Tiles.Count);
        var finalTiles = new List<(Vector2i, Tile)>(still.Tiles.Count);
        foreach (var tile in still.Tiles)
        {
            under.Add((tile.Indices - new Vector2i((int) origin.X, (int) origin.Y), plating));
            finalTiles.Add((tile.Indices - new Vector2i((int) origin.X, (int) origin.Y), new Tile(tile.TypeId, tile.Flags, tile.Variant, tile.RotationMirroring)));
        }

        _map.SetTiles(grid.Owner, grid.Comp, under);

        var spawned = new List<EntityUid>(still.Entities.Count);
        foreach (var entry in still.Entities)
        {
            var parent = entry.Parent < 0 ? grid.Owner : spawned[entry.Parent];
            var place = entry.LocalPosition;
            if (entry.Parent < 0)
                place -= origin;
            var uid = EntityManager.CreateEntityUninitialized(entry.Prototype, new EntityCoordinates(parent, entry.Container.Length == 0 || entry.ShowContents ? place : Vector2.Zero), rotation: entry.LocalRotation);
            if (entry.Parent < 0 && (entry.Container.Length == 0 || entry.ShowContents))
            {
                var xform = Transform(uid);
#pragma warning disable CS0618
                xform.Anchored = entry.Anchored;
#pragma warning restore CS0618
            }

            if (TryComp<GravityAffectedComponent>(uid, out var weight))
                weight.Weightless = false;

            spawned.Add(uid);
        }

        for (var i = 0; i < spawned.Count; i++)
            EntityManager.InitializeAndStartEntity(new Entity<MetaDataComponent?>(spawned[i], null), true);

        for (var i = 0; i < spawned.Count; i++)
        {
            var entry = still.Entities[i];
            var place = entry.Parent < 0 ? entry.LocalPosition - origin : entry.LocalPosition;
            if (entry.Container.Length == 0 || entry.ShowContents)
                _xform.SetLocalPositionRotation(spawned[i], place, entry.LocalRotation);
        }

        for (var i = 0; i < spawned.Count; i++)
            ApplyCopiedState(spawned[i], still.Entities[i]);

        Dress(spawned, still);
        HideCloaked(spawned, still);

        _map.SetTiles(grid.Owner, grid.Comp, finalTiles);

        for (var i = 0; i < spawned.Count; i++)
            ApplyAppearance(spawned[i], still.Entities[i].Appearance);

        if (playId != _playId)
        {
            Clear();
            return new CameraArchiveView(EntityUid.Invalid, EntityUid.Invalid, MapId.Nullspace, new FixedEye());
        }

        var eyeLocal = still.CameraLocal - origin + still.CameraFacing.RotateVec(new Vector2(CameraArchive.EyeLead, 0f));
        var eyePos = _xform.ToMapCoordinates(new EntityCoordinates(grid.Owner, eyeLocal));
        var eye = new FixedEye
        {
            Position = eyePos,
            Rotation = Angle.Zero,
            Zoom = still.Zoom,
            DrawFov = true,
            DrawLight = true,
        };

        return new CameraArchiveView(mapUid, grid.Owner, mapId, eye);
    }

    public void Settle()
    {
        var appearance = EntityManager.System<AppearanceSystem>();
        var smooth = EntityManager.System<IconSmoothSystem>();
        for (var i = 0; i < 12; i++)
        {
            appearance.FrameUpdate(0.016f);
            smooth.FrameUpdate(0.016f);
        }
    }

    public void Clear()
    {
        if (_mapId is { } mapId && _map.MapExists(mapId))
            _map.DeleteMap(mapId);

        _mapId = null;
    }

    private void HideCloaked(List<EntityUid> spawned, CameraArchive still)
    {
        for (var i = 0; i < spawned.Count; i++)
        {
            if (!HiddenByCloak(still, i))
                continue;

            var stealth = EnsureComp<StealthComponent>(spawned[i]);
            _stealth.SetVisibility(spawned[i], stealth.MinVisibility, stealth);
            _stealth.SetEnabled(spawned[i], true, stealth);
        }
    }

    private static bool HiddenByCloak(CameraArchive still, int index)
    {
        var guard = 0;
        while (index >= 0 && index < still.Entities.Count && guard++ < 8)
        {
            if (still.Entities[index].Conceal)
                return true;

            index = still.Entities[index].Parent;
        }

        return false;
    }

    private void ApplyCopiedState(EntityUid uid, CameraArchiveEntity entry)
    {
        if (entry.CopySsd && !entry.IsSsd && TryComp<SSDIndicatorComponent>(uid, out var ssd))
        {
            ssd.IsSSD = false;
            _status.TryRemoveStatusEffect(uid, SSDIndicatorSystem.StatusEffectSSDSleeping);
        }

        if (entry.SignText.Length > 0 && TryComp<SignBoardComponent>(uid, out var sign))
            sign.Text = entry.SignText;

        if (entry.ScreenText.Length > 0)
            _appearance.SetData(uid, TextScreenVisuals.ScreenText, entry.ScreenText);

        ApplyHumanoid(uid, entry.Humanoid);
    }

    private void ApplyHumanoid(EntityUid uid, byte[]? bytes)
    {
        if (bytes == null || bytes.Length == 0 || !TryComp<HumanoidAppearanceComponent>(uid, out var humanoid))
            return;

        using var stream = new MemoryStream(bytes);
        _serializer.DeserializeDirect<HumanoidAppearanceComponent.HumanoidAppearanceComponent_AutoState>(stream, out var state);
        var handle = new ComponentHandleState(state, null);
        EntityManager.EventBus.RaiseComponentEvent(uid, humanoid, ref handle);
    }

    private void Dress(List<EntityUid> spawned, CameraArchive still)
    {
        var inventory = EntityManager.System<InventorySystem>();
        var hands = EntityManager.System<SharedHandsSystem>();
        for (var i = 0; i < spawned.Count; i++)
        {
            var entry = still.Entities[i];
            if (entry.Container.Length == 0)
                continue;

            var worn = false;
            if (entry.Parent >= 0)
            {
                var holder = spawned[entry.Parent];
                var item = spawned[i];
                if (inventory.HasSlot(holder, entry.Container))
                    worn = inventory.TryEquip(holder, item, entry.Container, silent: true, force: true);
                else if (entry.InHand)
                {
                    if (!hands.TryGetHand(holder, entry.Container, out _))
                        hands.AddHand(holder, entry.Container, (HandLocation) entry.HandSide);
                    worn = hands.TryForcePickup(holder, item, entry.Container, checkActionBlocker: false);
                }
            }

            if (!worn && !entry.ShowContents && TryComp<SpriteComponent>(spawned[i], out var sprite))
                sprite.Visible = false;
        }
    }

    private void ApplyAppearance(EntityUid uid, byte[]? bytes)
    {
        if (bytes == null || bytes.Length == 0)
            return;

        using var stream = new MemoryStream(bytes);
        _serializer.DeserializeDirect<AppearanceComponentState>(stream, out var state);

        var appearance = EnsureComp<AppearanceComponent>(uid);
        var handle = new ComponentHandleState(state, null);
        EntityManager.EventBus.RaiseComponentEvent(uid, appearance, ref handle);
    }

    private Tile Plating()
    {
        if (!_platingReady)
        {
            _platingId = _tileDefs["Plating"].TileId;
            _platingReady = true;
        }

        return new Tile(_platingId);
    }
}

public sealed class CameraArchiveView(EntityUid map, EntityUid grid, MapId mapId, FixedEye eye)
{
    public EntityUid Map { get; } = map;
    public EntityUid Grid { get; } = grid;
    public MapId MapId { get; } = mapId;
    public FixedEye Eye { get; } = eye;
}

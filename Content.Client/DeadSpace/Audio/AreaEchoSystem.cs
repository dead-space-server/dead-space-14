// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Client.Light.EntitySystems;
using Content.Shared.DeadSpace.Audio;
using Content.Shared.GameTicking;
using Content.Shared.Light.Components;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Robust.Client.Audio;
using Robust.Client.Audio.Sources;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.Audio;

/// <summary>
/// Estimates the room around a positional sound and applies a local reverb send.
/// </summary>
public sealed class AreaEchoSystem : EntitySystem
{
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly IAudioManager _audioManager = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly SharedMapSystem _maps = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly RoofSystem _roof = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    private const float ProbeRange = 48f;
    private const int ProbesPerTick = 8;
    private const int CacheLimit = 512;
    // Full-height boundaries, including glass doors, but excluding people and ordinary machinery.
    private const int BoundaryMask = (int) (CollisionGroup.Impassable | CollisionGroup.InteractImpassable);
    private static readonly ProtoId<AudioPresetPrototype>[] Presets =
        ["Hallway", "Auditorium", "ConcertHall", "Hangar"];

    private readonly Dictionary<(EntityUid Grid, Vector2i Tile, EntityUid Source), int> _rooms = new();
    private readonly Dictionary<int, (EntityUid Auxiliary, EntityUid Effect)> _effects = new();
    private readonly Dictionary<EntityUid, (AudioComponent Audio, EntityUid Auxiliary)> _applied = new();
    private readonly List<EntityUid> _stale = new();
    private TimeSpan _refreshAt;
    private bool _enabled;
    private bool _highQuality;
    private bool _backendUnavailable;
    private bool _backendChecked;
    private GameTick _probeTick;
    private int _probesRemaining = ProbesPerTick;

    public override void Initialize()
    {
        base.Initialize();
        UpdatesBefore.Add(typeof(AudioSystem));
        SubscribeLocalEvent<AudioComponent, EntParentChangedMessage>(OnAudioParentChanged);
        Subs.CVar(_cfg, AreaEchoCVars.Enabled, OnEnabledChanged, true);
        Subs.CVar(_cfg, AreaEchoCVars.HighQuality, OnQualityChanged, true);
        SubscribeNetworkEvent<RoundRestartCleanupEvent>(_ => ClearEcho());
    }

    public override void Shutdown()
    {
        ClearEcho();
        foreach (var effect in _effects.Values)
        {
            if (!Deleted(effect.Auxiliary))
                Del(effect.Auxiliary);
            if (!Deleted(effect.Effect))
                Del(effect.Effect);
        }
        _effects.Clear();
        base.Shutdown();
    }

    private void OnEnabledChanged(bool enabled)
    {
        _enabled = enabled;
        if (!enabled)
            ClearEcho();
    }

    private void OnQualityChanged(bool highQuality)
    {
        _highQuality = highQuality;
        _rooms.Clear();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        if (!_enabled || _backendUnavailable)
            return;

        _stale.Clear();
        foreach (var (uid, applied) in _applied)
        {
            if (!TryComp<AudioComponent>(uid, out var current) || current != applied.Audio)
                _stale.Add(uid);
        }
        foreach (var uid in _stale)
            _applied.Remove(uid);

        var listener = _audio.GetListenerCoordinates();
        var query = EntityQueryEnumerator<AudioComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var sound, out var xform))
        {
            UpdateEcho((uid, sound, xform), listener);
            if (_backendUnavailable)
                return;
        }
    }

    private void OnAudioParentChanged(Entity<AudioComponent> sound, ref EntParentChangedMessage args)
    {
        // Local PlayEntity/PlayStatic sets the parent after loading, before its first audible ProcessStream.
        if (_enabled && !_backendUnavailable && _timing.IsFirstTimePredicted)
            UpdateEcho((sound.Owner, sound.Comp, args.Transform), _audio.GetListenerCoordinates());
    }

    private void UpdateEcho(Entity<AudioComponent, TransformComponent> sound, MapCoordinates listener)
    {
        var ownsEffect = _applied.TryGetValue(sound, out var applied) &&
                         sound.Comp1 == applied.Audio && sound.Comp1.Auxiliary == applied.Auxiliary;
        if (!ownsEffect)
            _applied.Remove(sound);

        // Authored effects and effects assigned by another system take priority.
        if (sound.Comp1.Auxiliary != null && !ownsEffect)
            return;

        if (!TryGetRoomPreset(sound, listener, out var preset))
            return;

        if (preset < 0)
        {
            RemoveEcho(sound, sound.Comp1);
            return;
        }

        if (!TryGetAuxiliary(preset, out var auxiliary))
        {
            ClearEcho();
            return;
        }

        if (sound.Comp1.Auxiliary != auxiliary)
            SetAuxiliaryLocally((sound.Owner, sound.Comp1), auxiliary);
        _applied[sound] = (sound.Comp1, auxiliary);
    }

    internal bool TryGetRoomPreset(Entity<AudioComponent, TransformComponent> sound,
        MapCoordinates listener, out int preset)
    {
        preset = -1;
        var xform = sound.Comp2;
        // Playing is a native playback flag; short sounds need their send before that flag becomes true.
        if (!sound.Comp1.Loaded || sound.Comp1.Global || sound.Comp1.State != AudioState.Playing ||
            xform.MapID == MapId.Nullspace || xform.MapID != listener.MapId)
            return true;

        var position = _transform.GetWorldPosition(xform);
        var gridUid = xform.GridUid ?? EntityUid.Invalid;
        if (!TryComp<MapGridComponent>(gridUid, out var grid) &&
            !_mapManager.TryFindGridAt(xform.MapID, position, out gridUid, out grid))
            return true;

        if (_timing.RealTime >= _refreshAt)
        {
            _rooms.Clear();
            _refreshAt = _timing.RealTime + TimeSpan.FromSeconds(1);
        }
        if (_probeTick != _timing.CurTick)
        {
            _probeTick = _timing.CurTick;
            _probesRemaining = ProbesPerTick;
        }

        var tile = _maps.WorldToTile(gridUid, grid, position);
        var key = (gridUid, tile, xform.ParentUid);
        if (_rooms.TryGetValue(key, out preset))
            return true;
        if (_probesRemaining <= 0)
            return false;

        _probesRemaining--;
        preset = MeasureRoom((gridUid, grid), tile, xform.ParentUid);
        if (_rooms.Count >= CacheLimit)
            _rooms.Clear();
        _rooms[key] = preset;
        return true;
    }

    private bool TryGetAuxiliary(int preset, out EntityUid auxiliary)
    {
        if (_effects.TryGetValue(preset, out var cached) &&
            HasComp<AudioAuxiliaryComponent>(cached.Auxiliary) && HasComp<AudioEffectComponent>(cached.Effect))
        {
            auxiliary = cached.Auxiliary;
            return true;
        }

        auxiliary = EntityUid.Invalid;
        var effect = EntityUid.Invalid;
        try
        {
            if (!_backendChecked)
            {
                // Probe the public audio factory without playing anything. Headless sources cannot use EFX.
                using var probeStream = _audioManager.LoadAudioRaw(new short[1], 1, 8000);
                using var probeSource = _audioManager.CreateAudioSource(probeStream);
                _backendChecked = true;
                if (probeSource is not BaseAudioSource)
                {
                    _backendUnavailable = true;
                    return false;
                }
            }

            // Reuse at most four slots across rounds: the engine has no public native-slot disposal API.
            auxiliary = Spawn(null, MapCoordinates.Nullspace);
            effect = Spawn(null, MapCoordinates.Nullspace);
            var effectComp = AddComp<AudioEffectComponent>(effect);
            var auxComp = AddComp<AudioAuxiliaryComponent>(auxiliary);
            _audio.SetEffectPreset(effect, effectComp, _prototypes.Index(Presets[preset]));
            _audio.SetEffect(auxiliary, auxComp, effect);
            _effects[preset] = (auxiliary, effect);
            return true;
        }
        catch (Exception e)
        {
            // EFX is optional; an unavailable audio backend must not interrupt ordinary playback.
            _backendUnavailable = true;
            Log.Warning($"Room echo is unavailable on this audio backend: {e.Message}");
            if (auxiliary.IsValid())
                Del(auxiliary);
            if (effect.IsValid())
                Del(effect);
            return false;
        }
    }

    /// <summary>
    /// Measures grid-relative diameters. Rays escaping through missing floor or roof do not reflect.
    /// A negative result means the room is too small or too exposed for reverb.
    /// </summary>
    internal int MeasureRoom(Entity<MapGridComponent> grid, Vector2i tile, EntityUid? source = null)
    {
        TryComp<RoofComponent>(grid, out var roof);
        if (!IsCovered(grid, roof, tile))
            return -1;

        var matrix = _transform.GetWorldMatrix(grid);
        var localOrigin = (tile + new Vector2(0.5f)) * grid.Comp.TileSize;
        var origin = Vector2.Transform(localOrigin, matrix);
        var mapId = Transform(grid).MapID;
        var rays = _highQuality ? 16 : 8;
        var step = grid.Comp.TileSize * (_highQuality ? 0.25f : 0.5f);
        var enclosed = 0;
        var diameter = 0f;

        for (var i = 0; i < rays; i++)
        {
            var angle = MathF.Tau * i / rays;
            var localDirection = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var direction = Vector2.TransformNormal(localDirection, matrix);
            var distance = ProbeRange;
            // On this engine, first-hit mode uses tree traversal order rather than nearest distance.
            foreach (var hit in _physics.IntersectRay(mapId, new CollisionRay(origin, direction, BoundaryMask),
                         maxLength: ProbeRange, ignoredEnt: source ?? grid.Owner, returnOnFirstHit: false))
            {
                distance = MathF.Min(distance, hit.Distance);
            }

            var escaped = false;
            for (var travelled = step; travelled < distance; travelled += step)
            {
                var point = (localOrigin + localDirection * travelled) / grid.Comp.TileSize;
                var sample = new Vector2i((int) MathF.Floor(point.X), (int) MathF.Floor(point.Y));
                if (IsCovered(grid, roof, sample))
                    continue;

                escaped = true;
                break;
            }
            if (escaped)
                continue;

            enclosed++;
            diameter += 2f * distance;
        }

        if (enclosed < rays * 3 / 4)
            return -1;

        diameter /= rays;
        return diameter switch
        {
            < 10f => -1,
            < 18f => 0,
            < 28f => 1,
            < 40f => 2,
            _ => 3,
        };
    }

    private bool IsCovered(Entity<MapGridComponent> grid, RoofComponent? roof, Vector2i tile)
    {
        if (!_maps.TryGetTileRef(grid, grid.Comp, tile, out var tileRef) ||
            tileRef.Tile.IsEmpty || _turf.IsSpace(tileRef))
            return false;

        return roof != null
            ? _roof.IsRooved((grid.Owner, grid.Comp, roof), tile)
            : HasComp<ImplicitRoofComponent>(grid);
    }

    private void RemoveEcho(EntityUid uid, AudioComponent sound)
    {
        if (_applied.Remove(uid, out var applied) &&
            sound == applied.Audio && sound.Auxiliary == applied.Auxiliary)
            SetAuxiliaryLocally((uid, sound), null);
    }

    private void ClearEcho()
    {
        foreach (var (uid, applied) in _applied)
        {
            if (TryComp<AudioComponent>(uid, out var sound) && sound == applied.Audio &&
                sound.Auxiliary == applied.Auxiliary)
                SetAuxiliaryLocally((uid, sound), null);
        }
        _applied.Clear();
        _rooms.Clear();
    }

    internal void SetAuxiliaryLocally(Entity<AudioComponent> sound, EntityUid? auxiliary)
    {
        // SetAuxiliary also dirties AudioComponent. A predicted reset then seeks/restarts the sound.
        // Suppress only that synchronous dirty operation, restoring normal replication immediately.
        var netSync = sound.Comp.NetSyncEnabled;
        sound.Comp.NetSyncEnabled = false;
        try
        {
            _audio.SetAuxiliary(sound, sound.Comp, auxiliary);
        }
        finally
        {
            sound.Comp.NetSyncEnabled = netSync;
        }
    }
}

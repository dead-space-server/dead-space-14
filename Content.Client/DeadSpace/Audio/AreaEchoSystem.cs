// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Client.Light.EntitySystems;
using Content.Shared.DeadSpace.Audio;
using Content.Shared.Audio.Jukebox;
using Content.Shared.DeadSpace.Ports.Jukebox;
using Content.Shared.GameTicking;
using Content.Shared.Light.Components;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Robust.Client.Audio;
using Robust.Client.Audio.Sources;
using Robust.Client.Graphics;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Effects;
using Robust.Shared.Physics.Components;
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
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly SharedMapSystem _maps = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly RoofSystem _roof = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly SpaceMufflingSystem _muffling = default!;

    private const float ProbeRange = 48f;
    private const int ProbesPerTick = 6;
    private const int CacheLimit = 512;
    private const int EffectLimit = 12;
    // Furniture scatters sound, but must not close a room like a full-height wall.
    private const int BoundaryMask = (int) (CollisionGroup.Impassable | CollisionGroup.InteractImpassable);
    private const int ProbeMask = BoundaryMask | (int) (CollisionGroup.MidImpassable | CollisionGroup.LowImpassable);
    private static readonly ProtoId<AudioPresetPrototype> DrugPreset = "Drugged";

    /// <summary>
    /// When set, applies the strong "Drugged" reverb to every audible positional sound,
    /// regardless of the measured room. Used for the drug echo effect.
    /// </summary>
    public bool DrugEchoOverride { get; set; }

    private readonly Dictionary<(EntityUid Grid, Vector2i Tile, EntityUid Source), RoomAcoustics> _rooms = new();
    private readonly List<(EntityUid Auxiliary, EntityUid Effect)> _effects = new();
    private readonly HashSet<EntityUid> _leased = new();
    private readonly Dictionary<EntityUid, (AudioComponent Audio, EntityUid Auxiliary, RoomAcoustics Room)> _applied = new();
    private readonly List<EntityUid> _stale = new();
    private readonly float[] _rayLengths = new float[32];
    private readonly bool[] _rayReflected = new bool[32];
    private readonly float[] _roomDiameters = new float[16];
    private TimeSpan _refreshAt;
    private TimeSpan _effectsUpdateAt;
    private bool _enabled;
    private bool _highQuality;
    private bool _backendUnavailable;
    private bool _backendChecked;
    private bool _ghostListener;
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
        foreach (var effect in _effects)
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
        var ghost = _muffling.ListenerIsGhost;
        if (ghost != _ghostListener)
        {
            _ghostListener = ghost;
            if (ghost)
                ClearEcho();
        }
        if (!_enabled || _backendUnavailable || ghost)
            return;

        _stale.Clear();
        foreach (var (uid, applied) in _applied)
        {
            if (!TryComp<AudioComponent>(uid, out var current) || current != applied.Audio)
                _stale.Add(uid);
        }
        foreach (var uid in _stale)
        {
            _leased.Remove(_applied[uid].Auxiliary);
            _applied.Remove(uid);
        }

        if (_timing.RealTime < _effectsUpdateAt)
            return;
        _effectsUpdateAt = _timing.RealTime + TimeSpan.FromSeconds(0.1);
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

    private void UpdateEcho(Entity<AudioComponent, TransformComponent> sound, MapCoordinates listener, bool smooth = true)
    {
        var ownsEffect = _applied.TryGetValue(sound, out var applied) &&
                         sound.Comp1 == applied.Audio && sound.Comp1.Auxiliary == applied.Auxiliary;
        if (!ownsEffect && _applied.Remove(sound, out var previous))
            _leased.Remove(previous.Auxiliary);

        // Authored effects and effects assigned by another system take priority.
        if (sound.Comp1.Auxiliary != null && !ownsEffect)
            return;

        var echoGain = TryComp<SpeechAudioComponent>(sound, out var speech) ? speech.EchoGain : 1f;
        var drugEcho = DrugEchoOverride;
        if (!drugEcho && echoGain <= 0f)
        {
            RemoveEcho(sound, sound.Comp1);
            return;
        }

        var room = RoomAcoustics.Empty;
        if (drugEcho)
        {
            if (_muffling.ListenerIsGhost || !IsAudiblePositional(sound, listener) ||
                Vector2.Distance(_transform.GetWorldPosition(sound.Comp2), listener.Position) >= sound.Comp1.Params.MaxDistance)
            {
                RemoveEcho(sound, sound.Comp1);
                return;
            }
        }
        else
        {
            if (!TryGetRoom(sound, listener, out room))
                return;

            if (!room.Grid.IsValid())
            {
                RemoveEcho(sound, sound.Comp1);
                return;
            }

            // Smooth geometry at the 10 Hz update rate. Distance, pressure and wall attenuation stay immediate.
            if (ownsEffect && smooth && applied.Room.Grid == room.Grid)
            {
                var oldRoom = applied.Room;
                const float blend = 0.4f;
                room = room with
                {
                    Size = oldRoom.Size + (room.Size - oldRoom.Size) * blend,
                    Enclosure = oldRoom.Enclosure + (room.Enclosure - oldRoom.Enclosure) * blend,
                    Scattering = oldRoom.Scattering + (room.Scattering - oldRoom.Scattering) * blend,
                    Reflection = Vector2.Lerp(oldRoom.Reflection, room.Reflection, blend),
                    ReflectionDistance = oldRoom.ReflectionDistance + (room.ReflectionDistance - oldRoom.ReflectionDistance) * blend,
                    TailSize = oldRoom.TailSize + (room.TailSize - oldRoom.TailSize) * blend,
                    TailGain = oldRoom.TailGain + (room.TailGain - oldRoom.TailGain) * blend,
                    LateReflection = Vector2.Lerp(oldRoom.LateReflection, room.LateReflection, blend),
                    EchoSpan = oldRoom.EchoSpan + (room.EchoSpan - oldRoom.EchoSpan) * blend,
                };
            }
            if (room.Strength <= 0.001f)
            {
                RemoveEcho(sound, sound.Comp1);
                return;
            }
        }

        var auxiliary = applied.Auxiliary;
        if (!ownsEffect && !TryGetAuxiliary(out auxiliary))
        {
            if (_backendUnavailable)
                ClearEcho();
            return;
        }

        var effect = Comp<AudioAuxiliaryComponent>(auxiliary).Effect;
        if (effect == null)
            return;

        if (drugEcho)
        {
            _audio.SetEffectPreset(effect.Value, Comp<AudioEffectComponent>(effect.Value), _prototypes.Index(DrugPreset));
        }
        else
        {
            var position = _transform.GetWorldPosition(sound.Comp2);
            var delta = position - listener.Position;
            var distance = delta.Length();
            var parameters = sound.Comp1.Params;
            var distanceGain = SpatialAudio.GetDistanceGain(distance, parameters.MaxDistance, parameters.ReferenceDistance);
            var occlusion = _muffling.GetOcclusion(listener, delta, distance, sound.Comp2.ParentUid);
            // Native direct-path occlusion does not filter the auxiliary send. Attenuate the wet path explicitly.
            var gain = distanceGain * MathF.Exp(-occlusion) * echoGain;
            var preset = CreatePreset(room, listener.Position, _transform.GetWorldMatrix(room.Grid),
                _eye.CurrentEye.Rotation, gain);
            _audio.SetEffectPreset(effect.Value, Comp<AudioEffectComponent>(effect.Value), preset);
        }
        _audio.SetEffect(auxiliary, Comp<AudioAuxiliaryComponent>(auxiliary), effect);

        if (!ownsEffect)
        {
            SetAuxiliaryLocally((sound.Owner, sound.Comp1), auxiliary);
            _leased.Add(auxiliary);
        }
        _applied[sound] = (sound.Comp1, auxiliary, room);
    }

    internal void ConfigureSpeech(Entity<AudioComponent> sound, bool whisper, bool radio, bool suppressEcho = false)
    {
        EnsureComp<SpeechAudioComponent>(sound).EchoGain = radio || suppressEcho ? 0f : whisper ? 0.04f : 1f;
        if (radio)
        {
            sound.Comp.Flags |= AudioFlags.NoOcclusion;
            sound.Comp.Occlusion = 0f;
        }
        if ((radio || suppressEcho) && !DrugEchoOverride)
            RemoveEcho(sound, sound.Comp);
        else if (_enabled && !_backendUnavailable)
            UpdateEcho((sound.Owner, sound.Comp, Transform(sound)), _audio.GetListenerCoordinates(), smooth: false);
    }

    private static bool IsAudiblePositional(Entity<AudioComponent, TransformComponent> sound, MapCoordinates listener)
    {
        var xform = sound.Comp2;
        // Playing is a native playback flag; short sounds need their send before that flag becomes true.
        return sound.Comp1.Loaded && !sound.Comp1.Global && sound.Comp1.State == AudioState.Playing &&
               xform.MapID != MapId.Nullspace && xform.MapID == listener.MapId;
    }

    internal bool TryGetRoomPreset(Entity<AudioComponent, TransformComponent> sound,
        MapCoordinates listener, out int preset)
    {
        var measured = TryGetRoom(sound, listener, out var room);
        preset = room.Preset;
        return measured;
    }

    private bool TryGetRoom(Entity<AudioComponent, TransformComponent> sound,
        MapCoordinates listener, out RoomAcoustics room)
    {
        room = RoomAcoustics.Empty;
        var xform = sound.Comp2;
        if (_muffling.ListenerIsGhost || !IsAudiblePositional(sound, listener) ||
            (sound.Comp1.Flags & AudioFlags.NoOcclusion) != 0 ||
            HasComp<JukeboxComponent>(xform.ParentUid) || HasComp<WhiteJukeboxComponent>(xform.ParentUid))
            return true;

        var position = _transform.GetWorldPosition(xform);
        if (Vector2.Distance(position, listener.Position) >= sound.Comp1.Params.MaxDistance)
            return true;

        var gridUid = xform.GridUid ?? EntityUid.Invalid;
        if (!TryComp<MapGridComponent>(gridUid, out var grid) &&
            !_mapManager.TryFindGridAt(xform.MapID, position, out gridUid, out grid))
            return true;

        if (_timing.RealTime >= _refreshAt)
        {
            _rooms.Clear();
            _refreshAt = _timing.RealTime + TimeSpan.FromSeconds(0.35);
        }
        if (_probeTick != _timing.CurTick)
        {
            _probeTick = _timing.CurTick;
            _probesRemaining = ProbesPerTick;
        }

        var tile = _maps.WorldToTile(gridUid, grid, position);
        var key = (gridUid, tile, xform.ParentUid);
        if (_rooms.TryGetValue(key, out room))
            return true;
        if (_probesRemaining <= 0)
            return false;

        _probesRemaining--;
        room = MeasureAcoustics((gridUid, grid), tile, xform.ParentUid,
            Vector2.Transform(position, _transform.GetInvWorldMatrix(gridUid)));
        if (_rooms.Count >= CacheLimit)
            _rooms.Clear();
        _rooms[key] = room;
        return true;
    }

    private bool TryGetAuxiliary(out EntityUid auxiliary)
    {
        foreach (var slot in _effects)
        {
            if (_leased.Contains(slot.Auxiliary))
                continue;
            var component = Comp<AudioAuxiliaryComponent>(slot.Auxiliary);
            if (component.Effect == null)
                _audio.SetEffect(slot.Auxiliary, component, slot.Effect);
            auxiliary = slot.Auxiliary;
            return true;
        }

        auxiliary = EntityUid.Invalid;
        if (_effects.Count >= EffectLimit)
            return false;
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

            // Reuse a bounded pool across rounds: the engine has no public native-slot disposal API.
            auxiliary = Spawn(null, MapCoordinates.Nullspace);
            effect = Spawn(null, MapCoordinates.Nullspace);
            var effectComp = AddComp<AudioEffectComponent>(effect);
            var auxComp = AddComp<AudioAuxiliaryComponent>(auxiliary);
            _audio.SetEffectPreset(effect, effectComp, ReverbPresets.Generic);
            _audio.SetEffect(auxiliary, auxComp, effect);
            _effects.Add((auxiliary, effect));
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

    internal int MeasureRoom(Entity<MapGridComponent> grid, Vector2i tile, EntityUid? source = null)
        => MeasureAcoustics(grid, tile, source).Preset;

    internal RoomAcoustics MeasureAcoustics(Entity<MapGridComponent> grid, Vector2i tile, EntityUid? source = null,
        Vector2? localPosition = null)
    {
        TryComp<RoofComponent>(grid, out var roof);
        if (!IsCovered(grid, roof, tile))
            return RoomAcoustics.Empty;

        var matrix = _transform.GetWorldMatrix(grid);
        var localOrigin = localPosition ?? (tile + new Vector2(0.5f)) * grid.Comp.TileSize;
        var mapId = Transform(grid).MapID;
        var rays = _highQuality ? 32 : 16;
        var step = grid.Comp.TileSize * (_highQuality ? 0.25f : 0.5f);
        var lengths = _rayLengths;
        var enclosed = 0;
        var furniture = 0;
        var reflectionDistance = 0f;
        var reflection = Vector2.Zero;
        var reflectionWeight = 0f;
        var lateReflection = Vector2.Zero;
        var lateWeight = 0f;

        for (var i = 0; i < rays; i++)
        {
            var angle = MathF.Tau * i / rays;
            var localDirection = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var (distance, reflected, scattered) = Probe(localOrigin, localDirection);
            lengths[i] = distance;
            _rayReflected[i] = reflected;
            if (scattered)
                furniture++;
            if (!reflected)
                continue;

            enclosed++;
            // Nearby surfaces dominate the first reflection; distant walls shape the late tail.
            var wall = localOrigin + localDirection * MathF.Max(0f, distance - 0.1f);
            var weight = 1f / (1f + distance * distance);
            reflection += wall * weight;
            reflectionDistance += distance * weight;
            reflectionWeight += weight;
            lateReflection += wall * distance;
            lateWeight += distance;
        }

        // Openness scales the send instead of switching it off at an arbitrary fraction of closed rays.
        if (enclosed == 0)
            return RoomAcoustics.Empty;
        reflection /= reflectionWeight;
        reflectionDistance /= reflectionWeight;
        lateReflection = lateWeight > 0f ? lateReflection / lateWeight : localOrigin;

        // A single ray through a doorway must not turn the source room into the adjoining hall.
        // Keep the long axis separately: even a three-tile-wide corridor needs a distant tail.
        var diameters = _roomDiameters;
        var echoSpan = 0f;
        for (var i = 0; i < rays / 2; i++)
        {
            diameters[i] = lengths[i] + lengths[i + rays / 2];
            if (_rayReflected[i] && _rayReflected[i + rays / 2])
                echoSpan = MathF.Max(echoSpan, diameters[i]);
        }
        Array.Sort(diameters, 0, rays / 2);
        var size = (diameters[rays / 4 - 1] + diameters[rays / 4]) * 0.5f;
        var tailSize = MathF.Max(size, diameters[rays / 2 - 1] * 0.65f);
        var localStrength = RoomAcoustics.GetStrength(size);
        var longPaths = 0f;
        var opening = Vector2.Zero;
        for (var i = 0; i < rays; i++)
        {
            var weight = Math.Clamp((lengths[i] - size * 0.75f) / MathF.Max(1f, size * 0.5f), 0f, 1f);
            if (weight <= 0f)
                continue;
            longPaths += weight;
            var angle = MathF.Tau * i / rays;
            opening += new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * weight;
        }
        var coupling = MathF.Min(1f, 1.5f * MathF.Sqrt(longPaths / rays));
        var tailGain = localStrength + (1f - localStrength) * RoomAcoustics.GetStrength(tailSize) * coupling;
        if (longPaths > 0)
        {
            var portal = localOrigin + opening / longPaths * size;
            lateReflection = Vector2.Lerp(lateReflection, portal, 1f - localStrength);
        }

        // Look across the longest unobstructed cardinal ray. This couples a vestibule to an open T-junction
        // without allowing an adjacent hall to bleed through closed doors, walls, or missing floor/roof.
        var axis = 0;
        for (var i = rays / 4; i < rays; i += rays / 4)
        {
            if (lengths[i] > lengths[axis])
                axis = i;
        }
        var along = new Vector2(MathF.Cos(MathF.Tau * axis / rays), MathF.Sin(MathF.Tau * axis / rays));
        var across = new Vector2(-along.Y, along.X);
        var openingWidth = lengths[(axis + rays / 4) % rays] + lengths[(axis + rays * 3 / 4) % rays];
        var probeLimit = MathF.Min(8f, lengths[axis] - grid.Comp.TileSize * 0.5f);
        for (var sample = 1; sample <= 8 && localStrength < 1f; sample++)
        {
            var distance = sample * grid.Comp.TileSize;
            if (distance > probeLimit)
                break;
            var point = localOrigin + along * distance;
            var (left, leftReflected, _) = Probe(point, across);
            var (right, rightReflected, _) = Probe(point, -across);
            openingWidth = MathF.Min(openingWidth, left + right);
            if (!leftReflected && !rightReflected)
                continue;
            var connectedSize = (left + right) * 0.65f;
            if (connectedSize <= tailSize)
                continue;

            // Measure the passage itself, not how many sparse rays can see a point deep inside the hall.
            // The narrowest section limits transmission; distance to its opening gives a continuous falloff.
            var separation = MathF.Max(0f, distance - grid.Comp.TileSize * 0.5f);
            var connection = openingWidth / MathF.Max(0.001f,
                MathF.Sqrt(openingWidth * openingWidth + separation * separation));
            var connectedGain = (1f - localStrength) * RoomAcoustics.GetStrength(connectedSize) * connection;
            if (connectedGain <= tailGain - localStrength)
                continue;

            tailSize = connectedSize;
            tailGain = localStrength + connectedGain;
            if (leftReflected && rightReflected)
                echoSpan = MathF.Max(echoSpan, left + right);
            // Around a corner, only the late tail enters through the passage. Local walls stay local.
            lateReflection = Vector2.Lerp(lateReflection, point, connectedGain / MathF.Max(tailGain, 0.001f));
        }

        return new RoomAcoustics(grid, size, (float) enclosed / rays,
            1f - 0.2f * furniture / rays, reflection, reflectionDistance, tailSize, tailGain, lateReflection, echoSpan);

        (float Distance, bool Reflected, bool Scattered) Probe(Vector2 start, Vector2 localDirection)
        {
            var origin = Vector2.Transform(start, matrix);
            var direction = Vector2.TransformNormal(localDirection, matrix);
            var distance = ProbeRange;
            var obstacle = ProbeRange;
            var reflected = false;
            // First-hit mode uses tree traversal order, not nearest distance, on this engine.
            foreach (var hit in _physics.IntersectRay(mapId, new CollisionRay(origin, direction, ProbeMask),
                         maxLength: ProbeRange, ignoredEnt: source ?? grid.Owner, returnOnFirstHit: false))
            {
                if (!TryComp<PhysicsComponent>(hit.HitEntity, out var body))
                    continue;
                if ((body.CollisionLayer & BoundaryMask) != 0)
                {
                    distance = MathF.Min(distance, hit.Distance);
                    reflected = true;
                }
                // Static impact/landing sounds are parented to the grid, not to the struck object.
                // Its enclosing fixture has distance zero in every direction and is not room furnishing.
                else if (hit.Distance > 0.01f)
                    obstacle = MathF.Min(obstacle, hit.Distance);
            }

            for (var travelled = step; travelled < distance; travelled += step)
            {
                var point = (start + localDirection * travelled) / grid.Comp.TileSize;
                var sample = new Vector2i((int) MathF.Floor(point.X), (int) MathF.Floor(point.Y));
                if (IsCovered(grid, roof, sample))
                    continue;
                distance = travelled;
                reflected = false;
                break;
            }

            return (distance, reflected, obstacle < distance);
        }
    }

    internal readonly record struct RoomAcoustics(EntityUid Grid, float Size, float Enclosure,
        float Scattering, Vector2 Reflection, float ReflectionDistance, float TailSize, float TailGain,
        Vector2 LateReflection, float EchoSpan)
    {
        public static readonly RoomAcoustics Empty = new(EntityUid.Invalid, 0f, 0f, 1f, Vector2.Zero, 0f, 0f, 0f, Vector2.Zero, 0f);

        public int Preset => Strength <= 0f ? -1 : TailSize switch { < 18f => 0, < 28f => 1, < 40f => 2, _ => 3 };

        public float Strength => MathF.Max(GetStrength(Size), TailGain);

        public static float GetStrength(float roomSize)
        {
            var size = Math.Clamp((roomSize - 4f) / 8f, 0f, 1f);
            return size * size * (3f - 2f * size);
        }
    }

    internal static ReverbProperties CreatePreset(RoomAcoustics room, Vector2 listener, Matrix3x2 gridMatrix,
        Angle listenerRotation, float gain)
    {
        var early = Vector2.Transform(room.Reflection, gridMatrix) - listener;
        var late = Vector2.Transform(room.LateReflection, gridMatrix) - listener;
        // EFX panning is listener-relative. Match the engine's eye rotation, with screen north in front.
        early = listenerRotation.RotateVec(early) / MathF.Max(room.ReflectionDistance, early.Length() + 0.1f);
        late = listenerRotation.RotateVec(late) / MathF.Max(room.Size * 0.25f, late.Length() + 0.1f);
        var preset = ReverbPresets.Generic;
        preset.Gain = Math.Clamp(0.3f * room.Enclosure * gain, 0f, 1f);
        preset.GainHF = 0.9f * room.Scattering;
        preset.DecayTime = Math.Clamp((0.35f + room.TailSize * 0.085f) * room.Enclosure * room.Scattering, 0.15f, 4.5f);
        preset.DecayHFRatio = 0.55f + 0.3f * room.Scattering;
        preset.Density = Math.Clamp(0.3f + room.Size / 80f, 0.3f, 0.85f);
        // EFX repeats inside the decaying tail, without replaying or seeking the original audio stream.
        // Use the path between reflecting boundaries; an open end must not invent a returning wave.
        preset.EchoTime = Math.Clamp(room.EchoSpan / 343f, 0.075f, 0.25f);
        preset.EchoDepth = Math.Clamp((room.EchoSpan - 20f) / 40f, 0f, 0.65f) * room.Scattering;
        preset.Diffusion = Math.Clamp(0.5f + room.Size / 160f + (1f - room.Scattering) * 0.65f - preset.EchoDepth * 0.3f, 0.3f, 0.95f);
        preset.ReflectionsGain = 0.65f * RoomAcoustics.GetStrength(room.Size);
        preset.ReflectionsDelay = Math.Clamp(2f * room.ReflectionDistance / 343f, 0.003f, 0.15f);
        preset.ReflectionsPan = new Vector3(early.X, 0f, early.Y) * 0.85f;
        preset.LateReverbGain = 1.1f * room.Strength;
        preset.LateReverbDelay = Math.Clamp(room.TailSize / 343f, 0.01f, 0.1f);
        preset.LateReverbPan = new Vector3(late.X, 0f, late.Y) * 0.75f;
        // The send already has explicit distance attenuation, including stereo sources.
        preset.RoomRolloffFactor = 0f;
        return preset;
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
        {
            SetAuxiliaryLocally((uid, sound), null);
            _leased.Remove(applied.Auxiliary);
        }
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
        _leased.Clear();
        _rooms.Clear();
        // Detaching sources alone leaves the native reverb tail playing. Stop our owned slots as well.
        if (!_backendUnavailable)
        {
            foreach (var slot in _effects)
            {
                if (TryComp<AudioAuxiliaryComponent>(slot.Auxiliary, out var auxiliary))
                    _audio.SetEffect(slot.Auxiliary, auxiliary, null);
            }
        }
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

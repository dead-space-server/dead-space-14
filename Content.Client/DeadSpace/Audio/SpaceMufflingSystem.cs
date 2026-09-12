// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Client.Atmos.Components;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.EntitySystems;
using Content.Shared.DeadSpace.Audio;
using Robust.Client.Audio;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;

namespace Content.Client.DeadSpace.Audio;

/// <summary>
/// Adds pressure-dependent filtering to positional sounds, preserving their playback and wall occlusion.
/// </summary>
public sealed class SpaceMufflingSystem : EntitySystem
{
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly SharedMapSystem _maps = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;

    private float _maxRayLength;
    private bool _enabled;

    public override void Initialize()
    {
        base.Initialize();
        Subs.CVar(_cfg, AreaEchoCVars.SpaceMuffling, value => _enabled = value, true);
        Subs.CVar(_cfg, Robust.Shared.CVars.AudioRaycastLength, value => _maxRayLength = value, true);
        _audio.GetOcclusionOverride += GetOcclusion;
    }

    public override void Shutdown()
    {
        _audio.GetOcclusionOverride -= GetOcclusion;
        base.Shutdown();
    }

    internal float GetOcclusion(MapCoordinates listener, Vector2 delta, float distance, EntityUid? ignoredEntity)
    {
        var occlusion = 0f;
        if (distance > 0.1f)
        {
            var ray = new CollisionRay(listener.Position, delta / distance, _audio.OcclusionCollisionMask);
            occlusion = _physics.IntersectRayPenetration(listener.MapId, ray,
                MathF.Min(distance, _maxRayLength), ignoredEntity);
        }

        // AudioSystem calls this on worker threads. Keep endpoint queries read-only and scratch state local.
        if (_enabled)
            occlusion += MathF.Max(GetAtmosphericOcclusion(listener),
                GetAtmosphericOcclusion(new MapCoordinates(listener.Position + delta, listener.MapId)));

        return occlusion;
    }

    internal float GetAtmosphericOcclusion(MapCoordinates position)
    {
        if (!_maps.TryGetMap(position.MapId, out var map))
            return 0f;

        if (_mapManager.TryFindGridAt(position, out var gridUid, out var grid))
        {
            var tile = _maps.WorldToTile(gridUid, grid, position.Position);
            if (TryComp<GasTileOverlayComponent>(gridUid, out var overlay) &&
                overlay.Chunks.TryGetValue(SharedGasTileOverlaySystem.GetGasChunkIndices(tile), out var chunk))
            {
                var local = tile - chunk.Origin;
                var data = chunk.TileData[local.X + local.Y * SharedGasTileOverlaySystem.ChunkSize];
                if (data.Opacity != null)
                    return AtmosphericAudio.GetOcclusion(data.ByteGasPressure);
            }

            // An in-range station tile may arrive before its atmospheric chunk. Missing data is not vacuum.
            if (gridUid != map.Value)
                return 0f;
        }

        // Uncovered positions use the map's actual atmosphere, including planetary and CentComm environments.
        return TryComp<MapAtmosphereComponent>(map, out var atmosphere)
            ? AtmosphericAudio.GetOcclusion(atmosphere.OverlayData.ByteGasPressure)
            : AtmosphericAudio.VacuumOcclusion;
    }
}

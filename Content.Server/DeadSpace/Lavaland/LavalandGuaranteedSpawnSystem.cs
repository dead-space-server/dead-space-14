// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Threading;
using System.Threading.Tasks;
using Content.Shared.DeadSpace.Lavaland;
using Content.Shared.Parallax.Biomes;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Server.DeadSpace.Lavaland;

/// <summary>
/// Places the planet's <see cref="LavalandPlanetPrototype.GuaranteedSpawns"/> as it is made: the mini-bosses are
/// always there, not left to the rare peaks of a biome layer's noise.
/// </summary>
public sealed class LavalandGuaranteedSpawnSystem : EntitySystem
{
    [Dependency] private readonly LavalandNecropolisTendrilPlacementSystem _placement = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;

    private readonly List<Vector2i> _placed = new();

    public async Task SetupMap(EntityUid mapUid, LavalandPlanetPrototype planet, CancellationToken cancellation)
    {
        if (planet.GuaranteedSpawns.Count == 0 ||
            !TryComp<MapGridComponent>(mapUid, out var grid) ||
            !TryComp<BiomeComponent>(mapUid, out var biome))
        {
            return;
        }

        _placed.Clear();
        foreach (var entry in planet.GuaranteedSpawns)
        {
            if (!_prototype.HasIndex<EntityPrototype>(entry.Prototype))
            {
                Log.Error($"Lavaland guaranteed spawn cannot place missing prototype {entry.Prototype}.");
                continue;
            }

            var spawned = 0;
            for (var i = 0; i < entry.Count; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                if (!_placement.TryFindSpawnTile(mapUid,
                        grid,
                        biome,
                        planet,
                        _placed,
                        entry.MinDistance,
                        entry.MaxDistance,
                        entry.MinSeparation,
                        out var indices) ||
                    !_map.TryGetTileRef(mapUid, grid, indices, out var tile))
                {
                    continue;
                }

                _placement.ReserveArea(mapUid, grid, biome, indices, entry.ClearRadius);
                Spawn(entry.Prototype, _map.ToCenterCoordinates(tile, grid));
                _placed.Add(indices);
                spawned++;
            }

            if (spawned < entry.Count)
                Log.Warning($"Lavaland guaranteed spawn only placed {spawned}/{entry.Count} of {entry.Prototype}.");

            await Timer.Delay(1, cancellation);
        }
    }
}

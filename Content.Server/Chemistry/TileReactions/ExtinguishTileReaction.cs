using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using JetBrains.Annotations;
using Robust.Shared.Map;

namespace Content.Server.Chemistry.TileReactions
{
    [UsedImplicitly]
    [DataDefinition]
    public sealed partial class ExtinguishTileReaction : ITileReaction
    {
        [DataField] public float MinCoolingTemperature = Atmospherics.T20C; // DS14

        public FixedPoint2 TileReact(TileRef tile,
            ReagentPrototype reagent,
            FixedPoint2 reactVolume,
            IEntityManager entityManager,
            List<ReagentData>? data)
        {
            if (reactVolume <= FixedPoint2.Zero || tile.Tile.IsEmpty)
                return FixedPoint2.Zero;

            var atmosphereSystem = entityManager.System<AtmosphereSystem>();

            var environment = atmosphereSystem.GetTileMixture(tile.GridUid, null, tile.GridIndices, true);

            if (environment == null || !atmosphereSystem.IsHotspotActive(tile.GridUid, tile.GridIndices))
                return FixedPoint2.Zero;

            // DS14-start
            var coolingFloor = MathF.Max(MinCoolingTemperature, Atmospherics.TCMB);
            if (environment.Temperature > coolingFloor)
                environment.Temperature = coolingFloor;
            // DS14-end

            atmosphereSystem.ReactTile(tile.GridUid, tile.GridIndices);
            atmosphereSystem.HotspotExtinguish(tile.GridUid, tile.GridIndices);

            return FixedPoint2.Zero;
        }
    }
}

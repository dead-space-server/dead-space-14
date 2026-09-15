// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Atmos.EntitySystems;
using Content.Server.Atmos.Piping.Components;
using Content.Server.NodeContainer.EntitySystems;
using Content.Server.NodeContainer.Nodes;
using Content.Shared.Atmos;
using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server.DeadSpace.Atmos.Piping;

[UsedImplicitly]
public sealed class GasHeatExchangerSystem : EntitySystem
{
    [Dependency] private readonly AtmosphereSystem _atmosphereSystem = default!;
    [Dependency] private readonly NodeContainerSystem _nodeContainer = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;

    private static readonly Direction[] Cardinals =
        { Direction.North, Direction.East, Direction.South, Direction.West };

    public override void Initialize()
    {
        SubscribeLocalEvent<GasHeatExchangerComponent, AtmosDeviceUpdateEvent>(OnUpdate);
    }

    private void OnUpdate(Entity<GasHeatExchangerComponent> ent, ref AtmosDeviceUpdateEvent args)
    {
        if (!_nodeContainer.TryGetNode(ent.Owner, ent.Comp.PipeName, out PipeNode? myPipe))
            return;

        var xform = Transform(ent);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var myTile = _map.CoordinatesToTile(gridUid, grid, xform.Coordinates);

        foreach (var dir in Cardinals)
        {
            var tile = myTile + dir.ToIntVec();
            var enumerator = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
            while (enumerator.MoveNext(out var other))
            {
                if (other == ent.Owner || !TryComp<GasHeatExchangerComponent>(other, out var otherComp))
                    continue;

                if (!_nodeContainer.TryGetNode(other.Value, otherComp.PipeName, out PipeNode? otherPipe))
                    continue;

                ExchangeHeat(myPipe.Air, otherPipe.Air, ent.Comp.TransferFraction);
            }
        }
    }

    private void ExchangeHeat(GasMixture a, GasMixture b, float fraction)
    {
        var ca = _atmosphereSystem.GetHeatCapacity(a, true);
        var cb = _atmosphereSystem.GetHeatCapacity(b, true);
        if (ca < Atmospherics.MinimumHeatCapacity || cb < Atmospherics.MinimumHeatCapacity)
            return;

        if (a.Temperature == b.Temperature)
            return;

        var equilibrium = (a.Temperature * ca + b.Temperature * cb) / (ca + cb);
        var f = Math.Clamp(fraction, 0f, 1f);

        a.Temperature += (equilibrium - a.Temperature) * f;
        b.Temperature += (equilibrium - b.Temperature) * f;
    }
}

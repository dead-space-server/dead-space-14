using Content.Server.NodeContainer.Nodes;
using Content.Shared.NodeContainer;
using Robust.Shared.Map.Components;

namespace Content.Server.Power.Nodes
{
    [DataDefinition]
    public sealed partial class CableTerminalPortNode : Node
    {
        public override IEnumerable<Node> GetReachableNodes(
            Entity<TransformComponent> xform,
            EntityQuery<NodeContainerComponent> nodeQuery,
            EntityQuery<TransformComponent> xformQuery,
            Entity<MapGridComponent>? grid,
            IEntityManager entMan)
        {
            if (!xform.Comp.Anchored || grid is not { } gridEnt)
                yield break;

<<<<<<< ours
            var gridUid = xform.GridUid!.Value;
            var map = entMan.System<SharedMapSystem>();
            var gridIndex = map.TileIndicesFor(gridUid, grid, xform.Coordinates);
||||||| base
            var gridIndex = grid.TileIndicesFor(xform.Coordinates);
=======
            var mapSystem = entMan.System<SharedMapSystem>();
            var gridIndex = mapSystem.TileIndicesFor(gridEnt, xform.Comp.Coordinates);
>>>>>>> theirs

<<<<<<< ours
            var nodes = NodeHelpers.GetCardinalNeighborNodes(nodeQuery, map, gridUid, grid, gridIndex, includeSameTile: false);
||||||| base
            var nodes = NodeHelpers.GetCardinalNeighborNodes(nodeQuery, grid, gridIndex, includeSameTile: false);
=======
            var nodes = NodeHelpers.GetCardinalNeighborNodes(nodeQuery, gridEnt, gridIndex, mapSystem, includeSameTile: false);
>>>>>>> theirs
            foreach (var (dir, node) in nodes)
            {
                if (node is CableTerminalNode
                    && dir != Direction.Invalid
                    && xformQuery.GetComponent(node.Owner).LocalRotation.GetCardinalDir().GetOpposite() == dir)
                    yield return node;
            }
        }
    }
}

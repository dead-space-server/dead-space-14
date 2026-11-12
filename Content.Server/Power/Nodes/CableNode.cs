using Content.Server.NodeContainer.Nodes;
using Content.Shared.NodeContainer;
using Robust.Shared.Map.Components;

namespace Content.Server.Power.Nodes
{
    [DataDefinition]
    public sealed partial class CableNode : Node
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

            // While we go over adjacent nodes, we build a list of blocked directions due to
            // incoming or outgoing wire terminals.
            var terminalDirs = 0;
            List<(Direction, Node)> nodeDirs = new();

<<<<<<< ours
            foreach (var (dir, node) in NodeHelpers.GetCardinalNeighborNodes(nodeQuery, map, gridUid, grid, gridIndex))
||||||| base
            foreach (var (dir, node) in NodeHelpers.GetCardinalNeighborNodes(nodeQuery, grid, gridIndex))
=======
            foreach (var (dir, node) in NodeHelpers.GetCardinalNeighborNodes(nodeQuery, gridEnt, gridIndex, mapSystem))
>>>>>>> theirs
            {
                if (node is CableNode && node != this)
                {
                    nodeDirs.Add((dir, node));
                }

                if (node is CableDeviceNode && dir == Direction.Invalid)
                {
                    // device on same tile
                    nodeDirs.Add((Direction.Invalid, node));
                }

                if (node is CableTerminalNode)
                {
                    if (dir == Direction.Invalid)
                    {
                        // On own tile, block direction it faces
                        terminalDirs |= 1 << (int) xformQuery.GetComponent(node.Owner).LocalRotation.GetCardinalDir();
                    }
                    else
                    {
                        var terminalDir = xformQuery.GetComponent(node.Owner).LocalRotation.GetCardinalDir();
                        if (terminalDir.GetOpposite() == dir)
                        {
                            // Target tile has a terminal towards us, block the direction.
                            terminalDirs |= 1 << (int) dir;
                        }
                    }
                }
            }

            foreach (var (dir, node) in nodeDirs)
            {
                // If there is a wire terminal connecting across this direction, skip the node.
                if (dir != Direction.Invalid && (terminalDirs & (1 << (int) dir)) != 0)
                    continue;

                yield return node;
            }
        }
    }
}

using Content.Server.NodeContainer;
using Content.Server.NodeContainer.Nodes;
using Content.Shared.NodeContainer;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server.Power.Nodes
{
    [DataDefinition]
    public sealed partial class CableTerminalNode : CableDeviceNode
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

            var dir = xform.Comp.LocalRotation.GetDir();
            var targetIdx = gridIndex.Offset(dir);

<<<<<<< ours
            foreach (var node in NodeHelpers.GetNodesInTile(nodeQuery, map, gridUid, grid, targetIdx))
||||||| base
            foreach (var node in NodeHelpers.GetNodesInTile(nodeQuery, grid, targetIdx))
=======
            foreach (var node in NodeHelpers.GetNodesInTile(nodeQuery, gridEnt, targetIdx, mapSystem))
>>>>>>> theirs
            {
                if (node is CableTerminalPortNode)
                    yield return node;
            }

            foreach (var node in base.GetReachableNodes(xform, nodeQuery, xformQuery, grid, entMan))
            {
                yield return node;
            }
        }
    }
}

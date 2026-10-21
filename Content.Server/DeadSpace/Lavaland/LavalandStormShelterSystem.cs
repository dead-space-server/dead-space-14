// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Lavaland;

/// <summary>
/// The storm does not reach the tiles around this thing (the Celestial pit, the chromite caves pit): the ash is neither
/// drawn nor burns there. Done with invisible anchored weather blockers - the same as doors - so the light is untouched.
/// </summary>
[RegisterComponent]
public sealed partial class LavalandStormShelterComponent : Component
{
    /// <summary>Tiles around its own tile that the storm does not reach.</summary>
    [DataField]
    public int Radius = 2;

    [DataField]
    public EntProtoId Blocker = "LavalandStormShelterBlocker";

    [ViewVariables]
    public readonly List<EntityUid> Blockers = new();

    [ViewVariables]
    public bool Built;
}

public sealed class LavalandStormShelterSystem : EntitySystem
{
    [Dependency] private readonly SharedMapSystem _map = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LavalandStormShelterComponent, ComponentShutdown>(OnShutdown);
    }

    /// <summary>Built once it stands on a grid: pits are placed right after they are spawned.</summary>
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<LavalandStormShelterComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var shelter, out var xform))
        {
            if (shelter.Built || xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
                continue;

            shelter.Built = true;
            var center = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
            for (var dx = -shelter.Radius; dx <= shelter.Radius; dx++)
            {
                for (var dy = -shelter.Radius; dy <= shelter.Radius; dy++)
                {
                    var tile = center + new Vector2i(dx, dy);
                    var blocker = Spawn(shelter.Blocker, _map.GridTileToLocal(gridUid, grid, tile));
                    shelter.Blockers.Add(blocker);
                }
            }
        }
    }

    private void OnShutdown(Entity<LavalandStormShelterComponent> ent, ref ComponentShutdown args)
    {
        foreach (var blocker in ent.Comp.Blockers)
        {
            if (!TerminatingOrDeleted(blocker))
                QueueDel(blocker);
        }

        ent.Comp.Blockers.Clear();
    }
}

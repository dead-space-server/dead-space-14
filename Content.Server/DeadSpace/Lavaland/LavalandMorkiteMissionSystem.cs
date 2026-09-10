// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using Content.Server.DeadSpace.Lavaland.Components;
using Robust.Shared.Map;

namespace Content.Server.DeadSpace.Lavaland;

public sealed class LavalandMorkiteMissionSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Update(float frameTime)
    {
        var missions = EntityQueryEnumerator<LavalandMorkiteMissionComponent>();
        while (missions.MoveNext(out _, out var mission))
        {
            if (mission.Spawned || !TryFindLavalandOutpost(out var map, out var outpost))
                continue;
            mission.Spawned = true;
            var center = _transform.GetMapCoordinates(outpost).Position;
            var count = Math.Max(1, mission.ExtractorCount);
            for (var i = 0; i < count; i++)
            {
                var angle = Angle.FromDegrees(360f * i / count + 45f);
                var coordinates = new EntityCoordinates(map, center + angle.ToWorldVec() * mission.SpawnDistance);
                var extractor = Spawn(mission.ExtractorPrototype, coordinates);
                _transform.AnchorEntity(extractor);
            }
        }
    }

    private bool TryFindLavalandOutpost(out EntityUid map, out EntityUid outpost)
    {
        var query = EntityQueryEnumerator<LavalandOutpostComponent>();
        while (query.MoveNext(out var uid, out var marker))
        {
            if (!Exists(marker.Map))
                continue;
            map = marker.Map;
            outpost = uid;
            return true;
        }
        map = default;
        outpost = default;
        return false;
    }
}

// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Shared.DeadSpace.DeviceLinkVis;

public static class DeviceLinkVisMap
{
    public static MapId Resolve(IEntityManager entities, EntityUid uid, TransformComponent xform)
    {
        if (xform.MapID != MapId.Nullspace)
            return xform.MapID;

        var currentUid = uid;
        var current = xform;
        for (var i = 0; i < 16; i++)
        {
            if (entities.TryGetComponent(currentUid, out MapComponent? map))
                return map.MapId;

            if (!current.ParentUid.IsValid() || !entities.TryGetComponent(current.ParentUid, out TransformComponent? parent))
                return MapId.Nullspace;

            if (parent.MapID != MapId.Nullspace)
                return parent.MapID;

            currentUid = current.ParentUid;
            current = parent;
        }

        return MapId.Nullspace;
    }

    public static bool IsOnMap(IEntityManager entities, EntityUid uid, TransformComponent xform, MapId map)
    {
        if (map == MapId.Nullspace)
            return false;

        if (xform.MapID == map)
            return true;

        return xform.MapID == MapId.Nullspace && Resolve(entities, uid, xform) == map;
    }
}

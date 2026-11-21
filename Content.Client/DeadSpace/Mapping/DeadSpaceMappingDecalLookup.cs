using System.Numerics;
using Content.Shared.Decals;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.Client.DeadSpace.Mapping;

public static class DeadSpaceMappingDecalLookup
{
    public static HashSet<(uint Index, Decal Decal)> GetDecalsInRange(
        IEntityManager entityManager,
        EntityUid gridUid,
        Vector2 position,
        float distance = 0.75f,
        Func<Decal, bool>? validDelegate = null)
    {
        var decals = new HashSet<(uint Index, Decal Decal)>();

        if (!entityManager.TryGetComponent<DecalGridComponent>(gridUid, out var grid) ||
            !grid.ChunkCollection.ChunkCollection.TryGetValue(SharedDecalSystem.GetChunkIndices(position), out var chunk))
        {
            return decals;
        }

        foreach (var (index, decal) in chunk.Decals)
        {
            if ((position - decal.Coordinates - new Vector2(0.5f, 0.5f)).Length() > distance)
                continue;

            if (validDelegate == null || validDelegate(decal))
                decals.Add((index, decal));
        }

        return decals;
    }
}

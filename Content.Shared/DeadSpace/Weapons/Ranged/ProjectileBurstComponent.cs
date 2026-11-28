// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Weapons.Ranged;

/// <summary>
/// A slug that flies like a normal bullet and, once it is <see cref="Distance"/> tiles from where it was fired,
/// bursts into a fan of pellets.
/// </summary>
[RegisterComponent]
public sealed partial class ProjectileBurstComponent : Component
{
    [DataField]
    public float Distance = 6.5f;

    /// <summary>
    /// Hitscan pellet fired from the burst point.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Pellet;

    [DataField]
    public int Count = 5;

    /// <summary>
    /// Width of the pellet fan.
    /// </summary>
    [DataField]
    public Angle Spread = Angle.FromDegrees(40);

    [ViewVariables]
    public MapId OriginMap = MapId.Nullspace;

    [ViewVariables]
    public Vector2 Origin;
}

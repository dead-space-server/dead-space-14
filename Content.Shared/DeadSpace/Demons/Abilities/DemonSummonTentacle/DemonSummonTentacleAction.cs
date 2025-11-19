using Content.Shared.Actions;
using Robust.Shared.Prototypes;

namespace Content.Shared.Abilities.Demon;

public sealed partial class DemonSummonTentacleAction : WorldTargetActionEvent
{
    /// <summary>
    /// The ID of the entity that is spawned.
    /// </summary>
    [DataField]
    public EntProtoId EntityId = "EffectGoliathTentacleSpawn";

    /// <summary>
    /// Directions determining where the entities will spawn.
    /// </summary>
    [DataField]
    public List<Direction> OffsetDirections = new()
    {
        Direction.North,
    };

    /// <summary>
    /// How many entities will spawn beyond the original one at the target location?
    /// </summary>
    [DataField]
    public int ExtraSpawns = 0;
}
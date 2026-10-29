using Content.Shared.Mobs;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Halloween;

// DS14-start: vent fauna from gamerules drop halloween candies on death
/// <summary>
/// Spawns candies at the entity's position when it dies (used by vent fauna from gamerules).
/// </summary>
[RegisterComponent]
public sealed partial class CandyDropComponent : Component
{
    [DataField]
    public int Amount = 3;

    [DataField]
    public EntProtoId Candy = "HalloweenCandy";
}
// DS14-end

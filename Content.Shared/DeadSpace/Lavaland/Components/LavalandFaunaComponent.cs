using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Lavaland.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class LavalandFaunaComponent : Component
{
    /// <summary>
    /// Structural damage added to every blow: lavaland fauna break walls, doors and machines.
    /// Living targets don't take it.
    /// </summary>
    [DataField]
    public float StructuralDamage = 25f;
}

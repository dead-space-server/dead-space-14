using Robust.Shared.GameStates;

namespace Content.Shared.Damage.Components;

/// <summary>
/// Сущность нельзя вылечить: отрицательный урон (лечение) отбрасывается, а Rejuvenate не сбрасывает урон.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class HealingImmuneComponent : Component
{
}

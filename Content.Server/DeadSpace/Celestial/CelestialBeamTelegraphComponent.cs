using Robust.Shared.GameStates;

namespace Content.Server.DeadSpace.Celestial;

/// <summary>
/// Визуальный телеграф удара луча Селестиала (крупный красный луч).
/// Удаляется системой босса при ударе.
/// </summary>
[RegisterComponent]
public sealed partial class CelestialBeamTelegraphComponent : Component;

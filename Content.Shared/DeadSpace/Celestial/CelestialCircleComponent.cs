using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Celestial;

/// <summary>
/// Кружок атаки РАЗНЕСУ: полупрозрачный розовый, медленно дрейфует,
/// затем чернеет с тонкой розовой обводкой и наносит урон.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CelestialCircleComponent : Component
{
    /// <summary>Радиус кружка, метры.</summary>
    [DataField]
    public float Radius = 2.5f;

    /// <summary>Время дрейфа, сек.</summary>
    [DataField]
    public float WanderTime = 5f;

    /// <summary>Время чёрной фазы, сек.</summary>
    [DataField]
    public float DarkTime = 1.4f;

    /// <summary>Скорость дрейфа, м/с.</summary>
    [DataField]
    public float WanderSpeed = 1.5f;

    /// <summary>Как часто кружок меняет направление, сек.</summary>
    [DataField]
    public float WanderChangeInterval = 1.6f;

    /// <summary>Урон за тик в чёрной фазе.</summary>
    [DataField]
    public float DamagePerTick = 25f;

    /// <summary>Период урона, сек.</summary>
    [DataField]
    public float DamageInterval = 0.5f;

    [ViewVariables]
    public Vector2 WanderDir;

    [ViewVariables]
    public float WanderTimer;

    [ViewVariables]
    public float DamageAccumulator;

    /// <summary>Чёрная фаза уже началась (для мгновенного первого урона).</summary>
    [ViewVariables]
    public bool DarkStarted;

    [ViewVariables]
    public float Elapsed;
}

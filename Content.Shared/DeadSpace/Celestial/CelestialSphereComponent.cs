using Robust.Shared.GameStates;
using System.Numerics;

namespace Content.Shared.DeadSpace.Celestial;

/// <summary>
/// Сфера Селестиала: растёт, наносит урон всем, кто в неё попал,
/// и по истечении Lifetime исчезает (без взрыва).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CelestialSphereComponent : Component
{
    /// <summary>Начальный радиус сферы, метры.</summary>
    [DataField]
    public float StartScale = 0.3f;

    /// <summary>Конечный радиус сферы, метры.</summary>
    [DataField]
    public float EndScale = 1.6f;

    /// <summary>Степень резкости роста: < 1 — медленно потом резко.</summary>
    [DataField]
    public float GrowthExponent = 1.8f;

    /// <summary>Время жизни сферы до взрыва, сек.</summary>
    [DataField]
    public float Lifetime = 1.5f;

    /// <summary>Урон за тик всем сущностям внутри сферы.</summary>
    [DataField]
    public float DamagePerTick = 30f;

    /// <summary>Период нанесения урона, сек.</summary>
    [DataField]
    public float DamageInterval = 0.4f;

    /// <summary>Скорость подлёта сферы к игроку, м/с.</summary>
    [DataField]
    public float ChaseSpeed = 3.5f;

    /// <summary>Какую долю времени жизни сфера может лететь за игроком.</summary>
    [DataField]
    public float ChaseFraction = 0.6f;

    /// <summary>За кем летит сфера (не сериализуется, ставится при спавне).</summary>
    [ViewVariables]
    public EntityUid? Target;

    /// <summary>Сфера дошла до своей орбитальной точки и больше не двигается.</summary>
    [ViewVariables]
    public bool Arrived;

    /// <summary>Точка орбиты относительно игрока.</summary>
    [ViewVariables]
    public Vector2 OrbitOffset;

    [ViewVariables]
    public float Elapsed;

    [ViewVariables]
    public float DamageAccumulator;
}

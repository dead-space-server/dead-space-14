using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Celestial;

/// <summary>
/// Сфера Селестиала: растёт, наносит урон всем, кто в неё попал,
/// и по истечении Lifetime исчезает (без взрыва).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CelestialSphereComponent : Component
{
    /// <summary>Начальный радиус сферы, метры.</summary>
    [DataField, AutoNetworkedField]
    public float StartScale = 0.3f;

    /// <summary>Конечный радиус сферы, метры.</summary>
    [DataField, AutoNetworkedField]
    public float EndScale = 1.6f;

    /// <summary>Степень резкости роста: < 1 — медленно потом резко.</summary>
    [DataField, AutoNetworkedField]
    public float GrowthExponent = 1.8f;

    /// <summary>Время жизни сферы до исчезновения, сек.</summary>
    [DataField, AutoNetworkedField]
    public float Lifetime = 1.5f;

    /// <summary>Урон за тик всем сущностям внутри сферы.</summary>
    [DataField]
    public float DamagePerTick = 30f;

    /// <summary>Период нанесения урона, сек.</summary>
    [DataField]
    public float DamageInterval = 0.4f;

    [ViewVariables]
    public float Elapsed;

    [ViewVariables]
    public float DamageAccumulator;
}

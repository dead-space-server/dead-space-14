using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Celestial;

/// <summary>
/// Шар из трещины: петляет и быстро летит к игроку, стреляя мелкими лучами.
/// Рисуется клиентом вектором (чёрный круг с розовой обводкой).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CelestialOrbComponent : Component
{
    /// <summary>Радиус шара, метры.</summary>
    [DataField]
    public float Radius = 0.45f;

    /// <summary>Максимальное время жизни, сек.</summary>
    [DataField]
    public float Lifetime = 6f;

    /// <summary>Скорость полёта, м/с.</summary>
    [DataField]
    public float Speed = 9f;

    /// <summary>Как часто шар стреляет мелкими лучами, сек.</summary>
    [DataField]
    public float FireInterval = 0.18f;

    /// <summary>Минимальный урон мелкого луча.</summary>
    [DataField]
    public float BeamDamageMin = 15f;

    /// <summary>Максимальный урон мелкого луча.</summary>
    [DataField]
    public float BeamDamageMax = 30f;

    /// <summary>Радиус попадания мелкого луча, метры.</summary>
    [DataField]
    public float BeamRadius = 0.5f;

    /// <summary>Визуальные события шара видят только игроки на карте босса.</summary>
    [ViewVariables]
    public bool LocalizedEvents;

    /// <summary>Кому летит шар.</summary>
    [ViewVariables]
    public EntityUid? Target;

    /// <summary>Амплитуда петляния, метры.</summary>
    [DataField]
    public float WeaveAmp = 0.7f;

    /// <summary>Частота петляния.</summary>
    [DataField]
    public float WeaveFreq = 7f;

    /// <summary>Фаза петляния.</summary>
    [ViewVariables]
    public float Phase;

    /// <summary>Скорость поворота шара, рад/сек - чем меньше, тем неповоротливее и тем проще уйти.</summary>
    [DataField]
    public float TurnRate = 1.4f;

    /// <summary>Текущее направление полёта шара.</summary>
    [ViewVariables]
    public Vector2 VelocityDir;

    [ViewVariables]
    public float Elapsed;

    [ViewVariables]
    public float FireAccumulator;
}

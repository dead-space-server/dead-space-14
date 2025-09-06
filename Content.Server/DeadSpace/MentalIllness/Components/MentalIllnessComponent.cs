using Content.Shared.DeadSpace.MentalIllness;
using Robust.Shared.Audio;

namespace Content.Server.DeadSpace.MentalIllness.Components;

[RegisterComponent]
public sealed partial class MentalIllnessComponent : Component
{
    [DataField]
    [ViewVariables(VVAccess.ReadWrite)]
    public List<MentalIllnessType> ActiveIllnesses = new();

    /// <summary>
    ///     Тяжесть для каждого заболевания
    /// </summary>
    [DataField]
    [ViewVariables(VVAccess.ReadWrite)]
    public Dictionary<MentalIllnessType, float> Severity = new();

    /// <summary>
    ///     Интервалы для каждого заболевания
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public Dictionary<MentalIllnessType, TimeSpan> IllnessTickIntervals = new();

    [ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan TimeUntilUpdate = TimeSpan.Zero;

    [DataField]
    [ViewVariables(VVAccess.ReadWrite)]
    public float MinTickIntervals = 1f;

    [DataField]
    [ViewVariables(VVAccess.ReadWrite)]
    public float MaxTickIntervals = 40f;

    [DataField]
    [ViewVariables(VVAccess.ReadOnly)]
    public SoundSpecifier? SoundParanoia = new SoundCollectionSpecifier("Paranoia");

    [ViewVariables(VVAccess.ReadWrite)]
    public float MovementSpeedModifier = 1f;

    [ViewVariables(VVAccess.ReadWrite)]
    public float DepressionMovementSpeedModifier = 1f;

    [ViewVariables(VVAccess.ReadWrite)]
    public float ManiaMovementSpeedModifier = 1f;

    [ViewVariables(VVAccess.ReadWrite)]
    public float TotalMovementSpeedModifier = 1f;

    [ViewVariables(VVAccess.ReadWrite)]
    public float DamageMultiply = 1f;

    [ViewVariables(VVAccess.ReadWrite)]
    public float DepressionDamageMultiply = 1f;

    [ViewVariables(VVAccess.ReadWrite)]
    public float ManiaDamageMultiply = 1f;

    [DataField]
    [ViewVariables(VVAccess.ReadWrite)]
    public float MedicinesEffect = 0f;

    [DataField]
    [ViewVariables(VVAccess.ReadWrite)]
    public float MedicinesEffectRegen = 0.001f;

    [DataField]
    [ViewVariables(VVAccess.ReadWrite)]
    public float SeverityRegen = 0.001f;

    /// <summary>
    ///     Тяжесть при инициализации
    /// </summary>
    [DataField]
    [ViewVariables(VVAccess.ReadWrite)]
    public float SeverityOnInitialization = 0.3f;

    [DataField]
    [ViewVariables(VVAccess.ReadWrite)]
    public bool RandomOnInitialization = false;

}

// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Damage;
using Robust.Shared.Audio;

namespace Content.Server.DeadSpace.Drug.Components;

[RegisterComponent]
public sealed partial class DrugAddicationComponent : Component
{
    [DataField]
    public int DependencyLevel = 1;

    [DataField]
    public float AddictionLevel = 6;

    [DataField]
    public float Tolerance = 0.03f;

    [DataField]
    public float WithdrawalLevel = 0;

    [DataField]
    public float EffectStrengthModify = 1;

    [DataField]
    public float WithdrawalRate = 0;

    [DataField]
    public float MaxWithdrawalLvl = 0;

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public float StaminaMultiply = 0.5f;

    [DataField]
    public float TimeLastAppointment = 0;

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public float SomeThresholdTime = 0;

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public float StandartTemperature = 0;

    [DataField]
    public float AddictionLevelRegeneration = 0.5f;

    [DataField]
    public float ToleranceRegeneration = 0.01f;

    [DataField]
    public float UpdateDuration = 1f;

    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan TimeUtilUpdate = TimeSpan.Zero;

    [DataField]
    public float SendMessageDuration = 5f;

    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan TimeUtilSendMessage = TimeSpan.Zero;

    [DataField]
    public float ChangeAddictionDuration = 30f;

    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan TimeUtilChangeAddiction = TimeSpan.Zero;

    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan DurationOfActionWeakDrug = TimeSpan.Zero;

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public bool IsTimeSendMessage = true;

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public bool IsTakeWeakDrug = false;

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public bool IsStaminaEdit = false;

    [DataField("damage")]
    public DamageSpecifier Damage = new()
    {
        DamageDict = new()
        {
            { "Asphyxiation", 2.5 }
        }
    };

    [DataField]
    [ViewVariables(VVAccess.ReadOnly)]
    public SoundSpecifier? SoundHighEffect = new SoundPathSpecifier("/Audio/DeadSpace/Drug/high_effect.ogg");

}

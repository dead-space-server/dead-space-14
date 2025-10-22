// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Prototypes;
using Content.Shared.Damage;

namespace Content.Server.DeadSpace.Asthma.Components;

[RegisterComponent]
public sealed partial class AsthmaComponent : Component
{
    [DataField]
    public bool IsAsthmaAttack = false;

    /// <summary>
    ///     Шанс получить оглушение при приступе.
    /// </summary>
    [DataField]
    public float StunChance = 0.3f;

    /// <summary>
    ///     Уменьшение скорости при приступе.
    /// </summary>
    [DataField]
    public float SpeedDebuff = 0.8f;

    /// <summary>
    ///     Текущее изменение скорости.
    /// </summary>
    [DataField]
    public float MovementSpeedMultiplier = 1f;

    /// <summary>
    ///     Время до начала следующий атаки приступа.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public TimedWindow TimeToStartAsthmaAttack = new(1f, 3f);

    /// <summary>
    ///     Время до начала присупа.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public TimedWindow TimeToStartEffect = new(360f, 900f);

    /// <summary>
    ///     При ударе уменьшается время до приступа астмы.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public TimedWindow AsthmaAttackTimeReductionOnMelee = new(1f, 25f);

    /// <summary>
    ///     При получении урона уменьшается время до приступа астмы.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public TimedWindow AsthmaAttackTimeReductionOnDamageChanged = new(1f, 60f);

    /// <summary>
    ///     Дополнительный урон от астмы.
    /// </summary>
    [DataField("damage")]
    public DamageSpecifier AdditionalDamage = new()
    {
        DamageDict = new()
        {
            { "Asphyxiation", 1 }
        }
    };

}

[DataDefinition]
public partial struct TimedWindow
{
    [DataField("min")]
    [ViewVariables(VVAccess.ReadWrite)]
    public float MinSeconds;

    [DataField("max")]
    [ViewVariables(VVAccess.ReadWrite)]
    public float MaxSeconds;

    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan Remaining = TimeSpan.Zero;

    public TimedWindow(float minSeconds, float maxSeconds)
    {
        MinSeconds = minSeconds;
        MaxSeconds = maxSeconds;
        Remaining = TimeSpan.Zero;
    }
}

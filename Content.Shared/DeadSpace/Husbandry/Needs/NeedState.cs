// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.Damage;

namespace Content.Shared.DeadSpace.Husbandry.Needs;

/// <summary>
/// One need of an animal, see <see cref="AnimalNeedsComponent"/>.
/// </summary>
[DataDefinition]
public sealed partial class NeedState
{
    /// <summary>
    /// Current value. A negative number means it is rolled from <see cref="StartingRange"/> on map init.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float Value = -1f;

    [DataField]
    public float Max = 100f;

    /// <summary>
    /// Used when <see cref="Value"/> is not defined explicitly.
    /// </summary>
    [DataField]
    public Vector2 StartingRange = new(70f, 100f);

    [DataField]
    public float DecayPerMinute = 1f;

    /// <summary>
    /// At this value or lower the animal goes looking for food or water.
    /// </summary>
    [DataField]
    public float SeekBelow = 50f;

    /// <summary>
    /// Damage per minute while the need is empty.
    /// </summary>
    [DataField]
    public DamageSpecifier DeprivationDamage = new();

    /// <summary>
    /// Minutes spent empty since damage was applied last.
    /// </summary>
    [ViewVariables]
    public float DeprivedMinutes;

    [ViewVariables]
    public NeedLevel GetLevel()
    {
        if (Value <= 0f)
            return NeedLevel.Empty;

        return Value <= SeekBelow ? NeedLevel.Low : NeedLevel.Satisfied;
    }
}

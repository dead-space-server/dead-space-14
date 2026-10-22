using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.Shared.DeadSpace.Destructible;

/// <summary>
/// Маркер на шлюзе, означающий, что при взрыве он должен «выбиваться» на две половины.
/// Хранит последний эпицентр взрыва, чтобы поведение знало направление вылета.
/// </summary>
[RegisterComponent]
public sealed partial class ExplosionKnockoutComponent : Component
{
    /// <summary>
    /// Позиция последнего взрыва, задевшего эту сущность.
    /// </summary>
    [ViewVariables]
    public MapCoordinates? RecentEpicenter;

    /// <summary>
    /// Когда истекает сохранённый эпицентр (тик + запас).
    /// </summary>
    [ViewVariables]
    public TimeSpan EpicenterExpiresAt;
}

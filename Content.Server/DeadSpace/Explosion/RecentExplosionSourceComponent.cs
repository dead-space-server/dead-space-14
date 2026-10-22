using Robust.Shared.GameObjects;

namespace Content.Server.DeadSpace.Explosion;

/// <summary>
/// Временно сохраняет источник последнего взрыва для сущности.
/// Используется для определения направления выбивания шлюзов.
/// </summary>
[RegisterComponent]
public sealed partial class RecentExplosionSourceComponent : Component
{
    [DataField]
    public EntityUid Source { get; set; }

    [DataField]
    public TimeSpan ExpiresAt { get; set; }

    [DataField]
    public float Lifetime { get; set; } = 2f;
}

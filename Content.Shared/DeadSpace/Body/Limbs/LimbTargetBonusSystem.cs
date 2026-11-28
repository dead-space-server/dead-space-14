// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Sandevistan;

namespace Content.Shared.DeadSpace.Body.Limbs;

public sealed partial class LimbTargetBonusSystem : EntitySystem
{
    [Dependency] private SharedSandevistanSystem _sandevistan = default!;

    public float GetChance(EntityUid uid)
    {
        if (!TryComp<LimbTargetBonusComponent>(uid, out var bonus))
            return 0f;

        var chance = _sandevistan.IsActive(uid) ? bonus.ActiveChance : bonus.PassiveChance;
        return Math.Clamp(chance, 0f, 1f);
    }
}

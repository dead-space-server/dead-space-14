// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Atmos.Rotting;
using Content.Shared.DeadSpace.Body.Limbs;

namespace Content.Shared.DeadSpace.Surgery;

public static class SurgeryRot
{
    public const float Limit = 0.9f;

    public static float Brain(LimbBodyComponent limbs)
    {
        var index = (int) LimbOrgan.Brain;
        if ((limbs.Present & LimbOrganMask.Brain) == 0 || (uint) index >= (uint) limbs.Organs.Count)
            return 0f;

        var brain = limbs.Organs[index];
        if (brain.Missing)
            return 0f;

        return brain.Ruptured ? 1f : Math.Clamp(brain.Damage / LimbOrganRules.Rupture, 0f, 1f);
    }

    public static float Body(IEntityManager entities, EntityUid patient)
    {
        if (!entities.TryGetComponent<PerishableComponent>(patient, out var perishable) || perishable.RotAfter <= TimeSpan.Zero)
            return 0f;

        if (entities.HasComponent<RottingComponent>(patient))
            return 1f;

        return Math.Clamp((float) (perishable.RotAccumulator / perishable.RotAfter), 0f, 1f);
    }

    public static bool Treatable(IEntityManager entities, EntityUid patient, LimbZone zone, LimbBodyComponent limbs)
    {
        var brain = Brain(limbs);
        if (brain >= Limit)
            return false;

        return zone switch
        {
            LimbZone.Head => brain * LimbOrganRules.Rupture >= SurgeryConditions.OrganCrackMin,
            LimbZone.Torso => Body(entities, patient) > 0.05f,
            _ => false,
        };
    }
}

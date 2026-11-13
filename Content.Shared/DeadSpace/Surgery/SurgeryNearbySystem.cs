// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.Hands.EntitySystems;

namespace Content.Shared.DeadSpace.Surgery;

public sealed partial class SurgeryNearbySystem : EntitySystem
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private IComponentFactory _factory = default!;
    [Dependency] private SharedHandsSystem _hands = default!;

    public bool Has(EntityUid patient, SurgeryOperationPrototype operation, EntityUid? user = null)
    {
        foreach (var name in operation.Nearby)
        {
            if (!_factory.TryGetRegistration(name, out var registration))
                return false;

            var type = registration.Type;
            if (!Find(patient, user, operation.NearbyRange, near => HasComp(near, type)))
                return false;
        }

        if (operation.Implant.Length > 0 && operation.ImplantPut)
        {
            var slot = operation.Implant;
            return Find(patient, user, operation.NearbyRange,
                near => TryComp<LimbImplantComponent>(near, out var implant) && implant.Host == null && implant.Slots.Contains(slot));
        }

        return true;
    }

    private bool Find(EntityUid patient, EntityUid? user, float range, Func<EntityUid, bool> wanted)
    {
        if (user != null)
        {
            foreach (var held in _hands.EnumerateHeld(user.Value))
            {
                if (wanted(held))
                    return true;
            }
        }

        foreach (var near in _lookup.GetEntitiesInRange(Transform(patient).Coordinates, range))
        {
            if (near != patient && wanted(near))
                return true;
        }

        return false;
    }
}

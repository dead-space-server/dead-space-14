// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Ninja.Systems;
using Content.Shared.Physics;
using Content.Shared.PowerCell;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Server.DeadSpace.Ninja.Systems;

public sealed class NinjaSpiritFormSystem : SharedNinjaSpiritFormSystem
{
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!;

    private readonly Dictionary<EntityUid, Dictionary<string, int>> _originalMasks = new();

    protected override void SetSpiritCollision(
        EntityUid user,
        bool phasing)
    {
        if (!TryComp<PhysicsComponent>(user, out var physics) || !TryComp<FixturesComponent>(user, out var fixtures))
        {
            if (!phasing)
                _originalMasks.Remove(user);

            return;
        }

        if (phasing && !_originalMasks.ContainsKey(user))
            _originalMasks[user] = new Dictionary<string, int>(fixtures.Fixtures.Count);

        foreach (var (id, fixture) in fixtures.Fixtures)
        {
            if (phasing)
            {
                _originalMasks[user][id] = fixture.CollisionMask;

                var mask = fixture.CollisionMask
                            & ~(int)CollisionGroup.Impassable
                            & ~(int)CollisionGroup.MidImpassable
                            & ~(int)CollisionGroup.HighImpassable
                            & ~(int)CollisionGroup.LowImpassable;

                _physics.SetCollisionMask(
                    user,
                    id,
                    fixture,
                    mask,
                    fixtures,
                    physics);
            }
            else if (_originalMasks.TryGetValue(user, out var masks)
                     && masks.TryGetValue(id, out var originalMask))
            {
                _physics.SetCollisionMask(
                    user,
                    id,
                    fixture,
                    originalMask,
                    fixtures,
                    physics);
            }
        }

        if (!phasing)
            _originalMasks.Remove(user);
    }

    protected override bool TryGetSuitBatteryMax(
        EntityUid suit,
        out float maxCharge)
    {
        maxCharge = 0f;

        if (!_powerCell.TryGetBatteryFromSlot(suit, out var battery))
            return false;

        maxCharge = battery.Value.Comp.MaxCharge;
        return true;
    }
}
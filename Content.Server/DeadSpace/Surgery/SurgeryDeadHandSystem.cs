// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory.VirtualItem;

namespace Content.Server.DeadSpace.Surgery;

public sealed class SurgeryDeadHandSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedVirtualItemSystem _virtual = default!;

    private float _accumulator;

    public override void Update(float frameTime)
    {
        _accumulator += frameTime;
        if (_accumulator < 1f)
            return;

        _accumulator = 0f;
        var query = EntityQueryEnumerator<LimbBodyComponent, HandsComponent>();
        while (query.MoveNext(out var uid, out var limbs, out var hands))
        {
            foreach (var handId in _hands.EnumerateHands((uid, hands)))
            {
                if (!_hands.TryGetHand((uid, hands), handId, out var hand) || hand.Value.Location == HandLocation.Middle)
                    continue;

                var left = hand.Value.Location == HandLocation.Left;
                var dead = Dead(limbs, left ? LimbZone.ArmLeft : LimbZone.ArmRight) || Dead(limbs, left ? LimbZone.HandLeft : LimbZone.HandRight);
                _hands.TryGetHeldItem((uid, hands), handId, out var held);
                var blocked = held is { } item && TryComp<VirtualItemComponent>(item, out var virt) && virt.BlockingEntity == uid;
                if (dead && !blocked)
                {
                    if (held != null && !_hands.TryDrop((uid, hands), handId, checkActionBlocker: false))
                        continue;

                    _hands.TryGetHeldItem((uid, hands), handId, out held);
                    if (held == null)
                        _virtual.TrySpawnVirtualItemInHand(uid, uid, out _, false, handId, true);
                }
                else if (!dead && blocked && TryComp<VirtualItemComponent>(held, out var stale))
                {
                    _virtual.DeleteVirtualItem((held!.Value, stale), uid);
                }
            }
        }
    }

    private static bool Dead(LimbBodyComponent limbs, LimbZone zone)
    {
        var index = LimbWoundRules.Index(zone);
        return (uint) index < (uint) limbs.Zones.Count && limbs.Zones[index] is { Atrophied: true, Severed: false };
    }
}

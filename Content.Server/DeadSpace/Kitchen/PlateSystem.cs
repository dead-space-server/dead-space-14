// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using Content.Server.Destructible;
using Content.Server.Destructible.Thresholds.Behaviors;
using Content.Server.Hands.Systems;
using Content.Server.Storage.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DeadSpace.Kitchen.Components;
using Content.Shared.Destructible;
using Content.Shared.Hands.Components;
using Content.Shared.Storage;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Map;

namespace Content.Server.DeadSpace.Kitchen;

public sealed partial class PlateSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private HandsSystem _hands = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private StorageSystem _storage = default!;
    [Dependency] private TransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlateComponent, DamageThresholdReached>(OnDamageThresholdReached);
    }

    private void OnDamageThresholdReached(Entity<PlateComponent> ent, ref DamageThresholdReached args)
    {
        if (!IsBreakingThreshold(args))
            return;

        if (!TryComp<ItemSlotsComponent>(ent.Owner, out var itemSlots) ||
            !_itemSlots.TryGetSlot(ent.Owner, ent.Comp.SlotId, out var slot, itemSlots) ||
            slot.ContainerSlot?.ContainedEntity is not { } content)
        {
            return;
        }

        _containers.TryGetContainingContainer(ent.Owner, out var plateContainer);
        var dropCoordinates = GetDropCoordinates(ent.Owner);

        if (plateContainer != null &&
            TryComp<StorageComponent>(plateContainer.Owner, out var storage) &&
            plateContainer.ID == StorageComponent.ContainerId)
        {
            _containers.Remove(ent.Owner, plateContainer, destination: dropCoordinates);

            if (_storage.Insert(plateContainer.Owner, content, out _, storageComp: storage, playSound: false))
                return;
        }
        else if (plateContainer != null &&
                 TryComp<HandsComponent>(plateContainer.Owner, out var hands) &&
                 _hands.TryPickupAnyHand(
                     plateContainer.Owner,
                     content,
                     checkActionBlocker: false,
                     animate: false,
                     handsComp: hands))
        {
            return;
        }

        _containers.Remove(content, slot.ContainerSlot, destination: dropCoordinates);
    }

    private static bool IsBreakingThreshold(DamageThresholdReached args)
    {
        return args.Threshold.Behaviors.Any(behavior =>
            behavior is DoActsBehavior acts &&
            acts.HasAct(ThresholdActs.Breakage | ThresholdActs.Destruction));
    }

    private EntityCoordinates GetDropCoordinates(EntityUid plate)
    {
        if (TryGetCarrier(plate, out var carrier))
            return _transform.GetMoverCoordinates(carrier);

        return _transform.GetMoverCoordinates(plate);
    }

    private bool TryGetCarrier(EntityUid entity, out EntityUid carrier)
    {
        carrier = EntityUid.Invalid;
        var current = entity;

        while (_containers.TryGetContainingContainer(current, out var container))
        {
            current = container.Owner;

            if (HasComp<HandsComponent>(current))
            {
                carrier = current;
                return true;
            }
        }

        return false;
    }
}

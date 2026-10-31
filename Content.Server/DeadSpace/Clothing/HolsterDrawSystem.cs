// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Actions;
using Content.Shared.Clothing;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Storage;
using Content.Shared.Inventory;
using Robust.Shared.Containers;
using Content.Shared.Hands.Components;
using System.Linq;
using Content.Shared.Popups;


namespace Content.Server.Clothing;

public sealed class HolsterDrawSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HolsterDrawComponent, GetItemActionsEvent>(OnGetActions);
        SubscribeLocalEvent<HolsterDrawComponent, HolsterDrawActionEvent>(OnDrawAction);
    }

    private void OnGetActions(Entity<HolsterDrawComponent> ent, ref GetItemActionsEvent args)
    {
        if (!_inventory.TryGetSlotEntity(args.User, "belt", out var beltEntity)
            || beltEntity != ent.Owner)
            return;

        args.AddAction(ref ent.Comp.ActionEntity, ent.Comp.ActionProto.Id, ent.Owner);
    }

    private void OnDrawAction(Entity<HolsterDrawComponent> ent, ref HolsterDrawActionEvent args)
    {
        if (args.Handled)
            return;

        var user = args.Performer;

        if (!TryComp<StorageComponent>(ent, out var storage))
            return;
        var targetProtoId = ent.Comp.WeaponProto.Id;
        var revolvers = new List<EntityUid>();
        foreach (var item in storage.Container.ContainedEntities)
        {
            if (MetaData(item).EntityPrototype?.ID == targetProtoId)
                revolvers.Add(item);
        }
        if (revolvers.Count == 0)
            return;
        if (!TryComp<HandsComponent>(user, out var hands))
            return;

        var freeHandIds = new List<string>();
        foreach (var handId in hands.Hands.Keys)
        {
            if (_hands.HandIsEmpty(user, handId))
                freeHandIds.Add(handId);
        }

        if (freeHandIds.Count == 0)
        {
            _popup.PopupEntity(
                "Все ваши руки заняты! Вы не можете достать револьверы",
                user,
                user);
            return;
        }

        var orderedHandIds = new List<string>(freeHandIds.Count);

        var activeHandId = _hands.GetActiveHand((user, hands));

        if (activeHandId != null && freeHandIds.Contains(activeHandId))
        {
            orderedHandIds.Add(activeHandId);
            foreach (var handId in freeHandIds)
            {
                if (handId != activeHandId)
                    orderedHandIds.Add(handId);
            }
        }
        else
        {
            orderedHandIds.AddRange(freeHandIds);
        }

        var drawnCount = 0;

        for (var i = 0; i < orderedHandIds.Count; i++)
        {
            if (drawnCount >= revolvers.Count)
                break;

            var rev = revolvers[drawnCount];
            var handId = orderedHandIds[i];

            if (!_container.Remove(rev, storage.Container))
                continue;

            if (_hands.TryPickup(user, rev, handId))
            {
                drawnCount++;
            }
            else
            {
                _container.Insert(rev, storage.Container);
                break;
            }
        }

        args.Handled = true;
    }
}
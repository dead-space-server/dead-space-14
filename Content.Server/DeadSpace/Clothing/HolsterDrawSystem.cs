// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Actions;
using Content.Shared.DeadSpace.Clothing;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared.Storage;

namespace Content.Server.DeadSpace.Clothing;

public sealed partial class HolsterDrawSystem : EntitySystem
{
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HolsterDrawComponent, GetItemActionsEvent>(OnGetActions);
        SubscribeLocalEvent<HolsterDrawComponent, HolsterDrawActionEvent>(OnDrawAction);
    }

    private void OnGetActions(Entity<HolsterDrawComponent> ent, ref GetItemActionsEvent args)
    {
        if (args.SlotFlags != SlotFlags.BELT)
            return;

        args.AddAction(ref ent.Comp.ActionEntity, ent.Comp.ActionProto.Id, ent.Owner);
    }

    private void OnDrawAction(Entity<HolsterDrawComponent> ent, ref HolsterDrawActionEvent args)
    {
        if (args.Handled)
            return;

        var user = args.Performer;

        if (!TryComp<StorageComponent>(ent, out var storage) ||
            !TryComp<HandsComponent>(user, out var hands))
            return;

        var weapons = new List<EntityUid>();
        foreach (var item in storage.Container.ContainedEntities)
        {
            if (MetaData(item).EntityPrototype?.ID == ent.Comp.WeaponProto.Id)
                weapons.Add(item);
        }

        if (weapons.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("holster-draw-empty"), user, user);
            return;
        }

        var activeHand = _hands.GetActiveHand((user, hands));
        var freeHands = new List<string>();

        if (activeHand != null && _hands.HandIsEmpty((user, hands), activeHand))
            freeHands.Add(activeHand);

        foreach (var handId in hands.Hands.Keys)
        {
            if (handId != activeHand && _hands.HandIsEmpty((user, hands), handId))
                freeHands.Add(handId);
        }

        if (freeHands.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("holster-draw-hands-full"), user, user);
            return;
        }

        var drawn = 0;

        foreach (var handId in freeHands)
        {
            if (drawn >= weapons.Count)
                break;

            if (_hands.TryPickup(user, weapons[drawn], handId))
                drawn++;
        }

        args.Handled = drawn > 0;
    }
}
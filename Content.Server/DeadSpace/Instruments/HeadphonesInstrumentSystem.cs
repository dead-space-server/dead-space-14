// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Instruments;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Inventory.Events;
using Content.Shared.Popups;
using Content.Server.Actions;

namespace Content.Server.DeadSpace.Instruments;

public sealed partial class HeadphonesInstrumentSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private ActionsSystem _actions = default!;
    [Dependency] private ItemToggleSystem _itemToggle = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HeadphonesInstrumentComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<HeadphonesInstrumentComponent, GotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<HeadphonesInstrumentComponent, ItemToggledEvent>(OnItemToggled);

        SubscribeLocalEvent<NoiseCancellingWearerComponent, ToggleNoiseCancellingActionEvent>(OnActionToggled);
    }

    private void OnEquipped(EntityUid uid, HeadphonesInstrumentComponent comp, ref GotEquippedEvent args)
    {
        var wearerComp = EnsureComp<NoiseCancellingWearerComponent>(args.Equipee);
        var wasActive = HasActiveHeadphones(wearerComp);
        wearerComp.Headphones.Add(uid);

        _actions.AddAction(args.Equipee, ref comp.ActionEntity, comp.NoiseCancellingAction);

        if (!wasActive && TryComp<ItemToggleComponent>(uid, out var toggle) && toggle.Activated)
            ApplyNoiseCancelling(args.Equipee);
    }

    private void OnUnequipped(EntityUid uid, HeadphonesInstrumentComponent comp, ref GotUnequippedEvent args)
    {
        var wasActive = TryComp<ItemToggleComponent>(uid, out var toggle) && toggle.Activated;

        if (TryComp<NoiseCancellingWearerComponent>(args.Equipee, out var wearerComp))
        {
            wearerComp.Headphones.Remove(uid);

            if (wasActive && !HasActiveHeadphones(wearerComp))
                RemoveNoiseCancelling(args.Equipee);

            if (wearerComp.Headphones.Count == 0)
                RemComp<NoiseCancellingWearerComponent>(args.Equipee);
        }

        _actions.RemoveAction(args.Equipee, comp.ActionEntity);
        comp.ActionEntity = null;
    }

    private void OnActionToggled(EntityUid uid, NoiseCancellingWearerComponent comp, ToggleNoiseCancellingActionEvent args)
    {
        if (!TryGetHeadphonesForAction(comp, args.Action.Owner, out var headphones))
            return;

        _itemToggle.Toggle(headphones, uid);
        args.Handled = true;
    }

    private void OnItemToggled(EntityUid uid, HeadphonesInstrumentComponent comp, ref ItemToggledEvent args)
    {
        if (!TryGetWearer(uid, out var wearer, out var wearerComp))
            return;

        if (args.Activated)
            ApplyNoiseCancelling(wearer);
        else if (!HasActiveHeadphones(wearerComp))
            RemoveNoiseCancelling(wearer);
    }

    private void ApplyNoiseCancelling(EntityUid wearer)
    {
        _popup.PopupEntity(Loc.GetString("headphones-noise-cancelling-enabled"), wearer, wearer);
    }

    private void RemoveNoiseCancelling(EntityUid wearer)
    {
        _popup.PopupEntity(Loc.GetString("headphones-noise-cancelling-disabled"), wearer, wearer);
    }

    private bool HasActiveHeadphones(NoiseCancellingWearerComponent comp)
    {
        foreach (var headphones in comp.Headphones)
        {
            if (TryComp<ItemToggleComponent>(headphones, out var toggle) && toggle.Activated)
                return true;
        }

        return false;
    }

    private bool TryGetHeadphonesForAction(
        NoiseCancellingWearerComponent comp,
        EntityUid action,
        out EntityUid headphones)
    {
        foreach (var uid in comp.Headphones)
        {
            if (TryComp<HeadphonesInstrumentComponent>(uid, out var headphonesComp) &&
                headphonesComp.ActionEntity == action)
            {
                headphones = uid;
                return true;
            }
        }

        headphones = default;
        return false;
    }

    private bool TryGetWearer(
        EntityUid headphones,
        out EntityUid wearer,
        out NoiseCancellingWearerComponent wearerComp)
    {
        wearer = Transform(headphones).ParentUid;
        if (wearer.Valid &&
            TryComp<NoiseCancellingWearerComponent>(wearer, out var parentWearerComp) &&
            parentWearerComp.Headphones.Contains(headphones))
        {
            wearerComp = parentWearerComp;
            return true;
        }

        var query = EntityQueryEnumerator<NoiseCancellingWearerComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.Headphones.Contains(headphones))
                continue;

            wearer = uid;
            wearerComp = comp;
            return true;
        }

        wearer = default;
        wearerComp = default!;
        return false;
    }
}

// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using Content.Server.DeadSpace.Arkalyse.Components;
using Content.Server.DeadSpace.MartialArts.Arkalyse.Components;
using Content.Shared.Actions;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;

namespace Content.Server.DeadSpace.Arkalyse;

public sealed partial class ArkalyseGlovesSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ArkalyseGlovesComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<ArkalyseGlovesComponent, GotUnequippedEvent>(OnUnequipped);
    }

    private void OnEquipped(Entity<ArkalyseGlovesComponent> ent, ref GotEquippedEvent args)
    {
        if (args.SlotFlags != SlotFlags.GLOVES)
            return;

        if (TryComp<ArkalyseComponent>(args.Equipee, out var existing) && existing.LearnedFromManual)
        {
            ent.Comp.AddedArkalyseComponent = false;
            return;
        }

        ent.Comp.AddedArkalyseComponent = !HasComp<ArkalyseComponent>(args.Equipee);
        var arkalyse = EnsureComp<ArkalyseComponent>(args.Equipee);
        arkalyse.Params = ent.Comp.Params;

        ent.Comp.GrantedActions.Clear();
        foreach (var protoId in arkalyse.BaseArkalyse)
        {
            // Стандартная сигнатура SharedActionsSystem на сервере
            EntityUid? actionId = null;
            _actions.AddAction(args.Equipee, ref actionId, protoId);

            if (actionId.HasValue)
                ent.Comp.GrantedActions.Add(actionId.Value);
        }
    }

    private void OnUnequipped(Entity<ArkalyseGlovesComponent> ent, ref GotUnequippedEvent args)
    {
        if (args.SlotFlags != SlotFlags.GLOVES)
            return;

        foreach (var actionId in ent.Comp.GrantedActions)
            _actions.RemoveAction(args.Equipee, actionId);

        ent.Comp.GrantedActions.Clear();

        if (ent.Comp.AddedArkalyseComponent)
            RemComp<ArkalyseComponent>(args.Equipee);
    }
}
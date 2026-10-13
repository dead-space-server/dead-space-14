// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Diagnostics.CodeAnalysis;
using Content.Shared.Actions.Components;
using Content.Shared.DeadSpace.Ninja.Components;
using Content.Shared.Interaction;
using Content.Shared.Charges.Systems;
using Content.Shared.Charges.Components;
using Content.Shared.Stacks;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Ninja.Systems;

public sealed class NinjaSuitRefillSystem : EntitySystem
{
    [Dependency] private readonly SharedChargesSystem _charges = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaSuitRefillComponent, InteractUsingEvent>(OnInteractUsing);
    }

    private void OnInteractUsing(Entity<NinjaSuitRefillComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        // Проверяем что предмет это стак
        if (!TryComp<StackComponent>(args.Used, out var stack))
            return;

        foreach (var (actionProtoId, cost) in ent.Comp.ActionMaterials)
        {
            if (stack.StackTypeId != cost.Stack)
                continue;

            if (!TryFindAction(ent.Owner, actionProtoId, out var actionUid))
                continue;

            if (!TryComp<LimitedChargesComponent>(actionUid, out var charges))
                continue;

            if (charges.LastCharges >= charges.MaxCharges)
                continue;

            if (stack.Count < cost.Amount)
                continue;

            if (!_stack.TryUse(args.Used, cost.Amount))
                continue;

            _charges.AddCharges((actionUid, charges), 1);
            _popup.PopupEntity(Loc.GetString("ninja-action-refill", ("action", MetaData(actionUid).EntityName)), args.User, args.User, PopupType.Small);
            args.Handled = true;
        }
    }

    private bool TryFindAction(EntityUid suitUid, EntProtoId actionProto, [NotNullWhen(true)] out EntityUid actionUid)
    {
        actionUid = default;
        if (!TryComp<ActionsContainerComponent>(suitUid, out var container))
            return false;

        foreach (var contained in container.Container.ContainedEntities)
        {
            if (MetaData(contained).EntityPrototype?.ID == actionProto.Id)
            {
                actionUid = contained;
                return true;
            }
        }

        return false;
    }
}
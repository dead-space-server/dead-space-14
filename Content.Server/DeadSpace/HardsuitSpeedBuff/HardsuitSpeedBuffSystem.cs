// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.HardsuitSpeedBuff;
using Content.Shared.PowerCell;
using Content.Shared.PowerCell.Components;
using Content.Shared.Clothing;
using Content.Shared.Movement.Systems;
using Content.Shared.Actions;

namespace Content.Server.DeadSpace.HardsuitSpeedBuff;

public sealed partial class HardsuitSpeedBuffSystem : EntitySystem
{
    [Dependency] private MovementSpeedModifierSystem _movement = default!;
    [Dependency] private PowerCellSystem _cell = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<HardsuitSpeedBuffComponent, GetItemActionsEvent>(OnGetActions);
        SubscribeLocalEvent<HardsuitSpeedBuffComponent, ActivateSpeedBuffActionEvent>(OnSpeedBuffActivate);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<HardsuitSpeedBuffComponent, PowerCellDrawComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var comp, out _, out var xform))
        {
            if (!comp.Activated)
                continue;

            if (!_cell.HasCharge(uid, 10))
            {
                comp.Activated = false;
                Dirty(uid, comp);
                _movement.RefreshMovementSpeedModifiers(xform.ParentUid);
            }
        }
    }

    private void OnGetActions(EntityUid uid, HardsuitSpeedBuffComponent comp, GetItemActionsEvent args)
    {
        args.AddAction(ref comp.ActionEntity, comp.Action);
    }

    private void OnSpeedBuffActivate(Entity<HardsuitSpeedBuffComponent> ent, ref ActivateSpeedBuffActionEvent args)
    {
        if (!TryComp<PowerCellDrawComponent>(ent.Owner, out var powerCell))
            return;

        if (!TryComp<ClothingSpeedModifierComponent>(ent.Owner, out _))
            return;

        if (powerCell.Enabled)
        {
            _cell.SetDrawEnabled((ent.Owner, powerCell), false);
            ent.Comp.Activated = false;
            Dirty(ent);
            _movement.RefreshMovementSpeedModifiers(args.Performer);
        }
        else
        {
            _cell.SetDrawEnabled((ent.Owner, powerCell), true);
            ent.Comp.Activated = true;
            Dirty(ent);
            _movement.RefreshMovementSpeedModifiers(args.Performer);
        }
    }
}

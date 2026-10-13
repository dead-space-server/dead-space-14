// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Emp;
using Content.Shared.Actions;
using Content.Shared.DeadSpace.Ninja.Components;
using Content.Shared.Popups;
using Robust.Shared.Network;
using Content.Shared.Ninja.Systems;

namespace Content.Shared.DeadSpace.Ninja.Systems;

public sealed class SharedNinjaEmpAbilitySystem : EntitySystem
{
    [Dependency] private readonly ActionContainerSystem _actionContainer = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedEmpSystem _emp = default!;
    [Dependency] private readonly SharedSpaceNinjaSystem _ninja = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly INetManager _net = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaEmpAbilityComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<NinjaEmpAbilityComponent, GetItemActionsEvent>(OnGetActions);
        SubscribeLocalEvent<NinjaEmpAbilityComponent, NinjaEmpEvent>(OnEmp);

        SubscribeLocalEvent<NinjaEmpAbilityComponent, SpiderOSPowerChangedEvent>(OnSpiderOSPowerChanged);
    }

    private void OnMapInit(Entity<NinjaEmpAbilityComponent> ent, ref MapInitEvent args)
    {
        var (uid, comp) = ent;
        _actionContainer.EnsureAction(uid, ref comp.EmpActionEntity, comp.EmpAction);
        Dirty(uid, comp);
    }

    private void OnGetActions(Entity<NinjaEmpAbilityComponent> ent, ref GetItemActionsEvent args)
    {
        if (args.InHands)
            return;

        if (!TryComp<SpiderOSComponent>(ent.Owner, out var os) || !os.SuitActivated)
            return;

        args.AddAction(ent.Comp.EmpActionEntity);
    }

    private void OnSpiderOSPowerChanged(Entity<NinjaEmpAbilityComponent> ent, ref SpiderOSPowerChangedEvent args)
    {
        if (!args.Activated)
        {
            _actions.RemoveAction(ent.Comp.EmpActionEntity);
        }
    }

    private void OnEmp(Entity<NinjaEmpAbilityComponent> ent, ref NinjaEmpEvent args)
    {
        if (_net.IsClient)
            return;

        if (!_ninja.TryUseCharge(args.Performer, ent.Comp.Charge))
        {
            _popup.PopupEntity(Loc.GetString("ninja-no-power"), args.Performer, args.Performer);
            return;
        }

        args.Handled = true;
        var (uid, comp) = ent;
        _emp.EmpPulse(Transform(uid).Coordinates, comp.EmpRange, comp.EmpConsumption, comp.EmpDuration, args.Performer, false);
    }
}

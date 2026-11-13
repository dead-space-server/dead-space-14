// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Actions;
using Content.Shared.DeadSpace.Necromorphs.Unitology;
using Content.Server.DeadSpace.Necromorphs.Unitology.Components;
using Content.Shared.DoAfter;
using Content.Server.GameTicking.Rules.Components;
using Content.Shared.DeadSpace.Necromorphs.Necroobelisk;
using Content.Shared.GameTicking;
using Content.Server.DeadSpace.Necromorphs.Necroobelisk;

namespace Content.Server.DeadSpace.Necromorphs.Unitology;

public sealed partial class ObeliskActivateAbilitySystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actionsSystem = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private NecroobeliskSystem _necroobeliskSystem = default!;
    private bool _canActiveObelisk = true;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ObeliskActivateAbilityComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<ObeliskActivateAbilityComponent, ComponentShutdown>(OnShutDown);
        SubscribeLocalEvent<ObeliskActivateAbilityComponent, ObeliskActivateActionEvent>(OnObeliskActivate);
        SubscribeLocalEvent<ObeliskActivateAbilityComponent, ObeliskActivateDoAfterEvent>(OnObeliskActivateDoAfter);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);

    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _canActiveObelisk = true;
    }

    private void OnComponentInit(EntityUid uid, ObeliskActivateAbilityComponent component, ComponentInit args)
    {
        _actionsSystem.AddAction(uid, ref component.ActionObeliskActivateEntity, component.ActionObeliskActivate, uid);
    }

    private void OnShutDown(EntityUid uid, ObeliskActivateAbilityComponent component, ComponentShutdown args)
    {
        _actionsSystem.RemoveAction(uid, component.ActionObeliskActivateEntity);
    }

    private void OnObeliskActivate(EntityUid uid, ObeliskActivateAbilityComponent component, ObeliskActivateActionEvent args)
    {
        if (args.Handled
            || args.Target == uid
            || !HasComp<NecroobeliskComponent>(args.Target)
            || !_canActiveObelisk)
        {
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, uid, component.ActivationDuration, new ObeliskActivateDoAfterEvent(), uid, target: args.Target)
        {
            Hidden = true,
            Broadcast = true,
            BreakOnDamage = true,
            BreakOnMove = true,
            BlockDuplicate = true,
            CancelDuplicate = true,
            DistanceThreshold = component.ActivationDistantion
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;
    }

    private void OnObeliskActivateDoAfter(EntityUid uid, ObeliskActivateAbilityComponent component, ObeliskActivateDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Args.Target == null || !_canActiveObelisk)
            return;

        if (!TryComp<NecroobeliskComponent>(args.Args.Target, out var obeliskComp))
            return;

        obeliskComp.IsActive = true;
        obeliskComp.IsStoper = false;
        _canActiveObelisk = false;

        _necroobeliskSystem.UpdateState(args.Args.Target.Value, obeliskComp);

        var ev = new StageObeliskEvent(args.Args.Target.Value);

        var query = EntityQueryEnumerator<CircleOpsRuleComponent>();
        while (query.MoveNext(out var ent, out _))
        {
            RaiseLocalEvent(ent, ref ev);
        }
    }
}

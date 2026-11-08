// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.DeadSpace.Surgery;
using Content.Shared.Actions;
using Content.Shared.DeadSpace.Demons.Shadowling;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Mobs;
using Content.Shared.FixedPoint;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;

namespace Content.Server.DeadSpace.Demons.Shadowling;

public sealed class ShadowlingBlackMedSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SurgeryLimbPort _limbs = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ShadowlingBlackMedComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<ShadowlingBlackMedComponent, ShadowlingBlackMedEvent>(OnBlackMedAction);
        SubscribeLocalEvent<ShadowlingBlackMedComponent, ShadowlingBlackMedDoAfterEvent>(OnDoAfter);
    }

    private void OnComponentInit(EntityUid uid, ShadowlingBlackMedComponent component, ComponentInit args)
    {
        _actions.AddAction(uid, ref component.ActionBlackMedEntity, component.ActionBlackMed);
    }

    private void OnBlackMedAction(EntityUid uid, ShadowlingBlackMedComponent component, ShadowlingBlackMedEvent args)
    {
        if (args.Handled) return;

        var target = args.Target;

        if (!HasComp<ShadowlingSlaveComponent>(target))
            return;

        var doAfterArgs = new DoAfterArgs(EntityManager, uid, TimeSpan.FromSeconds(component.Duration), new ShadowlingBlackMedDoAfterEvent(), uid, target: target)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
            DistanceThreshold = 2f
        };
        _doAfter.TryStartDoAfter(doAfterArgs);

        args.Handled = true;
    }

    private void OnDoAfter(EntityUid uid, ShadowlingBlackMedComponent component, ShadowlingBlackMedDoAfterEvent args)
    {
        var target = args.Args.Target ?? args.Target;
        if (args.Cancelled || target == null) return;

        var targetUid = target.Value;

        if (!HasComp<ShadowlingSlaveComponent>(targetUid))
            return;

        _limbs.Restore(targetUid);

        if (TryComp<DamageableComponent>(targetUid, out var damageable))
            _damageable.SetAllDamage(targetUid, FixedPoint2.Zero);

        if (_mobState.IsDead(targetUid) || _mobState.IsCritical(targetUid))
            _mobState.ChangeMobState(targetUid, MobState.Alive);

        _popup.PopupEntity("Тёмная энергия восстанавливает ваше тело!", targetUid, targetUid, PopupType.Medium);
    }
}
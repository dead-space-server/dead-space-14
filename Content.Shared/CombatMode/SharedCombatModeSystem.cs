using Content.Shared.Actions;
using Content.Shared.DeadSpace.Surgery; //DS-14
using Content.Shared.Mind;
using Content.Shared.MouseRotator;
using Content.Shared.Movement.Components;
using Content.Shared.Popups;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared.CombatMode;

// DS14-Start
[ByRefEvent]
public record struct CombatModeChangedEvent(EntityUid Entity, bool IsInCombatMode);
// DS14-End

public abstract class SharedCombatModeSystem : EntitySystem
{
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] private   readonly SharedActionsSystem _actionsSystem = default!;
    [Dependency] private   readonly SharedPopupSystem _popup = default!;
    [Dependency] private   readonly SharedMindSystem  _mind = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CombatModeComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CombatModeComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<CombatModeComponent, ToggleCombatActionEvent>(OnActionPerform);
    }

    private void OnMapInit(EntityUid uid, CombatModeComponent component, MapInitEvent args)
    {
        _actionsSystem.AddAction(uid, ref component.CombatToggleActionEntity, component.CombatToggleAction);
        Dirty(uid, component);
    }

    private void OnShutdown(EntityUid uid, CombatModeComponent component, ComponentShutdown args)
    {
        _actionsSystem.RemoveAction(uid, component.CombatToggleActionEntity);

        SetMouseRotatorComponents(uid, false);
    }

    private void OnActionPerform(EntityUid uid, CombatModeComponent component, ToggleCombatActionEvent args)
    {
        if (args.Handled)
            return;

        //DS-14 start
        if (!component.IsInCombatMode && HasComp<SurgeryLobotomyComponent>(uid))
        {
            args.Handled = true;
            _popup.PopupClient(Loc.GetString("surgery-lobotomy-combat"), args.Performer, args.Performer);
            return;
        }
        //DS-14 end

        args.Handled = true;
        SetInCombatMode(uid, !component.IsInCombatMode, component);

        var msg = component.IsInCombatMode ? "action-popup-combat-enabled" : "action-popup-combat-disabled";
        _popup.PopupClient(Loc.GetString(msg), args.Performer, args.Performer);
    }

    public void SetCanDisarm(EntityUid entity, bool canDisarm, CombatModeComponent? component = null)
    {
        if (!Resolve(entity, ref component))
            return;

        component.CanDisarm = canDisarm;
    }

    public bool IsInCombatMode(EntityUid? entity, CombatModeComponent? component = null)
    {
        return entity != null && Resolve(entity.Value, ref component, false) && component.IsInCombatMode;
    }

    public virtual void SetInCombatMode(EntityUid entity, bool value, CombatModeComponent? component = null)
    {
        if (!Resolve(entity, ref component))
            return;

        if (value && HasComp<SurgeryLobotomyComponent>(entity)) //DS-14
            return; //DS-14

        if (component.IsInCombatMode == value)
            return;

        component.IsInCombatMode = value;
        Dirty(entity, component);

        if (component.CombatToggleActionEntity != null)
            _actionsSystem.SetToggled(component.CombatToggleActionEntity, component.IsInCombatMode);

        // DS14-Start
        var ev = new CombatModeChangedEvent(entity, value);
        RaiseLocalEvent(entity, ref ev);
        // DS14-End

        // Change mouse rotator comps if flag is set
        if (!component.ToggleMouseRotator || IsNpc(entity) && !_mind.TryGetMind(entity, out _, out _))
            return;

        SetMouseRotatorComponents(entity, value);
    }

    private void SetMouseRotatorComponents(EntityUid uid, bool value)
    {
        if (value)
        {
            EnsureComp<MouseRotatorComponent>(uid);
            EnsureComp<NoRotateOnMoveComponent>(uid);
        }
        else
        {
            RemComp<MouseRotatorComponent>(uid);
            RemComp<NoRotateOnMoveComponent>(uid);
        }
    }

    // todo: When we stop making fucking garbage abstract shared components, remove this shit too.
    protected abstract bool IsNpc(EntityUid uid);
}

public sealed partial class ToggleCombatActionEvent : InstantActionEvent
{

}

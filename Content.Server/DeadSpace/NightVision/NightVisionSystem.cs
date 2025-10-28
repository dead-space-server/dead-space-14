using Content.Shared.Actions;
using Content.Shared.DeadSpace.NightVision;
using JetBrains.Annotations;

namespace Content.Server.DeadSpace.NightVision;

public sealed class NightVisionSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NightVisionComponent, ComponentStartup>(OnComponentStartup);
        SubscribeLocalEvent<NightVisionComponent, ComponentRemove>(OnComponentRemove);
        SubscribeLocalEvent<NightVisionComponent, ToggleNightVisionActionEvent>(OnToggleNightVision);
    }

    private void OnComponentStartup(EntityUid uid, NightVisionComponent component, ComponentStartup args)
    {
        ToggleNightVision(uid, component);
        _actions.AddAction(uid, ref component.ActionToggleNightVisionEntity, component.ActionToggleNightVision);
    }

    private void OnComponentRemove(EntityUid uid, NightVisionComponent component, ComponentRemove args)
    {
        ToggleNightVision(uid, component);
        _actions.RemoveAction(uid, component.ActionToggleNightVisionEntity);
    }

    private void OnToggleNightVision(EntityUid uid, NightVisionComponent component, ToggleNightVisionActionEvent args)
    {
        if (args.Handled)
            return;

        ToggleNightVision(uid, component);
    }

    private void ToggleNightVision(EntityUid uid, NightVisionComponent component)
    {
        component.IsNightVision = !component.IsNightVision;
        Dirty(uid, component);
    }

    [PublicAPI]
    public void UpdateIsNightVision(Entity<NightVisionComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        var old = ent.Comp.IsNightVision;

        var ev = new CanNightVisionAttemptEvent();
        RaiseLocalEvent(ent.Owner, ev);
        ent.Comp.IsNightVision = ev.NightVision;

        if (old == ent.Comp.IsNightVision)
            return;

        Dirty(ent.Owner, ent.Comp);
    }
}

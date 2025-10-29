using Content.Shared.Actions;
using Content.Shared.DeadSpace.Components.NightVision;
using Robust.Shared.GameStates;

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
        SubscribeLocalEvent<NightVisionComponent, ComponentGetState>(OnNightVisionGetState);
    }

    private void OnNightVisionGetState(EntityUid uid, NightVisionComponent component, ref ComponentGetState args)
    {
        args.State = new NightVisionComponentState(component.IsNightVision);
    }

    private void OnComponentStartup(EntityUid uid, NightVisionComponent component, ComponentStartup args)
    {
        _actions.AddAction(uid, ref component.ActionToggleNightVisionEntity, component.ActionToggleNightVision);
    }

    private void OnComponentRemove(EntityUid uid, NightVisionComponent component, ComponentRemove args)
    {
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

}

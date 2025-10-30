using Content.Shared.Actions;
using Content.Server.DeadSpace.Components.NightVision;
using Content.Shared.DeadSpace.NightVision;
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
        SubscribeLocalEvent<NightVisionComponent, ComponentGetState>(OnNightVisionGetState);
    }

    private void OnNightVisionGetState(EntityUid uid, NightVisionComponent component, ref ComponentGetState args)
    {
        args.State = new NightVisionComponentState(component.Color);
    }

    private void OnComponentStartup(EntityUid uid, NightVisionComponent component, ComponentStartup args)
    {
        _actions.AddAction(uid, ref component.ActionToggleNightVisionEntity, component.ActionToggleNightVision);
    }

    private void OnComponentRemove(EntityUid uid, NightVisionComponent component, ComponentRemove args)
    {
        _actions.RemoveAction(uid, component.ActionToggleNightVisionEntity);
    }

}

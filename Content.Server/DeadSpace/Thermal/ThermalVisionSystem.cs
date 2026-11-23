// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Actions;
using Content.Shared.DeadSpace.ThermalVision;

namespace Content.Server.DeadSpace.ThermalVision;

public sealed partial class ThermalVisionSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ThermalVisionComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<ThermalVisionComponent, ComponentRemove>(OnRemove);
        SubscribeLocalEvent<ThermalVisionComponent, ToggleThermalVisionActionEvent>(OnToggle);
    }

    private void OnStartup(EntityUid uid, ThermalVisionComponent component, ComponentStartup args)
    {
        _actions.AddAction(uid, ref component.ActionToggleThermalVisionEntity, component.ActionToggleThermalVision);
    }

    private void OnRemove(EntityUid uid, ThermalVisionComponent component, ComponentRemove args)
    {
        _actions.RemoveAction(uid, component.ActionToggleThermalVisionEntity);
    }

    private void OnToggle(EntityUid uid, ThermalVisionComponent component, ToggleThermalVisionActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        component.IsActive = !component.IsActive;
        Dirty(uid, component);
    }
}